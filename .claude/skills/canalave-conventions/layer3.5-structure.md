# Layer 3.5 — UI Structure (Composition + Skeleton)

The markup skeleton: which child Razor components form the tree, what HTML elements exist,
what conditions drive `@if`/`@foreach`, how data flows through `[Parameter]` to children.
Decidable once the component system is known — before visual design.

## Composition Principles

### Data Flow: DTOs Through the Tree

DTOs flow from the page to children as `[Parameter]`. The page loads once; children receive and
render. Don't decompose DTOs into primitive parameters at the composition boundary:

```razor
@* Page passes the DTO *@
<StoryDeck Stories="Stories" OnPageChanged="HandlePageChanged" />

@* Composites pass through to leaves *@
<StoryCard Story="story" />
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

Pages and pass-through composites control spatial arrangement of children. Children are
agnostic about their placement — they work the same in a 3-column grid and a 1-column stack.

### Responsive Structure — One Tree, Every Viewport (settled 2026-07-18)

The site is a **single responsive site**: one component tree, one DOM, all viewports. There is no
device detection, no Desktop/Mobile component fork, and no dispatcher branching — the former
`IDeviceDetectionService` / `DeviceLayout` / `{X}Desktop`+`{X}Mobile` paradigm was removed
2026-07-18 (history and rationale: `render-and-layout.md` §"Responsive Layout Architecture").
Pages load data AND render their own markup; extract a child component for **reuse or
coordination state**, never as a single-consumer pass-through tier.

Express every viewport difference at the lowest rung of the adaptivity ladder that can carry it
(full ladder + the rung-3 trigger rule: `layer4-style.md` §"Responsive Adaptivity Ladder"):
CSS reflow → CSS capability queries → *(future, trigger-gated)* `ViewportState` cascade →
separate compositions only behind that trigger, at the smallest divergent subtree.

Merged pages are **desktop-composition-first with graceful narrow degradation** (sidebars stack
below content, tab bars wrap or scroll). Deliberate narrow UX is a future mobile-phase activity —
current narrow rendering is provisional, not settled design.

### Moderator-Only Pages

Desktop-focused surfaces: same single responsive tree as everything else, but no narrow-viewport
investment beyond not-broken. Applies to: Reports (`/mod/reports`), Story Submissions
(`/mod/submissions`), User Management (`/mod/users`).

## Component Hierarchy Patterns

### Leaf (no children)

```razor
@* TagChip.razor — Tag is a TagChipDto (Core/Tags/); SpriteIdentifier is the raw key;
   the leaf resolves the sprite URL at render via injected ISpriteReadService + ThemeContext
   (see layer2-services.md §"Sprite URLs Are Resolved At Render Time, In the Component"). *@
@inject ISpriteReadService Sprites

<span class="...tag type styling...">
    @if (Tag.SpriteIdentifier is not null && _themeCtx is not null)
    {
        <img src="@Sprites.GetSpriteUrl(_themeCtx.Slug, Tag.SpriteIdentifier, _themeCtx.PrefersAnimated)"
             alt="" class="..." />
    }
    @Tag.TagName
    @if (OnRemove.HasDelegate)
    {
        <button @onclick="() => OnRemove.InvokeAsync()" class="...">✕</button>
    }
</span>

