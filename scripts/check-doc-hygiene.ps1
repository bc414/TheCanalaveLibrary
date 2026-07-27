# check-doc-hygiene.ps1 — process-doc staleness gate (local + CI), sibling of check-design-tokens.ps1.
#
# Enforces CLAUDE.md's "Retiring or closing" rule mechanically: when a WU retires a pattern, term,
# or component, its name is APPENDED to $retiredTerms below in the same WU — from then on, any
# non-historical mention of it in a live doc fails the build. Prose rules alone already failed
# twice (the Global Flip and the Desktop/Mobile removal each left 8+ stale passages that survived
# five months of sessions until the 2026-07-27 WU-DocHygiene sweep found them).
#
# Three checks, all heuristic line-level lints (not proofs):
#   1. Retired terms in LIVE docs (skills, CLAUDE.md, status/grid_axes/folder_clusters,
#      middle_plan_v2) — a hit passes only if the same line carries a historical marker word
#      ("retired", "replaced", "former", ...). Dated ledgers (workplan*, audit/, retired plans)
#      are exempt: entries there are as-of-date records and legitimately name dead things.
#   2. Session-relative language ("this session", "just now", ...) anywhere in the process docs
#      except workplan* (whose dated DONE blocks anchor such phrases to the entry date).
#      CLAUDE.md bans it outright in persistent docs.
#   3. Live pointers into the retired plan files (forward_plan.md, v1 middle_plan.md) from live
#      docs, unless the line marks them as retired/carried-forward.
#
# Exit 0 = clean; exit 1 = violations listed. False positive? Add a marker word to the line
# (say WHY the dead name is mentioned — that's the fix a reader needs anyway).

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

# ---------------------------------------------------------------------------------------------
# Retired-term registry. APPEND a line per retirement WU; never delete (the term stays dead).
# Format: label = regex (case-sensitive where the identifier is).
$retiredTerms = [ordered]@{
    'Blazored.Typeahead (removed at Global Flip, 2026-07-13)' = 'Blazored\.?Typeahead'
    'Device-fork components (WU-ResponsiveMerge, 2026-07-18)' = '\b(TreeSearch|Story|Group|Search|Home|Bookshelves)(Desktop|Mobile)\b'
    'IDeviceDetectionService / DeviceLayout (2026-07-18)'     = 'IDeviceDetectionService|\bDeviceLayout\b'
    'SettingDetail (folded onto junction, WU-TagFanon 2026-07-26)' = '\bSettingDetail\b'
    'CharacterRelationshipType (deleted WU37.5, 2026-06-26)'  = 'CharacterRelationshipType'
    'MessagesHub (SignalR permanently ruled out, 2026-07-07)' = '\bMessagesHub\b'
    'layer7-redis.md (L7 dissolved, 2026-07-06)'              = 'layer7-redis'
}

# A line mentioning a retired term is legitimate when it says so. Loose by design — this is a
# lint over terms that are ALREADY dead; past-tense/negation words are strong-enough signal.
$historicalMarker = 'retired|superseded|replaced|REMOVED|removed|deleted|dissolved|former|pre-merge|absorbed|carri|historical|archived|cancelled|\bruled\b|dropped|exorcised|no longer|\bno\b|\bnot\b|\bCUT\b|folded|renamed|merged|used to|\bmoot\b|proposed|\bwas\b|\bwere\b'

# Live docs: loaded-as-current conventions and orientation. Everything else is a dated record.
$liveDocs = @(
    Get-Item 'CLAUDE.md'
    Get-Item '.claude/status.md'
    Get-Item '.claude/grid_axes.md'
    Get-Item '.claude/folder_clusters.md'
    Get-Item '.claude/middle_plan_v2.md'
    Get-ChildItem '.claude/skills' -Recurse -Filter '*.md'
)

$violations = New-Object System.Collections.Generic.List[string]

# --- Check 1: retired terms in live docs -------------------------------------------------------
foreach ($doc in $liveDocs) {
    foreach ($entry in $retiredTerms.GetEnumerator()) {
        $hits = Select-String -Path $doc.FullName -Pattern $entry.Value
        foreach ($hit in $hits) {
            if ($hit.Line -notmatch $historicalMarker) {
                $violations.Add(("RETIRED TERM [{0}] {1}:{2}: {3}" -f $entry.Key, $hit.Path, $hit.LineNumber, $hit.Line.Trim()))
            }
        }
    }
}

# --- Check 2: session-relative language --------------------------------------------------------
$sessionRelative = 'this session|last session|previous session|earlier today|just now'
$processDocs = @(Get-ChildItem '.claude' -Recurse -Filter '*.md') + @(Get-Item 'CLAUDE.md') |
    Where-Object { $_.Name -notlike 'workplan*' }
foreach ($doc in $processDocs) {
    $hits = Select-String -Path $doc.FullName -Pattern $sessionRelative
    foreach ($hit in $hits) {
        # CLAUDE.md's own rule text quotes the banned phrases — that's the rule, not a violation.
        if ($hit.Line -match 'Never write|session-relative') { continue }
        $violations.Add(("SESSION-RELATIVE {0}:{1}: {2}" -f $hit.Path, $hit.LineNumber, $hit.Line.Trim()))
    }
}

# --- Check 3: live pointers into retired plan files --------------------------------------------
$retiredPlanPointer = 'forward_plan(\.md)?|middle_plan\.md'
$planMarker = 'retired|carri|superseded|historical|mapping|Successor'
# middle_plan_v2 is the successor doc — it references its ancestry (v1 rows, carried-forward
# entries, the phase-mapping table) by design, so it is exempt from this check only.
$check3Docs = $liveDocs | Where-Object { $_.Name -ne 'middle_plan_v2.md' }
foreach ($doc in $check3Docs) {
    $hits = Select-String -Path $doc.FullName -Pattern $retiredPlanPointer
    foreach ($hit in $hits) {
        if ($hit.Line -notmatch $planMarker) {
            $violations.Add(("RETIRED-PLAN POINTER {0}:{1}: {2}" -f $hit.Path, $hit.LineNumber, $hit.Line.Trim()))
        }
    }
}

# --- Report -------------------------------------------------------------------------------------
if ($violations.Count -gt 0) {
    Write-Host "check-doc-hygiene: $($violations.Count) violation(s):" -ForegroundColor Red
    $violations | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    Write-Host "Fix the text, or add a historical marker word to the line if the mention is a deliberate as-of-date record."
    exit 1
}

Write-Host ("check-doc-hygiene: clean ({0} live docs, {1} retired terms, {2} process docs swept)." -f `
    $liveDocs.Count, $retiredTerms.Count, $processDocs.Count)
exit 0
