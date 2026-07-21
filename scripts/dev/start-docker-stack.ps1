param()

$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Resolve-Path (Join-Path $ScriptRoot "..\..")
$EnvFile = Join-Path $RepoRoot ".env"
$EnvExampleFile = Join-Path $RepoRoot ".env.example"
$ComposeFile = Join-Path $RepoRoot "docker-compose.local.yml"
$PidRoot = Join-Path $RepoRoot ".dev\pids"

function Write-Step([string]$Message) {
    Write-Host "[docker-stack] $Message"
}

function Stop-WithMessage([string]$Message) {
    Write-Host "[docker-stack] ERROR: $Message" -ForegroundColor Red
    exit 1
}

function Require-Command([string]$Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $command) {
        Stop-WithMessage "Required command '$Name' was not found in PATH."
    }

    return $command.Source
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

function Stop-TrackedProcess([string]$Name, [string]$PidFileName) {
    $pidFile = Join-Path $PidRoot $PidFileName
    if (-not (Test-Path $pidFile)) {
        return
    }

    $raw = (Get-Content -Raw -LiteralPath $pidFile).Trim()
    if ($raw -match "^\d+$") {
        $process = Get-Process -Id ([int]$raw) -ErrorAction SilentlyContinue
        if ($process) {
            Write-Step "Stopping tracked fast-dev $Name process (pid $raw) to free Docker stack ports."
            Stop-ProcessTree ([int]$raw)
        }
    }

    Remove-Item -LiteralPath $pidFile -Force -ErrorAction SilentlyContinue
}

function Wait-ContainerHealthy([string]$Name, [int]$TimeoutSeconds = 120) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        $status = & docker inspect -f "{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}" $Name 2>$null
        if ($LASTEXITCODE -eq 0 -and $status -eq "healthy") {
            return
        }

        Start-Sleep -Seconds 2
    }

    Stop-WithMessage "Container '$Name' did not become healthy within $TimeoutSeconds seconds."
}

function Assert-ContainerRunning([string]$Name) {
    $running = & docker inspect -f "{{.State.Running}}" $Name 2>$null
    if ($LASTEXITCODE -ne 0 -or $running -ne "true") {
        Stop-WithMessage "Container '$Name' is not running."
    }
}

Write-Step "Checking prerequisites..."
Require-Command "docker" | Out-Null
& docker compose version *> $null
if ($LASTEXITCODE -ne 0) {
    Stop-WithMessage "Docker Compose is not available through 'docker compose'."
}

if (-not (Test-Path $ComposeFile)) {
    Stop-WithMessage "Missing docker compose file: $ComposeFile"
}

if (-not (Test-Path $EnvFile)) {
    if (-not (Test-Path $EnvExampleFile)) {
        Stop-WithMessage "Missing .env and .env.example."
    }

    Copy-Item -LiteralPath $EnvExampleFile -Destination $EnvFile
    Write-Step "Created .env from .env.example."
}

New-Item -ItemType Directory -Force -Path $PidRoot | Out-Null
Stop-TrackedProcess "backend API" "backend.pid"
Stop-TrackedProcess "frontend Vite" "frontend.pid"
Stop-TrackedProcess "analytics worker" "worker.pid"
Stop-TrackedProcess "analytics worker" "analytics-worker.pid"

Write-Step "Starting infrastructure containers..."
& docker compose -f $ComposeFile --env-file $EnvFile up -d --build postgres redis minio pgadmin intelligence
if ($LASTEXITCODE -ne 0) {
    Stop-WithMessage "Docker compose failed to start infrastructure."
}

Write-Step "Waiting for infrastructure health..."
Wait-ContainerHealthy "ashmes-postgres"
Wait-ContainerHealthy "ashmes-redis"
Wait-ContainerHealthy "ashmes-minio"
Wait-ContainerHealthy "ashmes-intelligence" 180

Write-Step "Running database migrations..."
& docker compose -f $ComposeFile --env-file $EnvFile --profile tools run --rm migrator
if ($LASTEXITCODE -ne 0) {
    Stop-WithMessage "Database migration failed."
}

Write-Step "Starting application containers..."
& docker compose -f $ComposeFile --env-file $EnvFile up -d --build api analytics-worker parser-launcher frontend
if ($LASTEXITCODE -ne 0) {
    Stop-WithMessage "Docker compose failed to start application services."
}

Write-Step "Waiting for application health..."
Wait-ContainerHealthy "ashmes-api" 180
Wait-ContainerHealthy "ashmes-frontend"
Assert-ContainerRunning "ashmes-analytics-worker"
Assert-ContainerRunning "ashmes-parser-launcher"

Write-Host ""
Write-Host "Ashmes full Docker stack is running:"
Write-Host "  Frontend:     http://localhost:5173"
Write-Host "  API health:   http://localhost:5019/api/v1/health"
Write-Host "  Swagger:      http://localhost:5019/swagger"
Write-Host "  pgAdmin:      http://localhost:5050"
Write-Host "  Intelligence: http://localhost:8020"
Write-Host "  Worker:       ashmes-analytics-worker is running"
Write-Host "  Parser:       ashmes-parser-launcher is running"
Write-Host ""
Write-Host "Stop stack:"
Write-Host "  ./scripts/dev/stop-docker-stack.ps1"
Write-Host "Backup PostgreSQL before risky operations:"
Write-Host "  ./scripts/dev/backup-docker-postgres.ps1"
Write-Host "Destructive local data wipe, requires confirmation:"
Write-Host "  ./scripts/dev/reset-docker-stack.ps1 -DestroyVolumes"