@code {
    [CascadingParameter] private ThemeContext? _themeCtx { get; set; }
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
everything is inline anchors and disclosure panels.

**`ChapterNavigation` is reading-context-only** — `CurrentChapterNumber` is `[EditorRequired]`; it
renders prev/next chapter arrows and a "Chapter N: Title" dropdown. It is **not** used on the story
landing page (`StoryPage`), which instead uses the `ChapterList` leaf (WU25, `SharedUI/Chapters/`).
`ChapterList` shows the full chapter list as a flat/indented list, not a navigation bar:
- One row per chapter → links to primary version `/story/{id}/{ch}`
- Non-primary alternates: indented sub-rows labeled `ChapterTitle - VersionName`
- `ShowDrafts` param gates unpublished chapters (shown with "Draft" marker for authors)

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

### Owner-Conditional Edit Affordances on a Display Composite

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

A boolean `Compact` toolbar parameter was tried (WU6) and discarded: Quill binds toolbar-button
listeners once, at construction, so changing the `ToolbarContent` RenderFragment later doesn't
retroactively rewire anything — a genuinely different toolbar needs `@key`-driven destroy/recreate,
not a runtime toggle inside one `EditorView`. **MVP ships the desktop toolbar only; a
compact-toolbar variant is a future mobile-phase decision** (WU-EditorMobile, re-scoped 2026-07-18:
if ever built, it rides the adaptivity ladder's rung-3 trigger — not a device fork, which no
longer exists as a pattern).

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
                @if (tag.SpriteIdentifier is not null && _themeCtx is not null) { <img src="@Sprites.GetSpriteUrl(_themeCtx.Slug, tag.SpriteIdentifier, _themeCtx.PrefersAnimated)" class="w-4 h-4" alt="" /> }
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

**`SelectedTemplate` is mandatory, not optional.** `BlazoredTypeahead.OnInitialized()` throws
`InvalidOperationException: ... requires a SelectedTemplate parameter` if it's omitted — unlike
`ResultTemplate`/`NotFoundTemplate`, which the package defaults sensibly. Omitting it doesn't fail
quietly in single-select mode (the bound value silently resets to `null` after each pick), making
it tempting to assume the template is decorative. The failure eventually surfaces as an unrelated
`NullReferenceException` in `Dispose()` — a downstream symptom of `OnInitialized()` never
completing. Chase `OnInitialized()`, not the disposal trace.

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
| `CommentSection` | Coordination composite | Chapter reading pages (WU20), Blog post view pages (WU31) — see "CommentSection — Multi-Context Dispatch" |
| `NotificationItem` | Leaf | Notification bell flyout (WU33), Notifications page (WU33) |
| `NotificationBell` | Coordination composite (cross-cutting layout element) | `DesktopLayout`, `MobileLayout` (WU33) |

## StoryCard and StoryDeck

**StoryCard** (leaf, WU13): pure leaf, no service injection. Contract:
- `[Parameter, EditorRequired] StoryListingDto Story` — warm-partition projection; includes
  `ShortDescription` (nullable, tooltip + synopsis) and `Tags` (`TagChipDto` list; each chip resolves its sprite at render).
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

## `@key` on `@foreach` Over Stateful Children

When a `@foreach` renders child **components** (not plain HTML elements), Blazor matches live instances
to items **positionally by default** — the instance at slot *i* is reused for whatever item is now at
slot *i*, regardless of the item's identity. Only the `[Parameter]` values are overwritten; private
fields survive.

This is a **data-corruption bug** the instant a child caches a parameter into a private field and then
stops re-syncing it. The canonical example is `UserStoryInteractionPanel`:

```csharp
protected override void OnParametersSet()
{
    if (_localState is null)     // ← caches ONCE, never re-syncs
        _localState = State;
}
```

On pagination from stories [A, B, C] → [D, E, F], the panel at slot 0 stays alive, gets `StoryId`=D
but keeps `_localState` from A. `FlushAsync` then writes A's interaction booleans onto D's id —
server-side corruption.

### The fix: `@key` on a stable domain id

```razor
@foreach (var story in Stories)
{
    <StoryCard @key="story.StoryId" Story="story" ... />
}
```

`@key` changes the matching rule from positional to by-id. Blazor reuses the instance for the *same*
id (even if it moved slots), disposes instances whose ids disappear, and creates fresh instances for
new ids. The instance↔data identity is preserved.

### When `@key` is required

Any `@foreach` rendering a component that holds **per-item private state** MUST carry `@key` on a
stable domain id. Indicators that a child holds per-item state:

- An optimistic cache (`_localState`, `_like`, etc.) populated once in `OnParametersSet` with an
  `if (field is null)` guard — the hallmark of the corruption pattern.
- Ephemeral reveal / menu flags (`_isRevealed`, `_menuOpen`, `_showSpoilerConfirm`).
- A disposable resource scoped per row (debounce `CancellationTokenSource`).

### When `@key` is NOT required (and why)

Two safe categories do **not** require a key:

**Pure-display leaves** — children whose *only* state is their `[Parameter]`s. Blazor overwrites all
`[Parameter]` values correctly on every positional reuse, so no bleed is possible. Examples:
`RecommendationCard`, `GroupCard`, `BlogPostCard`, `ConversationListItem`, `MessageItem`. Keying them
changes nothing observable; prefer to omit so a key's presence signals "this child holds per-item
state" at a glance.

**Self-healing children** — children that *do* hold a private derived field but recompute it from
parameters on **every** `OnParametersSet` (no `if (is null)` guard). Example: `NotificationItem`
recomputes `_composed` unconditionally, so a reused instance fixes itself immediately. No bleed.

The aggravating pattern that turns a positional reuse into a bug is exactly the `if (field is null)`
cache guard — it caches on first render and then ignores all subsequent parameter updates. If you see
that guard on a per-row field, add `@key` on the parent loop.

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

## Chapter Versioning — Progressive Disclosure (WU26)

`ChapterContent` rows are **live alternates**, not revision history — one is the reader's default
(`Chapter.PrimaryContentId`). The guiding principle: **the version concept is invisible until a
second version exists** (settled WU26).

**Edit page (author):**
- Single-version (or new): the editor is a plain chapter form with one low-emphasis
  **"Add an alternate version"** link as the only versioning affordance.
- Multi-version (`VersionCount > 1`): reveals a compact **version switcher** (which version you're
  editing + links to per-version edit routes) plus per-version controls: rename, **set as primary**,
  delete (disabled for the primary — enforced by the Restrict FK), add another.

**"Primary" badge driven by `IsPrimary` DTO field, never by `SortOrder == 0`.** The primary can
change; SortOrder is stable identity within a chapter's version list and should not carry semantic load.

**Rating floor invariant (WU26):** a version's effective rating must be ≥ the story rating. An M story
is mature throughout; a T story allows T or M versions, not E. NULL = inherit story rating (always
passes the floor). The **primary** version's effective rating must equal the story rating (naturally
satisfied by NULL/inherit) — guarantees any reader who can see the story can always read its primary
chapters without a content-gate block.

