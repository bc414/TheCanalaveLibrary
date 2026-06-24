# Layer 3.5 — UI Structure (Composition + Skeleton)

The markup skeleton: which child Razor components form the tree, what HTML elements exist,
what conditions drive `@if`/`@foreach`, how data flows through `[Parameter]` to children.
Decidable once the component system is known — before visual design.

## Composition Principles

### Data Flow: DTOs Through the Tree

DTOs flow from dispatcher to children as `[Parameter]`. The dispatcher loads once; children
receive and render. Don't decompose DTOs into primitive parameters at the composition boundary:

```razor
@* Dispatcher passes the DTO *@
<StoryDesktop Story="Story" Chapters="Chapters" OnFavorite="HandleFavorite" />

@* StoryDesktop passes through to leaves *@
<StoryCard Story="Story" />
```

**Context-specific augmentation** — when a parent has data that doesn't belong in the child's DTO
(e.g., `FavoriteDate` alongside a `StoryCard`), the parent renders it as a sibling, not by
contaminating the DTO or the child:

```razor
@foreach (var item in FavoriteItems)
{
    <div class="...">
        <StoryCard Story="item.Story" />
        <span>Added @item.FavoriteDate.ToShortDateString()</span>
    </div>
}
```

### Parent-owns-arrangement

Dispatcher and pass-through composites control spatial arrangement of children. Children are
agnostic about their placement — they work the same in a 3-column grid and a 1-column stack.

### Desktop/Mobile Branching

The dispatcher pattern — the dispatcher loads data, detects device, branches to a presentation
composite:

```razor
@* StoryPage.razor — dispatcher *@
@if (_isMobile)
{
    <StoryMobile Story="Story" Chapters="Chapters" OnFavorite="HandleFavorite" />
}
else
{
    <StoryDesktop Story="Story" Chapters="Chapters" OnFavorite="HandleFavorite" />
}
```

Desktop and mobile composites reuse the **same leaf components**. They differ in arrangement
(grid vs stack), component ordering, and which elements appear.

### When to Create Separate Desktop/Mobile vs. Responsive Prefixes

**Responsive prefixes** (one component): the elements are identical, only sizing/layout changes.

**Separate components**: different elements, different hierarchy, different interaction patterns.
A top bar on desktop and a bottom sheet on mobile are structurally different.

### Moderator-Only Pages

Pages gated to moderator/admin roles skip the dispatcher/desktop/mobile pattern. Desktop-only,
single layout. Applies to: Reports (`/mod/reports`), Story Submissions (`/mod/submissions`),
User Management (`/mod/users`).

## Component Hierarchy Patterns

### Leaf (no children)

```razor
@* TagChip.razor — Tag is a TagChipDto (Core/Tags/); SpriteUrl arrives pre-resolved by the
   producing read service (layer2-services.md "Sprite URLs Are Resolved Server-Side") *@
<span class="...tag type styling...">
    @if (Tag.SpriteUrl is not null)
    {
        <img src="@Tag.SpriteUrl" alt="" class="..." />
    }
    @Tag.TagName
    @if (OnRemove.HasDelegate)
    {
        <button @onclick="() => OnRemove.InvokeAsync()" class="...">✕</button>
    }
</span>

@code {
    [Parameter, EditorRequired] public TagChipDto Tag { get; set; } = null!;
    [Parameter] public EventCallback OnRemove { get; set; }
}
```

No child Razor components. Only raw HTML elements. `@if`/`@foreach` driven by parameters.

### Coordination Composite (manages state across children)

```razor
@* UserStoryInteractionPanel.razor *@
@* Parameters:
     StoryId (int, EditorRequired)
     State (UserStoryInteractionStateDto?, flows in from batch-loading parent — panel does NOT inject
            the read service; null treated as all-false. N+1 rule: page/deck loads state in one batch
            query and passes each card its slice.)
     Context (InteractionDisplayContext enum: Listing|Detail — controls clickable vs read-only)
     IsOwnStory (bool — renders Edit link instead of interaction buttons)
   Injects: IUserStoryInteractionWriteService (self-contained write, no read service) *@
<div class="flex items-center gap-2">
    @if (!IsOwnStory)
    {
        @* Iterate in enum declaration order — that IS the locked button order:
           Favorite → PrivateFavorite → Follow → Complete → ReadLater → Ignore
           IconPath/AccentColor/Label come from InteractionVisuals (SharedUI); values are transcribed
           verbatim from the locked table in audit/UserStoryInteractions.md (2026-06-22).
           Inline SVG, not a sprite URL — see layer4-style.md "Interaction Icons Are Inline SVG". *@
        @foreach (var type in Enum.GetValues<InteractionTypeEnum>())
        {
            var visuals = InteractionVisuals.For(type);
            <UserStoryInteractionButton IsActive="@GetIsActive(type)"
                               OnToggle="@(IsClickable(type) ? HandleToggle(type) : default)"
                               IconPath="@visuals.IconPath"
                               AccentColor="@visuals.AccentColor"
                               Label="@visuals.Label" />
        }
    }
    else
    {
        <a href="/story/@StoryId/edit">Edit Story</a>
    }
</div>
```

