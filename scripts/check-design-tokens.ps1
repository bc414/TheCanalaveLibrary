# Design-token enforcement (Phase F of the design-solidification plan, 2026-07-10).
#
# The codebase's visual layer failed silently for its first three weeks (Tailwind v4 toolchain,
# v3-idiom authorship: bracket forms, bare-name guesses, undeclared tokens, oklch commas — all
# compiled to nothing). This script is the missing feedback loop: it fails the build when a
# color/z/shadow reference doesn't exist in @theme, when raw palette/hex colors appear outside
# the sanctioned files, or when RichTextView/EditorView render outside a ContentSurface.
# Run locally (.\scripts\check-design-tokens.ps1) or in CI.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$appCss = Join-Path $root 'TheCanalaveLibrary.Server\Styles\app.css'
$failures = New-Object System.Collections.Generic.List[string]

# Source files under scrutiny (UI markup + class-string-bearing code-behind).
$uiFiles = Get-ChildItem -Path (Join-Path $root 'TheCanalaveLibrary.SharedUI'), (Join-Path $root 'TheCanalaveLibrary.Server\Components'), (Join-Path $root 'TheCanalaveLibrary.Server\Identity') -Recurse -Include *.razor, *.cs |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }

# Sanctioned exceptions:
#   DevLoginBar        — deliberate dev-only raw yellow/blue signal.
#   DesignGalleryPage  — dev-only gallery; carries inline candidate values for review rounds.
#   ContentSurface     — Light/Sepia/Dark reader-override hexes live here by design (Phase E).
$rawColorExempt = 'DevLoginBar\.razor|DesignGalleryPage\.razor|ContentSurface\.razor'

# 1. Every referenced (--color-*), (--z-*) token must be declared in @theme.
$theme = Get-Content $appCss -Raw
$declared = [regex]::Matches($theme, '--(color|z|shadow|font)-[a-z0-9-]+(?=\s*:)') | ForEach-Object { $_.Value } | Sort-Object -Unique
foreach ($file in $uiFiles) {
    $text = Get-Content $file.FullName -Raw
    foreach ($m in [regex]::Matches($text, '\(--(?:color|z|shadow|font)-[a-z0-9-]+\)')) {
        $token = $m.Value.Trim('(', ')')
        if ($declared -notcontains $token) {
            $failures.Add("UNDECLARED TOKEN  $token  in $($file.FullName.Substring($root.Length + 1))")
        }
    }
    # var(--color-...) references in styles/attributes too
    foreach ($m in [regex]::Matches($text, 'var\((--(?:color|z|shadow)-[a-z0-9-]*[a-z0-9])\)')) {
        $token = $m.Groups[1].Value
        if ($declared -notcontains $token) {
            $failures.Add("UNDECLARED TOKEN  $token  in $($file.FullName.Substring($root.Length + 1))")
        }
    }
}

# 2. Raw Tailwind palette colors in class strings (outside exemptions).
$palettePattern = '\b(?:bg|text|border|ring|fill|stroke|accent|from|to|via|outline|decoration|divide|placeholder)-(?:red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose|slate|gray|zinc|neutral|stone)-\d{2,3}\b'
foreach ($file in $uiFiles) {
    if ($file.Name -match $rawColorExempt) { continue }
    $lineNo = 0
    foreach ($line in Get-Content $file.FullName) {
        $lineNo++
        if ($line -match $palettePattern) {
            $failures.Add("RAW PALETTE  $($Matches[0])  $($file.FullName.Substring($root.Length + 1)):$lineNo")
        }
    }
}

# 3. Raw hex colors in class strings or svg fill/stroke attributes (outside exemptions).
foreach ($file in $uiFiles) {
    if ($file.Name -match $rawColorExempt) { continue }
    $lineNo = 0
    foreach ($line in Get-Content $file.FullName) {
        $lineNo++
        if ($line -match '(?:class="[^"]*\[#[0-9A-Fa-f]{6}\]|(?:fill|stroke)="#[0-9A-Fa-f]{6}")') {
            $failures.Add("RAW HEX  $($file.FullName.Substring($root.Length + 1)):$lineNo")
        }
    }
}

# 4. Raw shadow/z/backdrop utilities (the role system uses tokens).
$overlayPattern = '\b(?:shadow-(?:sm|md|lg|xl|2xl)|z-(?:10|20|30|40|50)|bg-black/50)\b'
foreach ($file in $uiFiles) {
    if ($file.Name -match $rawColorExempt) { continue }
    $lineNo = 0
    foreach ($line in Get-Content $file.FullName) {
        $lineNo++
        if ($line -match $overlayPattern) {
            $failures.Add("RAW OVERLAY UTILITY  $($Matches[0])  $($file.FullName.Substring($root.Length + 1)):$lineNo")
        }
    }
}

# 5. RichTextView/EditorView must render inside a ContentSurface (file-level approximation;
#    the RichText folder itself and the gallery are the sanctioned homes of bare usage).
$csExempt = '\\RichText\\|DesignGalleryPage\.razor'
foreach ($file in ($uiFiles | Where-Object { $_.Extension -eq '.razor' })) {
    if ($file.FullName -match $csExempt) { continue }
    $text = Get-Content $file.FullName -Raw
    if ($text -match '<(RichTextView|EditorView)\b' -and $text -notmatch '<ContentSurface\b') {
        $failures.Add("UGC OUTSIDE CONTENTSURFACE  $($file.FullName.Substring($root.Length + 1))")
    }
}

if ($failures.Count -gt 0) {
    Write-Host "Design-token check FAILED ($($failures.Count) finding(s)):" -ForegroundColor Red
    $failures | Sort-Object -Unique | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host "Design-token check passed (tokens declared, no raw palette/hex/overlay utilities, UGC on ContentSurface)." -ForegroundColor Green
exit 0