## CommentSection — Multi-Context Dispatch (WU31 / WU32 / WU30)

`CommentSection` (SharedUI/Comments/) is a coordination composite that dispatches to the correct
comment thread based on which context parameter is set. Exactly one of `ChapterId`, `BlogPostId`,
`GroupId`, or `ProfileUserId` must be set — validated via a guard in `OnInitializedAsync`
(compile-time enforcement is impractical for optional int parameters). A small private
`CommentTarget` enum encapsulates "which target" so that `LoadAsync`, post, and edit calls dispatch
cleanly without per-call `if/else` chains.

```csharp
// CommentSection @code — target dispatch
private enum CommentTarget { Chapter, BlogPost, Group, UserProfile }

private CommentTarget Target =>
    BlogPostId.HasValue ? CommentTarget.BlogPost
    : GroupId.HasValue  ? CommentTarget.Group
    : ProfileUserId.HasValue ? CommentTarget.UserProfile
    : CommentTarget.Chapter;
```

**Backward compatibility:** existing WU20 chapter-context call sites pass only `ChapterId` — no
change required. `UserHasCompletedStory` and the spoiler toggle remain chapter-only parameters;
they are ignored (have no effect) when `Target != Chapter`.

**Service dispatch per context:**
- Chapter: `GetChapterCommentsAsync` / `PostChapterCommentAsync` (WU20)
- BlogPost: `GetBlogPostCommentsAsync` / `PostBlogPostCommentAsync` (WU31)
- Group: `GetGroupCommentsAsync` / `PostGroupCommentAsync` (WU32)
- UserProfile: `GetUserProfileCommentsAsync` / `PostUserProfileCommentAsync` (WU30)

**UserProfile context gating (WU30):** the Profile tab enables the comment wall and posting
per the profile owner's `PrivacySettings.AllowProfileComments`
(`Public` → any authenticated user; `UsersOnly` → any authenticated user; `Off` → wall hidden).
The dispatcher passes the resolved `bool AllowComments` as a `[Parameter]` so `CommentSection`
can suppress the wall entirely when `Off`. `CommentsWritten` increments on each post here too
(counter map in `layer2-services.md` "UserStats Updates").

## Notification Presentation Model — Static Presenter + Per-Type Templates (WU33)

The notification cluster introduces two static visual-metadata classes that follow the `BookshelfTabVisuals`
/ `UserStoryInteractionVisuals` precedent (settled WU33):

**`NotificationCategoryVisuals`** (static, `SharedUI/Notifications/`):
`NotificationCategoryEnum → Info(IconPath, AccentColor, Label, SortOrder)`. Labels and sort-order mirror the
seeded `NotificationCategory` table (no service exposes category names). **Icons reuse existing constants as the
single source of truth**, exactly as `BookshelfTabVisuals` sources from `UserStoryInteractionVisuals` and
`RecommendationIcons`:
- `YourFollows` → `UserStoryInteractionVisuals.For(Follow)` (teal `#2DBBA0`)
- `YourStories` → `BookshelfTabVisuals.MyStoriesPath` (`#2F7D4F`)
- `YourRecommendations` → `RecommendationIcons.RecommendationIconPath` (`#5BB85A`)
- `Warnings` → `UserStoryInteractionVisuals.For(Ignore)` (red `#C04030`)
- New glyphs (no existing equivalent): `SiteNews` megaphone, `YourProfile` user-circle, `Collaborations` link,
  `Groups` user-group, `YourReports` flag — all 24×24 viewBox nonzero fill, defined in this class.

**`NotificationPresenter`** (static, `SharedUI/Notifications/`):
`Compose(NotificationDto) → (string Text, string IconPath, string AccentColor)`.
Per-`NotificationTypeEnum` message template interpolating `SourceUserName` (fallback `"Someone"`/`"A user"`
when null — source was deleted via SET NULL) and `TargetTitle`. Icon/accent default to the category visuals
from `NotificationCategoryVisuals`, with per-type overrides for distinctive types (e.g. `HiddenGem` →
`RecommendationIcons.HiddenGem*`). **The DTO carries data; the UI owns copy** — message text is never stored.

