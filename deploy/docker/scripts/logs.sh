#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_common.sh
source "${SCRIPT_DIR}/_common.sh"

require_command docker
require_env_file

if [[ "$#" -eq 0 ]]; then
  info "Showing logs for all services. Pass service names to limit output, for example: $0 api proxy"
  compose logs -f
else
  compose logs -f "$@"
fi
