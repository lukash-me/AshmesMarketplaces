#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
ENV_FILE="$REPO_ROOT/.env"
COMPOSE_FILE="$REPO_ROOT/docker-compose.local.yml"

step() {
  printf '[docker-stack] %s\n' "$1"
}

if [[ ! -f "$ENV_FILE" ]]; then
  step ".env is missing; nothing to stop."
  exit 0
fi

step "Stopping local full Docker stack without deleting volumes..."
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" down --remove-orphans
step "Done."