**`NotificationBell`** uses the **UserCard caret pattern**: `relative` container + `@onclick="Toggle"` button
with unread-count badge + `@if (_open)` `absolute top-full z-10` flyout panel. NOT the `fixed inset-0` modal
pattern. Wrapped in `<AuthorizeView><Authorized>`. Does NOT inject `IActiveUserContext` — the underlying service
self-scopes. See `render-and-layout.md` "Notification bell."

**`NotificationItem`** (leaf, no injection): inline SVG icon (path from `NotificationPresenter`), composed
message text with entity link (`<a href="@n.TargetUrl">@n.TargetTitle</a>` when present, else plain text),
relative timestamp, unread dot. Exposes `EventCallback OnActivate` (parent marks read + navigates).
Outer-margin rule honored.

## Filter-Axis Component Pattern

**The unit of reuse is the individual filter axis, not the assembled panel.** The Automatic tree
search tab reuses the `TagFilter` + `UserStoryInteractionFilter` axes directly (see "Unified Tree
Search Page — Automatic Tab (WU44)" above) — no `StoryDeck`, no sort, no FTS reuse needed there
either, but the axes themselves compose in. The assembled `ResultsFilterPanel` is one *assembler*
of the axes; a page can assemble its own subset directly.

**Corrected 2026-07-12 (WU40):** this section previously stated "Manual tree search reuses tag +
interaction-exclusion filtering" — that was speculative, written before Feature 33 was designed,
and does not match the design WU40 actually settled. Manual tree search's two tabs (Explore, Deep
Dive) are **not** filtered by `TagFilter`/`UserStoryInteractionFilter` — candidate results are
scoped by **edge-type checkboxes** and rendered through the section model (see "Manual Tree Search
— Explore & Deep Dive (WU40)" above), not by tag/FTS/interaction axes. Composing those axes into
the candidate pane is a plausible future enhancement but is explicitly **not** part of WU40's
initial build — do not assume it exists until a Stage note says otherwise.

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

**WU28 additions to `ResultsFilterPanel`:**
- `[Parameter] bool ShowTagIncludeModeToggle { get; set; } = false` — forward to
  `TagFilter.AllowIncludeModeToggle`. Only `/discover` passes `true`; Bookshelves/Profile unaffected.
- `[Parameter] IReadOnlyList<TagChipDto> InitialIncludedTags` / `InitialExcludedTags` (default `[]`)
  — seed tag chips into `TagFilter` in `OnInitialized`. The dispatcher pre-loads chips
  via `ITagReadService.GetTagChipsByIdsAsync` and passes them in; the panel is still injection-free.
- Buffer `TagIncludeMode` from `TagFilterSelection.IncludeMode`; set `StoryFilterDto.IncludeMode`
  on Apply; seed from `InitialFilter.IncludeMode`.

**WU28 additions to `TagFilter`:**
- `[Parameter] bool AllowIncludeModeToggle { get; set; } = false` — when `true`, render a small
  AND/OR segmented control **above the include selectors only** (exclude selectors are unchanged).
  Default `false` keeps all existing usages unchanged.
- `TagFilterSelection` gains `TagIncludeMode IncludeMode { get; init; } = TagIncludeMode.And`;
  `EmitAsync` includes it.

### Search Page Dual-Mode (`/discover`) (WU28)

The `/discover` dispatcher (`SearchPage.razor`) runs in two modes keyed on `_filter.Sort`:

**Random mode (`Sort = Random`):**
- `_items` is an append-only display list. Each "Give me more" call invokes
  `GetRandomBatchAsync(_filter, RandomBatchSize)` and appends the result. No dedup.
- `StoryDeck` receives `TotalCount = Items.Count` and `PageSize = Items.Count` → forces
  `TotalPages = 1` → embedded `PaginationControls` self-hides.
- A **"Give me more"** button sits below the deck; it is absent in sorted mode.

**Sorted mode (`Sort = DatePublished` or `Relevance`):**
- `GetListingsAsync(_filter)` with offset pagination; `OnPageChanged` re-queries
  `_filter with { Page = page }`.
- Real `TotalCount`/`PageSize` passed to `StoryDeck`; `PaginationControls` is live.
- No "Give me more" button.

Switching mode (via `OnSearch` from the panel) resets `_items`, page to 1, and reloads.
`Sort = Random` is the initial default (page is never blank on load).

**Bookshelf narrowing pattern (WU27):** when `ResultsFilterPanel` is used for *narrowing* (not
discovery), the dispatcher first computes a **candidate ID set** (e.g. all story IDs the user has
favorited via `IUserStoryInteractionReadService.GetBookshelfStoryIdsAsync`), then passes
`restrictToStoryIds` to `IStoryReadService.GetListingsAsync(filter, restrictToStoryIds)`. The panel's
tag/text/sort selections narrow within that set. The candidate set is the outer constraint; the content-
rating global filter still applies inside `GetListingsAsync` — never duplicated in the bookshelf query.
The `ResultsFilterPanel` is unaware of the candidate constraint; the dispatcher owns the two-step
composition.

### Unified Tree Search Page — Automatic Tab (WU44)

`TreeSearchPage.razor` (`SharedUI/Discovery/`) is the dispatcher for spec §5.26's Unified Tree
Search Page — routes `/discover/me`, `/discover/user/{userId:int}`, `/discover/story/{storyId:int}`
(`[AllowAnonymous]`; `/discover/me` resolves the current user from the auth cascade). It resolves
the root entity, branches mobile/desktop (`TreeSearchDesktop`/`TreeSearchMobile`, same
dispatcher-owns-data pattern as `SearchPage`), and renders a root-entity header (story → compact
story header; user (incl. `/discover/me`) → `UserCard`) above a tab strip. **Corrected 2026-07-12
(WU40): three tabs, not two** — **Automatic** (built here), **Explore**, and **Deep Dive** (the
latter two were a single "Manual" placeholder — "Graph view coming soon" — until WU40 designed and
split them; see "Manual Tree Search — Explore & Deep Dive (WU40)" below). This diverges from spec
§5.26's literal "two tabs" (spec is read-only; the divergence is deliberate and recorded in
`audit/Discovery.md` Feature 33). Tab selection is ephemeral UI state, not URL state.

