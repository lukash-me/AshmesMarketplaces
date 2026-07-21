#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_common.sh
source "${SCRIPT_DIR}/_common.sh"

require_command docker
require_env_file

info "Starting production parser worker."
compose --profile worker up -d parser

info "Parser worker start requested. Use parser-logs.sh to watch progress."
