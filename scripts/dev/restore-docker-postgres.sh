#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
ENV_FILE="$REPO_ROOT/.env"
COMPOSE_FILE="$REPO_ROOT/docker-compose.local.yml"

if [[ "$#" -lt 1 || "$#" -gt 3 ]]; then
  printf 'Usage: %s path/to/backup.dump [--confirm "RESTORE LOCAL DATA"]\n' "$0" >&2
  exit 1
fi

dump_file="$1"
shift
confirmation=""

while [[ "$#" -gt 0 ]]; do
  case "$1" in
    --confirm)
      [[ "$#" -ge 2 ]] || { printf '[docker-stack] --confirm requires a value.\n' >&2; exit 1; }
      confirmation="$2"
      shift 2
      ;;
    *)
      printf '[docker-stack] Unknown argument: %s\n' "$1" >&2
      exit 1
      ;;
  esac
done

[[ -f "$dump_file" ]] || { printf '[docker-stack] Dump file does not exist: %s\n' "$dump_file" >&2; exit 1; }

printf '[docker-stack] WARNING: this will replace the local PostgreSQL database.\n' >&2
printf '[docker-stack] Dump file: %s\n' "$dump_file" >&2

if [[ -z "$confirmation" ]]; then
  printf 'Type RESTORE LOCAL DATA to continue: '
  read -r confirmation
fi

if [[ "$confirmation" != "RESTORE LOCAL DATA" ]]; then
  printf '[docker-stack] Confirmation did not match. Restore was not run.\n' >&2
  exit 1
fi

container_path="/tmp/ashmes_restore.dump"
docker cp "$dump_file" "ashmes-postgres:$container_path"

docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" exec -T postgres sh -lc '
set -e
psql -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1 -v dbname="$POSTGRES_DB" -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = :'\'dbname\'' AND pid <> pg_backend_pid();"
psql -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1 -v dbname="$POSTGRES_DB" -c "DROP DATABASE IF EXISTS :\"dbname\";"
psql -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1 -v dbname="$POSTGRES_DB" -v dbowner="$POSTGRES_USER" -c "CREATE DATABASE :\"dbname\" OWNER :\"dbowner\";"
pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists --no-owner --no-privileges /tmp/ashmes_restore.dump
rm -f /tmp/ashmes_restore.dump
'

printf '[docker-stack] Restore complete.\n'
