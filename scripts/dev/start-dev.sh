#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
ENV_FILE="$REPO_ROOT/.env"
ENV_EXAMPLE_FILE="$REPO_ROOT/.env.example"
COMPOSE_FILE="$REPO_ROOT/docker-compose.local.yml"
FRONTEND_ROOT="$REPO_ROOT/Frontend"
BACKEND_PROJECT="$REPO_ROOT/Backend/AshmesMarketplaces.API/AshmesMarketplaces.API.csproj"
WORKER_PROJECT="$REPO_ROOT/Backend/AshmesMarketplaces.AnalyticsWorker/AshmesMarketplaces.AnalyticsWorker.csproj"
DEV_ROOT="$REPO_ROOT/.dev"
LOG_ROOT="$DEV_ROOT/logs"
PID_ROOT="$DEV_ROOT/pids"
BACKEND_LOG="$LOG_ROOT/backend.log"
WORKER_LOG="$LOG_ROOT/worker.log"
FRONTEND_LOG="$LOG_ROOT/frontend.log"
BACKEND_PID_FILE="$PID_ROOT/backend.pid"
WORKER_PID_FILE="$PID_ROOT/worker.pid"
FRONTEND_PID_FILE="$PID_ROOT/frontend.pid"

API_URL="http://localhost:5019"
SWAGGER_URL="$API_URL/swagger"
FRONTEND_URL="http://localhost:5173"
PGADMIN_URL="http://localhost:5050"
INTELLIGENCE_URL="http://localhost:8020"

step() {
  printf '[dev] %s\n' "$1" >&2
}