### Pass-Through Layout Composite (arranges children)

`ChapterNavigation` (WU18) is the canonical example. It is **injection-free** — all data arrives
as parameters; the dispatcher (WU26) loads the TOC + version list once and passes them to both
the top and bottom instances. Navigation is plain `<a href>` links (Blazor's Router intercepts
internal links in both InteractiveServer and InteractiveWasm — no full page reload, no
`NavigationManager` injection needed here). The chapter-select and version-picker disclosures
use HTML `<details>`/`<summary>` (no Blazor state, no JS) so all links are always present in
the DOM for bUnit testing regardless of the native open/close state. No sub-components —
everything is inline anchors and disclosure panels:

```razor
@* ChapterNavigation.razor — SharedUI/Chapters/ *@
<nav class="flex flex-wrap items-center gap-2" aria-label="Chapter navigation">
    @if (PreviousChapterNumber.HasValue)
    {
        <a href="/story/@StoryId/@PreviousChapterNumber" class="@NavLinkClasses(false)"
           aria-label="Previous chapter">&lsaquo;</a>
    }
    else
    {
        <span class="@NavLinkClasses(true)" aria-disabled="true"
              aria-label="Previous chapter">&lsaquo;</span>
    }

    <details class="relative">
        <summary class="flex cursor-pointer list-none items-center gap-1 ...">
            Chapter @CurrentChapterNumber
        </summary>
        <div class="absolute left-0 top-full z-10 ...">
            @foreach (var entry in Toc)
            {
                <a href="/story/@StoryId/@entry.ChapterNumber"
                   class="@TocEntryClasses(entry)"
                   aria-current="@(entry.ChapterNumber == CurrentChapterNumber ? "page" : null)">
                    @entry.ChapterNumber. @entry.Title
                    @if (entry.HasAlternateVersions)
                    {
                        <span title="Has alternate versions">&#8942;</span>
                    }
                </a>
            }
        </div>
    </details>

    @if (Versions.Count > 1)
    {
        <details class="relative">
            <summary class="...">@CurrentVersionLabel</summary>
            <div class="absolute ...">
                @foreach (var version in Versions)
                {
                    <a href="@VersionUrl(version)"
                       aria-current="@(version.VersionOrder == CurrentVersionOrder ? "page" : null)">
                        @VersionLabel(version)
                    </a>
                }
            </div>
        </details>
    }

    @if (NextChapterNumber.HasValue) { ... } else { <span aria-disabled="true" ...> }
</nav>

@code {
    [Parameter, EditorRequired] public int StoryId { get; set; }
    [Parameter, EditorRequired] public int CurrentChapterNumber { get; set; }
    [Parameter] public int CurrentVersionOrder { get; set; }    // 0 = primary
    [Parameter] public int? PreviousChapterNumber { get; set; }
    [Parameter] public int? NextChapterNumber { get; set; }
    [Parameter, EditorRequired] public IReadOnlyList<ChapterTocEntryDto> Toc { get; set; } = [];
    [Parameter] public IReadOnlyList<ChapterVersionDto> Versions { get; set; } = [];
    // URL helpers, style helpers — no lifecycle hooks, no IDisposable, no service injection.
    private string VersionUrl(ChapterVersionDto v) =>
        v.IsPrimary ? $"/story/{StoryId}/{CurrentChapterNumber}"
                    : $"/story/{StoryId}/{CurrentChapterNumber}/{v.VersionOrder}";
}
```

**Key rules this example establishes:**
- **`<details>`/`<summary>` for CSS disclosures** — native open/close, no JS/Blazor state.
  Apply `display: flex` to `<summary>` (via `flex` Tailwind class) to suppress the default
  browser marker triangle without needing `list-none` alone.
- **`<a>` for link variants; `<span aria-disabled>` for unavailable endpoints** — not
  `<button disabled>`, because these are navigation, not actions.
- **`aria-current="page"` on the currently-selected entry** in both the chapter dropdown and
  the version picker. Blazor renders `aria-current="@(condition ? "page" : null)"` as either
  the attribute (present, value `"page"`) or absent (not `aria-current=""`), matching
  the W3C pattern and making bUnit assertions on the attribute straightforward.
