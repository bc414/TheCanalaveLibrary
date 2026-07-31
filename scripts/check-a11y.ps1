# Accessibility mechanical gates (WU-A11y (Structure), 2026-07-31 — decision row 12 resolved;
# see .claude/audit/Accessibility.md and roadmap.md §Resolved).
#
# A separate script from check-design-tokens.ps1 rather than an extension of it: that script's
# name and header both narrate a token-specific purpose, and a11y rules will churn independently
# of the frozen token rules. Mirrors its structure (file-set expression, $failures list, reporting,
# exit codes) so both gates read the same way in CI.
#
# Scope is static/naming accessibility only, by design: labelling, validation association, image
# alt text, and the Modal primitive's shell recipe staying confined to one file. What is
# DELIBERATELY NOT here, and why — none of these are statically detectable from markup alone:
#   - Keyboard operability / focus order — needs a rendered document with real Tab traversal.
#     WU-A11y-Keyboard's scope (browser band), not a PowerShell gate's.
#   - Contrast — needs computed CSS. Verified once via axe-DevTools per WU-A11y (Structure)'s
#     browser pass, not mechanically gated going forward (see audit/Accessibility.md).
#   - Heading order — needs a rendered document (conditional @if/@foreach branches make static
#     heading-level tracking unreliable).
#   - role="dialog" implies aria-modal — moot while aria-modal itself is deferred (gate B already
#     confines the modal recipe to one file; there is nothing else to check yet).
#   - @onclick on a <div>/<span> without role+tabindex — genuinely valuable and statically
#     detectable, but [data-flyout-catcher], [data-modal], and role="option" rows (added by
#     WU-A11y-Keyboard) are all legitimate non-interactive-role clickables. Deferred: run
#     report-only once triaged, then decide whether it becomes a hard gate.
# Run locally (.\scripts\check-a11y.ps1) or in CI.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$failures = New-Object System.Collections.Generic.List[string]

# Source files under scrutiny — same three directories as check-design-tokens.ps1.
$uiFiles = Get-ChildItem -Path (Join-Path $root 'TheCanalaveLibrary.SharedUI'), (Join-Path $root 'TheCanalaveLibrary.Server\Components'), (Join-Path $root 'TheCanalaveLibrary.Server\Identity') -Recurse -Include *.razor, *.cs |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }
$razorFiles = $uiFiles | Where-Object { $_.Extension -eq '.razor' }

# Every gate below reads a file's markup through this, not raw Get-Content: Razor comments
# (@* ... *@) routinely contain illustrative snippets of markup in prose (e.g. "<img src>" as a
# shorthand reference, or a retired pattern kept as a historical note) — matching gate patterns
# against a doc-comment produces a failure that points at nothing a developer can act on. Strip
# comments once per file, at the source, rather than teaching every individual gate regex to dodge
# them.
function Get-RazorMarkup([string]$path) {
    $raw = Get-Content $path -Raw
    return [regex]::Replace($raw, '@\*.*?\*@', '', 'Singleline')
}

# B. The modal overlay recipe (z-(--z-modal) + the tokenized shell) lives in exactly one file,
#    Dialogs/Modal.razor. Extracted WU-A11y 2026-07-31 (layer3.5-structure.md "Container
#    Composite") precisely so the shell can't be re-hand-rolled per consumer the way it was
#    before (9 sites shared one copy-pasted shell). This is the single highest-value rule here —
#    it's what makes the extraction stick.
foreach ($file in $razorFiles) {
    if ($file.Name -eq 'Modal.razor') { continue }
    $text = Get-RazorMarkup $file.FullName
    if ($text -match [regex]::Escape('z-(--z-modal)')) {
        $failures.Add("MODAL RECIPE OUTSIDE Modal.razor  $($file.FullName.Substring($root.Length + 1)) - the modal shell (backdrop/panel/z-layer) belongs in Dialogs/Modal.razor; use the Modal component instead of hand-rolling it.")
    }
}

