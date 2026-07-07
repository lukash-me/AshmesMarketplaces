param(
    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Resolve-Path (Join-Path $ScriptRoot "..\..")
$EnvFile = Join-Path $RepoRoot ".env"
$ComposeFile = Join-Path $RepoRoot "docker-compose.local.yml"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $RepoRoot ".dev\backups"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$timestamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
$containerPath = "/tmp/ashmes_postgres_$timestamp.dump"
$backupPath = Join-Path $OutputDirectory "ashmes_postgres_$timestamp.dump"

Write-Host "[docker-stack] Creating PostgreSQL backup..."
& docker compose -f $ComposeFile --env-file $EnvFile exec -T postgres sh -lc "pg_dump -U `"`$POSTGRES_USER`" -d `"`$POSTGRES_DB`" --format=custom --file=$containerPath"
if ($LASTEXITCODE -ne 0) {
    throw "pg_dump failed."
}

& docker cp "ashmes-postgres:$containerPath" $backupPath
if ($LASTEXITCODE -ne 0) {
    throw "docker cp failed."
}

& docker compose -f $ComposeFile --env-file $EnvFile exec -T postgres rm -f $containerPath | Out-Null

Write-Host "[docker-stack] Backup complete: $backupPath"
