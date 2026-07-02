# Stops the Canalave Library dev server by killing whatever process is listening on the port.
# The PID a backgrounded `dotnet run` hands back is the launcher, not the actual
# TheCanalaveLibrary.Server.exe worker - killing by port is the only reliable stop.
# NOTE: keep this file ASCII-only. PowerShell 5.1 reads BOM-less .ps1 as ANSI; UTF-8
# punctuation (em-dashes etc.) decodes into smart-quote bytes that break parsing.
# Workflow doc: .claude/skills/run-server/SKILL.md

param(
    [int]$Port = 5028
)

Set-StrictMode -Version Latest

$conn = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
if ($conn) {
    $procIds = $conn.OwningProcess | Select-Object -Unique
    foreach ($p in $procIds) {
        Stop-Process -Id $p -Force -ErrorAction SilentlyContinue
        Write-Host "Stopped process $p (was listening on port $Port)."
    }
    Start-Sleep -Milliseconds 500
}
else {
    Write-Host "Nothing listening on port $Port."
}

# Verify the port is actually free.
$still = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
if ($still) {
    Write-Error "Port $Port is STILL in use (pid $($still.OwningProcess | Select-Object -Unique)). Kill did not take."
    exit 1
}
Write-Host "Port $Port is free."
exit 0
