# Starts the full Aspire-orchestrated dev environment: AppHost -> dashboard + DCP -> containers
# (Postgres 5433, Redis 6379, Garage S3 3900, all persistent-lifetime with named volumes) + the
# web server on the SAME port as the server-only path (5028), so every existing verification flow
# (curl, browser tools, DevLoginBar) works unchanged. Mutually exclusive with
# scripts\start-dev-server.ps1 - both paths bind 5028 and this script refuses to double-start.
#
#   .\scripts\start-aspire.ps1                # foreground (Ctrl+C to stop) - manual use
#   .\scripts\start-aspire.ps1 -Background    # detached, logs to file, waits for web readiness
#
# First run pulls container images (postgres/redis/minio) - can take minutes; later runs are fast
# because containers are persistent (they keep running after the AppHost stops).
#
# NOTE: keep this file ASCII-only (PowerShell 5.1 + BOM-less file encoding trap).
# Workflow doc: .claude/skills/run-server/SKILL.md ("Aspire path")

param(
    [switch]$Background,
    [int]$WebPort = 5028,
    [int]$DashboardPort = 15031,
    [int]$TimeoutSeconds = 300,
    [string]$LogPath = (Join-Path $env:TEMP "canalave-aspire.log")
)

Set-StrictMode -Version Latest

$repoRoot    = Split-Path -Parent $PSScriptRoot
$appHostProj = Join-Path $repoRoot "AppHost"

# Docker must be up - Aspire provisions Postgres/Redis/MinIO as containers.
docker info *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Error "Docker is not running. Start Docker Desktop first (Aspire needs it for Postgres/Redis/MinIO containers)."
    exit 1
}

# Refuse to double-start (either a previous AppHost, or the server-only path holding 5028).
foreach ($port in @($DashboardPort, $WebPort)) {
    $existing = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Error "Port $port already has a listener (pid $($existing.OwningProcess | Select-Object -Unique)). Run scripts\stop-aspire.ps1 (or stop-dev-server.ps1 if the server-only path is running) first."
        exit 1
    }
}

# Mirror AppHost\Properties\launchSettings.json "http" profile, minus launchBrowser.
# (--no-launch-profile keeps dotnet run from popping a browser; Aspire reads these env vars.)
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:$DashboardPort"
$env:ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL = "http://localhost:19134"
$env:ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL = "http://localhost:20129"
# Required for an http (non-https) apphost URL - loopback-only local dev, same trade as the
# http launch profile. See https://aka.ms/aspire/allowunsecuredtransport
$env:ASPIRE_ALLOW_UNSECURED_TRANSPORT = "true"

if (-not $Background) {
    Write-Host "Starting Aspire AppHost (foreground). Dashboard: http://localhost:$DashboardPort  Web: http://localhost:$WebPort"
    Write-Host "Watch the console for the dashboard login URL (contains ?t=<token>). Ctrl+C to stop."
    Set-Location $appHostProj
    dotnet run --no-launch-profile
    exit $LASTEXITCODE
}

# Background: detached process, stdout+stderr to log, wait until the WEB APP (not just the
# dashboard) answers - containers + migrations + seeding all sit between launch and readiness.
if (Test-Path $LogPath) { Remove-Item $LogPath -Force }
$errPath = $LogPath + ".err"
if (Test-Path $errPath) { Remove-Item $errPath -Force }
Write-Host "Starting Aspire AppHost (background); log: $LogPath"

$proc = Start-Process -FilePath "dotnet" `
    -ArgumentList @("run", "--no-launch-profile") `
    -WorkingDirectory $appHostProj `
    -RedirectStandardOutput $LogPath `
    -RedirectStandardError  $errPath `
    -PassThru -WindowStyle Hidden

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$webUp = $false
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 2
    if ($proc.HasExited) {
        Write-Error "AppHost exited early (code $($proc.ExitCode)). Log tail:"
        if (Test-Path $LogPath) { Get-Content $LogPath -Tail 30 | Write-Host }
        if (Test-Path $errPath) { Get-Content $errPath -Tail 30 | Write-Host }
        exit 1
    }
    try {
        $resp = Invoke-WebRequest -Uri "http://localhost:$WebPort/" -UseBasicParsing -TimeoutSec 5
        if ($resp.StatusCode -eq 200) { $webUp = $true; break }
    } catch { }
}

if (-not $webUp) {
    Write-Error "Timed out (${TimeoutSeconds}s) waiting for http://localhost:$WebPort/. First runs pull container images - retry with -TimeoutSeconds 600, and check the dashboard. Log tail:"
    if (Test-Path $LogPath) { Get-Content $LogPath -Tail 30 | Write-Host }
    exit 1
}

# Surface the tokenized dashboard login URL from the log (dashboard auth is on by default).
$loginLine = $null
if (Test-Path $LogPath) {
    $loginLine = (Select-String -Path $LogPath -Pattern "login\?t=" | Select-Object -First 1)
}
Write-Host "Web app is up: http://localhost:$WebPort"
if ($loginLine) {
    Write-Host ("Dashboard: " + ($loginLine.Line -replace '^.*(http\S+login\?t=\S+).*$', '$1'))
} else {
    Write-Host "Dashboard: http://localhost:$DashboardPort (login token in $LogPath - search 'login?t=')"
}
Write-Host "Stop with scripts\stop-aspire.ps1 (containers stay up; add -StopContainers to stop them too)."
exit 0
