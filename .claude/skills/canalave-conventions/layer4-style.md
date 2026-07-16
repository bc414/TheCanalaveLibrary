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
> (`flex`, `gap-`, `px-`/`py-`, …) for the skeleton instead. **The former Identity exception is
> revoked (2026-07-10):** the 2026-07-10 surface audit confirmed no Bootstrap stylesheet is loaded
> anywhere, so `Identity/Pages/**`'s kept classnames render nothing — the auth pages are unstyled
> native HTML (including destructive actions rendering as plain links). Brian's determination:
> restyle Identity pages now under the role system (see "Element Roles" below) as part of the
> design-solidification sweeps, instead of waiting for a separate scaffold unblock. Until that
> sweep lands, treat every Bootstrap classname there as dead.

## Prerequisite: Design Tokens (LOCKED at the Phase A gate, 2026-07-10)

The role-based manifest in `TheCanalaveLibrary.Server/Styles/app.css` `@theme {}` is the locked
token set (gate review on `/dev/design-gallery`, values tuned live with Brian). Full manifest
with comments lives in the file itself; the durable facts:

- **Grounds:** `--color-canvas` oklch(0.84 0.13 130) — deep vibrant grass, painted ONLY by the
  `body` rule (components never use `bg-canvas`); `--color-surface` (Container beige);
  `--color-surface-hover` (THE neutral hover/selected tint); `--color-surface-raised` (Overlay
  chrome — deliberately lighter than surface); `--color-paper` + `--color-paper-frame` (Content
  Surface material — **frame treatment ratified: side rails**, `border-x-4` in the frame color);
  `--color-backdrop` (modal dimming).
- **Ink/lines:** `--color-text`, `--color-text-muted`, `--color-border` — web-neutral warm
  grays, contrast-derived.
- **Control:** `--color-action` oklch(0.76 0.13 145) — light fill carrying **dark `--color-text`
  ink** (7.9:1), `--color-action-hover`, `--color-action-ink` (links/active text/focus ring);
  `--color-mission` oklch(0.56 0.185 259) + `--color-mission-hover`; `--color-danger-strong`
  (destructive hover, 5.8:1 white).
- **Contrast policy (Brian-ratified 2026-07-10): WCAG AA 4.5:1 for ALL text, no large-text
  carve-outs.** This is why mission stays 0.56 — every lighter blue (0.58–0.62) measures
  3.7–4.4:1 against both white and dark ink. Measured, not asserted; the palette artifact
  computes ratios live.
- **Indicator:** HP trio (`success`/`warning`/`danger` — warning dark enough for text duty),
  `--color-progress` (EXP light blue, never mission blue).
- **Feature accents as tokens:** `--color-interaction-*` ×6 (**follow retuned to orange
  `#E0782F` at the gate** — was Manaphy Teal `#2DBBA0`, conflicted with the rec/gem/my-stories
  greens; supersedes the WU27 reskin note below), `--color-rec`, `--color-gem`,
  `--color-tab-*` ×3. `*Visuals.cs` classes carry `var(--color-…)` references, never hex.
