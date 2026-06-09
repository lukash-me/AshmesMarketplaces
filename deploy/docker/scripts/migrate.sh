#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_common.sh
source "${SCRIPT_DIR}/_common.sh"

require_command docker
require_env_file

info "Running the manual EF migrator. This is not part of default up -d."
compose --profile tools run --rm migrator