**Automatic tab controls** (`TreeSearchControls`, injection-free, emits a buffered
`TreeSearchRequest` on Apply — same batched-Apply discipline as the Filter-Axis pattern below,
since live re-filtering would relayout results as the edge/degree selection changes):
- Degree slider (1–8, service ceiling), edge-type multi-select (grouped wide/mid/chain-of-trust
  per `TreeSearchEdgeType`), Random/ByDegree sort toggle.
- `IncludePaths` is **never a raw checkbox** — the UI auto-derives it from the edge selection
  (`true` only when selected edges ⊆ {HiddenGem, AuthorSpotlight}) so it can never send the
  service's rejected combination.
- `ResultsFilterPanel` (tags/FTS/interaction) is reused alongside the tree controls, seeded from
  `IDiscoveryDefaultsReadService` (`SiteSearchModes.AutoTreeSearch`) — see `layer2-services.md`
  "Tree Search — Automatic Tab Composition (WU44)" for how the panel's `StoryFilterDto` composes
  with the traversal server-side via `ITreeSearchReadService.SearchAsync`.

**Results** reuse `StoryDeck` (`TreeSearchListingResultDto.Items`, already degree-sorted/hydrated
by `SearchAsync`) plus:
- A **degree badge** per card ("2nd-degree connection").
- A **path chip**, chain-of-trust results only (HiddenGem/AuthorSpotlight edge sets — the only
  sets `IncludePaths` ever allows): the raw path alternates user/story ids. **Render both — story
  AND user hops, with real usernames** (e.g. "Story A → User X → Story B"), each a clickable link
  (`/story/{id}`, `/user/{id}`). **Corrected 2026-07-12 (WU40) — this previously said "collapse
  user hops, never render a username."** That was an over-broad reading of §5.4. §5.4's "graph
  never reveals identity" protects exactly one thing: anonymized *hidden*-favorite contributors,
  whose reach is merged into the mart's indistinguishable `Favorite` edge. A chain-of-trust path
  (Hidden Gem, Author Spotlight) carries no anonymized contributor — every hop is a public,
  human-conferred, curated act (a self-designated gem, or an author publicly rewarding a
  recommender) — so identity is safe and informative to show. The same reasoning is why Manual
  Tree Search (below) renders every node with a real name; see `audit/Discovery.md` Feature 33's
  WU40 settled note for the full privacy-model analysis.
- A flooding-indicator banner when `ResultCapTruncated`.

### Manual Tree Search — Explore & Deep Dive (WU40)

Feature 33 is **two distinct interactive paradigms**, not one graph view — both share the
`TreeSearchPage` shell's root resolution and both are entirely client-driven, stateless-pivot
(each node selection is a fresh server query; no traversal state persisted server-side). Full
design rationale — the edge × direction boundedness table, the Pinned Story edge, and the
service-layer gap analysis — lives in `audit/Discovery.md` Feature 33's WU40 settled note; this
section records the component/composition shape only. (Interaction shape finalized through the
Phase-1 mock, four iterations, 2026-07-12.)

