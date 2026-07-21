param()

$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Resolve-Path (Join-Path $ScriptRoot "..\..")
$EnvFile = Join-Path $RepoRoot ".env"
$EnvExampleFile = Join-Path $RepoRoot ".env.example"
$ComposeFile = Join-Path $RepoRoot "docker-compose.local.yml"
$FrontendRoot = Join-Path $RepoRoot "Frontend"
$BackendProject = Join-Path $RepoRoot "Backend\AshmesMarketplaces.API\AshmesMarketplaces.API.csproj"
$WorkerProject = Join-Path $RepoRoot "Backend\AshmesMarketplaces.AnalyticsWorker\AshmesMarketplaces.AnalyticsWorker.csproj"
$DevRoot = Join-Path $RepoRoot ".dev"
$LogRoot = Join-Path $DevRoot "logs"
$PidRoot = Join-Path $DevRoot "pids"
$BackendLog = Join-Path $LogRoot "backend.log"
$WorkerLog = Join-Path $LogRoot "worker.log"
$FrontendLog = Join-Path $LogRoot "frontend.log"
$BackendPidFile = Join-Path $PidRoot "backend.pid"
$WorkerPidFile = Join-Path $PidRoot "worker.pid"
$FrontendPidFile = Join-Path $PidRoot "frontend.pid"

$ApiUrl = "http://localhost:5019"
$SwaggerUrl = "$ApiUrl/swagger"
$FrontendUrl = "http://localhost:5173"
$PgAdminUrl = "http://localhost:5050"
$IntelligenceUrl = "http://localhost:8020"

function Write-Step([string]$Message) {
    Write-Host "[dev] $Message"
}

function Stop-WithMessage([string]$Message) {
    Write-Host "[dev] ERROR: $Message" -ForegroundColor Red
    exit 1
}

function Require-Command([string]$Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $command) {
        Stop-WithMessage "Required command '$Name' was not found in PATH."
    }

    return $command.Source
}

function Get-ManagedPid([string]$PidFile) {
    if (-not (Test-Path $PidFile)) {
        return $null
    }

    $raw = (Get-Content -Raw -LiteralPath $PidFile).Trim()
    if (-not ($raw -match "^\d+$")) {
        Remove-Item -LiteralPath $PidFile -Force
        return $null
    }

    $process = Get-Process -Id ([int]$raw) -ErrorAction SilentlyContinue
    if (-not $process) {
        Remove-Item -LiteralPath $PidFile -Force
        return $null
    }

    return [int]$raw
}

function Test-ContainerRunning([string]$Name) {
    $result = & docker inspect -f "{{.State.Running}}" $Name 2>$null
    return $LASTEXITCODE -eq 0 -and $result -eq "true"
}

function Assert-PortAvailable([int]$Port, [string]$Purpose, [string]$PidFile = "", [string]$AllowedContainer = "") {
    $listeners = @(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue)
    if ($listeners.Count -eq 0) {
        return
    }

    if ($PidFile) {
        $managedPid = Get-ManagedPid $PidFile
        if ($managedPid -and ($listeners | Where-Object { $_.OwningProcess -eq $managedPid })) {
            Write-Step "$Purpose already appears to be running on port $Port (pid $managedPid)."
            return
        }
    }

    if ($AllowedContainer -and (Test-ContainerRunning $AllowedContainer)) {
        Write-Step "$Purpose port $Port is already owned by container $AllowedContainer."
        return
    }

    $details = $listeners |
        Select-Object LocalAddress, LocalPort, OwningProcess |
        Format-Table -AutoSize |
        Out-String
    Stop-WithMessage "Port $Port is already in use for $Purpose by an unmanaged process/container.`n$details"
}

function Wait-ContainerHealthy([string]$Name, [int]$TimeoutSeconds = 90) {
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

function Wait-Port([int]$Port, [string]$Purpose, [int]$TimeoutSeconds = 60) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        $listeners = @(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue)
        if ($listeners.Count -gt 0) {
            return
        }

        Start-Sleep -Seconds 1
    }

    Stop-WithMessage "$Purpose did not start listening on port $Port within $TimeoutSeconds seconds."
}

function Wait-ManagedProcess([int]$ProcessId, [string]$Purpose, [string]$LogPath, [int]$DelaySeconds = 3) {
    Start-Sleep -Seconds $DelaySeconds
    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if (-not $process) {
        $tail = if (Test-Path $LogPath) { Get-Content -LiteralPath $LogPath -Tail 40 | Out-String } else { "" }
        Stop-WithMessage "$Purpose exited shortly after startup.`n$tail"
    }
}

Write-Step "Checking local development prerequisites..."
Require-Command "docker" | Out-Null
Require-Command "dotnet" | Out-Null
$npmPath = Require-Command "npm"

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

