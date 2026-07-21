#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
ENV_FILE="$REPO_ROOT/.env"
COMPOSE_FILE="$REPO_ROOT/docker-compose.local.yml"
CONFIRMATION=""
DESTROY_VOLUMES=0

step() {
  printf '[docker-stack] %s\n' "$1"
}

while [[ "$#" -gt 0 ]]; do
  case "$1" in
    --destroy-volumes)
      DESTROY_VOLUMES=1
      shift
      ;;
    --confirm)
      [[ "$#" -ge 2 ]] || { step "--confirm requires a value."; exit 1; }
      CONFIRMATION="$2"
      shift 2
      ;;
    *)
      step "Unknown argument: $1"
      exit 1
      ;;
  esac
done

if [[ "$DESTROY_VOLUMES" -ne 1 ]]; then
  cat <<'EOF'
[docker-stack] Refusing to reset without explicit destructive confirmation.

Safe restart, preserving PostgreSQL/MinIO/Redis volumes:
  bash scripts/dev/stop-docker-stack.sh
  bash scripts/dev/start-docker-stack.sh

Create a PostgreSQL backup before destructive reset:
  bash scripts/dev/backup-docker-postgres.sh

If you intentionally want to DELETE local Docker volumes, run:
  bash scripts/dev/reset-docker-stack.sh --destroy-volumes
EOF
  exit 1
fi

cat <<'EOF'
[docker-stack] WARNING: this will DELETE local Docker volumes for this stack.
[docker-stack] PostgreSQL data, MinIO files, Redis data, pgAdmin data, and uploaded files will be removed.
[docker-stack] This action is not a restart. It is a local data wipe.
EOF

if [[ -z "$CONFIRMATION" ]]; then
  printf 'Type RESET LOCAL DATA to continue: '
  read -r CONFIRMATION
fi

if [[ "$CONFIRMATION" != "RESET LOCAL DATA" ]]; then
  step "Confirmation did not match. No volumes were removed."
  exit 1
fi

if [[ -f "$ENV_FILE" ]]; then
  step "Removing local Docker stack containers, networks, and volumes..."
  docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" down -v --remove-orphans
else
  step ".env is missing; skipping compose down and letting start script create it."
fi

step "Starting fresh local Docker stack..."
bash "$SCRIPT_DIR/start-docker-stack.sh"