# A. Every <label for="X"> resolves to an id="X" somewhere in the same file. Validated by hand
#    against the tree before this rule was written: it found two real bugs on an otherwise-clean
#    tree (ComposeConversationModal's dangling compose-recipient, LoginWith2fa's dangling
#    remember-machine) with zero false positives. Interpolated for= values (Razor @-expressions,
#    e.g. for="@id") are unresolvable statically and skipped - fails open, not closed.
foreach ($file in $razorFiles) {
    $text = Get-RazorMarkup $file.FullName
    $ids = [regex]::Matches($text, 'id="([^"@]+)"') | ForEach-Object { $_.Groups[1].Value }
    foreach ($m in [regex]::Matches($text, '<label\b[^>]*\bfor="([^"]+)"')) {
        $target = $m.Groups[1].Value
        if ($target -match '@') { continue }
        if ($ids -notcontains $target) {
            $failures.Add("LABEL FOR TARGET MISSING  $target  in $($file.FullName.Substring($root.Length + 1)) - no element in the file carries id=`"$target`".")
        }
    }
}

# C. Every ValidationMessage carries an id, and every such id is referenced by an
#    aria-describedby in the same file. Both halves are file-scoped by construction (ids are
#    per-form, never shared across files), so a plain containment check is sufficient - no need to
#    pair a specific ValidationMessage with a specific input.
foreach ($file in $razorFiles) {
    $text = Get-RazorMarkup $file.FullName
    foreach ($m in [regex]::Matches($text, '<ValidationMessage\b[^>]*>')) {
        $tag = $m.Value
        if ($tag -notmatch 'id="([^"]+)"') {
            $failures.Add("VALIDATIONMESSAGE MISSING ID  $($file.FullName.Substring($root.Length + 1)) - every ValidationMessage needs an id= so its input can reference it via aria-describedby.")
            continue
        }
        $vmId = $Matches[1]
        $describedByPattern = 'aria-describedby="' + [regex]::Escape($vmId) + '"'
        if ($text -notmatch $describedByPattern) {
            $failures.Add("VALIDATIONMESSAGE NOT DESCRIBEDBY  $vmId  in $($file.FullName.Substring($root.Length + 1)) - no input in the file has aria-describedby=`"$vmId`".")
        }
    }
}

# E. Orphan <label> - carries neither for= nor wraps a labelable control. File-scoped only, never
#    line-by-line: a <label>...</label> pair can legitimately span many lines (e.g. wrapping a
#    RenderFragment child). Classifies every <label> in the file into three buckets and flags only
#    the third:
#      - has for=                              -> associated, skip
#      - wraps <input>/<select>/<textarea>/     -> wrapping (native or Blazor built-in Input*
#        <InputXxx>                                component), skip
#      - neither                                -> orphan
#    Validated by hand against the tree before this rule was written: a clean 49/42/43-ish
#    with-for/wrapping/orphan partition, with the only "false positives" being labels that wrap a
#    hidden <InputFile> across multiple Razor conditional branches - those still match the wrapping
#    regex (Input[A-Za-z]+ covers the full component name, not just its first letter - an earlier
#    draft of this regex used Input[A-Z]\b, which never matches a multi-letter suffix like
#    InputFile/InputText/InputSelect/InputTextArea/InputDate/InputCheckbox because \b requires a
#    word boundary immediately after the single capital, which multi-letter names never have).
#    A label straddling markup this regex can't parse (e.g. an @if split across the label
#    boundary) simply fails to match at all - fails open, not closed.
foreach ($file in $razorFiles) {
    $text = Get-RazorMarkup $file.FullName
    foreach ($m in [regex]::Matches($text, '<label\b([^>]*)>(.*?)</label>', 'Singleline')) {
        $attrs = $m.Groups[1].Value
        $inner = $m.Groups[2].Value
        if ($attrs -match '\bfor=') { continue }
        if ($inner -match '<(input|select|textarea|Input[A-Za-z]+)\b') { continue }
        $snippet = ($inner -replace '\s+', ' ').Trim()
        if ($snippet.Length -gt 60) { $snippet = $snippet.Substring(0, 60) + '...' }
        $failures.Add("ORPHAN LABEL  $($file.FullName.Substring($root.Length + 1)) - `"$snippet`" has no for= and wraps no labelable control.")
    }
}

# D. Icon-only <button> (its entire trimmed inner content is a single literal glyph from a fixed
#    set) needs an accessible name - neither aria-label nor title present on the opening tag.
#    Deliberately narrow: the general "button with no text content" case needs Razor-expression
#    parsing (interpolated labels, ternaries, child components) and would drown in false
#    positives. This form is a single-line regex with near-zero false-positive risk, and it's
#    already 100% clean across the tree (TagChip's "Remove tag" X already carries aria-label) -
#    this rule is a regression ratchet, not a fix-up. Glyphs are written as \uXXXX escapes, not
#    literal characters, so this file's executable lines stay pure ASCII (Windows PowerShell 5.1
#    misreads non-BOM UTF-8 in executable code, though not in comments - see git history for the
#    parse error this caused the first time gate B's message string carried a literal em dash).
$iconGlyphCodepoints = 0x2715, 0x22EF, 0x22EE, 0x2191, 0x2193, 0x00D7, 0x25B4, 0x25BE, 0x2190, 0x2192, 0x2699, 0x2691
$iconGlyphPattern = ($iconGlyphCodepoints | ForEach-Object { [regex]::Escape([char]$_) }) -join '|'
foreach ($file in $razorFiles) {
    $text = Get-RazorMarkup $file.FullName
    $pattern = '<button(?![^>]*\baria-label=)(?![^>]*\btitle=)[^>]*>\s*(?:' + $iconGlyphPattern + ')\s*</button>'
    foreach ($m in [regex]::Matches($text, $pattern)) {
        $failures.Add("ICON BUTTON MISSING NAME  $($file.FullName.Substring($root.Length + 1)) - a single-glyph button needs aria-label (or title).")
    }
}

# F. Every <img> carries alt=; and alt="" + title= together is a defect (layer4-style.md's "no
#    title-only essential info" rule - title is hover-only and unreliably exposed to AT, so
#    pairing it with a decorative alt="" makes the image's information invisible to assistive
#    tech). The missing-alt half is a pure non-regression ratchet - already 100% compliant
#    (24 of 24 <img> tags carry alt today).
foreach ($file in $razorFiles) {
    $text = Get-RazorMarkup $file.FullName
    foreach ($m in [regex]::Matches($text, '<img\b[^>]*/?>')) {
        $tag = $m.Value
        if ($tag -notmatch '\balt=') {
            $failures.Add("IMG MISSING ALT  $($file.FullName.Substring($root.Length + 1)) - every <img> needs an alt attribute (alt=`"`" is valid for decorative images).")
            continue
        }
        if ($tag -match 'alt=""' -and $tag -match '\btitle=') {
            $failures.Add("IMG ALT EMPTY WITH TITLE  $($file.FullName.Substring($root.Length + 1)) - alt=`"`" plus title= hides the image's info from AT (title is hover-only, not reliably exposed).")
        }
    }
}

# G. The prefers-reduced-motion block (WU-A11y (Structure), 2026-07-31) stays present in app.css -
#    a one-line latch against it being deleted by an unrelated CSS cleanup pass.
$appCss = Join-Path $root 'TheCanalaveLibrary.Server\Styles\app.css'
if ((Get-Content $appCss -Raw) -notmatch 'prefers-reduced-motion') {
    $failures.Add("REDUCED MOTION RULE MISSING  Server/Styles/app.css - the @media (prefers-reduced-motion: reduce) block was removed.")
}

if ($failures.Count -gt 0) {
    Write-Host "check-a11y: $($failures.Count) violation(s):" -ForegroundColor Red
    $failures | Sort-Object -Unique | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host "check-a11y passed." -ForegroundColor Green
exit 0
