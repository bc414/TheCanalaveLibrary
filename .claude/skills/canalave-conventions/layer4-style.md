# Layer 4 — UI Style (Visual + Layout)

Tailwind utility classes, sprite resolution, responsive variants, images, conditional class
expressions. Blocked on design tokens being locked.

> **Tailwind version note:** the project uses **Tailwind v4, CSS-first configuration** — tokens live
> in an `@theme {}` block inside `TheCanalaveLibrary.Server/Styles/app.css` (the input stylesheet),
> not in a `tailwind.config.js`. Spec §2.1 and the original Axiom 9 phrasing describe the older v3
> `tailwind.config.js` model; the spec is a read-only historical snapshot and is not edited — this is
> the resolved, authoritative convention. See `forward_plan.md` Phase C "Resolved."

> **Bootstrap debris warning:** the original ASP.NET template and the Identity scaffold left
> Bootstrap/template classnames throughout the tree — `top-row`, `page`, `sidebar`, `bottom-nav`,
> `nav-pills`, `btn-lg`, `btn-danger`, `form-floating`, `alert alert-warning`, `text-danger`,
> `row`/`col-lg-*`, etc. Bootstrap's stylesheet was removed in Phase C, so **most of these classes
> render nothing** — they look like real, working markup but apply zero style. **Never copy a
> classname from a neighboring element or an existing file without verifying it's an actual Tailwind
> utility or a token defined in `app.css`'s `@theme` block.** This applies even to "just wire up the
> markup, no styling needed" work — that phrase means *no new visual design decisions*, not *license
> to paste whatever classname is already on the nearest `<div>`*. Use real Tailwind layout utilities
> (`flex`, `gap-`, `px-`/`py-`, …) for the skeleton instead. The one deliberate exception is Identity's
> own scaffolded pages (`Identity/Pages/**`), which intentionally keep their Bootstrap classes until
> that scaffold's own L4-Style unblocks (Stage 1, see `audit/Identity.md`) — don't extend that
> exception to any other file, including the persistent layout that wraps those pages.

## Prerequisite: Design Tokens (LOCKED — Phase C, 2026-06-20)

Tokens are defined in `TheCanalaveLibrary.Server/Styles/app.css` inside an `@theme {}` block:

- **Color palette:** a green palette rooted in Pokémon Gen 4/5 — Torterra (the Sinnoh grass-starter
  mascot) and the natural, slightly-muted grass-texture greens of the GBA/DS era (Gens 3–5).
  Explicitly **not** neon green and **not** a blue theme. Semantic names: `--color-surface`,
  `--color-surface-raised`, `--color-primary` (deep grass green), `--color-primary-strong`,
  `--color-accent` (warm amber/earth), `--color-text`, `--color-text-muted`, `--color-border`,
  `--color-success`, `--color-danger`, `--color-warning`.
- **Type scale:** `--font-display` (warm, characterful — e.g. Fraunces) and `--font-body` (warm,
  readable humanist sans — e.g. Mulish). Deliberately **not** the games' pixelated font. **Scope:**
  these tokens apply to site chrome only (nav, labels, buttons, cards, headings, page structure) —
  see "Reader Settings as CSS" below for why they do not apply inside `RichTextView`/`RichTextEditor`.
- **Spacing:** Tailwind's default scale (no custom override).
- **Border radii:** `rounded-xl` for card surfaces, `rounded-md` for inputs, `rounded-full` for chips
  and avatars.
- **Shadow levels:** `--shadow-subtle` / `--shadow-medium` / `--shadow-prominent` (elevation tiers).

This config is the first act of code generation, not a spec task.

### Consuming tokens in classes: `-(--token)`, never `-[--token]` (Tailwind v4)

Reference a token in a utility with the **parenthesized** CSS-variable shorthand:
`bg-(--color-surface)`, `text-(--color-text-muted)`, `border-(--color-border)`,
`hover:bg-(--color-primary)/20`. The square-bracket form `bg-[--color-surface]` is **Tailwind v3
syntax that v4 no longer supports** — v4 treats the bracket content as a literal arbitrary value and
compiles it to invalid CSS (`background-color: --color-surface`), which the browser silently drops.
The class *looks* right in markup, builds without error, and renders as nothing: transparent
flyouts/dialogs, invisible badges. 987 usages were converted in one sweep on 2026-07-01 after a
browser pass caught the transparent NotificationBell flyout; the compiled `wwwroot/app.css` is the
place to confirm a token class actually emits `var(--…)`. bUnit tests must also assert the paren
form when a class assertion is unavoidable (see `ConversationListItemTests`). Rebuild CSS with
`npm run css:build` in `TheCanalaveLibrary.Server/` after class changes.

