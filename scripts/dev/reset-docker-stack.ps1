param(
    [switch]$DestroyVolumes,
    [string]$Confirmation
)

$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Resolve-Path (Join-Path $ScriptRoot "..\..")
$EnvFile = Join-Path $RepoRoot ".env"
$ComposeFile = Join-Path $RepoRoot "docker-compose.local.yml"
$StartScript = Join-Path $ScriptRoot "start-docker-stack.ps1"

function Write-Step([string]$Message) {
    Write-Host "[docker-stack] $Message"
}

if (-not $DestroyVolumes) {
    Write-Host "[docker-stack] Refusing to reset without explicit destructive confirmation." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Safe restart, preserving PostgreSQL/MinIO/Redis volumes:"
    Write-Host "  ./scripts/dev/stop-docker-stack.ps1"
    Write-Host "  ./scripts/dev/start-docker-stack.ps1"
    Write-Host ""
    Write-Host "Create a PostgreSQL backup before destructive reset:"
    Write-Host "  ./scripts/dev/backup-docker-postgres.ps1"
    Write-Host ""
    Write-Host "If you intentionally want to DELETE local Docker volumes, run:"
    Write-Host "  ./scripts/dev/reset-docker-stack.ps1 -DestroyVolumes"
    exit 1
}

Write-Host "[docker-stack] WARNING: this will DELETE local Docker volumes for this stack." -ForegroundColor Red
Write-Host "[docker-stack] PostgreSQL data, MinIO files, Redis data, pgAdmin data, and uploaded files will be removed." -ForegroundColor Red
Write-Host "[docker-stack] This action is not a restart. It is a local data wipe." -ForegroundColor Red

if ([string]::IsNullOrWhiteSpace($Confirmation)) {
    $Confirmation = Read-Host "Type RESET LOCAL DATA to continue"
}

if ($Confirmation -ne "RESET LOCAL DATA") {
    Write-Host "[docker-stack] Confirmation did not match. No volumes were removed." -ForegroundColor Yellow
    exit 1
}

if (Test-Path $EnvFile) {
    Write-Step "Removing local Docker stack containers, networks, and volumes..."
    & docker compose -f $ComposeFile --env-file $EnvFile down -v --remove-orphans
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[docker-stack] WARNING: docker compose down -v returned a non-zero exit code." -ForegroundColor Yellow
    }
} else {
    Write-Step ".env is missing; skipping compose down and letting start script create it."
}

Write-Step "Starting fresh local Docker stack..."
& powershell -ExecutionPolicy Bypass -File $StartScript
