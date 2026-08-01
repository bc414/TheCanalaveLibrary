# Render-mode gate (WU-H10Fix, 2026-07-31 - see .claude/audit/Identity.md and tracker item H10).
#
# Enforces ONE rule, the one that has now shipped broken twice:
#
#   A component that can render on a STATIC-SSR page must not use [PersistentState].
#
# Why it needs a gate rather than a paragraph. layer5-wasm.md has stated this rule since the
# Global Flip wave (it was written the first time it happened, to ReaderDisplayProvider). It was
# then violated twice anyway - MessagesNavLink on 2026-07-13 and NotificationBellInner on
# 2026-07-15 - and the whole /Account/* funnel returned a raw 500 for eighteen days with a green
# test suite the entire time. The failure mode is invisible to the bUnit tier by construction
# (bUnit renders with no render mode in every test, so it never runs
# ComponentStatePersistenceManager.InferRenderModes at all). StaticSsrPageRenderTests covers the
# symptom at the wire; this covers the cause, statically, before anything runs.
#
# The mechanism it defends against: on a static-SSR render App.razor's PageRenderMode is null, so
# a [PersistentState] property registers a persistence callback the framework cannot infer a
# render mode for, and InferRenderModes throws at persist time - 500ing the entire page, not just
# the component. The sanctioned alternative is the manual API with an EXPLICIT render mode:
# ApplicationState.RegisterOnPersisting(callback, RenderMode.InteractiveAuto). See
# ReaderDisplayProvider.razor / MessagesNavLink.razor for worked examples.
#
# The closure is COMPUTED, not listed. That is the whole point: a new chrome component added to
# MainLayout in a year is caught without anyone remembering this rule exists. Structure (file-set
# expression, $failures list, reporting, exit codes) mirrors check-a11y.ps1 and
# check-design-tokens.ps1 so all three gates read the same way in CI.
#
# Executable lines stay pure ASCII - Windows PowerShell 5.1 misreads non-BOM UTF-8 in code
# (comments are fine). Same constraint check-a11y.ps1 documents.
#
# Run locally (.\scripts\check-render-modes.ps1) or in CI.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$failures = New-Object System.Collections.Generic.List[string]

$projectDirs = @(
    (Join-Path $root 'TheCanalaveLibrary.SharedUI'),
    (Join-Path $root 'TheCanalaveLibrary.Client'),
    (Join-Path $root 'TheCanalaveLibrary.Server')
)

$razorFiles = Get-ChildItem -Path $projectDirs -Recurse -Include *.razor |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }

# Component name -> file(s). A duplicate simple name resolves to every match, which makes the
# closure slightly wider than reality - deliberately fails closed, not open.
$byName = @{}
foreach ($f in $razorFiles) {
    $name = [IO.Path]::GetFileNameWithoutExtension($f.Name)
    if (-not $byName.ContainsKey($name)) { $byName[$name] = New-Object System.Collections.Generic.List[string] }
    $byName[$name].Add($f.FullName)
}

# Razor comments routinely quote the very pattern this gate looks for - both fixed components
# carry a header explaining why they do NOT use [PersistentState]. Strip comments at the source
# rather than teaching the match to dodge them (same approach, and same reason, as
# check-a11y.ps1's Get-RazorMarkup).
function Get-RazorMarkup([string]$path) {
    $raw = Get-Content $path -Raw
    return [regex]::Replace($raw, '@\*.*?\*@', '', 'Singleline')
}

function Rel([string]$path) { return $path.Substring($root.Length + 1) }

# --- Roots of the static-SSR closure -------------------------------------------------------
#
# Three distinct ways a component ends up rendering with no render mode:
#
#   1. Client/Routes.razor - wraps EVERY route, including the statically-routed ones. Anything
#      it renders directly (ThemeContextProvider, ReaderDisplayProvider, UserActivityTracker)
#      renders on the Identity pages too. This is how ReaderDisplayProvider was caught in 2026-07.
#   2. SharedUI/Layout/MainLayout.razor - the AuthorizeRouteView DefaultLayout. Every static-SSR
#      page that declares no @layout of its own (all of Identity/Pages/*, /Error) gets it, so its
#      chrome renders under whatever render mode the PAGE resolved to. This is how MessagesNavLink
#      and NotificationBellInner were missed in 2026-07 (H10).
#   3. Any .razor file governed by [ExcludeFromInteractiveRouting] - the attribute that makes
#      App.razor's AcceptsInteractiveRouting() return false in the first place. Today it is set
#      folder-wide by Identity/Pages/_Imports.razor and ContentGate/_Imports.razor; a page that
#      ever carries it directly is picked up too.
$roots = New-Object System.Collections.Generic.List[string]