**Design intent:** Non-corporate, warm community feel. Pokémon-fandom identity, anchored specifically
in Gen 4/5 (the generation with the most prolific fanfiction and the strongest in-game storytelling).
Engaging visual design with cover art (Fimfiction reference, not AO3 plainness). Not predatory, not
generic. Themes (the `Theme` entity / sprite system) support different visual flavors without
database changes — distinct from this Tailwind color palette, which is the one fixed site look.

## Outer Margin Rule (Non-Negotiable)

Components own their internal padding but **never** their outer margin. Parent containers control
spacing between siblings via `gap`.

**Forbidden on component root elements:** `mt-`, `mb-`, `mx-`, `my-`, `m-`.
**Allowed on component root elements:** `p-`, `px-`, `py-` (internal padding).
**Parents use:** `gap-`, `space-y-`, `space-x-` for child spacing.

**Rationale:** A component with `mb-6` inside a grid with `gap-6` produces doubled bottom spacing.

## Where Style Lives by Component Tier

| Tier | Style Weight | What It Contains |
|---|---|---|
| **Leaf** | Full | All visual identity. Colors, typography, borders, shadows, hover/focus states, transitions, sprite rendering, conditional class expressions for active/inactive/disabled states. |
| **Composite** | Light | Layout arrangement (`flex`, `grid`, `gap`, column spans). Container visual framing if the composite is a vessel (card surface, section border). Responsive breakpoints that change child arrangement. |
| **Page** | Near zero | Possibly a minimum height or page-level loading skeleton. No visual identity. |

## Parameter-Based Variants, Not Class Overrides

Tailwind class conflicts from parent overriding child are unpredictable (stylesheet order, not
markup order). Components expose typed parameters for variation:

```razor
@code {
    [Parameter] public bool Compact { get; set; }
    [Parameter] public string? AdditionalClass { get; set; }
}

<div class="@(Compact ? "p-2 gap-2 text-sm" : "p-4 gap-4") rounded-xl bg-surface @AdditionalClass">
```

`AdditionalClass` is for additive-only use: extra margin from parent, width constraint, `hidden`
toggle. Never for overriding internal styles.

## Conditional Classes

```razor
<button class="@(IsActive ? "bg-accent text-white" : "bg-surface text-muted")
               rounded-full px-3 py-1 transition-colors hover:bg-accent/80">
```

For complex conditional classes, extract to a computed property:

```razor
@code {
    private string ButtonClasses => IsActive
        ? "bg-accent text-white shadow-sm"
        : "bg-surface text-muted hover:bg-surface-hover";
}

<button class="@ButtonClasses rounded-full px-3 py-1 transition-colors">
```

## Sprite Resolution

Sprite-bearing DTOs (e.g. `TagChipDto`) carry the **raw `SpriteIdentifier` key** — not a resolved
URL. The rendering leaf `@inject`s `ISpriteReadService` and resolves at render time via
`GetSpriteUrl(slug, id, prefersAnimated)` (real signature: slug first, then key, then animated flag),
using the theme slug and animation preference cascaded from `ThemeContext`:

```razor
@inject ISpriteReadService Sprites
@* [CascadingParameter] ThemeContext _themeCtx — supplies slug + PrefersAnimated *@
@if (Tag.SpriteIdentifier is not null && _themeCtx is not null)
{
    <img src="@Sprites.GetSpriteUrl(_themeCtx.Slug, Tag.SpriteIdentifier, _themeCtx.PrefersAnimated)"
         alt="" class="w-4 h-4" loading="lazy" />
}
```

See `layer2-services.md` §"Sprite URLs Are Resolved At Render Time, In the Component" for the full
rule — including the no-cross-user-cache consequence and the ThemeContext plumbing.

## Avatars Are Stored URLs, Not Sprite Keys

A user's profile picture is **not** theme-pack art resolved via `GetSpriteUrl`. `User.ProfilePictureRelativeUrl`
is a user-uploaded blob path stored directly on the entity — the producing read service copies it into
the display DTO verbatim (e.g. `UserCardDto.AvatarUrl`). `ISpriteReadService.GetSpriteUrl` plays no
role for a user's own avatar; it would only apply if a feature ever needed a **themed default
placeholder** for users with no upload, and that's the producing service's call to make, not the
leaf's. The leaf (`UserCard`) just renders whatever `AvatarUrl` it's given, falling back to a static
`wwwroot` placeholder asset when null:

