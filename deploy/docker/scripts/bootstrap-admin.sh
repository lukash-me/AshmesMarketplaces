#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_common.sh
source "${SCRIPT_DIR}/_common.sh"

require_command docker
require_command openssl
require_env_file

if [[ "${EUID}" -ne 0 ]]; then
  die "Run this script with sudo so credentials can be written to /etc/ashmes/admin-credentials.env."
fi

ADMIN_LOGIN="${ASHMES_ADMIN_LOGIN:-admin@ashmes.local}"
ADMIN_WORKSPACE_NAME="${ASHMES_ADMIN_WORKSPACE_NAME:-Ashmes Production Workspace}"
ADMIN_CREDENTIALS_FILE="${ASHMES_ADMIN_CREDENTIALS_FILE:-/etc/ashmes/admin-credentials.env}"

if [[ -f "${ADMIN_CREDENTIALS_FILE}" ]]; then
  # shellcheck disable=SC1090
  source "${ADMIN_CREDENTIALS_FILE}"
  ADMIN_LOGIN="${ADMIN_LOGIN:-admin@ashmes.local}"
  ADMIN_WORKSPACE_NAME="${ADMIN_WORKSPACE_NAME:-Ashmes Production Workspace}"
  [[ -n "${ADMIN_PASSWORD:-}" ]] || die "${ADMIN_CREDENTIALS_FILE} exists but ADMIN_PASSWORD is empty."
  info "Using existing admin credentials file: ${ADMIN_CREDENTIALS_FILE}"
else
  ADMIN_PASSWORD="$(openssl rand -hex 24)"
fi

info "Ensuring production admin user ${ADMIN_LOGIN}."
ASHMES_ADMIN_PASSWORD="${ADMIN_PASSWORD}" \
ASHMES_ADMIN_WORKSPACE_NAME="${ADMIN_WORKSPACE_NAME}" \
  compose --profile tools run --rm \
    -e ASHMES_ADMIN_PASSWORD \
    -e ASHMES_ADMIN_WORKSPACE_NAME \
    migrator \
    dotnet run --project Backend/AshmesMarketplaces.ParserIngestionCli/AshmesMarketplaces.ParserIngestionCli.csproj -- \
      ensure-admin-user "${ADMIN_LOGIN}" --reset-password

if [[ ! -f "${ADMIN_CREDENTIALS_FILE}" ]]; then
  install -d -m 700 -o root -g root "$(dirname "${ADMIN_CREDENTIALS_FILE}")"
  umask 077
  {
    printf 'ADMIN_LOGIN=%q\n' "${ADMIN_LOGIN}"
    printf 'ADMIN_PASSWORD=%q\n' "${ADMIN_PASSWORD}"
    printf 'ADMIN_WORKSPACE_NAME=%q\n' "${ADMIN_WORKSPACE_NAME}"
  } >"${ADMIN_CREDENTIALS_FILE}"
  chmod 600 "${ADMIN_CREDENTIALS_FILE}"
  chown root:root "${ADMIN_CREDENTIALS_FILE}"
  info "Admin credentials saved to ${ADMIN_CREDENTIALS_FILE}."
else
  info "Admin credentials kept in ${ADMIN_CREDENTIALS_FILE}."
fi

info "Admin bootstrap completed."
