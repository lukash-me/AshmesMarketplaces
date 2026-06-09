#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_common.sh
source "${SCRIPT_DIR}/_common.sh"

require_command docker
require_command curl
load_env_file

[[ -n "${ASHMES_PUBLIC_HOST:-}" ]] || die "ASHMES_PUBLIC_HOST is missing in ${ENV_FILE}."

BASE_URL="https://${ASHMES_PUBLIC_HOST}"
RECOMMENDATIONS_URL="${BASE_URL}/api/v1/market/recommendations/hot-products"

info "Docker Compose service status:"
compose ps

check_http_200() {
  local url="$1"
  local label="$2"
  local status
  local curl_output

  set +e
  curl_output="$(curl --silent --show-error --location --max-time 15 --output /dev/null --write-out '%{http_code}' "${url}" 2>&1)"
  local curl_exit=$?
  set -e

  if [[ "${curl_exit}" -ne 0 ]]; then
    printf '[ashmes-docker] %s check could not reach %s\n' "${label}" "${url}" >&2
    printf '[ashmes-docker] This often means DNS is not propagated yet, ports 80/443 are blocked, or Caddy has not obtained a certificate.\n' >&2
    printf '[ashmes-docker] curl output: %s\n' "${curl_output}" >&2
    return 1
  fi

  status="${curl_output: -3}"
  if [[ "${status}" == "200" ]]; then
    info "${label} returned HTTP 200."
    return 0
  fi

  printf '[ashmes-docker] %s returned HTTP %s for %s\n' "${label}" "${status}" "${url}" >&2
  return 1
}

check_http_200 "${BASE_URL}/" "Frontend"
check_http_200 "${RECOMMENDATIONS_URL}" "Recommendations API"

info "Smoke checks completed. An empty recommendations response is valid on a fresh database."