**Privacy note (do not reintroduce anonymization here):** every edge available in manual is a
genuinely public action (manual excludes hidden favorites entirely, unlike Automatic). Every node
— story or user — renders with real, clickable identity. See the corrected Automatic-tab path-chip
note above for why this isn't a contradiction of §5.4.

#### The shared tree canvas

Both tabs render the curated tree as a **2D top-down node-link diagram** — root at top, children
fanning out in a row beneath, straight SVG lines colored per edge type — NOT a nested/indented
DOM-outline (`<details>`/list) structure. **Tidy-tree layout in C#** (leaves take sequential
horizontal slots; each parent centers over its own children; depth = row): deterministic,
Unit-tier-testable math, recomputed only on structural change. Nodes are a shared compact
**node-chip leaf** (~56px square, circular for users; thumbnail + caption below; ghost state =
dashed). The two pane *arrangements* are separate composites (Explore's embedded pane vs. Deep
Dive's full-viewport canvas — structurally different, same rule as Desktop/Mobile branching).

**Per-frame gestures live in a thin JS module, never on the circuit** (`manual-tree-search.js`):
CSS-transform drag-to-pan + zoom on the canvas, drag/resize for Deep Dive's floating panel, and
`localStorage` persistence. Structural changes (pivot, add-node) go through Blazor normally —
they're server queries anyway. localStorage persists **IDs + edges only** ({entityId, entityType,
edgeLabel, ghost, children}, one document per (mode, root)); display data rehydrates on load via
the existing batch reads, and entities the viewer can no longer see prune silently.

**Edge selection is per (edge, direction) pair — every pair independently toggleable in BOTH
modes** (shared toggle-pill control). "Recommendation from a story" (who recommended it) and
"Recommendation from a user" (what they recommended) are different traversal semantics — never
one flag reinterpreted by anchor type. Author(story→author) and Pinned(user→story) are toggleable
too — never hardcoded on (Author×Pinned composes into an identity round-trip on any self-pinned
story; toggles are the escape). Explore's toggle row swaps wholesale per the current anchor's
direction with direction-annotated labels; Deep Dive shows all four whitelisted pairs at once.

#### Explore tab — coordination composite, two-pane (~50/50)

Left pane: the **persistent, client-curated tree canvas** the user builds by hand, root seeded
from the route. Right pane: a **disposable, stateless "candidate results" pane** for the
currently-selected node's neighbors, with the anchor pinned above the results as a reminder of
context. Injection-free — data flows from `TreeSearchDesktop`/`TreeSearchMobile` exactly as the
Automatic tab's controls do.

- A node added but not yet selected/explored renders in the **ghost state** — a visible frontier
  of "where I could go next." Selecting a ghost node solidifies it and populates the right pane.
- **Section model** (right pane) — one section per underlying table, NOT a ranked/deduped stack;
  every section toggle-gated (none hardcoded on). **Unbounded sections are paged**: first page
  ≈10 items + per-section totalCount + a "Show more" that pages that section only. One pivot =
  ONE service call returning all sections (`ManualTreeNeighborsDto`); direction is enforced by
  two request types (`StoryNeighborsRequest` / `UserNeighborsRequest`), not flag naming.
  - **Author** (story anchor only) — identity, singular.
  - **Recommendation family** (either anchor) — one query against `recommendations`;
    Recommendation / Hidden Gem / Author Spotlight toggles only widen or narrow its `WHERE`
    clause; a row matching multiple flags shows **once**, badges stacked. **Rendered as compound
    rows** — the recommended story's `StoryCard` and the recommendation panel side by side as one
    row, compounds stacked vertically (not a card grid). The rec half omits the embedded story
    reference (the real StoryCard sits beside it): additive `ShowStoryReference` param
    (default `true`) on `RecommendationCard` — existing consumers unchanged.
  - **Favorite** — always its own section (separate table; a target can legitimately also appear
    in the Recommendation family section — two independent signals, not a duplicate to dedup).
  - **Authored** (user anchor only) — the user's full catalog; their Pinned Story, if set, is
    badged and always sorted first within this list — not a separate section.
  - **Vouch** (user anchor, forward direction ONLY — never from a story anchor; incoming vouches
    are owner-private per §5.8) — its own section, a distinct mechanism (voucher → vouchee → their
    stories), not foldable into anything else.
- **Card reuse, not bespoke cards.** Story-valued results render the real `StoryCard` (triage —
  favorite/Read-It-Later/Ignore — stays the primary action, unchanged; **no immediate-hide on
  triage**, since this pane is stateless/disposable, unlike a persisted deck). User-valued results
  render the real `UserCard`. **"Add to tree" is a new composed sibling action** (per
  "Context-specific augmentation" above) — rendered alongside `StoryCard`'s triage row
  (co-important, NOT in the caret menu), and the **primary** action for `UserCard` results (which
  have no triage equivalent).
