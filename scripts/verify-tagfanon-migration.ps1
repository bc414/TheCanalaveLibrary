<#
.SYNOPSIS
  Proves the WU-TagFanon overlay migration is data-preserving, against POPULATED pre-migration data.

.DESCRIPTION
  A data-migration's transformation SQL only runs when there are rows to transform. Applying it to
  a freshly-dropped dev database exercises none of it -- the fold/merge/truncate steps all run
  against zero rows and "it applied cleanly" proves nothing. This script stands up a scratch
  database at the PRE-overlay schema, seeds representative old-shape rows (both gate flags
  independently, an over-length description, an OC overlay, a SettingDetail side-row), applies the
  migration, and asserts every preservation claim.

  Run it whenever the WU-TagFanon migrations are edited. Non-zero exit = a preservation claim broke.

.NOTES
  Server-only path (local PG 18 on 5432). Uses `dotnet ef --connection` deliberately: the EF
  design-time host does NOT read appsettings.Development.json (see run-server/SKILL.md
  "Known tooling false-alarm").
#>
[CmdletBinding()]
param(
    [string]$DbName   = 'canalave_migtest',
    [string]$PgHost   = 'localhost',
    [int]   $PgPort   = 5432,
    [string]$PgUser   = 'postgres',
    [string]$PgPass   = 'butterfree',
    [string]$PreMigration = 'RecLifecycle'   # the migration immediately before the overlay one
)

# 'Continue', NOT 'Stop': PowerShell 5.1 surfaces ANY native-command stderr as an ErrorRecord,
# so psql's harmless NOTICE lines (e.g. "database does not exist, skipping") would terminate the
# script under 'Stop'. Correctness here comes from explicit $LASTEXITCODE checks + the assertions.
$ErrorActionPreference = 'Continue'
$env:PGPASSWORD = $PgPass
$conn = "Host=$PgHost;Port=$PgPort;Database=$DbName;Username=$PgUser;Password=$PgPass"
$repoRoot  = Split-Path $PSScriptRoot -Parent
$serverDir = Join-Path $repoRoot 'TheCanalaveLibrary.Server'
$failures  = New-Object System.Collections.Generic.List[string]

function Invoke-Psql([string]$Db, [string]$Sql) {
    # NO 2>&1 here: PowerShell 5.1 wraps a native command's stderr in ErrorRecords
    # (NativeCommandError) and, under $ErrorActionPreference='Stop', throws on psql's harmless
    # NOTICE output. Let stderr flow to the console and judge success by $LASTEXITCODE only.
    # SQL goes through a FILE, not -c: Windows PowerShell's native-argument quoting strips the
    # embedded double quotes that Postgres needs around case-sensitive identifiers
    # (INSERT INTO "AspNetUsers" arrived as AspNetUsers -> relation does not exist).
    # stderr goes to a temp file rather than $null so a real failure still reports WHY.
    $sqlFile = [System.IO.Path]::GetTempFileName()
    $errFile = [System.IO.Path]::GetTempFileName()
    try {
        [System.IO.File]::WriteAllText($sqlFile, $Sql, (New-Object System.Text.UTF8Encoding($false)))
        $out = & psql -h $PgHost -p $PgPort -U $PgUser -d $Db -t -A -q -v ON_ERROR_STOP=1 -f $sqlFile 2>$errFile
        if ($LASTEXITCODE -ne 0) {
            $stderr = (Get-Content $errFile -Raw)
            throw "psql failed (exit $LASTEXITCODE).`nSTDERR:`n$stderr"
        }
    } finally {
        Remove-Item $sqlFile -ErrorAction SilentlyContinue
        Remove-Item $errFile -ErrorAction SilentlyContinue
    }
    return ($out | Where-Object { $_ -ne '' })
}

function Assert-Equal([string]$Label, $Expected, $Actual) {
    if ("$Expected" -eq "$Actual") {
        Write-Host "  PASS  $Label" -ForegroundColor Green
    } else {
        Write-Host "  FAIL  $Label (expected '$Expected', got '$Actual')" -ForegroundColor Red
        $failures.Add($Label)
    }
}

Write-Host "`n== Scratch database: $DbName ==" -ForegroundColor Cyan
Invoke-Psql 'postgres' "DROP DATABASE IF EXISTS $DbName;" | Out-Null
Invoke-Psql 'postgres' "CREATE DATABASE $DbName;"         | Out-Null

Write-Host "== Applying migrations up to '$PreMigration' (pre-overlay schema) ==" -ForegroundColor Cyan
Push-Location $serverDir
try {
    & dotnet ef database update $PreMigration --context ApplicationDbContext --connection $conn |
        Select-String -Pattern 'Applying migration|Done' | Select-Object -Last 3 | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet ef update to $PreMigration failed." }
} finally { Pop-Location }

Write-Host "== Seeding representative OLD-SHAPE rows ==" -ForegroundColor Cyan
$seed = @'
BEGIN;
INSERT INTO tags (tag_name, tag_type_id, is_fanon, allow_oc_details, allow_setting_details, description, sprite_identifier)
VALUES ('OcOnly',0,false,true,false,repeat('x',512),'spr_oconly'),
       ('SettingOnly',1,false,false,true,'short setting desc',NULL),
       ('BothFlags',0,false,true,true,NULL,NULL),
       ('NeitherFlag',2,false,false,false,'plain genre',NULL);
