#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
ENV_FILE="$REPO_ROOT/.env"
COMPOSE_FILE="$REPO_ROOT/docker-compose.local.yml"
OUTPUT_DIR="${1:-$REPO_ROOT/.dev/backups}"

mkdir -p "$OUTPUT_DIR"

timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
container_path="/tmp/ashmes_postgres_${timestamp}.dump"
backup_path="$OUTPUT_DIR/ashmes_postgres_${timestamp}.dump"

printf '[docker-stack] Creating PostgreSQL backup...\n'
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" exec -T postgres sh -lc \
  "pg_dump -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" --format=custom --file=$container_path"

docker cp "ashmes-postgres:$container_path" "$backup_path"
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" exec -T postgres rm -f "$container_path" >/dev/null

chmod 600 "$backup_path" 2>/dev/null || true
printf '[docker-stack] Backup complete: %s\n' "$backup_path"
