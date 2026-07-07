#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
ENV_FILE="$REPO_ROOT/.env"
ENV_EXAMPLE_FILE="$REPO_ROOT/.env.example"
COMPOSE_FILE="$REPO_ROOT/docker-compose.local.yml"
PID_ROOT="$REPO_ROOT/.dev/pids"

step() {
  printf '[docker-stack] %s\n' "$1" >&2
}

fail() {
  printf '[docker-stack] ERROR: %s\n' "$1" >&2
  exit 1
}

resolve_command() {
  local label="$1"
  shift

  local candidate
  for candidate in "$@"; do
    if command -v "$candidate" >/dev/null 2>&1; then
      command -v "$candidate"
      return
    fi
  done

  fail "Required command '$label' was not found in PATH."
}

stop_tracked_process() {
  local name="$1"
  local pid_file="$2"

  [[ -f "$pid_file" ]] || return 0
  local pid
  pid="$(tr -d '[:space:]' < "$pid_file")"
  if [[ "$pid" =~ ^[0-9]+$ ]] && kill -0 "$pid" >/dev/null 2>&1; then
    step "Stopping tracked fast-dev $name process (pid $pid) to free Docker stack ports."
    kill "$pid" >/dev/null 2>&1 || true
    sleep 1
    kill -9 "$pid" >/dev/null 2>&1 || true
  fi

  rm -f "$pid_file"
}

wait_container_healthy() {
  local name="$1"
  local timeout_seconds="${2:-120}"
  local deadline=$((SECONDS + timeout_seconds))

  while (( SECONDS < deadline )); do
    local status
    status="$("$DOCKER_BIN" inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$name" 2>/dev/null || true)"
    if [[ "$status" == "healthy" ]]; then
      return
    fi
    sleep 2
  done

  fail "Container '$name' did not become healthy within $timeout_seconds seconds."
}

assert_container_running() {
  local name="$1"
  local running
  running="$("$DOCKER_BIN" inspect -f '{{.State.Running}}' "$name" 2>/dev/null || true)"
  [[ "$running" == "true" ]] || fail "Container '$name' is not running."
}

step "Checking prerequisites..."
DOCKER_BIN="$(resolve_command docker docker docker.exe)"
"$DOCKER_BIN" compose version >/dev/null 2>&1 || fail "Docker Compose is not available through 'docker compose'."
[[ -f "$COMPOSE_FILE" ]] || fail "Missing docker compose file: $COMPOSE_FILE"

if [[ ! -f "$ENV_FILE" ]]; then
  [[ -f "$ENV_EXAMPLE_FILE" ]] || fail "Missing .env and .env.example."
  cp "$ENV_EXAMPLE_FILE" "$ENV_FILE"
  step "Created .env from .env.example."
fi

mkdir -p "$PID_ROOT"
stop_tracked_process "backend API" "$PID_ROOT/backend.pid"
stop_tracked_process "frontend Vite" "$PID_ROOT/frontend.pid"
stop_tracked_process "analytics worker" "$PID_ROOT/worker.pid"
stop_tracked_process "analytics worker" "$PID_ROOT/analytics-worker.pid"

step "Starting infrastructure containers..."
"$DOCKER_BIN" compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" up -d --build postgres redis minio pgadmin intelligence

step "Waiting for infrastructure health..."
wait_container_healthy "ashmes-postgres"
wait_container_healthy "ashmes-redis"
wait_container_healthy "ashmes-minio"
wait_container_healthy "ashmes-intelligence" 180

step "Running database migrations..."
"$DOCKER_BIN" compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" --profile tools run --rm migrator

step "Starting application containers..."
"$DOCKER_BIN" compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" up -d --build api analytics-worker parser-launcher frontend

step "Waiting for application health..."
wait_container_healthy "ashmes-api" 180
wait_container_healthy "ashmes-frontend"
assert_container_running "ashmes-analytics-worker"
assert_container_running "ashmes-parser-launcher"

cat >&2 <<EOF

Ashmes full Docker stack is running:
  Frontend:     http://localhost:5173
  API health:   http://localhost:5019/api/v1/health
  Swagger:      http://localhost:5019/swagger
  pgAdmin:      http://localhost:5050
  Intelligence: http://localhost:8020
  Worker:       ashmes-analytics-worker is running
  Parser:       ashmes-parser-launcher is running

Stop stack:
  bash scripts/dev/stop-docker-stack.sh
Backup PostgreSQL before risky operations:
  bash scripts/dev/backup-docker-postgres.sh
Destructive local data wipe, requires confirmation:
  bash scripts/dev/reset-docker-stack.sh --destroy-volumes
EOF
