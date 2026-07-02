# Starts the Canalave Library dev server (TheCanalaveLibrary.Server, Development environment).
# On startup the app applies EF migrations (creating the database if it does not exist) and runs
# DataSeeder (mode per appsettings.Development.json "DevSeed"). MVP dev runs the Server project
# directly - no Aspire AppHost, no Redis, no WASM.
#
#   .\scripts\start-dev-server.ps1                # foreground (Ctrl+C to stop) - manual use
#   .\scripts\start-dev-server.ps1 -Background    # detached, logs to file, waits for "Now listening"
#
# NOTE: keep this file ASCII-only (PowerShell 5.1 + BOM-less file encoding trap).
# Workflow doc: .claude/skills/run-server/SKILL.md

param(
    [switch]$Background,
    [int]$Port = 5028,
    [string]$LogPath = (Join-Path $env:TEMP "canalave-dev-server.log")
)

Set-StrictMode -Version Latest

$repoRoot   = Split-Path -Parent $PSScriptRoot
$serverProj = Join-Path $repoRoot "TheCanalaveLibrary.Server"
$urls       = "http://localhost:$Port"

# Refuse to double-start.
$existing = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
if ($existing) {
    Write-Error "Port $Port already has a listener (pid $($existing.OwningProcess | Select-Object -Unique)). Run scripts\stop-dev-server.ps1 first."
    exit 1
}

$env:ASPNETCORE_ENVIRONMENT = "Development"

if (-not $Background) {
    Write-Host "Starting dev server (foreground) on $urls - Ctrl+C to stop."
    Set-Location $serverProj
    dotnet run --no-launch-profile --urls $urls
    exit $LASTEXITCODE
}

# Background: detached process, stdout+stderr to log, wait until listening.
if (Test-Path $LogPath) { Remove-Item $LogPath -Force }
$errPath = $LogPath + ".err"
if (Test-Path $errPath) { Remove-Item $errPath -Force }
Write-Host "Starting dev server (background) on $urls; log: $LogPath"

$proc = Start-Process -FilePath "dotnet" `
    -ArgumentList @("run", "--no-launch-profile", "--urls", $urls) `
    -WorkingDirectory $serverProj `
    -RedirectStandardOutput $LogPath `
    -RedirectStandardError  $errPath `
    -PassThru -WindowStyle Hidden

$deadline = (Get-Date).AddSeconds(120)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 1
    if ($proc.HasExited) {
        Write-Error "Server process exited early (code $($proc.ExitCode)). Log tail:"
        if (Test-Path $LogPath) { Get-Content $LogPath -Tail 25 | Write-Host }
        if (Test-Path $errPath) { Get-Content $errPath -Tail 25 | Write-Host }
        exit 1
    }
    if ((Test-Path $LogPath) -and (Select-String -Path $LogPath -Pattern "Now listening on" -Quiet)) {
        $procId = $proc.Id
        Write-Host "Server is up: $urls (launcher pid $procId; stop with scripts\stop-dev-server.ps1)."
        exit 0
    }
}

Write-Error "Timed out (120s) waiting for 'Now listening on'. Log tail:"
if (Test-Path $LogPath) { Get-Content $LogPath -Tail 25 | Write-Host }
exit 1