- **Tag types:** `--color-tagtype-*` ×5 (Water/Ground/Psychic/Fire/Dragon per the ratified
  mapping). **Chip recipe ratified at gate: SOLID type-color ground with standard black/white
  text** (dark ink on Setting's light Ground tan; white on the rest) — the tint recipe is for
  status badges/alerts only.
- **Fonts SHIPPED:** Fraunces + Mulish as self-hosted variable woff2 in `wwwroot/fonts/` with
  `@font-face` in app.css; `body` carries `font-body` + `--color-text`. Chrome-only, as before.
- **Radii:** unchanged convention (`rounded-xl` cards, `rounded-md` inputs, `rounded-full`
  chips/avatars). **Shadows:** subtle=sticky bar, medium=dropdowns, prominent=modals/drawers/
  toasts (hybrid elevation: borders at rest). **Z ladder:** `--z-dropdown/sticky/drawer/modal/
  toast/error` = 10–60, consumed as `z-(--z-x)`.
- `--color-primary`/`--color-primary-strong`/`--color-accent` exist only as a Phase A–B **alias
  bridge** and are deleted when the Phase B sweep completes; new code never references them.

`/dev/design-gallery` (Development-only) is the living composition reference for every recipe;
the palette-report artifact carries swatches/hex/contrast.

## Element Roles (design constitution, ratified 2026-07-10)

Every visual element has exactly one **role**; the role determines its ground, border, ink, and
states. The seven roles — Canvas / Wayfinding / Container / Content Surface / Control /
Indicator / Overlay — are defined with game-metaphor rationale and a full per-component audit in
`.claude/design/surface-registry.md`; the durable rules live here.

**The Content Surface vs Container test (ratified):**

- **Content Surface** = user-generated *prose*: anything rendered through `RichTextView` or
  authored through `EditorView`/Quill (chapter text, comment bodies, recommendation blurbs,
  profile bios, private messages, blog posts, vouch notes). One shared material (near-white
  "dialog box" ground), encoded once in a `ContentSurface` wrapper with a closed variant set
  (≈ Reading / Inline / Input); owners choose variant and placement, never invent a treatment.
  `RichTextView`/`EditorView` never render outside a ContentSurface (grep-enforceable).
- **Container text** = user-*written* words serving a **site function**: length-capped fields
  (story short description, group description), identifiers (titles, usernames, taglines), and
  snippets/excerpts of UGC shown in listings. The cap exists precisely because the text is
  co-opted into site furniture — so it wears Container/Wayfinding treatment, not the sheet.
  **The mechanical test: rendered via RichTextView/EditorView → Content Surface; anything else →
  Container/Wayfinding text.** No third role is needed for capped fields.
- **Comment decomposition (ratified):** a comment is a Container header (author link, avatar,
  timestamp, action row — site-created metadata) + a Content Surface body. Same decomposition
  applies to recommendations and vouches.
- **Private messages (ratified):** `MessageItem` uses Content Surface for the body — **no
  colored chat bubbles** (the current own-message-primary / other-message-raised bubbles were an
  unratified implementation default and are to be removed). Authorship is signaled by layout:
  own messages right-aligned with the active user's avatar + name, the other party left-aligned
  with theirs. Both sides sit on the same Content Surface material.
- **Content Surface material (ratified):** framed dialog box — near-white ground + visible
  border + a restrained frame accent, the D/P text box abstracted. The frame treatment is the
  future Theme/ReaderSettings hook (the games' own "Window Frame" option). Encoded once in the
  `ContentSurface` wrapper.
- **Feature accent colors → tokens (ratified):** the deliberate per-item accents (bookshelf
  tabs, notification categories, interaction types, recommendation green + gem emerald) move
  into `@theme` as named tokens; the `*Visuals.cs` classes keep the enum→visual mapping but
  carry `var(--color-…)` references instead of hex. Rationale: identical usage patterns keep
  working (utilities, the WU7 `--accent` inline var, svg fills) while the colors join theme
  overrides (future dark mode = plain CSS re-valuing of the same variables under a
  `[data-theme]` selector — `@theme` itself never changes), contrast tooling, and the
  token-existence check. Raw hex in class strings or `style=` attributes is a defect once the
  sweep lands. Discovery's `sky-600` family is NOT deliberate — it sweeps to `primary`.
- **Tag-type palette → Pokémon type colors (ratified):** the TagChip/TagSelector pastel
  carve-out is superseded; the five `TagTypeEnum` hues become type-color-derived tokens
  (⚖️ which type maps to which TagType is Brian's pick during Phase A). Chips must hold their
  identity on the beige Container ground.
- **Read-only interaction squares → passive look (ratified):** when `UserStoryInteractionButton`
  has no `OnToggle`, the active state renders visibly passive (tinted ground + accent-colored
  icon rather than solid accent fill + white icon; no pointer cursor) so Indicators stop
  impersonating Controls. Supersedes the WU7 note's "static active/inverted look".
- **Control color families (ratified 2026-07-10):** two families replace `primary`/`accent`
  (both names retire with the Phase A sweep). **`--color-action` / `--color-action-hover` /
  `--color-action-ink`** — the everyday interactive green: `action`/`action-hover` are light
  FILLS carrying dark `--color-text` ink (inviting, not dark green); `action-ink` is the
  TEXT-shaped form — links (links stay green, not hyperlink blue), active/selected text states,
  the focus ring, outline-button text. **`--color-mission` / `--color-mission-hover`** — surf
  blue, reserved for CTAs of the mission-defining features: creation (Write, New Story/Post/
  Group, compose), tree search, random search, interaction-history filtering, and the
  recommendation/hidden-gem/vouch/spotlight actions. Membership test for any new button: "is
  this the mission, or is this plumbing?" Everything not mission is `action`.
- **Feature accent vs mission split (ratified):** feature accents (rec Roserade green, gem
  emerald, bookshelf tab colors, interaction accents) color *identity* — badges, icons, ribbons,
  glows, tab identity (Indicator role). The *actionable Controls* of those same features wear
  mission blue ("Recommend this story", mark-as-Hidden-Gem, spotlight toggle, Vouch). The two
  formerly-accidental `sky-600` sites (Discovery "Give me more", interaction-filter checkboxes)
  are mission-bucket features and become deliberate `--color-mission`.

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
form when a class assertion is unavoidable (see `ConversationListItemTests`; note parens are invalid
in a CSS *selector*, so bUnit lookups use an attribute-substring selector —
`[class*='text-(--color-text-muted)']` — see `UserCardTests`). Rebuild CSS with
`npm run css:build` in `TheCanalaveLibrary.Server/` after class changes.

**The bare-name trap (second silent-failure mode):** a bare utility like `bg-surface` works only
because the class suffix exactly equals the token's key after the namespace prefix (`--color-surface`
→ `surface`). For `--color-text-muted` the generated bare utility is `text-text-muted` — the
intuitive-looking `text-muted` matches no token, so Tailwind emits **nothing** (same
looks-right-renders-as-nothing failure as the bracket form; 49 usages swept to the paren form
2026-07-10). Rule: single-word tokens (`bg-surface`, `bg-accent`, `text-text`) may use the bare
form; any multi-word token is written in the paren form (`text-(--color-text-muted)`,
`bg-(--color-surface-raised)`) — never a guessed short name.

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
<button class="@(IsActive ? "bg-accent text-white" : "bg-surface text-(--color-text-muted)")
               rounded-full px-3 py-1 transition-colors hover:bg-accent/80">
```

For complex conditional classes, extract to a computed property:

```razor
@code {
    private string ButtonClasses => IsActive
        ? "bg-accent text-white shadow-sm"
        : "bg-surface text-(--color-text-muted) hover:bg-surface-hover";
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

Read-only buttons (no `OnToggle`) render only when active — **look superseded 2026-07-10**: per
"Element Roles" they get a distinct passive treatment (tinted ground + accent-colored icon, no
pointer cursor) instead of the active/inverted Control look, so passive Indicators stop
impersonating clickable Controls. No `group-hover` either way (nothing to hover toward). Extract
the state-dependent class strings to a computed property per "Conditional Classes" above. `Label` (`[Parameter]`) drives `aria-label` +
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

## Typeahead styling (CanalaveTypeahead — superseded Blazored.Typeahead at the Global Flip)

Blazored.Typeahead (and its package CSS + hardcoded `#007bff` chrome, which this section used to
sanction as a leave-as-is skeleton) was removed in the Global Flip wave: the archived library's
programmatic-Value-clear bug crashed the WASM renderer outright (Blazored/Typeahead#221). Its
replacement, `SharedUI/Controls/CanalaveTypeahead.razor`, is fully token-driven — Control-role
input chrome, Overlay-role dropdown panel matching `UserMenu`'s flyout grammar
(`z-(--z-dropdown)`, `bg-(--color-surface-raised)`, `shadow-medium`), and the one neutral
`--color-surface-hover` doubling as the keyboard-highlight tint. No foreign CSS remains; the
old "revisit at a future visual pass" flag is closed.

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

**`TagChip` (WU4, 2026-06-21; color mapping superseded 2026-07-10):** root is `rounded-full`,
internal padding only (`px-2 py-0.5` — no outer margin; parents space chips with
`gap-`/`flex flex-wrap gap-2`). The Tailwind-pastel tag-type mapping below is **superseded** —
per "Element Roles", tag types become Pokémon-type-color tokens during the design-solidification
sweeps (the distinguishable-hues rationale survives; the specific pastels don't — they wash out
on the beige Container ground). Historical mapping, do not copy into new code:

| `TagTypeEnum` | Classes |
|---|---|
| `Character` | `bg-emerald-100 text-emerald-800` |
| `Setting` | `bg-violet-100 text-violet-800` |
| `Genre` | `bg-sky-100 text-sky-800` |
| `ContentWarning` | `bg-rose-100 text-rose-800` |
| `CrossoverFandom` | `bg-amber-100 text-amber-900` |

**`RichTextView` (WU5, 2026-06-21; vessel rule superseded 2026-07-10):** root is a single `<div>`
carrying only typography inline styles (`font-family`/`font-size`/`line-height`/`max-width`/
`text-align`, from the cascaded `ReaderDisplaySettings`, defaulting to `ReaderSettings`' own
defaults when no provider is present) — **no border, no background, no padding.** Renders nothing
when `HtmlContent` is null/empty. *Superseded part:* WU5 originally delegated the vessel to
"whichever context composes the leaf" on the assumption that different contexts legitimately want
different looks ("a chapter-reading page wants bare content"). That assumption was invisible while
the page ground was white and was refuted when the canvas got a real color — ten owners had made
ten different vessel choices. Per "Element Roles" above, the bare leaf and reader-owned typography
survive, but every composition site now presents it inside the shared `ContentSurface` material;
owners select a variant and placement only.

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
text-(--color-text-muted)` directly beneath it; badges (when present) are a `flex items-center gap-1` row of
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
(`flex items-center justify-center py-12 text-(--color-text-muted)`); `Count == 0` ⇒ same shell with `EmptyMessage`
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

**`PollView` / `PollEditorForm` (WU-Polls, 2026-07-12):** poll card root is
`flex flex-col gap-3 rounded-xl border border-(--color-border) bg-(--color-surface) p-4` — a
Container (poll name/description are capped plain text, never RichTextView → no ContentSurface).
Status badge uses the semantic-tint recipe (`bg-(--color-success)/15 …` Open, warning tint
Pending, neutral surface-hover ring Closed). Result bars are Indicator role:
`h-2 rounded-full bg-(--color-progress)` fill inside a `bg-(--color-surface-hover)` track — EXP
blue, never mission blue. Vote/Update = `action` family; Create Poll / New Site Poll / Add Poll =
`mission` (creation bucket); Close now = warning tint; Delete = danger tint + `ConfirmDialog
IsDestructive`. Manage row is separated by `border-t border-(--color-border) pt-2`. Voter names
are standard green links (`text-(--color-action-ink) hover:underline`).

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

## Interaction States (one recipe per role — Phase C, 2026-07-10)

The blind-era drift (4 hover recipes, 8 panel chromes, 3 z-values for sibling dropdowns) is
resolved by this grammar. Components look these up; they never invent states.

| State | Recipe | Notes |
|---|---|---|
| **Neutral hover** (menu rows, list rows, neutral buttons, disclosure summaries) | `hover:bg-(--color-surface-hover)` | THE one neutral hover. The old `surface`, `surface-raised`, and primary-tint hover recipes are retired. |
| **Selected** (current TOC entry, selected conversation, active version pill) | `bg-(--color-action-ink)/10 text-(--color-action-ink)` (+ `border-(--color-action-ink)` where bordered) | Selected ≠ hover: selection is action-ink tinted, hover is a beige step. |
| **Action button hover** | `hover:bg-(--color-action-hover)` | Light-fill family keeps dark ink in all states. |
| **Mission button hover** | `hover:bg-(--color-mission-hover)` | White ink in all states. |
| **Destructive button hover** | `hover:bg-(--color-danger-strong)` | Filled danger only; Ban-tier actions. |
| **Semantic tint buttons** (mod approve/warn/suspend) | `bg-(--color-X)/15 text-(--color-X) ring-1 ring-(--color-X)/30 hover:bg-(--color-X)/25` | Filled semantic buttons are reserved for destructive; everything else uses the tint recipe (matches Indicator badges). |
| **Focus (keyboard)** | Global rule in `app.css`: `:where(a, button, [role="button"], input, select, textarea, summary):focus-visible { outline: 2px solid var(--color-action-ink); outline-offset: 2px; }` | Site-wide D/P cursor ring at zero specificity; per-component `focus:ring-*` recipes may layer on top. Never remove the global rule. |
| **Disabled** | `disabled:opacity-50 cursor-not-allowed` (or `opacity-50` on non-button roots) | One opacity everywhere; the 30/40 variants are retired. |
| **Links** | `text-(--color-action-ink) hover:underline` | Green links, ratified. |

**Accessibility as a Stage-5 criterion (added 2026-07-15, Feature 65 / WU-A11y, `middle-addendum.md`
§3 #22).** The global focus-visible rule and the WCAG AA 4.5:1 contrast policy above are necessary
but not sufficient — neither is currently *verified* as part of any Stage-5 sign-off. A component's
L4 Stage 5 claim should additionally mean: interactive elements are keyboard-navigable (reachable
and operable via Tab/Enter/Space, not just clickable), the global focus-visible ring is not
suppressed, and form inputs have an associated `<label>` (or `aria-label`) — not full WCAG AA
verification per component, which is out of scope until WU-A11y's whole-site pass (gated on
`middle_plan_v2.md` decision row 12; see `audit/Accessibility.md`) sets the real bar and revisits
already-shipped components.

**Overlay recipe (one chrome, one z-ladder, one dismissal):**
- Dropdown/flyout panels: `absolute … z-(--z-dropdown) rounded-xl border border-(--color-border) bg-(--color-surface-raised) py-1 shadow-medium`.
- Modals: backdrop `fixed inset-0 z-(--z-modal) bg-(--color-backdrop)`; panel `rounded-xl border border-(--color-border) bg-(--color-surface-raised) p-6 shadow-prominent`.
- Drawers (mobile filter panels): `z-(--z-drawer)`, `shadow-prominent`, backdrop token.
- Sticky top bar `z-(--z-sticky) shadow-subtle`; ToastHost `z-(--z-toast)`; `#blazor-error-ui` `z-(--z-error)`. Raw `z-10/20/30/50`, raw `shadow-sm/md/lg/xl`, and `bg-black/50` are defects.
- **Dismissal** (`js/dismiss.js`, loaded in App.razor): Blazor-stateful flyouts render a
  transparent `[data-flyout-catcher]` div (`fixed inset-0 z-(--z-dropdown)`, `@onclick` = the
  component's Close) inside their `@if(open)` block, before the panel — outside-click closes via
  Blazor state, Escape "clicks" the topmost catcher, and exclusive-open falls out naturally.
  Native `<details>` dropdowns carry `data-dropdown` and are closed by the same script on
  outside-click/Escape. New flyouts MUST use one of these two shapes.

**Errors surface through the standing components:** transient action failures →
`IToastService.Show(ExceptionPresenter.GetUserMessage(ex), ToastLevel.Danger)` (GroupPage's
local fixed-position error div was converted 2026-07-10 — never hand-roll a toast); persistent
form/summary errors → `InlineAlert`. Raw `ex.Message` in UI remains a defect per
`error-handling.md`.