- **Narrow viewports:** tree and results stack via CSS reflow (rung 1) — provisional, like all
  narrow layouts. The former Tree ⇄ Results toggle died with the device-fork paradigm
  (2026-07-18); a deliberate narrow interaction model is a mobile-phase decision.

#### Deep Dive tab — coordination composite, full-viewport canvas + floating panel

Same stateless per-node pivot as Explore — **not** a recursive or auto-expanding server
traversal. Deep Dive's defining trait is momentum where Explore's is deliberateness:

- **Clicking a node is the ONLY gesture** — it selects the node (info display) AND auto-adds its
  whitelisted bounded connections in the same action. No separate "add" step, no "Explore more"
  button, no blocking modal (every walkable edge is ≤5 or 1, so bulk-add is always safe).
- **Anti-bounce guard:** auto-add skips a child whose entity equals the clicked node's *parent's*
  entity — kills the default A→B→A ring (pinned story → author → same user) independent of the
  toggles. Deeper cycles / cross-branch duplicates remain allowed (dedup stays per-node, per the
  path-reflecting design).
- **Edge whitelist** — exactly the four (edge, direction) pairs whose immediate result is ≤1 or
  ≤5 (full boundedness table in `audit/Discovery.md` F33): `AuthoredBy` (story→author, 1),
  `HiddenGem` (user→their own ≤5 gems), `AuthorSpotlight` (story→its own ≤5 spotlighted
  recommenders), `Pinned` (user→their 1 pinned story — new field, see below).
- **The info surface is a floating, resizable panel starting top-right over the canvas** —
  non-blocking (the canvas stays pannable/clickable beneath it); drag/resize via the JS module.
  Contents: the selected node's composed card (same StoryCard/UserCard + rec-panel composition as
  Explore's results, minus the Add button) plus a note of which toggle-gated groups were skipped.
- Full-viewport pannable/zoomable canvas (desktop: drag + zoom controls; mobile: native panning),
  reusing **Explore's node-chip leaf component** in a distinct arrangement — separate composite,
  not a responsive variant (embedded pane vs. full-viewport are structurally different).
- Separate `localStorage` tree from Explore (different mode, different edge scope, same anchor
  root may differ in content between the two).

#### Pinned Story (new, `User.PinnedStoryId`)

