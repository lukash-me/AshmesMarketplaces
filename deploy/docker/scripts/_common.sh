#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEPLOY_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
REPO_ROOT="$(cd "${DEPLOY_DIR}/../.." && pwd)"

COMPOSE_FILE="${REPO_ROOT}/deploy/docker/compose.prod.yml"
ENV_FILE="${REPO_ROOT}/deploy/docker/.env"
BACKUP_DIR="${REPO_ROOT}/deploy/docker/backups"

die() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 1
}

info() {
  printf '[ashmes-docker] %s\n' "$*"
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || die "Required command '$1' was not found."
}

require_env_file() {
  [[ -f "${ENV_FILE}" ]] || die "Missing ${ENV_FILE}. Copy deploy/docker/.env.example to deploy/docker/.env and edit placeholders first."
}

load_env_file() {
  require_env_file
  set -a
  # shellcheck disable=SC1090
  source "${ENV_FILE}"
  set +a
}

compose() {
  docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" "$@"
}

compose_project_name() {
  if [[ -n "${COMPOSE_PROJECT_NAME:-}" ]]; then
    printf '%s\n' "${COMPOSE_PROJECT_NAME}"
  else
    printf 'ashmes\n'
  fi
}