```razor
@* Inside UserCard — AvatarUrl is a stored path or null, never resolved here *@
<img src="@(User.AvatarUrl ?? "/img/default-avatar.png")" alt="" class="size-10 rounded-full" loading="lazy" />
```

Still governed by the "never inline SVG" rule (avatars are image assets) and the `rounded-full` radius
convention below.

## Out-of-Band Asset Images Always Carry an onerror Fallback

Sprites, badge icons, avatars, and cover art are all provisioned **outside the app** (Rclone/R2
folders, user uploads, ops-added files) — the app builds their URLs optimistically and can never
assume the asset exists. Every `<img>` in that family must therefore handle a miss in markup; a
broken-image glyph is never an acceptable render. Three sanctioned shapes, picked by what the
element means:

1. **Fallback chain** when a lesser variant is still meaningful — sprites:
   `onerror="spriteFallback(this)"` walks animated `.webp` → static `.png` → `unknown.png`.
2. **Hide** when adjacent text already carries the meaning — badge icons:
   `onerror="this.style.display='none'"` (`UserCard`, `BadgeSettingsForm`).
3. **Placeholder swap** when the slot must stay visually occupied — avatars (`onerror` → the
   default-avatar asset), cover art (`@onerror` flips a `_coverArtFailed` flag to a styled
   placeholder block — `StoryDesktop`/`StoryMobile`).

New image-bearing components pick one of these at build time — the miss case is part of the
component's contract, not an ops problem (badge icons shipped without one and rendered broken
glyphs until the L4.5 pass, 2026-07-02).

## Interaction Icons Are Inline SVG

Interaction icons (Favorite, Followed, Ignore, ReadItLater, HiddenFavorite, …) are **inline SVG
shapes**, not theme-swappable sprite URLs. This is a deliberate, permanent carve-out from the
"never inline SVG" rule above — that rule still governs everything else (tags, covers, avatars,
profile pictures: those stay `wwwroot` image assets, with avatars using stored URLs and sprites
resolved at render via `ISpriteReadService` — see the two sections above). The reason for the
split: interaction icons are small, single-color glyphs the site itself owns and styles per-state
(gray/hover/active), not theme-swappable art assets a Theme pack provides — they don't belong in
`wwwroot/sprites/`.

**Leaf stays dumb (panel supplies, leaf renders):** `UserStoryInteractionButton` takes `IconPath`
(an SVG `<path d>` string) + `AccentColor` (a CSS color) as `[Parameter]`s and renders one inline
`<svg><path d="@IconPath" /></svg>`. It has no knowledge of `InteractionTypeEnum` and injects no
service. The `InteractionTypeEnum → (IconPath, AccentColor, Label)` mapping is **locked** in
`audit/UserStoryInteractions.md` Feature 16 (table dated 2026-06-22) and transcribed verbatim into
`InteractionVisuals` (`SharedUI/UserStoryInteractions/InteractionVisuals.cs`, WU16). All six
interaction types are represented; `PrivateFavorite` reuses `Favorite`'s `IconPath` — color alone
signals privacy. **`InteractionTypeEnum` declaration order is the canonical left-to-right button
order** — the panel iterates `Enum.GetValues<InteractionTypeEnum>()` and the order is Favorite →
PrivateFavorite → Follow → Complete → ReadLater → Ignore. `ISpriteReadService.GetSpriteUrl` is
unaffected and unused here. See `audit/UserStoryInteractions.md` Feature 16 and `audit/Sprites.md`
Feature 3.

**Three-state square button (WU7 pattern):** `size-9 rounded-md grid place-items-center
transition-colors`, no outer margin (internal padding only, per the Outer Margin Rule). Accent comes
in as an inline CSS custom property — `style="--accent:@AccentColor"` — so Tailwind's JIT compiler
never has to see a dynamic class name; classes consume it via arbitrary-value syntax:

| State | Background | Shape `fill` |
|---|---|---|
| Inactive | `bg-gray-200` | `fill-gray-500` |
| Hover (interactive, not active) | `bg-gray-200` (unchanged) | `fill-[var(--accent)]` (via `group-hover:`) |
| Active (clicked, or `IsActive` true on load) | `bg-[var(--accent)]` | `fill-white` |