foreach ($known in @(
    (Join-Path $root 'TheCanalaveLibrary.Client\Routes.razor'),
    (Join-Path $root 'TheCanalaveLibrary.SharedUI\Layout\MainLayout.razor')
)) {
    if (-not (Test-Path $known)) {
        $failures.Add("CLOSURE ROOT MISSING  $(Rel $known) - this gate's closure is anchored on it. If the file moved, update scripts/check-render-modes.ps1 in the same change.")
        continue
    }
    $roots.Add($known)
}

# Folders whose _Imports.razor carries [ExcludeFromInteractiveRouting] govern themselves and every
# descendant folder (Razor _Imports semantics), so match on path prefix.
$excludedDirs = New-Object System.Collections.Generic.List[string]
foreach ($imports in ($razorFiles | Where-Object { $_.Name -eq '_Imports.razor' })) {
    if ((Get-RazorMarkup $imports.FullName) -match 'ExcludeFromInteractiveRouting') {
        $excludedDirs.Add($imports.DirectoryName)
    }
}
foreach ($f in $razorFiles) {
    $isStatic = (Get-RazorMarkup $f.FullName) -match 'ExcludeFromInteractiveRouting'
    if (-not $isStatic) {
        foreach ($dir in $excludedDirs) {
            if ($f.FullName.StartsWith($dir + '\', [StringComparison]::OrdinalIgnoreCase)) { $isStatic = $true; break }
        }
    }
    if ($isStatic) { $roots.Add($f.FullName) }
}

if ($excludedDirs.Count -eq 0) {
    $failures.Add("NO STATIC-SSR SURFACE FOUND  - no _Imports.razor carries [ExcludeFromInteractiveRouting]. Either the Identity pages stopped being static SSR (in which case this gate needs rewriting) or the scan path is wrong.")
}

# --- Transitive walk -----------------------------------------------------------------------
# Follows child components (<Foo ...>) and @layout references. Names that resolve to no .razor
# file are framework or built-in components (AuthorizeView, CascadingValue, ...) and drop out on
# their own - no allow-list to maintain.
$closure = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$queue = New-Object System.Collections.Generic.Queue[string]
foreach ($r in $roots) { if ($closure.Add($r)) { $queue.Enqueue($r) } }

while ($queue.Count -gt 0) {
    $current = $queue.Dequeue()
    $text = Get-RazorMarkup $current

    $names = New-Object System.Collections.Generic.List[string]
    foreach ($m in [regex]::Matches($text, '<([A-Z][A-Za-z0-9_]*)\b')) { $names.Add($m.Groups[1].Value) }
    foreach ($m in [regex]::Matches($text, '@layout\s+(?:[A-Za-z0-9_.]*\.)?([A-Za-z0-9_]+)')) { $names.Add($m.Groups[1].Value) }

    foreach ($name in $names) {
        if (-not $byName.ContainsKey($name)) { continue }
        foreach ($path in $byName[$name]) {
            if ($closure.Add($path)) { $queue.Enqueue($path) }
        }
    }
}

# --- The rule ------------------------------------------------------------------------------
foreach ($path in $closure) {
    $text = Get-RazorMarkup $path
    if ($text -match '\[PersistentState') {
        $failures.Add("PERSISTENTSTATE ON A STATIC-SSR SURFACE  $(Rel $path) - this component can render on a static-SSR page (Identity/*, /status-code/*), where the framework has no render mode to infer and InferRenderModes throws, 500ing the whole page. Use the manual API with an explicit render mode instead: ApplicationState.RegisterOnPersisting(callback, RenderMode.InteractiveAuto) - see ReaderDisplayProvider.razor and layer5-wasm.md 'Components that ALSO render on static-SSR pages'.")
    }
}

if ($failures.Count -gt 0) {
    Write-Host "check-render-modes: $($failures.Count) violation(s):" -ForegroundColor Red
    $failures | Sort-Object -Unique | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host "check-render-modes passed ($($closure.Count) components in the static-SSR closure)." -ForegroundColor Green
exit 0
