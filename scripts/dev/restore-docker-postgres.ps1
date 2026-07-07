param(
    [Parameter(Mandatory = $true)]
    [string]$DumpFile,
    [string]$Confirmation
)

$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Resolve-Path (Join-Path $ScriptRoot "..\..")
$EnvFile = Join-Path $RepoRoot ".env"
$ComposeFile = Join-Path $RepoRoot "docker-compose.local.yml"

if (-not (Test-Path $DumpFile)) {
    throw "Dump file does not exist: $DumpFile"
}

Write-Host "[docker-stack] WARNING: this will replace the local PostgreSQL database." -ForegroundColor Red
Write-Host "[docker-stack] Dump file: $DumpFile"

if ([string]::IsNullOrWhiteSpace($Confirmation)) {
    $Confirmation = Read-Host "Type RESTORE LOCAL DATA to continue"
}

if ($Confirmation -ne "RESTORE LOCAL DATA") {
    Write-Host "[docker-stack] Confirmation did not match. Restore was not run." -ForegroundColor Yellow
    exit 1
}

$containerPath = "/tmp/ashmes_restore.dump"
& docker cp $DumpFile "ashmes-postgres:$containerPath"
if ($LASTEXITCODE -ne 0) {
    throw "docker cp failed."
}

$restoreCommand = @'
set -e
psql -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1 -v dbname="$POSTGRES_DB" -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = :'dbname' AND pid <> pg_backend_pid();"
psql -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1 -v dbname="$POSTGRES_DB" -c 'DROP DATABASE IF EXISTS :"dbname";'
psql -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1 -v dbname="$POSTGRES_DB" -v dbowner="$POSTGRES_USER" -c 'CREATE DATABASE :"dbname" OWNER :"dbowner";'
pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists --no-owner --no-privileges /tmp/ashmes_restore.dump
rm -f /tmp/ashmes_restore.dump
'@

& docker compose -f $ComposeFile --env-file $EnvFile exec -T postgres sh -lc $restoreCommand
if ($LASTEXITCODE -ne 0) {
    throw "PostgreSQL restore failed."
}

Write-Host "[docker-stack] Restore complete."