Read-only buttons (no `OnToggle`) render only when active and use the static active/inverted
look — no `group-hover` (nothing to hover toward). Extract the state-dependent class strings to a
computed property per "Conditional Classes" above. `Label` (`[Parameter]`) drives `aria-label` +
`title` — required because an icon-only control has no visible text.

## Layout Tailwind (Composites and Parents)

### Grid Layouts

```razor
@* Story listing — parent owns the grid *@
<div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
    @foreach (var story in Stories)
    {
        <StoryCard Story="story" />
    }
</div>
```

### Flex Layouts

```razor
@* Chapter navigation — horizontal arrangement *@
<nav class="flex items-center justify-between gap-4 py-3">
    @* children *@
</nav>
```

### Responsive Breakpoints

Tailwind's mobile-first approach: write mobile styles first, add `md:` and `lg:` prefixes for
wider viewports. This only applies when using responsive prefixes within a single component.
When desktop and mobile are structurally different, use separate components instead.

## Quill.js Stylesheet Interaction

Blazored TextEditor (Quill.js) ships its own CSS (`quill.snow.css` or `quill.bubble.css`).
This stylesheet does not participate in Tailwind's system. Tailwind's Preflight resets may
interact unexpectedly with Quill's element selectors. Test the editor early in a page context
with full CSS applied. Override Quill styles via CSS custom properties or a scoped stylesheet
rather than fighting specificity.

## Blazored.Typeahead Stylesheet (same category as Quill, WU11)

`Blazored.Typeahead`'s package CSS (`_content/Blazored.Typeahead/blazored-typeahead.css`) provides
the input/dropdown **positioning skeleton** (`.blazored-typeahead` relative-positioned wrapper,
`.blazored-typeahead__results` absolute-positioned dropdown with its own `box-shadow`/`z-index`) —
keep it for the behavior it implements, but it also ships hardcoded brand colors (`#007bff` hover,
grey borders) that read as foreign next to the site's Tailwind tokens. `TagSelector`'s `ResultTemplate`
content (dot/sprite/name) is fully ours and already token-driven; the *chrome* around it (input border,
hover highlight, focus ring) is the package's — leave it as-is for MVP rather than fighting its
specificity with `!important` overrides. Revisit only if a future visual pass calls for full skeleton
replacement, the same way Quill's CSS is flagged for later scrutiny, not solved now.

## Reader Settings as CSS (font-scope boundary)

**Tailwind's `--font-display`/`--font-body` tokens stop at `RichTextView`/`EditorView`.** Those
two components render *all* user-generated content — chapter text, comments, recommendation blurbs,
profile bios, private messages — and own their own font via the user's `ReaderSettings` override, not
the site token. Do not apply `font-*` Tailwind utilities to content rendered inside them; site chrome
(nav, labels, buttons, headings, page structure) uses the Tailwind font tokens as normal.

Reader settings (font, size, line height, text width, justify) are applied as CSS on the
`RichTextView` container element. The component receives them as a single `[CascadingParameter]
ReaderDisplaySettings? Display` (see `layer3.5-structure.md` "Ambient Viewer Settings via Cascading
Slim Bags" for why it's cascaded rather than threaded as individual parameters, and why
`ReaderDisplaySettings` is a slim property bag rather than a `*Dto` — it never crosses the service
boundary) and maps the fields to inline styles, falling back to defaults when no provider is present:

```razor
<div style="font-family: @(Display?.FontName ?? "Georgia"); font-size: @((Display?.FontSize ?? 16))px;
            line-height: @(Display?.LineHeight ?? 1.5f); max-width: @((Display?.TextWidth ?? 800))px;
            text-align: @((Display?.JustifyText ?? false) ? "justify" : "left")">
    @((MarkupString)HtmlContent)
</div>
```

## Pattern Accumulation