INSERT INTO "AspNetUsers" (user_name, normalized_user_name, email, normalized_email, email_confirmed,
  password_hash, security_stamp, concurrency_stamp, phone_number_confirmed, two_factor_enabled,
  lockout_enabled, access_failed_count, show_mature_content, theme_id, account_status,
  active_report_count, allow_discovery_from_hidden_favorites, prefers_animated_sprites,
  prefers_data_saver_mode, author_settings, privacy_settings, reader_settings)
VALUES ('MigAuthor','MIGAUTHOR','m@x.invalid','M@X.INVALID',true,'h','s','c',false,false,true,0,false,1,0,0,false,true,false,'{}','{}','{}');
INSERT INTO stories (author_id, rating, story_status_id, published_date, last_updated_date, word_count, is_taken_down, active_report_count)
VALUES ((SELECT id FROM "AspNetUsers" WHERE user_name='MigAuthor'),0,2,now(),now(),0,false,0);
INSERT INTO story_characters (story_id, character_tag_id, priority, is_oc, oc_name, oc_bio)
VALUES ((SELECT story_id FROM stories LIMIT 1),(SELECT tag_id FROM tags WHERE tag_name='OcOnly'),1,true,'Legacy OC Name','Legacy OC bio prose.');
INSERT INTO story_tags (story_id, tag_id, priority)
VALUES ((SELECT story_id FROM stories LIMIT 1),(SELECT tag_id FROM tags WHERE tag_name='SettingOnly'),0);
INSERT INTO setting_details (story_id, base_tag_id, name, description)
VALUES ((SELECT story_id FROM stories LIMIT 1),(SELECT tag_id FROM tags WHERE tag_name='SettingOnly'),'Legacy Region Name','Legacy setting description.');
COMMIT;
'@
Invoke-Psql $DbName $seed | Out-Null

Write-Host "== Applying the WU-TagFanon migrations to POPULATED data ==" -ForegroundColor Cyan
Push-Location $serverDir
try {
    & dotnet ef database update --context ApplicationDbContext --connection $conn |
        Select-String -Pattern 'Applying migration|Done' | Select-Object -Last 4 | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet ef update to latest failed -- the migration did not survive real data." }
} finally { Pop-Location }

Write-Host "`n== Assertions ==" -ForegroundColor Cyan

# 1. Gate flags OR-merge: allow_custom_name = allow_oc_details OR allow_setting_details.
Assert-Equal 'OcOnly      -> allow_custom_name true'  't' (Invoke-Psql $DbName "SELECT allow_custom_name FROM tags WHERE tag_name='OcOnly';")
Assert-Equal 'SettingOnly -> allow_custom_name true'  't' (Invoke-Psql $DbName "SELECT allow_custom_name FROM tags WHERE tag_name='SettingOnly';")
Assert-Equal 'BothFlags   -> allow_custom_name true'  't' (Invoke-Psql $DbName "SELECT allow_custom_name FROM tags WHERE tag_name='BothFlags';")
Assert-Equal 'NeitherFlag -> allow_custom_name false' 'f' (Invoke-Psql $DbName "SELECT allow_custom_name FROM tags WHERE tag_name='NeitherFlag';")

# 2. Over-length description truncated to the new 500 bound rather than failing the ALTER.
Assert-Equal 'Over-length description truncated to 500' '500' (Invoke-Psql $DbName "SELECT length(description) FROM tags WHERE tag_name='OcOnly';")

# 3. Sprite identifier survives the 50 -> 100 widen.
Assert-Equal 'Sprite identifier preserved' 'spr_oconly' (Invoke-Psql $DbName "SELECT sprite_identifier FROM tags WHERE tag_name='OcOnly';")

# 4. Character overlay renamed with values intact.
Assert-Equal 'oc_name -> custom_name value' 'Legacy OC Name'       (Invoke-Psql $DbName "SELECT custom_name FROM story_characters;")
Assert-Equal 'oc_bio  -> nuance value'      'Legacy OC bio prose.' (Invoke-Psql $DbName "SELECT nuance FROM story_characters;")
Assert-Equal 'is_oc preserved'              't'                    (Invoke-Psql $DbName "SELECT is_oc FROM story_characters;")

# 5. The load-bearing step: SettingDetail side-row folded onto its StoryTag junction row.
Assert-Equal 'SettingDetail.name -> story_tags.custom_name'        'Legacy Region Name'          (Invoke-Psql $DbName "SELECT custom_name FROM story_tags;")
Assert-Equal 'SettingDetail.description -> story_tags.nuance'      'Legacy setting description.' (Invoke-Psql $DbName "SELECT nuance FROM story_tags;")

# 6. Old table gone.
Assert-Equal 'setting_details table dropped' '0' (Invoke-Psql $DbName "SELECT COUNT(*) FROM information_schema.tables WHERE table_name='setting_details';")

Write-Host "`n== Cleanup ==" -ForegroundColor Cyan
Invoke-Psql 'postgres' "DROP DATABASE IF EXISTS $DbName;" | Out-Null

if ($failures.Count -gt 0) {
    Write-Host "`n$($failures.Count) preservation claim(s) BROKEN:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}
Write-Host "`nAll data-preservation claims verified against populated pre-migration data." -ForegroundColor Green
exit 0
