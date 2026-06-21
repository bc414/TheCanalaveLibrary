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
@* StoryInteractionPanel.razor *@
<div class="...layout...">
    @if (!IsOwnStory)
    {
        @* IconPath/AccentColor are inline SVG, mapped from InteractionTypeEnum by this composite —
           not a sprite URL; see layer4-style.md "Interaction Icons Are Inline SVG" *@
        <UserStoryInteractionButton IsActive="State.IsFavorite"
                           OnToggle="() => Toggle(InteractionType.Favorite)"
                           IconPath="@IconFor(InteractionType.Favorite)"
                           AccentColor="@ColorFor(InteractionType.Favorite)"
                           Label="Favorite" />
        <UserStoryInteractionButton IsActive="State.IsFollowed"
                           OnToggle="() => Toggle(InteractionType.Follow)"
                           IconPath="@IconFor(InteractionType.Follow)"
                           AccentColor="@ColorFor(InteractionType.Follow)"
                           Label="Follow" />
        @* ... more buttons *@
    }
    else
    {
        <EditStoryButton StoryId="StoryId" />
    }
</div>
```

### Pass-Through Layout Composite (arranges children)

```razor
@* ChapterNavigation.razor *@
<nav class="...layout...">
    @if (HasPreviousChapter)
    {
        <PrevChapterButton Chapter="PreviousChapter" />
    }
    <ChapterSelector Chapters="AllChapters" CurrentChapter="CurrentChapter" />
    @if (HasVersions)
    {
        <VersionSwitcher Versions="AvailableVersions" Current="CurrentVersion" />
    }
    @if (HasNextChapter)
    {
        <NextChapterButton Chapter="NextChapter" />
    }
</nav>
```

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

**StoryCard** (leaf): takes `StoryListingDto`, renders with computed display properties. Composes
`TagChip` instances. Includes caret dropdown for secondary options (View Story, Discover from this
Story, Copy link, Report, Download/Export). StoryCard should NOT contain UserCard — only a
username hyperlink.

**StoryDeck** (pass-through layout composite): the container holding StoryCards. Three-state
pattern (loading/empty/populated). Grid layout. Named "Deck" because a deck is a curated ordered
set of cards — avoids confusion with `StoryListingDto`.

## ResultsFilterPanel

Coordination composite. Owner configures visible sections via parameters:

```razor
<ResultsFilterPanel ShowTagFilter="true"
                    ShowTextSearch="true"
                    ShowInteractionFilters="true"
                    AvailableSorts="@_sorts"
                    OnSearch="HandleSearch" />
```

Contains: `TagSelector`, FTS text input (debounced), interaction filter toggles, sort selector.
Button label: "Apply Filters" (not "Search" — misleading when source is already determined).
Does NOT need a ViewModel class — `@code` holds current selections.

## Conditional Rendering Patterns

### AuthorizeView Gates

```razor
<AuthorizeView Roles="Moderator,Admin">
    <Authorized>
        <AdminControls OnDelete="HandleDelete" OnEdit="HandleEdit" />
    </Authorized>
</AuthorizeView>
```

### Loading States

```razor
@if (Stories is null)
{
    <LoadingSkeleton />
}
else if (Stories.Length == 0)
{
    <EmptyState Message="No stories found." />
}
else
{
    <StoryDeck Stories="Stories" />
}
```

The three-state pattern (loading / empty / populated) appears on every data-driven page.

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