A nullable FK, exactly one self-chosen story per user (`ON DELETE SET NULL`). Fills the structural
gap that let the Hidden Gem chain self-sustain via the free `AuthoredBy` story→author connector,
but which Author Spotlight lacked (no bounded way back from a spotlighted recommender to a story).
Rendered as a badge on the Authored section's matching entry (always sorted first when present),
never as its own section — same "flag on a superset row" pattern as Hidden Gem/Spotlight badges
within the Recommendation family. Author-facing edit surface extends `AuthorSettingsForm.razor` /
`AuthorSettingsDto` (same shape as the existing Default Story Rating field — a story picker
sourced from the author's own catalog, with a "None" option).

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

## Profile Page Composition (WU30)

Route `/user/{UserId:int}/{*Tab}`. Tab slugs: `profile` (default), `favorites`,
`recommendations`, `authored`, `blog`. `ProfileTab` enum + `ProfileTabSlug` helper
(mirrors `BookshelfTab` / `BookshelfTabSlug`).

### Persistent banner (tab-independent, above all tabs)

`ProfileBanner` composite: avatar, username, tagline, relationship actions (`FollowButton` /
`VouchButton`, non-owner only), owner "Edit Profile" link → `/settings` (inline `@if` per
"Owner-Conditional Edit Affordances"), `UserStatsBlock` (hidden when `ShowUserStats = false`
for non-owners), badges row, outgoing `VouchList`.

- *Desktop:* horizontal band — `mx-auto max-w-6xl` container (Groups-header idiom); avatar
  left; name/tagline/actions beside it; stats strip below; badges + vouches in a row.
- *Mobile:* centered avatar, name, tagline; full-width action buttons; stats as a compact
  2-column grid; badges; vouches collapsed in `<details>` to save vertical space.

### Tabbed body

Each tab has a distinct layout — comments and story decks are **never on the same view**:

| Tab | Layout | Components |
|---|---|---|
| **Profile** (default) | Full-width stacked (both devices) | `RichTextView` (bio) + `CommentSection` (UserProfile context) — no filter sidebar |
| **Favorites / Recommendations / Authored** | Bookshelves idiom (deck + filter) | *Desktop:* `StoryDeck` (flex-1) + right `ResultsFilterPanel` sidebar. *Mobile:* "Filters" button → backdrop-overlay `ResultsFilterPanel` + full-width `StoryDeck` |
| **Blog** | Full-width stacked (both devices) | Paginated `BlogPostCard` list + `PaginationControls` — no filter sidebar |

**Blog tab owner vs. viewer (WU30):**
- *Viewer* (not owner): published posts only, view-only `BlogPostCard` (no owner params).
- *Owner* (`includePrivate = true`): `GetByAuthorAsync(..., includeUnpublished: true)` so
  drafts appear with a **"Draft" badge**; each card shows an **Edit** affordance
  (→ `/blog/{id}/edit`); tab header carries a **"+ New Post"** button (→ `/blog/new`).
  `BlogPostCard` is extended with optional owner affordances gated by an `IsOwner`/edit-href
  parameter — the Edit link is a **sibling** of the title anchor (not nested — invalid HTML).
  `GroupDesktop`'s existing `<BlogPostCard Post=...>` usage passes no owner params and is
  unaffected.

### Story-tab candidate-id pattern (mirrors BookshelvesPage)

The dispatcher computes a candidate ID set per tab:
- **Authored** → `IStoryReadService.GetStoryIdsByAuthorAsync(userId)` (existing).
- **Favorites** → `IUserStoryInteractionReadService.GetFavoriteStoryIdsAsync(userId, includePrivate)`
  (`WHERE IsFavorite AND (includePrivate OR NOT IsHiddenFavorite)`).
- **Recommendations** → `IRecommendationReadService.GetRecommendedStoryIdsByUserAsync(userId)`.
Listings and states then flow through the existing
`GetListingsAsync(filter, restrictToStoryIds)` + `GetStatesByStoryIdsAsync`, exactly as
`BookshelvesPage` does.

## Story Editor — Structured Tag Authoring (WU37)

`StoryPropertiesForm.razor` receives a `StoryPropertiesViewModel` and is presentational (no `@inject`).
For WU37, each tag type uses a distinct authoring pattern:

### Flat types (Genre / ContentWarning / CrossoverFandom)
Reuse `TagSelector` directly. One `TagSelector` instance per type, scoped by `TagTypeEnum`.
Genre and CrossoverFandom get a `TagPriority` picker per chip. ContentWarning gets no priority
picker (service coerces to `Primary`).

### Character — picker-plus-wrapper pattern

One `TagSelector` for catalog pick. Each selected chip spawns a `CharacterEntry` sub-component
(presentational leaf, no `@inject`). `CharacterEntry` receives the `TagChipDto` + a mutable
`StoryCharacterViewModel` and renders:
- Priority picker (Primary / Supporting)
- "OC" toggle — visible only when `chip.AllowOCDetails` is true (gate from `TagChipDto`)
- `OcName` / `OcBio` fields — revealed only when the OC toggle is on

```razor
@* StoryPropertiesForm — character section *@
<TagSelector TagType="TagTypeEnum.Character" OnSelectionChanged="HandleCharacterSelected" />
@foreach (var entry in _characters)
{
    <CharacterEntry Chip="entry.Chip" Model="entry" OnRemove="() => RemoveCharacter(entry)" />
}
```

`CharacterEntry` is an inner leaf — purely presentational, bUnit-testable. Feeds the
`StoryPropertiesViewModel.Characters` collection.

### Setting — picker-plus-optional-detail pattern

One `TagSelector` for catalog pick. Each selected chip spawns a `SettingEntry` sub-component.
`SettingEntry` renders a "Custom details" section only when `chip.AllowSettingDetails` is true.
When shown: `SettingName` (max 128) + `SettingDescription` (max 2048 chars) fields.

### Pairing builder — no TagSelector

The pairing section does **not** use `TagSelector`. It sources its member list from the story's
**own** `_characters` (selected above), not from the catalog. Structure:

```razor
@* Pairing section — entirely separate from catalog picking *@
<PairingBuilder Characters="_characters"
                Pairings="_pairings"
                OnPairingsChanged="HandlePairingsChanged" />
```

`PairingBuilder` is a coordination composite (manages pairing list state, emits on change):
- Member multi-select (checkboxes or pills) drawn from `_characters`
- `CharacterPairingType` selector: Romantic (`/`) or Platonic (`&`)
- Priority picker (Primary / Supporting)
- Add / Remove pairing buttons

Minimum 2 members per pairing enforced at service layer (not in the component itself).
Component-level guard: disable "Add pairing" button when fewer than 2 characters are selected.

All four sub-components are **presentational leaves** (no `@inject`) so they are bUnit-testable.
`StoryEditorPage` owns the `StoryPropertiesViewModel`, populates it from `GetStoryForEditAsync`
on edit-mode load, and maps it back to DTOs on submit.

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