if (-not (Test-Path (Join-Path $FrontendRoot "node_modules"))) {
    Stop-WithMessage "Frontend/node_modules is missing. Run 'npm install' in Frontend first."
}

New-Item -ItemType Directory -Force -Path $LogRoot, $PidRoot | Out-Null

Assert-PortAvailable 5432 "PostgreSQL" "" "ashmes-postgres"
Assert-PortAvailable 5050 "pgAdmin" "" "ashmes-pgadmin"
Assert-PortAvailable 8020 "Intelligence" "" "ashmes-intelligence"
Assert-PortAvailable 5019 "Backend API" $BackendPidFile ""
Assert-PortAvailable 5173 "Frontend Vite" $FrontendPidFile ""

Write-Step "Starting PostgreSQL, pgAdmin, and Intelligence..."
& docker compose -f $ComposeFile --env-file $EnvFile up -d postgres pgadmin intelligence
if ($LASTEXITCODE -ne 0) {
    Stop-WithMessage "Docker compose failed to start postgres/pgadmin/intelligence."
}

Write-Step "Waiting for PostgreSQL health..."
Wait-ContainerHealthy "ashmes-postgres"
Write-Step "PostgreSQL started."

Write-Step "Waiting for Intelligence health..."
Wait-ContainerHealthy "ashmes-intelligence" 180
Write-Step "Intelligence started."

$backendPid = Get-ManagedPid $BackendPidFile
if (-not $backendPid) {
    Write-Step "Starting backend API..."
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:Seed__EnableDevelopmentSeed = "false"
    $backendCommand = "dotnet run --project `"$BackendProject`" --no-launch-profile -- --urls $ApiUrl > `"$BackendLog`" 2>&1"
    $backendProcess = Start-Process -FilePath "cmd.exe" -ArgumentList @("/d", "/s", "/c", $backendCommand) -WorkingDirectory $RepoRoot -WindowStyle Hidden -PassThru
    Set-Content -LiteralPath $BackendPidFile -Value $backendProcess.Id
    Wait-Port 5019 "Backend API"
    Write-Step "Backend started (pid $($backendProcess.Id))."
}

$workerPid = Get-ManagedPid $WorkerPidFile
if (-not $workerPid) {
    Write-Step "Starting analytics worker..."
    $env:DOTNET_ENVIRONMENT = "Development"
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $workerCommand = "dotnet run --project `"$WorkerProject`" --no-launch-profile > `"$WorkerLog`" 2>&1"
    $workerProcess = Start-Process -FilePath "cmd.exe" -ArgumentList @("/d", "/s", "/c", $workerCommand) -WorkingDirectory $RepoRoot -WindowStyle Hidden -PassThru
    Set-Content -LiteralPath $WorkerPidFile -Value $workerProcess.Id
    Wait-ManagedProcess $workerProcess.Id "Analytics worker" $WorkerLog
    Write-Step "Analytics worker started (pid $($workerProcess.Id))."
}

$frontendPid = Get-ManagedPid $FrontendPidFile
if (-not $frontendPid) {
    Write-Step "Starting frontend Vite dev server..."
    $frontendCommand = "npm run dev -- --host 127.0.0.1 > `"$FrontendLog`" 2>&1"
    $frontendProcess = Start-Process -FilePath "cmd.exe" -ArgumentList @("/d", "/s", "/c", $frontendCommand) -WorkingDirectory $FrontendRoot -WindowStyle Hidden -PassThru
    Set-Content -LiteralPath $FrontendPidFile -Value $frontendProcess.Id
    Wait-Port 5173 "Frontend Vite"
    Write-Step "Frontend started (pid $($frontendProcess.Id))."
}

Write-Host ""
Write-Host "Ashmes local dev is running:"
Write-Host "  API:      $ApiUrl"
Write-Host "  Swagger:  $SwaggerUrl"
Write-Host "  Frontend: $FrontendUrl"
Write-Host "  pgAdmin:  $PgAdminUrl"
Write-Host "  Intelligence: $IntelligenceUrl"
Write-Host "  Worker:   local AnalyticsWorker"
Write-Host ""
Write-Host "Logs:"
Write-Host "  Backend:  $BackendLog"
Write-Host "  Worker:   $WorkerLog"
Write-Host "  Frontend: $FrontendLog"
Write-Host ""
Write-Host "Mode:"
Write-Host "  Fast dev mode: Docker infra + local API/frontend/worker"
Write-Host "  Full Docker stack: ./scripts/dev/start-docker-stack.ps1"
Write-Host ""
Write-Host "Stop app processes:"
Write-Host "  ./scripts/dev/stop-dev.ps1"
Write-Host "Stop app processes and docker services:"
Write-Host "  ./scripts/dev/stop-dev.ps1 -StopDocker"
