# Stops the Aspire AppHost (and with it the web server). DCP monitors the AppHost pid and tears
# down the session when it dies, so killing the AppHost process is the reliable stop - same
# rationale as stop-dev-server.ps1's kill-by-port. Persistent-lifetime containers (postgres,
# redis, garage) KEEP RUNNING by design - that is what makes the next start fast and the dev DB
# a persistent workbench. Add -StopContainers to also stop (not remove) them.
#
#   .\scripts\stop-aspire.ps1                   # stop AppHost + web; containers keep running
#   .\scripts\stop-aspire.ps1 -StopContainers   # also docker-stop the three backing containers
#
# NOTE: keep this file ASCII-only (PowerShell 5.1 + BOM-less file encoding trap).
# Workflow doc: .claude/skills/run-server/SKILL.md ("Aspire path")

param(
    [switch]$StopContainers,
    [int]$WebPort = 5028,
    [int]$DashboardPort = 15031
)

Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot

# The AppHost worker is AppHost.exe under this repo's AppHost\bin. Killing the `dotnet run`
# launcher alone would leave it alive (same launcher-vs-worker trap as stop-dev-server.ps1).
$killed = $false
$procs = Get-CimInstance Win32_Process -Filter "Name = 'AppHost.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($repoRoot) }
foreach ($p in $procs) {
    Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
    Write-Host "Stopped AppHost (pid $($p.ProcessId))."
    $killed = $true
}

# Fallback: whatever owns the dashboard port (covers a renamed/oddly-launched apphost).
if (-not $killed) {
    $conn = Get-NetTCPConnection -LocalPort $DashboardPort -State Listen -ErrorAction SilentlyContinue
    if ($conn) {
        foreach ($ownerPid in ($conn.OwningProcess | Select-Object -Unique)) {
            Stop-Process -Id $ownerPid -Force -ErrorAction SilentlyContinue
            Write-Host "Stopped dashboard-port owner (pid $ownerPid)."
            $killed = $true
        }
    }
}

if (-not $killed) {
    Write-Host "No running AppHost found."
}

# DCP notices the dead AppHost and tears down the web server; give it a moment, then verify.
$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline) {
    $web = Get-NetTCPConnection -LocalPort $WebPort -State Listen -ErrorAction SilentlyContinue
    if (-not $web) { break }
    Start-Sleep -Seconds 1
}
$web = Get-NetTCPConnection -LocalPort $WebPort -State Listen -ErrorAction SilentlyContinue
if ($web) {
    # Straggler (or the server-only path is what was actually running) - kill by port, verify.
    foreach ($ownerPid in ($web.OwningProcess | Select-Object -Unique)) {
        Stop-Process -Id $ownerPid -Force -ErrorAction SilentlyContinue
        Write-Host "Killed straggler on port $WebPort (pid $ownerPid)."
    }
    Start-Sleep -Milliseconds 500
    $still = Get-NetTCPConnection -LocalPort $WebPort -State Listen -ErrorAction SilentlyContinue
    if ($still) {
        Write-Error "Port $WebPort is STILL in use (pid $($still.OwningProcess | Select-Object -Unique))."
        exit 1
    }
}
Write-Host "Web port $WebPort is free."

if ($StopContainers) {
    foreach ($c in @("canalave-postgres", "canalave-redis", "canalave-garage")) {
        $id = docker ps -q --filter "name=^/$c$"
        if ($id) {
            docker stop $c | Out-Null
            Write-Host "Stopped container $c."
        }
    }
} else {
    $running = docker ps --format "{{.Names}}" --filter "name=canalave-"
    if ($running) {
        Write-Host "Backing containers still running (by design): $($running -join ', ')"
    }
}
exit 0
