#!/usr/bin/env bash
set -euo pipefail

umask 077

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_common.sh
source "${SCRIPT_DIR}/_common.sh"

require_command docker
load_env_file

[[ -n "${POSTGRES_DB:-}" ]] || die "POSTGRES_DB is missing in ${ENV_FILE}."
[[ -n "${POSTGRES_USER:-}" ]] || die "POSTGRES_USER is missing in ${ENV_FILE}."

mkdir -p "${BACKUP_DIR}"
chmod 700 "${BACKUP_DIR}" 2>/dev/null || true

timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup_file="${BACKUP_DIR}/${POSTGRES_DB}_${timestamp}.dump"

[[ ! -e "${backup_file}" ]] || die "Backup file already exists: ${backup_file}"

info "Creating PostgreSQL backup at ${backup_file}"
compose exec -T postgres pg_dump \
  -U "${POSTGRES_USER}" \
  -d "${POSTGRES_DB}" \
  --format=custom \
  --file=- > "${backup_file}"

chmod 600 "${backup_file}" 2>/dev/null || true
info "Backup complete: ${backup_file}"
