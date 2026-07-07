param()

$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Resolve-Path (Join-Path $ScriptRoot "..\..")
$EnvFile = Join-Path $RepoRoot ".env"
$ComposeFile = Join-Path $RepoRoot "docker-compose.local.yml"

function Write-Step([string]$Message) {
    Write-Host "[docker-stack] $Message"
}

if (-not (Test-Path $EnvFile)) {
    Write-Step ".env is missing; nothing to stop."
    exit 0
}

Write-Step "Stopping local full Docker stack without deleting volumes..."
& docker compose -f $ComposeFile --env-file $EnvFile down --remove-orphans
if ($LASTEXITCODE -ne 0) {
    Write-Host "[docker-stack] WARNING: docker compose down returned a non-zero exit code." -ForegroundColor Yellow
}

Write-Step "Done."
