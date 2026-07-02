# Wipes the Canalave Library dev database: stops the server, then DROP DATABASE ... WITH (FORCE).
# There is deliberately no CREATE here - the app's next Development startup recreates the database
# via EF migrations and re-runs DataSeeder (see TheCanalaveLibrary.Server/Data/DataSeeder.cs header
# for what a fresh DB contains). Never hand-delete rows to "clean" the dev DB; use this script.
#
#   .\scripts\reset-dev-db.ps1              # drop only (next server start rebuilds)
#   .\scripts\reset-dev-db.ps1 -Restart     # drop, then start the server in the background
#
# NOTE: keep this file ASCII-only (PowerShell 5.1 + BOM-less file encoding trap).
# Wipe vs keep decision guide: .claude/skills/run-server/SKILL.md "Dev DB lifecycle".

param(
    [switch]$Restart,
    [string]$Database = "TheCanalaveLibraryDB",
    [string]$PgHost = "localhost",
    [int]$PgPort = 5432,
    [string]$PgUser = "postgres",
    [int]$ServerPort = 5028
)

Set-StrictMode -Version Latest

# psql lives here per run-server/SKILL.md prerequisites; PATH in fresh shells already has it,
# but be explicit so the script works from any shell.
if (-not (Get-Command psql -ErrorAction SilentlyContinue)) {
    $env:Path += ";C:\Program Files\PostgreSQL\18\bin"
}
$env:PGPASSWORD = "butterfree"  # dev-only credentials, mirror appsettings.Development.json

# 1. Stop the server if it's running (open connections would otherwise hold the DB).
& (Join-Path $PSScriptRoot "stop-dev-server.ps1") -Port $ServerPort
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# 2. Drop the database. WITH (FORCE) terminates any straggler connections (PG 13+).
# The \" below is the PowerShell->native-exe escape for a literal double quote: the identifier
# MUST reach Postgres quoted, or PG lowercases it and silently "drops" a nonexistent database.
Write-Host "Dropping database ""$Database"" on ${PgHost}:${PgPort} ..."
$sql = 'DROP DATABASE IF EXISTS \"{0}\" WITH (FORCE);' -f $Database
psql -h $PgHost -p $PgPort -U $PgUser -d postgres -v ON_ERROR_STOP=1 -c $sql
if ($LASTEXITCODE -ne 0) {
    Write-Error "DROP DATABASE failed (psql exit $LASTEXITCODE)."
    exit 1
}

# Verify it is actually gone (guards against the identifier-quoting failure mode).
$exists = psql -h $PgHost -p $PgPort -U $PgUser -d postgres -t -A -c ("SELECT 1 FROM pg_database WHERE datname = '{0}';" -f $Database)
if ($exists -eq "1") {
    Write-Error "Database ""$Database"" still exists after DROP - identifier quoting failed?"
    exit 1
}
Write-Host "Database dropped. Next Development server start recreates + migrates + seeds it."

# 3. Optionally bring the server straight back up (which performs the rebuild now).
if ($Restart) {
    & (Join-Path $PSScriptRoot "start-dev-server.ps1") -Background -Port $ServerPort
    exit $LASTEXITCODE
}
exit 0
