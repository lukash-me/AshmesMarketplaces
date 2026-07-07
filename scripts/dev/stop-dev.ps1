param(
    [switch]$StopDocker
)

$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Resolve-Path (Join-Path $ScriptRoot "..\..")
$EnvFile = Join-Path $RepoRoot ".env"
$ComposeFile = Join-Path $RepoRoot "docker-compose.local.yml"
$PidRoot = Join-Path $RepoRoot ".dev\pids"
$BackendPidFile = Join-Path $PidRoot "backend.pid"
$WorkerPidFile = Join-Path $PidRoot "worker.pid"
$LegacyWorkerPidFile = Join-Path $PidRoot "analytics-worker.pid"
$FrontendPidFile = Join-Path $PidRoot "frontend.pid"

function Write-Step([string]$Message) {
    Write-Host "[dev] $Message"
}

function Stop-ProcessTree([int]$ProcessId) {
    $children = @(Get-CimInstance Win32_Process -Filter "ParentProcessId=$ProcessId" -ErrorAction SilentlyContinue)
    foreach ($child in $children) {
        Stop-ProcessTree ([int]$child.ProcessId)
    }

    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($process) {
        Stop-Process -Id $ProcessId -Force
    }
}

function Stop-ManagedProcess([string]$Name, [string]$PidFile) {
    if (-not (Test-Path $PidFile)) {
        Write-Step "$Name is not tracked as running."
        return
    }

    $raw = (Get-Content -Raw -LiteralPath $PidFile).Trim()
    if (-not ($raw -match "^\d+$")) {
        Remove-Item -LiteralPath $PidFile -Force
        Write-Step "$Name pid file was invalid and has been removed."
        return
    }

    $pidValue = [int]$raw
    $process = Get-Process -Id $pidValue -ErrorAction SilentlyContinue
    if ($process) {
        Write-Step "Stopping $Name (pid $pidValue)..."
        Stop-ProcessTree $pidValue
    } else {
        Write-Step "$Name process $pidValue is not running."
    }

    Remove-Item -LiteralPath $PidFile -Force
}

Stop-ManagedProcess "backend API" $BackendPidFile
Stop-ManagedProcess "analytics worker" $WorkerPidFile
Stop-ManagedProcess "analytics worker legacy pid" $LegacyWorkerPidFile
Stop-ManagedProcess "frontend Vite" $FrontendPidFile

if ($StopDocker) {
    if (-not (Test-Path $EnvFile)) {
        Write-Step ".env is missing; docker services were not stopped."
        exit 0
    }

    Write-Step "Stopping PostgreSQL, pgAdmin, and Intelligence containers..."
    & docker compose -f $ComposeFile --env-file $EnvFile stop postgres pgadmin intelligence
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[dev] WARNING: docker compose stop returned a non-zero exit code." -ForegroundColor Yellow
    }
}

Write-Step "Done."