- **Version URL contract (spec §5.30.3):** primary version → clean URL (no versionOrder
  segment); alternate → append `/{VersionOrder}` (the ChapterContent's `SortOrder`).

### Container Composite (provides a visual vessel)

```razor
@* Card.razor *@
<div class="rounded-xl bg-surface shadow-sm @AdditionalClass">
    @ChildContent
</div>

@code {
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string? AdditionalClass { get; set; }
}
```

`ConfirmDialog` (WU9, universal — spec §5.30.9) is the same subtype with a `@bind-IsOpen` contract
instead of always-rendered `ChildContent`, since a dialog's defining behavior is *whether it renders at
all*:

```razor
@* ConfirmDialog.razor — SharedUI/Dialogs/ (cross-cutting cluster, no owning feature) *@
@if (IsOpen)
{
    <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" @onclick="Cancel">
        <div class="max-w-md rounded-xl bg-surface p-6 shadow-lg" @onclick:stopPropagation="true">
            @if (Title is not null) { <h2 class="...">@Title</h2> }
            @if (ChildContent is not null) { @ChildContent } else { <p>@Message</p> }
            <div class="flex justify-end gap-2">
                <button @onclick="Cancel">@CancelText</button>
                <button class="@(IsDestructive ? "bg-danger" : "bg-primary")" @onclick="Confirm">@ConfirmText</button>
            </div>
        </div>
    </div>
}

@code {
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }  // enables @bind-IsOpen
    [Parameter] public string? Title { get; set; }
    [Parameter] public string? Message { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }       // wins over Message when set
    [Parameter] public string ConfirmText { get; set; } = "Confirm";
    [Parameter] public string CancelText { get; set; } = "Cancel";
    [Parameter] public bool IsDestructive { get; set; }
    [Parameter] public EventCallback OnConfirm { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }

    private async Task Confirm() { await OnConfirm.InvokeAsync(); await Close(); }
    private async Task Cancel() { await OnCancel.InvokeAsync(); await Close(); }
    private async Task Close() { IsOpen = false; await IsOpenChanged.InvokeAsync(false); }
}
```

Consumer side mirrors the spec's spoiler example (`layer3-logic.md` "Spoiler Comment State"):

```razor
<ConfirmDialog @bind-IsOpen="_showConfirmDialog"
               Message="You haven't finished the story. Are you sure?"
               ConfirmText="Reveal"
               OnConfirm="() => _isRevealed = true" />
```

Backdrop click cancels (safe default before a destructive action); `@onclick:stopPropagation` on the
panel keeps clicks inside the dialog from bubbling to the backdrop. No escape-key dismissal in MVP —
backdrop + Cancel button cover it; deferred, not blocking. The overlay shell (`fixed inset-0 z-50 ...
bg-black/50` backdrop, `rounded-xl bg-surface ... shadow-lg` panel) is the same shell EditorView's
preview popup uses (see below) — reused, not reinvented, but **not extracted into a shared `Modal`
primitive** with only two consumers and two different flows (confirm/cancel vs. content-preview); that
extraction is deferred until a third consumer's shape clarifies what the shared part actually is.

### Owner-Conditional Edit Affordances on a Display Composite (settled WU21)

A display composite that renders a list of items owned by a user — but is also consumed read-only by
non-owners — gates per-row mutation controls behind an `IsEditable` parameter rather than duplicating
the component into owner/viewer variants:

```razor
@* VouchList.razor — SharedUI/Following/ *@
@foreach (var vouch in Vouches)
{
    <div class="...">
        <UserCard User="vouch.User" />
        @if (vouch.VouchText is not null)
        {
            <RichTextView Content="vouch.VouchText" />
        }
        @if (IsEditable)
        {
            <button @onclick="() => OnRemoveVouch.InvokeAsync(vouch.User.UserId)">Remove vouch</button>
        }
    </div>
}

@code {
    [Parameter, EditorRequired] public IReadOnlyList<VouchDisplayDto> Vouches { get; set; } = [];
    [Parameter] public bool IsEditable { get; set; }
    [Parameter] public EventCallback<int> OnRemoveVouch { get; set; }
}
```

The **hosting page (WU30, the profile dispatcher)** sets `IsEditable = (targetUserId == currentUserId)` for the
outgoing-vouches list; the incoming-vouches list is always read-only and is only fetched at all for the owner (the
`GetIncomingVouchesAsync` service method is scoped to the active user — no `userId` param). The composite itself
has no service injection and no knowledge of "is the current user the owner" — that decision belongs to the
dispatcher. The same pattern extends to any list whose items carry mutation controls only for their owner
(bookmark notes, custom-list entries, etc.).

**`IsEditable` is distinct from auth.** Setting it to `true` does not grant the service call — `OnRemoveVouch`
bubbles the `targetUserId` up to the dispatcher, which calls `IFollowingWriteService.RemoveVouchAsync`. The
button's *visibility* is `IsEditable`; the *authorization* to remove is enforced in the service
(`IActiveUserContext.UserId` must match the voucher).

### Third-Party Wrapper Composite

Blazored TextEditor has no `@bind-Value`. `<ToolbarContent>` is a `RenderFragment` of raw `ql-*`-class
markup (Quill reads the classes, not Blazor bindings); `<EditorContent>` is a `RenderFragment` that
seeds the **initial** DOM only — Quill parses it once at construction (`OnAfterRenderAsync(firstRender)`
inside the package), it is not a live two-way binding. Reading the edited HTML back out is
`Task<string> GetHTML()` via `@ref`; setting it programmatically after construction is
`Task LoadHTMLContent(string)`. There's no change event, so `EditorView` exposes a public
`GetHtmlAsync()` that the consuming form's `@ref` calls at submit time — pull-on-submit, not
push-on-change.

A boolean `Compact` toolbar parameter was tried (WU6) and discarded for two reasons: Quill binds
toolbar-button listeners once, at construction, so changing the `ToolbarContent` RenderFragment later
doesn't retroactively rewire anything — forcing a real rebuild needs `@key`-driven destroy/recreate,
which is exactly the device-axis problem `layer4-style.md` Responsive Breakpoints already settles
("when desktop and mobile are structurally different, use separate components instead"). So the
device axis is a **separate composition**, the same way `HomeDesktop`/`HomeMobile` and
`StoryDesktop`/`StoryMobile` are — not a runtime toggle inside one `EditorView`. **MVP ships the
desktop toolbar only; the mobile-compact variant is deferred** (not MVP-blocking).

Preview is an **overlay popup**, not an in-place swap of the editor out of the tree — swapping
reflows the surrounding page every toggle (confusing) and would destroy/recreate Quill (losing
cursor/scroll position) for no reason. Quill stays mounted continuously; the popup renders
`RichTextView` on top of a dimmed backdrop:

```razor
@* EditorView.razor — wraps Quill.js *@
<BlazoredTextEditor @ref="_quill">
    <ToolbarContent>
        @* desktop toolbar only for MVP — see note above *@
    </ToolbarContent>
    <EditorContent>@((MarkupString)(Html ?? string.Empty))</EditorContent>
</BlazoredTextEditor>

<button @onclick="OpenPreview">Preview</button>

@if (_isPreviewMode)
{
    <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" @onclick="ClosePreview">
        <div class="max-h-full max-w-2xl overflow-y-auto rounded-xl bg-surface p-6 shadow-lg"
             @onclick:stopPropagation="true">
            <RichTextView HtmlContent="@_previewHtml" />
            <button @onclick="ClosePreview">Close</button>
        </div>
    </div>
}

@code {
    [Parameter] public string? Html { get; set; }
    private BlazoredTextEditor? _quill;
    private bool _isPreviewMode;
    private string? _previewHtml;

    public async Task<string> GetHtmlAsync() => await _quill!.GetHTML();

    private async Task OpenPreview()
    {
        _previewHtml = await _quill!.GetHTML();
        _isPreviewMode = true;
    }

    private void ClosePreview() => _isPreviewMode = false;
}
```

**EditorView is universal** across ALL text surfaces: chapters, comments, author notes,
descriptions, recommendations, profile bios, blog posts, AND private messages. `EditorView` never
sanitizes its own output — see `layer2-services.md` "User HTML Is Sanitized Once, On Save — Never On
Display" and "The allow-list is the inverse of the toolbar".

**`TagSelector` is the same composite subtype with a different third-party wrapper (WU11):**
single-select `BlazoredTypeahead` sourced by a per-keystroke `SearchMethod`, with the selector's own
chip list rendered *above* the input (the package's built-in multi-select renders chips *inside* the
input — wrong layout per spec §5.30.4, so it's not used). Selecting a result adds to the chip list and
resets the bound value to `null`, clearing the input for the next pick:

```razor
@* TagSelector.razor — wraps a single-select BlazoredTypeahead *@
<div class="flex flex-col gap-2">
    <label class="...">@Label</label>

    <div class="flex flex-wrap gap-2">
        @foreach (var tag in _selected)
        {
            <TagChip Tag="tag" OnRemove="@(() => Remove(tag))" />
        }
    </div>

    <BlazoredTypeahead TValue="TagChipDto" TItem="TagChipDto"
                       SearchMethod="SearchMethod" Debounce="300" MinimumLength="2"
                       ValueChanged="OnPicked" ValueExpression="@(() => _picked)">
        <ResultTemplate Context="tag">
            <span class="inline-flex items-center gap-2">
                <span class="w-2 h-2 rounded-full @DotClass(tag.TagTypeId)"></span>
                @if (tag.SpriteUrl is not null) { <img src="@tag.SpriteUrl" class="w-4 h-4" alt="" /> }
                @tag.TagName
            </span>
        </ResultTemplate>
        <SelectedTemplate Context="tag">@tag.TagName</SelectedTemplate>
        <NotFoundTemplate>No tags found</NotFoundTemplate>
    </BlazoredTypeahead>
</div>

@code {
    [Parameter, EditorRequired] public TagTypeEnum TagType { get; set; }
    [Parameter] public IReadOnlyList<TagChipDto> SelectedTags { get; set; } = [];
    [Parameter] public EventCallback<IReadOnlyList<TagChipDto>> OnSelectionChanged { get; set; }

    private List<TagChipDto> _selected = [];
    private TagChipDto? _picked;

    private async Task<IEnumerable<TagChipDto>> SearchMethod(string term) =>
        (await TagService.SearchTagChipsAsync(TagType, term))
            .Where(t => _selected.All(s => s.TagId != t.TagId));

    private async Task OnPicked(TagChipDto? tag)
    {
        if (tag is not null && _selected.All(s => s.TagId != tag.TagId))
        {
            _selected.Add(tag);
            await OnSelectionChanged.InvokeAsync(_selected);
        }
        _picked = null; // clears the input for the next pick
    }
}
```

**`SelectedTemplate` is mandatory, not optional (WU11).** `BlazoredTypeahead.OnInitialized()` throws
`InvalidOperationException: ... requires a SelectedTemplate parameter` if it's omitted — unlike
`ResultTemplate`/`NotFoundTemplate`, which the package defaults sensibly. Omitting it doesn't fail
quietly: in single-select mode it's barely visible (the bound value resets to `null` immediately
after each pick, per `OnPicked` above), which makes it tempting to assume it's decorative and skip
it — it isn't. First symptom looked like a **prerender incompatibility**: the missing-parameter
exception killed `OnInitialized()` mid-init, which left the component's internal state (its debounce
timer) never constructed, so when the half-initialized instance was later torn down —
either the prerendered copy at the end of the static-render request, or the interactive copy when
its circuit hit the same `OnInitialized` exception and got torn down — `Dispose()` threw a *second*,
unrelated-looking `NullReferenceException` on that timer field. Chasing the Dispose symptom first is
a dead end; the real fault is always upstream, at `OnInitialized()`. Once `SelectedTemplate` is
supplied, both the prerendered and interactive lifecycles complete and dispose cleanly — there is
**no actual prerendering incompatibility** in this package; the disposal trace was a downstream
symptom, not a separate bug.