As components are built, visual conventions emerge (e.g., "we use `rounded-xl` for card surfaces,
`rounded-md` for inputs, `rounded-full` for chips and avatars"). These must be captured in this
file after each implementation session. Without written conventions, future sessions will make
fresh choices that drift from established patterns.

**`TagChip` (WU4, 2026-06-21):** root is `rounded-full`, internal padding only (`px-2 py-0.5` —
no outer margin; parents space chips with `gap-`/`flex flex-wrap gap-2`). Tag-type → color mapping
(light bg / dark text pairs from Tailwind's default palette, deliberately distinct from the green
`@theme` chrome tokens — tag types need mutually distinguishable hues, not site-brand color):

| `TagTypeEnum` | Classes |
|---|---|
| `Character` | `bg-emerald-100 text-emerald-800` |
| `Setting` | `bg-violet-100 text-violet-800` |
| `Genre` | `bg-sky-100 text-sky-800` |
| `ContentWarning` | `bg-rose-100 text-rose-800` |
| `CrossoverFandom` | `bg-amber-100 text-amber-900` |

**`RichTextView` (WU5, 2026-06-21):** root is a single `<div>` carrying only typography inline
styles (`font-family`/`font-size`/`line-height`/`max-width`/`text-align`, from the cascaded
`ReaderDisplaySettings`, defaulting to `ReaderSettings`' own defaults when no provider is present) —
**no border, no background, no padding.** Borders/surfaces are a Container Composite concern
(`Card`), owned by whichever context composes the leaf, not the leaf itself — a chapter-reading page
wants bare content, a `CommentItem` already provides its own card and would double-box, and an
`EditorView` preview pane that wants a bordered look wraps `RichTextView` in `Card` rather than the
leaf growing one. Renders nothing when `HtmlContent` is null/empty.

**`PaginationControls` (WU8, 2026-06-21):** root is `flex flex-col gap-2` — button row, then a
"Showing X–Y of Z" summary (`text-sm text-[--color-text-muted]`) stacked *below* the row, not beside
it. **Fixed 7-slot window for page numbers:** the numbered cells live in an inner
`flex items-center justify-center gap-1 min-w-[17.25rem]` wrapper between Prev/Next
(`17.25rem` = 7 × `size-9` + 6 × `gap-1`) — at `TotalPages > 7` the window always yields exactly 7
slots (first/last always shown, ellipsis fills gaps, slides with `CurrentPage`); at
`TotalPages <= 7` every page renders with no ellipsis, centered in that same reserved width. Net
effect: **the control's total width never changes**, whether it backs a 3-page or 300-page listing —
established as the fix for an early review where the footprint visibly shifted between pages.
Buttons (Prev/Next + page numbers) are bordered solid blocks, not bare text/hover-only —
`size-9 rounded-md border grid place-items-center transition-colors`, `bg-[--color-surface-raised]`
at rest with `hover:bg-[--color-primary]/20`; current page is a flat `bg-[--color-primary]
text-white border-[--color-primary]` fill (no ring/outline variant — flat fill was correct,
an earlier "doesn't look active" review note traced to the demo not being wired to update on click,
not a styling gap). Disabled Prev/Next: muted bg/text + `cursor-not-allowed`, no hover. No outer
margin; renders nothing when `TotalPages <= 1`. Page size is supplied by the caller
(`User.ReaderSettings.DefaultPaginationSize`), never read by the leaf itself.

**`ConfirmDialog` (WU9, 2026-06-21):** modal overlay shell — backdrop `fixed inset-0 z-50 flex
items-center justify-center bg-black/50 p-4` (click-to-cancel), panel `max-w-md rounded-xl bg-surface
p-6 shadow-lg` (`@onclick:stopPropagation` so inner clicks don't bubble to the backdrop). This is the
same shell `EditorView`'s preview popup already used inline — now the one written-down convention both
follow, rather than two independent copies drifting apart. Confirm button is `bg-primary
hover:bg-primary-strong` by default, `bg-danger` when `IsDestructive` (destructive actions: account
deletion, leaving a group, unpublishing a story). Cancel button stays neutral (`bg-surface`, bordered).
Renders nothing when `!IsOpen` — no `display:none` div sitting in the DOM.

**`UserCard` (WU10, 2026-06-21):** root is `relative inline-flex items-center gap-2 rounded-xl
bg-surface px-3 py-2` — no outer margin; parents space cards with `gap-`. Avatar is `size-10
rounded-full`, `src` falling back to a static `/img/default-avatar.svg` when `AvatarUrl` is null
(an image asset, not inline SVG markup — see "Avatars Are Stored URLs, Not Sprite Keys" above).
Username is a `block truncate font-bold` link; tagline (when present) is `block truncate text-sm
text-muted` directly beneath it; badges (when present) are a `flex items-center gap-1` row of
`size-4` icons with `title` tooltips. Caret button (`▾`) sits `ml-auto self-start`; its dropdown is
`absolute right-0 top-full z-10 min-w-40 rounded-md bg-surface py-1 shadow-medium`, items
`block px-3 py-1.5 text-sm hover:bg-surface-hover`. View Profile is always the first item (a plain
link, not gated); the rest render only when their `EventCallback` `HasDelegate`.

**`TagSelector` (WU11, 2026-06-21):** root is `flex flex-col gap-2` — **no outer margin** (the
discarded version's `mb-4` is exactly the violation this rule exists to prevent; parents space the
whole selector with `gap-`/`space-y-` like any other composite). Selected-chips row is
`flex flex-wrap gap-2` of `TagChip` leaves, sitting *above* the typeahead input per spec §5.30.4.
Typeahead dropdown rows are intentionally lighter than a chip — a solid accent dot
(`w-2 h-2 rounded-full`, not the chip's light bg/dark text pairing) + optional `w-4 h-4` sprite + name,
so the scannable list format stays visually distinct from "this is already selected":

| `TagTypeEnum` | Dot class |
|---|---|
| `Character` | `bg-emerald-500` |
| `Setting` | `bg-violet-500` |
| `Genre` | `bg-sky-500` |
| `ContentWarning` | `bg-rose-500` |
| `CrossoverFandom` | `bg-amber-500` |

(Same hue family as the `TagChip` table above, solid `-500` instead of light `-100`/dark text — keeps
dot and chip visually associated as "the same tag type" without making the dot a tiny chip.)

**`ChapterNavigation` (WU18, 2026-06-23):** root `<nav>` is `flex flex-wrap items-center gap-2 `—
no outer margin; the composing page spaces instances with its own `gap-`/`space-y-` (outer-margin
rule). **Prev/Next:** same bordered-block shape as `PaginationControls`' Prev/Next —
`inline-grid size-9 place-items-center rounded-md border transition-colors`, using
`bg-[--color-surface-raised]`/`hover:bg-[--color-primary]/20` when available and
`bg-[--color-surface-raised]/50 text-[--color-text-muted] cursor-not-allowed` when disabled (no
hover). Disabled endpoints render as `<span aria-disabled="true">`, not `<button disabled>` —
these are navigation, not actions. **Disclosure dropdowns** (chapter-select + version picker):
`<details class="relative">` + `<summary class="flex ... rounded-md border border-[--color-border] bg-[--color-surface-raised] px-3 py-1.5 text-sm hover:bg-[--color-primary]/20 transition-colors">`.
The `flex` class on `<summary>` suppresses the default browser triangle marker (sets `display: flex`,
overriding the UA `display: list-item`). Dropdown panel: `absolute left-0 top-full z-10 mt-1
max-h-{N} min-w-{N} overflow-y-auto rounded-md border border-[--color-border] bg-[--color-surface]
py-1 shadow-md`. Rows inside: `block px-3 py-1.5 text-sm`; highlighted row (current chapter/version)
`bg-[--color-primary]/10 font-semibold text-[--color-primary]`; normal row
`text-[--color-text] hover:bg-[--color-surface-hover]`; unpublished/unavailable row
`pointer-events-none text-[--color-text-muted]`. Alt-version indicator in the chapter dropdown:
a `<span title="Has alternate versions">` with a small glyph (&#8942;) — visually subtle,
semantically distinguishable, testable via `title` attribute in bUnit.

**`StoryDeck` (WU14, 2026-06-23):** three-state composite — `Stories is null` ⇒ inline loading text
(`flex items-center justify-center py-12 text-muted`); `Count == 0` ⇒ same shell with `EmptyMessage`
text; populated ⇒ outer `flex flex-col gap-6` containing a grid and `PaginationControls`. Grid is
`grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6` (canonical story-listing grid; responsive 1→2→3
columns, `gap-6` between cards — `StoryCard` carries its own padding, so `gap-6` is the only spacer).
**No outer margin** — parent spaces the deck via `gap-` / `space-y-`. `PaginationControls` is always
embedded unconditionally — it self-hides when `TotalPages <= 1`, so consumers that don't paginate
simply leave `PageSize` at its zero default. Loading-skeleton upgrade (gray placeholder cards matching
the final grid layout) is deferred as a future *additive* swap behind the unchanged `Stories is null`
branch — no contract change, no consumer impact.

**Bookshelf tab bar and mobile filter overlay (WU27, 2026-06-23):**

*Interaction icon reskin:* `Follow` (the story-follow button) was reskinned from `#4A9B52` Eterna Green
to `#2DBBA0` Manaphy Teal so the green color family is freed for curation tabs (My Stories /
Recommendations / Hidden Gems) in `BookshelfTabVisuals`. **Only the story-follow `UserStoryInteractionButton`
is affected — the user-to-user `FollowButton.razor` bell is unrelated and unchanged.**

*Desktop tab bar:* a `<nav class="flex gap-1 flex-wrap border-b border-[--color-border] pb-2">` of
`<a href="/bookshelves/{slug}">` links styled as tab chips. Each chip: `inline-flex items-center gap-1.5
rounded-t-md px-3 py-1.5 text-sm transition-colors`. The active chip (determined by matching the current
URL slug) gets `aria-current="page"` and an accent-colored background + white icon/text; inactive chips
get a neutral surface hover. Icon is a `<svg viewBox="0 0 24 24" class="size-4 flex-shrink-0"><path d="…"/></svg>`
with `fill-current`. No outer margin on the `<nav>` root — page body provides spacing.

*Mobile tab selector:* a `<details>` disclosure following the ChapterNavigation pattern (see above). The
`<summary>` shows the active tab's icon + label and a chevron; the dropdown panel lists all 11 tabs with
icon + label. Same `absolute left-0 top-full z-10` positioning as the chapter-select dropdown.

*Mobile filter overlay:* `ResultsFilterPanel` on mobile surfaces from a "Filter" button. When open:
backdrop `fixed inset-0 z-50 bg-black/50 p-4` (click-to-close); panel `max-w-sm w-full rounded-xl
bg-[--color-surface] p-4 shadow-lg overflow-y-auto` with `@onclick:stopPropagation`; renders nothing
when closed. **This is the "third consumer" the WU9 note flagged for deciding on a shared `Modal`
primitive — decision: do NOT extract.** A slide-in/drawer filter panel is structurally different from
the centered ConfirmDialog; the `fixed inset-0` shell is the only shared part, too thin to justify a
wrapper.

*New bookshelf tab icons (all 24×24 viewBox, single-color, nonzero fill rule):*

| `BookshelfTab` | Color | Concept |
|---|---|---|
| `MyStories` | `#2F7D4F` Leafeon Green | Book body+spine (left ⅔) + diagonal quill pen crossing |
| `HiddenGems` | `#1FA37A` Torterra Emerald | Kite diamond with CCW crown-facet cutout |
| `Recommendations` | `#5BB85A` Roserade Green | 4-pointed star (top-right) + two diagonal streak trails |
| `ActivelyReading` | `#2E96A8` Lake Acuity Blue | Two open-book page halves + filled text-line rects |
| `Abandoned` | `#9A8580` Wayward Cave Gray | House silhouette (rect+triangle roof) + CCW door-opening |

The 6 interaction-backed tabs reuse `UserStoryInteractionVisuals.For(…)` verbatim. The gem + shooting
star constants also live in `SharedUI/Recommendations/RecommendationVisuals.cs` (consumed by WU29's
`RecommendationCard`). Visual sign-off for all new icons via `wwwroot/icon-preview.html` harness
(throwaway — remove before Stage 6).

**`RecommendationCard` (WU29, 2026-06-23):** three visual states layered on a base card surface
(`rounded-xl bg-surface px-4 py-4`) — no outer margin; the composing `RecommendationSection` spaces
cards via `gap-`.

| State | Visual treatment |
|---|---|
| Plain (default) | Base card surface only; no accent |
| `IsHighlightedByAuthor` (Author's Pick) | Accent border (`border-2 border-[--color-primary]`) + glow (`shadow-[0_0_0_2px_var(--color-primary)/20]`) + an "Author's Pick" ribbon label in `--color-primary` (Roserade Green `#5BB85A` maps to `--color-primary` in this feature's visual register) |
| `IsHiddenGem` | Gem badge icon (Torterra Emerald `#1FA37A` inline SVG, from `RecommendationVisuals`) pinned `absolute top-2 right-2`, `title="Hidden Gem"` |
| Both | Accent border/glow from spotlight + gem badge both render |

Like button: same bordered-block shape as `PaginationControls`/`ChapterNavigation` at rest
(`size-8 rounded-md border grid place-items-center transition-colors`), using Roserade Green
(`#5BB85A`) as the active-state accent (inline CSS custom property `--accent` pattern from WU7).
Successful-rec count rendered as a small `text-xs text-[--color-text-muted]` badge beside the like
button.

**`RecommendationEditor` (WU29, 2026-06-23):** root is `flex flex-col gap-3` — no outer margin.
Character-count meter: `text-xs text-[--color-text-muted]`, turns `text-[--color-success]` once
the 500-char minimum is met. Submit button disabled (`opacity-50 cursor-not-allowed`) until met.
Button row follows `CommentEditor`: primary button `bg-[--color-primary]`, cancel `bg-[--color-surface]
border`.

**`RecommendationHelpfulPrompt` (WU29, 2026-06-23):** inline non-blocking banner — root is
`flex items-center justify-between gap-3 rounded-xl border border-[--color-border] bg-[--color-surface]
p-3 text-sm` — no outer margin; the chapter reading page controls placement. Yes button:
`rounded-md bg-[--color-primary] px-3 py-1 text-white text-xs`. Dismiss link:
`text-[--color-text-muted] text-xs underline cursor-pointer`. Renders nothing when dismissed
(local `_dismissed` bool).

**Notification icons (WU33, design-pending visual sign-off):** notification category/type icons are **inline
SVG**, the same permanent carve-out as interaction icons. `NotificationCategoryVisuals.cs` is the single source
of truth, mirroring `BookshelfTabVisuals`. Reuse existing icon paths from `UserStoryInteractionVisuals` and
`RecommendationIcons` where the concept overlaps (YourFollows → Follow path, Warnings → Ignore path, etc.);
new glyphs introduced only for categories with no existing equivalent. Per-type overrides in `NotificationPresenter`
follow the same reuse discipline. L4 cells for Features 42/43 remain Stage 1 until visual sign-off — Tailwind
class choices for `NotificationItem` / `NotificationBell` / `NotificationsPage` / `NotificationSettingsPage`
are not locked here and will be added to Pattern Accumulation after visual review.

**`DesktopLayout` top bar / `UserMenu` / `CreateMenu` (2026-07-01):** replaced the placeholder
`w-64` empty sidebar + hardcoded MS "About" link with a single full-width sticky bar —
`sticky top-0 z-20 flex items-center gap-6 border-b border-(--color-border)
bg-(--color-surface-raised) px-6 py-3 shadow-subtle`. Layout: wordmark (`font-display`) → `<nav>`
of `<NavLink>`s (Home/Discover/Tags/Groups, `ActiveClass="font-semibold text-(--color-primary)"`,
Home uses `Match="NavLinkMatch.All"`) → `ml-auto` right-side chrome group. No left sidebar; no
inline search field (Discover link covers it). Mobile is a structurally separate composition
(`MobileLayout`, unchanged) per the desktop-vs-mobile split rule.

Two new dropdown components follow the `NotificationBell` caret pattern exactly (`relative` root +
`@onclick` toggle + `@if(_open)` `absolute right-0 top-full z-30` panel — not a `fixed inset-0`
modal):
- **`UserMenu`** replaces `LoginDisplay` on desktop (mobile keeps `LoginDisplay` as-is). Trigger
  shows `context.User.Identity?.Name`. Flyout: My Profile (`/user/{id}`, id resolved from the
  cascaded `Task<AuthenticationState>`'s `NameIdentifier` claim — never `IActiveUserContext` in
  SharedUI), Bookshelves, Settings, then a divider + `<AuthorizeView Roles="Moderator,Admin">`-gated
  Mod tools row, then a divider + the existing POST-`Account/Logout` form (antiforgery + `ReturnUrl`,
  copied verbatim from `LoginDisplay`). `NotAuthorized` renders a plain `Account/Login` link.
- **`CreateMenu`** is a `bg-(--color-accent)` "Write" button, `<AuthorizeView><Authorized>`-gated
  (renders nothing when anonymous), opening New Story / New Blog Post / New Group links.

Dropdown list items share one class string:
`block px-4 py-2 text-sm text-(--color-text) transition-colors hover:bg-(--color-surface)`, panel
chrome is `w-44`–`w-48 rounded-xl border border-(--color-border) bg-(--color-surface-raised) py-1
shadow-medium`, dividers are `my-1 border-t border-(--color-border)`. Verified via HTTP/HTML
inspection (no Chrome MCP tool available this session): anonymous shows only "Log in", no Write
button; TestUser shows username + Write, no Mod tools; AdminUser shows Mod tools; `/mod/reports`
returns 403 for TestUser and 200 for AdminUser. Click-driven flyout-open behavior was not
browser-verified this session (relies on the same `@onclick`/`_open` mechanism already proven by
`NotificationBell` in production) — worth a follow-up visual pass once a browser tool is available.
