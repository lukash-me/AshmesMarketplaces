#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_common.sh
source "${SCRIPT_DIR}/_common.sh"

require_command docker
docker compose version >/dev/null
require_env_file

info "Validating Docker Compose configuration..."
compose config >/dev/null
info "Prerequisites look usable."