**Contract deviation from the spec's literal wording, deliberate:** §5.30.4 says
`EventCallback<IReadOnlyList<Tag>> OnSelectionChanged` — `Tag` is the EF entity. The DTO Firewall
(`layer2-services.md` axiom 6) forbids that crossing the service boundary into UI, so the real
contract emits `IReadOnlyList<TagChipDto>` — the render-ready type already minted for this cluster
(WU4). `TagSelector` stays type-scoped (one `TagTypeEnum` per instance) and knows nothing of
`Priority`/`StoryTag`; the consuming form maps `TagChipDto` → `StoryTagDTO` with a priority.

### Ambient Viewer Settings via Cascading Slim Bags

Some settings are ambient and viewer-scoped — they apply to every instance of a component on the
page, regardless of which feature embeds it, and threading them through every intermediate composite's
`[Parameter]` list would pollute contracts that have nothing to do with the setting (e.g. `StoryCard`,
`CommentItem` shouldn't carry reader-display params just to relay them to a nested `RichTextView`).
The pattern: a layout-level ancestor reads the viewer's full settings object once, converts it to a
**slim property bag** holding only the fields the leaf needs, and provides that bag via
`<CascadingValue Value="@displaySettings">`. The leaf consumes it as a `[CascadingParameter]`,
nullable, with sensible defaults when no provider is present (anonymous viewers, or pages that haven't
wired the provider yet):

```razor
@* RichTextView.razor — leaf *@
<div style="font-family: @(Display?.FontName ?? "Georgia"); font-size: @((Display?.FontSize ?? 16))px;
            line-height: @(Display?.LineHeight ?? 1.5f); max-width: @((Display?.TextWidth ?? 800))px;
            text-align: @((Display?.JustifyText ?? false) ? "justify" : "left")">
    @((MarkupString)HtmlContent)
</div>

@code {
    [Parameter, EditorRequired] public string? HtmlContent { get; set; }
    [CascadingParameter] public ReaderDisplaySettings? Display { get; set; }
}
```

The slim bag is **not** a DTO — it never crosses the service boundary (the read service or page
loads the full settings object, e.g. `User.ReaderSettings`, and converts in-process). Naming carries
the distinction: `*Dto` types are minted for service-boundary transfer; slim bags fed to components
get a plain descriptive name (`ReaderDisplaySettings`, not `ReaderDisplaySettingsDto`). The full
settings object itself is not split to produce the bag — it stays one cohesive unit at the storage/
service layer; only the presentation layer narrows it. Each ambient concern gets its own bag sized to
its consumers — `ReaderDisplaySettings` (font/size/line-height/width/justify) feeds `RichTextView`;
a future behavior-settings bag (auto-load-next, collapse-threads, pagination size) would feed whatever
component owns that behavior, not get bolted onto the display bag.

## Universal Components (Cross-Feature)

| Component | Type | Used By |
|---|---|---|
| `EditorView` | Third-party wrapper composite | Chapters, Comments, Recommendations, BlogPosts, Profiles, Messaging |
| `RichTextView` | Leaf | Chapters (reading), Comments (display), Recommendations, BlogPosts, Profiles, Messaging |
| `TagChip` | Leaf | Tags (display), Stories (cards), Discovery (results), Tag Directory |
| `TagSelector` | Coordination composite | Stories (tagging), Discovery (filtering), ResultsFilterPanel |
| `UserStoryInteractionButton` | Leaf | UserStoryInteractions (panel), Following |
| `StoryCard` | Leaf | Stories, Discovery (all types), Bookshelves, Groups, Profiles, Also Favorited |
| `StoryDeck` | Pass-through layout composite | Search page, Bookshelves, Profile tabs, Also Favorited/Recommended, Group story listing. NOT manual tree search (graph visualization). |
| `ResultsFilterPanel` | Coordination composite | Search page, Profile page tabs, Tree search page, Also Favorited section |
| `PaginationControls` | Leaf | Comments, Discovery, StoryDeck |
| `UserCard` | Leaf | Following (vouch display), Profiles, Groups, Comments, Recommendations, Messaging, Users search, Tree search nodes |
| `ConfirmDialog` | Container composite | Spoiler reveal, account deletion, leaving group, deleting list, unpublishing story |

## StoryCard and StoryDeck

**StoryCard** (leaf, WU13): pure leaf, no service injection. Contract:
- `[Parameter, EditorRequired] StoryListingDto Story` — warm-partition projection; includes
  `ShortDescription` (nullable, tooltip + synopsis) and `Tags` (sprite-resolved `TagChipDto` list).
- `[Parameter] UserStoryInteractionStateDto? UserStoryInteractionState` — batch-loaded by the parent
  via `GetStatesByStoryIdsAsync`; forwarded to the nested `UserStoryInteractionPanel`. Null = all-false.
- `[Parameter] bool IsOwnStory` — forwarded to the panel (renders Edit link instead of buttons).
- Optional gated caret `EventCallback`s: `OnDiscoverFromStory`, `OnCopyLink`, `OnReport`,
  `OnDownload` — gated by `.HasDelegate`; consumers wire only what exists.
- Private state: `bool _menuOpen`, `bool _coverArtFailed`.

Composes: `TagChip` (read-only, no `OnRemove`) + `UserStoryInteractionPanel` in Listing context.
Author byline is a **plain hyperlink, NOT `UserCard`** — spec §5.30.7, StoryCard is too compact.
Cover art: stored URL (`CoverArtRelativeUrl`) used verbatim (same discipline as avatars); `_coverArtFailed`
fallback on `@onerror`. Outer Margin Rule on root: `rounded-xl bg-surface` padding, no `m-*`.

Caret: always-present "View Story" link + optional items per `HasDelegate` (mirror of `UserCard`).

**StoryDeck** (pass-through layout composite, WU14): the container holding StoryCards. Owns the
three-state pattern internally (`Stories is null` → loading, `Count == 0` → empty, populated → grid
+ `PaginationControls`). Grid layout (`grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6`). Named
"Deck" because a deck is a curated ordered set of cards — avoids confusion with `StoryListingDto`.

Contract: `[EditorRequired] IReadOnlyList<StoryListingDto>? Stories` (nullable — null means loading),
`IReadOnlyDictionary<int, UserStoryInteractionStateDto>? UserStoryInteractionStates` (batch-loaded by
parent, keyed by StoryId), `int? CurrentUserId` (deck computes `IsOwnStory` per card), `string EmptyMessage`
(defaults to "No stories found."), plus pagination forwards (`CurrentPage`, `PageSize`, `TotalCount`,
`EventCallback<int> OnPageChanged`). No service injection. Caret callbacks deferred — additive when
the first consumer (Discovery/Report/Export) needs them.

## Rich-Text Editor Shells — Separate Leaves, Not a Shared Abstraction (WU29)

`EditorView` (the raw Quill.js wrapper) is universal — see "Third-Party Wrapper Composite" above and
the Universal Components table. The **shell** wrapping it (save/cancel row, busy state, context extras)
is *feature-scoped*, because each rich-text surface adds different extras:

| Shell | Feature | Extras vs. a bare EditorView |
|---|---|---|
| `CommentEditor` (WU20) | Comments | `ShowSpoilerToggle` + two-way `@bind-Spoiler`; `SaveLabel` for edit/reply/new |
| `RecommendationEditor` (WU29) | Recommendations | **No spoiler** (deliberate, §5.6); live **500-char-minimum meter** (`RecommendationConstants.MinLength`); submit disabled until met |

**Why not a shared `EditorForm` abstraction?** WU9 ConfirmDialog established the rule: don't extract
a shared primitive for two consumers with *different* extras — wait until a third consumer's shape
clarifies what the shared part actually is. The save/cancel/busy row is the only true overlap; the
per-feature context extras (spoiler checkbox vs. char meter) are the whole reason the shells exist.
Candidates for a 3rd consumer: BlogPosts (WU31), Messaging (WU35), Profile bio (WU30). If their
shells prove identical to one of the above, extract then — not now.

**Pull-on-submit** is the correct interaction across all shells: hold an `@ref` to the nested
`EditorView` and call `await _editor.GetHtmlAsync()` in the submit handler. Never bind two-way.
The owning composite (never the shell leaf) sanitizes and persists.

## Filter-Axis Component Pattern

**The unit of reuse is the individual filter axis, not the assembled panel.** Manual tree search
reuses tag + interaction-exclusion filtering but has no `StoryDeck`, no sort, and no FTS. The
assembled `ResultsFilterPanel` is one *assembler* of the axes; the tree page assembles its own
subset directly.

Axis components: **emit on every change; never know about `StoryFilterDto`, sort, decks, or graphs.**
The assembler (panel or tree page) buffers each axis's emitted slice in `@code` and fires one batched
apply (via an "Apply Filters" button). Live re-filtering is never correct for the tree search —
changing edge counts cause wild graph relayout — so both paths use an Apply button.

### `TagFilter` (SharedUI/Tags/)

Include/exclude grouping over one or more `TagSelector` instances. Params:
- `IReadOnlyList<TagTypeEnum> TagTypes` — which tag types to expose (default = filterable set)
- `bool AllowExclude = true`
- `IReadOnlyList<int> IncludedTagIds` (seed)
- `IReadOnlyList<int> ExcludedTagIds` (seed)
- `EventCallback<TagFilterSelection> OnChanged` — emits `(IncludedTagIds, ExcludedTagIds)`

Owns the two id-sets and **cross-dedup** (a tag present in included cannot also be excluded and
vice versa). Composes one include + one optional exclude `TagSelector` per type. (`TagSelector`
injects `ITagReadService` itself — `TagFilter` is injection-free.)

### `UserStoryInteractionFilter` (SharedUI/UserStoryInteractions/)

Toggles over the filterable `UserStoryInteractionTypeEnum` kinds. Params:
- `IReadOnlyList<UserStoryInteractionTypeEnum> AvailableKinds` (default subset)
- `IReadOnlyList<UserStoryInteractionTypeEnum> ExcludedKinds` (seed)
- `EventCallback<IReadOnlyList<UserStoryInteractionTypeEnum>> OnChanged` — emits kinds to exclude

Injection-free — the server-side query applies exclusions against the active viewer. Distinct from
`UserStoryInteractionPanel` (per-story action buttons); this component is purely a discovery filter.

### `ResultsFilterPanel` (SharedUI/Discovery/)

Coordination composite. **Injection-free. No ViewModel** — `@code` holds buffered axis selections.

Params:
- `bool ShowTagFilter`, `bool ShowTextSearch`, `bool ShowInteractionFilters`
- `IReadOnlyList<DefaultSortOrder> AvailableSorts` — owner-supplied; never the §5.3.3-excluded
  sorts; include `Relevance` only when `TextQuery` is non-empty
- `StoryFilterDto? InitialFilter` — seeds all axes on first render
- `EventCallback<StoryFilterDto> OnSearch` — raised on Apply with the assembled DTO

Assembles `TagFilter` + `UserStoryInteractionFilter` + a debounced FTS `<input>` + a
`DefaultSortOrder` `<select>` + an **"Apply Filters"** button (label never "Search" — misleading
when the source is already determined by the parent). Each child axis raises `OnChanged`; the panel
buffers the emitted slice. On Apply, panel builds `StoryFilterDto` and raises `OnSearch`.

Usage:

```razor
<ResultsFilterPanel ShowTagFilter="true"
                    ShowTextSearch="true"
                    ShowInteractionFilters="true"
                    AvailableSorts="@_sorts"
                    InitialFilter="@_filter"
                    OnSearch="HandleSearch" />
```

The panel does **not** know which source the parent is querying — it raises a DTO and stops there.
Page-level composition: `ResultsFilterPanel` + `StoryDeck` are kept separate and wired at the
page/dispatcher level. Do **not** bundle them into a shared composite (spec §5.27 explicitly
rejected a bundled `UserListPage`; `StoryDeck` is also used without any panel at all).

**Bookshelf narrowing pattern (WU27):** when `ResultsFilterPanel` is used for *narrowing* (not
discovery), the dispatcher first computes a **candidate ID set** (e.g. all story IDs the user has
favorited via `IUserStoryInteractionReadService.GetBookshelfStoryIdsAsync`), then passes
`restrictToStoryIds` to `IStoryReadService.GetListingsAsync(filter, restrictToStoryIds)`. The panel's
tag/text/sort selections narrow within that set. The candidate set is the outer constraint; the content-
rating global filter still applies inside `GetListingsAsync` — never duplicated in the bookshelf query.
The `ResultsFilterPanel` is unaware of the candidate constraint; the dispatcher owns the two-step
composition.

## Conditional Rendering Patterns

### AuthorizeView Gates (authentication + roles only)

`<AuthorizeView>` is for **authentication and role gates** — "is anyone logged in?" or "is the viewer
a moderator?". It reads Blazor's cascading `AuthenticationState` and cannot express identity-equality
against a specific entity's owner id.

```razor
@* Role-gated mod section embedded in an otherwise-public page *@
<AuthorizeView Roles="Moderator,Admin">
    <Authorized>
        <button @onclick="HandleModAction">Moderator action</button>
    </Authorized>
</AuthorizeView>
```

### Owner-Conditional Edit Affordances (inline @if, no component)

**Ownership is identity-equality, not a role.** The pattern — established by `CommentItem`,
`UserStoryInteractionPanel`, and `VouchList` — is a **plain `@if` on a page-computed ownership bool**,
never a named component and never `AuthorizeView`:

```razor
@* CommentItem — edit/delete wired only when IsOwnComment and callback is present *@
@if (IsOwnComment && OnEdit.HasDelegate)
{
    <button type="button" @onclick="() => OnEdit.InvokeAsync(Comment.CommentId)">Edit</button>
}

@* On a story/chapter view page — the dispatcher compares its CurrentUserId to the entity AuthorId *@
@if (_isOwnStory)
{
    <a href="/story/@StoryId/edit">Edit story</a>
}
```

Rules:
- The **dispatcher / routable page** resolves `CurrentUserId` from `[CascadingParameter] Task<AuthenticationState>`
  and compares it to `entity.AuthorId`. Components receive the pre-computed bool (`IsOwnComment`,
  `IsOwnStory`, `IsEditable`). **SharedUI components never inject `IActiveUserContext`** — it is a
  server-only service that will not exist in a future WASM client.
- **Security lives in the write service**, not in the `@if`. The UI affordance is visibility only;
  the service gate (identity-equality against `IActiveUserContext.UserId`) is the actual control
  ([`ServerCommentWriteService.EditCommentAsync`](../../../../TheCanalaveLibrary.Server/Comments/ServerCommentWriteService.cs#L77)).
- **There is no shared "edit button" or `AdminControls` component.** Spec §5.17's `<AdminControls>`
  reference is stale — that component does not exist and should not be minted. Affordances differ per
  entity (link to edit page vs in-place swap) and are one-liners.
- **Moderation is a separate code path** (WU34), never an `OR` branch on the author gate.
  `IActiveUserContext.IsModerator/IsAdmin` are query-shaping hints and explicit mod pages, not a
  pass-through into the author's ownership check.

### Loading States

**`StoryDeck` owns its three-state internally** — the hosting page passes nullable `Stories`
(null = loading, empty list = no results, populated = grid) and the deck branches:

```razor
@* Page / dispatcher — pass Stories? directly; StoryDeck branches internally *@
<StoryDeck Stories="@_stories"
           UserStoryInteractionStates="@_interactionStates"
           CurrentUserId="@_currentUserId"
           CurrentPage="@_page"
           PageSize="@_pageSize"
           TotalCount="@_totalCount"
           OnPageChanged="HandlePageChanged" />
```

For other data-driven surfaces where a purpose-built deck component doesn't exist (e.g. a simple
comment list, a single-column notification feed), the three-state is expressed inline at the page:

```razor
@if (_items is null)
{
    <p class="text-muted">Loading…</p>
}
else if (_items.Count == 0)
{
    <p class="text-muted">Nothing here yet.</p>
}
else
{
    @foreach (var item in _items) { ... }
}
```

The three-state pattern (loading / empty / populated) appears on every data-driven surface.

## Composite Introduction Criteria

Introduce a composite only when one of these is true:
- **It has children** (a leaf by definition cannot).
- **It manages coordination state** spanning multiple children.
- **It appears multiple times** with identical structure.
- **It wraps a third-party component** needing Blazor adaptation.

If something only appears in one place and has no coordination logic, it belongs inline in its
parent — extracting it adds indirection without value.

## Page Route Reference

See spec §5.29 for the complete page inventory. Key dispatcher pages:

| Page | Route | Pattern |
|---|---|---|
| StoryPage | `/story/{StoryId:int}/{*StorySlug}` | Dispatcher → Desktop/Mobile |
| ChapterPage | `/story/{StoryId:int}/{ChapterNumber:int}/{VersionOrder:int?}` | Dispatcher → Desktop/Mobile |
| SearchPage | `/discover` | Dispatcher → Desktop/Mobile |
| TreeSearchPage | `/discover/me`, `/discover/user/{userId}`, `/discover/story/{storyId}` | Dispatcher → Desktop/Mobile |
| ProfilePage | `/user/{UserId:int}/{*Tab}` | Dispatcher → Desktop/Mobile |
| BookshelvesPage | `/bookshelves/{Tab}` | Dispatcher → Desktop/Mobile |
| TagDirectoryPage | `/tags` | Dispatcher → Desktop/Mobile (browse), Desktop-only (edit) |
| MessagingPage | `/messages/{ConversationId:int?}` | Dispatcher → Desktop/Mobile |
| Mod pages | `/mod/reports`, `/mod/submissions`, `/mod/users` | Desktop-only, no dispatcher |
