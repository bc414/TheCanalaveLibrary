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
@* TagChip.razor *@
<span class="...tag type styling...">
    @if (SpriteUrl is not null)
    {
        <img src="@SpriteUrl" alt="" class="..." />
    }
    @TagName
    @if (OnRemove.HasDelegate)
    {
        <button @onclick="() => OnRemove.InvokeAsync()" class="...">✕</button>
    }
</span>
```

No child Razor components. Only raw HTML elements. `@if`/`@foreach` driven by parameters.

### Coordination Composite (manages state across children)

```razor
@* StoryInteractionPanel.razor *@
<div class="...layout...">
    @if (!IsOwnStory)
    {
        <UserStoryInteractionButton IsActive="State.IsFavorite"
                           OnToggle="() => Toggle(InteractionType.Favorite)" ... />
        <UserStoryInteractionButton IsActive="State.IsFollowed"
                           OnToggle="() => Toggle(InteractionType.Follow)" ... />
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

```razor
@* EditorView.razor — wraps Quill.js *@
@if (_isPreviewMode)
{
    <RichTextView HtmlContent="@CurrentHtml" />
}
else
{
    <BlazoredTextEditor @bind-Value="CurrentHtml" ... />
}
<button @onclick="TogglePreview">@(_isPreviewMode ? "Edit" : "Preview")</button>
```

**EditorView is universal** across ALL text surfaces: chapters, comments, author notes,
descriptions, recommendations, profile bios, blog posts, AND private messages. The only
legitimate axis is device — desktop shows full toolbar, mobile shows compact toolbar with overflow.

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