fail() {
  printf '[dev] ERROR: %s\n' "$1" >&2
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

managed_pid() {
  local pid_file="$1"
  [[ -f "$pid_file" ]] || return 1

  local pid
  pid="$(tr -d '[:space:]' < "$pid_file")"
  if [[ ! "$pid" =~ ^[0-9]+$ ]]; then
    rm -f "$pid_file"
    return 1
  fi

  if kill -0 "$pid" >/dev/null 2>&1; then
    printf '%s' "$pid"
    return 0
  fi

  rm -f "$pid_file"
  return 1
}

container_running() {
  local name="$1"
  local running
  running="$("$DOCKER_BIN" inspect -f '{{.State.Running}}' "$name" 2>/dev/null || true)"
  [[ "$running" == "true" ]]
}

port_in_use() {
  local port="$1"

  if command -v lsof >/dev/null 2>&1; then
    lsof -nP -iTCP:"$port" -sTCP:LISTEN 2>/dev/null
    return $?
  fi

  if command -v ss >/dev/null 2>&1; then
    ss -ltnp "sport = :$port" 2>/dev/null | grep -q ":$port"
    return $?
  fi

  if command -v netstat >/dev/null 2>&1; then
    netstat -ano 2>/dev/null | grep -E "[.:]$port[[:space:]].*LISTEN" >/dev/null
    return $?
  fi

  return 1
}

port_details() {
  local port="$1"

  if command -v lsof >/dev/null 2>&1; then
    lsof -nP -iTCP:"$port" -sTCP:LISTEN 2>/dev/null || true
    return
  fi

  if command -v ss >/dev/null 2>&1; then
    ss -ltnp "sport = :$port" 2>/dev/null || true
    return
  fi

  if command -v netstat >/dev/null 2>&1; then
    netstat -ano 2>/dev/null | grep -E "[.:]$port[[:space:]].*LISTEN" || true
  fi
}

assert_port_available() {
  local port="$1"
  local purpose="$2"
  local pid_file="${3:-}"
  local allowed_container="${4:-}"

  if ! port_in_use "$port"; then
    return
  fi

  if [[ -n "$pid_file" ]]; then
    local pid
    if pid="$(managed_pid "$pid_file")"; then
      step "$purpose already appears to be running on port $port (pid $pid)."
      return
    fi
  fi

  if [[ -n "$allowed_container" ]] && container_running "$allowed_container"; then
    step "$purpose port $port is already owned by container $allowed_container."
    return
  fi

  fail "Port $port is already in use for $purpose by an unmanaged process/container:
$(port_details "$port")"
}

wait_container_healthy() {
  local name="$1"
  local timeout_seconds="${2:-90}"
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

wait_port() {
  local port="$1"
  local purpose="$2"
  local timeout_seconds="${3:-60}"
  local deadline=$((SECONDS + timeout_seconds))

  while (( SECONDS < deadline )); do
    if port_in_use "$port"; then
      return
    fi
    sleep 1
  done

  fail "$purpose did not start listening on port $port within $timeout_seconds seconds."
}

wait_managed_process() {
  local pid_file="$1"
  local purpose="$2"
  local log_path="$3"
  local delay_seconds="${4:-3}"

  sleep "$delay_seconds"
  local pid
  pid="$(tr -d '[:space:]' < "$pid_file")"
  if ! kill -0 "$pid" >/dev/null 2>&1; then
    fail "$purpose exited shortly after startup:
$(tail -n 40 "$log_path" 2>/dev/null || true)"
  fi
}

step "Checking local development prerequisites..."
DOCKER_BIN="$(resolve_command docker docker docker.exe)"
DOTNET_BIN="$(resolve_command dotnet dotnet dotnet.exe)"
NPM_BIN="$(resolve_command npm npm npm.cmd)"

"$DOCKER_BIN" compose version >/dev/null 2>&1 || fail "Docker Compose is not available through 'docker compose'."
[[ -f "$COMPOSE_FILE" ]] || fail "Missing docker compose file: $COMPOSE_FILE"

if [[ ! -f "$ENV_FILE" ]]; then
  [[ -f "$ENV_EXAMPLE_FILE" ]] || fail "Missing .env and .env.example."
  cp "$ENV_EXAMPLE_FILE" "$ENV_FILE"
  step "Created .env from .env.example."
fi

[[ -d "$FRONTEND_ROOT/node_modules" ]] || fail "Frontend/node_modules is missing. Run 'npm install' in Frontend first."

mkdir -p "$LOG_ROOT" "$PID_ROOT"

assert_port_available 5432 "PostgreSQL" "" "ashmes-postgres"
assert_port_available 5050 "pgAdmin" "" "ashmes-pgadmin"
assert_port_available 8020 "Intelligence" "" "ashmes-intelligence"
assert_port_available 5019 "Backend API" "$BACKEND_PID_FILE" ""
assert_port_available 5173 "Frontend Vite" "$FRONTEND_PID_FILE" ""

step "Starting PostgreSQL, pgAdmin, and Intelligence..."
"$DOCKER_BIN" compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" up -d postgres pgadmin intelligence

step "Waiting for PostgreSQL health..."
wait_container_healthy "ashmes-postgres"
step "PostgreSQL started."

step "Waiting for Intelligence health..."
wait_container_healthy "ashmes-intelligence" 180
step "Intelligence started."

if ! managed_pid "$BACKEND_PID_FILE" >/dev/null; then
  step "Starting backend API..."
  (
    cd "$REPO_ROOT"
    ASPNETCORE_ENVIRONMENT=Development Seed__EnableDevelopmentSeed=false \
      "$DOTNET_BIN" run --project "$BACKEND_PROJECT" --no-launch-profile -- --urls "$API_URL" \
      > "$BACKEND_LOG" 2>&1 &
    printf '%s' "$!" > "$BACKEND_PID_FILE"
  )
  wait_port 5019 "Backend API"
  step "Backend started (pid $(cat "$BACKEND_PID_FILE"))."
fi

if ! managed_pid "$WORKER_PID_FILE" >/dev/null; then
  step "Starting analytics worker..."
  (
    cd "$REPO_ROOT"
    DOTNET_ENVIRONMENT=Development ASPNETCORE_ENVIRONMENT=Development \
      "$DOTNET_BIN" run --project "$WORKER_PROJECT" --no-launch-profile \
      > "$WORKER_LOG" 2>&1 &
    printf '%s' "$!" > "$WORKER_PID_FILE"
  )
  wait_managed_process "$WORKER_PID_FILE" "Analytics worker" "$WORKER_LOG"
  step "Analytics worker started (pid $(cat "$WORKER_PID_FILE"))."
fi

if ! managed_pid "$FRONTEND_PID_FILE" >/dev/null; then
  step "Starting frontend Vite dev server..."
  (
    cd "$FRONTEND_ROOT"
    "$NPM_BIN" run dev -- --host 127.0.0.1 > "$FRONTEND_LOG" 2>&1 &
    printf '%s' "$!" > "$FRONTEND_PID_FILE"
  )
  wait_port 5173 "Frontend Vite"
  step "Frontend started (pid $(cat "$FRONTEND_PID_FILE"))."
fi

cat >&2 <<EOF

Ashmes local dev is running:
  API:      $API_URL
  Swagger:  $SWAGGER_URL
  Frontend: $FRONTEND_URL
  pgAdmin:  $PGADMIN_URL
  Intelligence: $INTELLIGENCE_URL
  Worker:   local AnalyticsWorker

Logs:
  Backend:  $BACKEND_LOG
  Worker:   $WORKER_LOG
  Frontend: $FRONTEND_LOG

Mode:
  Fast dev mode: Docker infra + local API/frontend/worker
  Full Docker stack: bash scripts/dev/start-docker-stack.sh

Stop app processes:
  bash scripts/dev/stop-dev.sh
Stop app processes and docker services:
  bash scripts/dev/stop-dev.sh --docker
EOF
