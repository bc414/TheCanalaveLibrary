# Wipes the ASPIRE-path dev database: stops the AppHost, then removes the persistent Postgres
# container AND its named data volume. The next AppHost start recreates the container, and the
# app's Development startup re-migrates + re-seeds (same rebuild contract as reset-dev-db.ps1).
# The server-only path's database (local PostgreSQL on 5432) is untouched - that one is
# reset-dev-db.ps1's job. Wipe-vs-keep decision guide: run-server/SKILL.md "Dev DB lifecycle"
# (applies to both paths).
#
#   .\scripts\reset-aspire-db.ps1              # wipe only (next AppHost start rebuilds)
#   .\scripts\reset-aspire-db.ps1 -Restart     # wipe, then start the AppHost in the background
#
# NOTE: keep this file ASCII-only (PowerShell 5.1 + BOM-less file encoding trap).

param(
    [switch]$Restart
)

Set-StrictMode -Version Latest

# 1. Stop the AppHost (a running web app holds DB connections; DCP would also fight the rm).
& (Join-Path $PSScriptRoot "stop-aspire.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# 2. Remove the container, then its volume (a stopped container still pins the volume).
$container = docker ps -aq --filter "name=^/canalave-postgres$"
if ($container) {
    docker rm -f canalave-postgres | Out-Null
    Write-Host "Removed container canalave-postgres."
} else {
    Write-Host "Container canalave-postgres not found (already removed)."
}

$volume = docker volume ls -q --filter "name=^canalave-postgres-data$"
if ($volume) {
    docker volume rm canalave-postgres-data | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Error "docker volume rm canalave-postgres-data failed - something still references it."
        exit 1
    }
    Write-Host "Removed volume canalave-postgres-data."
} else {
    Write-Host "Volume canalave-postgres-data not found (already removed)."
}

Write-Host "Aspire dev database wiped. Next AppHost start recreates + migrates + seeds it."

# 3. Optionally bring the whole environment straight back up.
if ($Restart) {
    & (Join-Path $PSScriptRoot "start-aspire.ps1") -Background
    exit $LASTEXITCODE
}
exit 0
