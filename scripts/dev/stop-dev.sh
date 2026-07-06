#!/usr/bin/env bash
set -euo pipefail

STOP_DOCKER=false
for arg in "$@"; do
  case "$arg" in
    --docker)
      STOP_DOCKER=true
      ;;
    *)
      printf '[dev] ERROR: Unknown argument: %s\n' "$arg" >&2
      exit 1
      ;;
  esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
ENV_FILE="$REPO_ROOT/.env"
COMPOSE_FILE="$REPO_ROOT/docker-compose.local.yml"
PID_ROOT="$REPO_ROOT/.dev/pids"
BACKEND_PID_FILE="$PID_ROOT/backend.pid"
FRONTEND_PID_FILE="$PID_ROOT/frontend.pid"

step() {
  printf '[dev] %s\n' "$1"
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

  printf '[dev] ERROR: Required command %s was not found in PATH.\n' "$label" >&2
  exit 1
}

stop_managed_process() {
  local name="$1"
  local pid_file="$2"

  if [[ ! -f "$pid_file" ]]; then
    step "$name is not tracked as running."
    return
  fi

  local pid
  pid="$(tr -d '[:space:]' < "$pid_file")"
  if [[ ! "$pid" =~ ^[0-9]+$ ]]; then
    rm -f "$pid_file"
    step "$name pid file was invalid and has been removed."
    return
  fi

  if kill -0 "$pid" >/dev/null 2>&1; then
    step "Stopping $name (pid $pid)..."
    kill "$pid" >/dev/null 2>&1 || true
    sleep 1
    if kill -0 "$pid" >/dev/null 2>&1; then
      kill -9 "$pid" >/dev/null 2>&1 || true
    fi
  else
    step "$name process $pid is not running."
  fi

  rm -f "$pid_file"
}

stop_managed_process "backend API" "$BACKEND_PID_FILE"
stop_managed_process "frontend Vite" "$FRONTEND_PID_FILE"

if [[ "$STOP_DOCKER" == "true" ]]; then
  if [[ ! -f "$ENV_FILE" ]]; then
    step ".env is missing; docker services were not stopped."
    exit 0
  fi

  step "Stopping PostgreSQL, pgAdmin, and Intelligence containers..."
  DOCKER_BIN="$(resolve_command docker docker docker.exe)"
  "$DOCKER_BIN" compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" stop postgres pgadmin intelligence
fi

step "Done."
