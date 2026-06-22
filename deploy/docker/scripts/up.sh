#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_common.sh
source "${SCRIPT_DIR}/_common.sh"

require_command docker
require_env_file

info "Starting PostgreSQL first. This script does not run migrations."
compose up -d postgres

info "Starting application services, analytics worker, and proxy."
compose up -d intelligence frontend api analytics-worker proxy

info "Stack start requested. Run deploy/docker/scripts/smoke.sh after DNS/TLS is ready."
