#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_common.sh
source "${SCRIPT_DIR}/_common.sh"

require_command docker
load_env_file

[[ "$#" -eq 1 ]] || die "Usage: $0 path/to/backup.dump"

dump_file="$1"
[[ -f "${dump_file}" ]] || die "Dump file does not exist: ${dump_file}"

[[ -n "${POSTGRES_DB:-}" ]] || die "POSTGRES_DB is missing in ${ENV_FILE}."
[[ -n "${POSTGRES_USER:-}" ]] || die "POSTGRES_USER is missing in ${ENV_FILE}."

project_name="$(compose_project_name)"

printf 'This will destructively restore PostgreSQL data.\n'
printf 'Compose project: %s\n' "${project_name}"
printf 'Target database: %s\n' "${POSTGRES_DB}"
printf 'Dump file: %s\n' "${dump_file}"
printf 'Type the exact word "restore" to continue: '
read -r confirmation

if [[ "${confirmation}" != "restore" ]]; then
  die "Confirmation did not match. Restore was not run."
fi

info "Restoring ${POSTGRES_DB} from ${dump_file}"

compose exec -T postgres psql -U "${POSTGRES_USER}" -d postgres \
  -v ON_ERROR_STOP=1 \
  -v dbname="${POSTGRES_DB}" \
  -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = :'dbname' AND pid <> pg_backend_pid();"

compose exec -T postgres psql -U "${POSTGRES_USER}" -d postgres \
  -v ON_ERROR_STOP=1 \
  -v dbname="${POSTGRES_DB}" \
  -c 'DROP DATABASE IF EXISTS :"dbname";'

compose exec -T postgres psql -U "${POSTGRES_USER}" -d postgres \
  -v ON_ERROR_STOP=1 \
  -v dbname="${POSTGRES_DB}" \
  -v dbowner="${POSTGRES_USER}" \
  -c 'CREATE DATABASE :"dbname" OWNER :"dbowner";'

compose exec -T postgres pg_restore \
  -U "${POSTGRES_USER}" \
  -d "${POSTGRES_DB}" \
  --clean \
  --if-exists \
  --no-owner \
  --no-privileges < "${dump_file}"

info "Restore complete."
