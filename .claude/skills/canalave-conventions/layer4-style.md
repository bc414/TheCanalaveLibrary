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

**Leaves never inject `ISpriteReadService`.** A sprite URL is resolved once, upstream, by whichever
**read service** is producing the display DTO — during its `.Select()`/mapping step, via
`ISpriteReadService.GetSpriteUrl(theme, spriteIdentifier, animated)` (note the real signature: theme
first, then key, then the animated flag) — using the current user's theme + animated-sprite
preference. The DTO carries the already-resolved relative URL; the leaf just renders it:

```razor
@* Inside a leaf component — Tag.SpriteUrl was set server-side, not here *@
@if (Tag.SpriteUrl is not null)
{
    <img src="@Tag.SpriteUrl" alt="" class="w-5 h-5 inline-block" loading="lazy" />
}
```

Sprites live at `wwwroot/sprites/themes/{theme}/static/{key}.png` or `animated/{key}.webp`. See
`layer2-services.md` §"Sprite URLs Are Resolved Server-Side, At Projection Time" for the full rule and
the request-scoping consequence (never cache a sprite-bearing DTO across users/themes).

## Interaction Icons Are Inline SVG (superseded the sprite-URL design — WU7)

**Settled (WU7), supersedes the WU2-era plan below `GetInteractionIcon` line:** interaction icons
(Favorite, Followed, Ignore, ReadItLater, HiddenFavorite, …) are **inline SVG shapes**, not
theme-swappable sprite URLs. This is a deliberate, permanent carve-out from the "never inline SVG"
rule above — that rule still governs everything else (tags, covers, avatars, profile pictures: those
stay `wwwroot` sprite/image assets resolved via `GetSpriteUrl`). The reason for the split: interaction
icons are small, single-color glyphs the site itself owns and styles per-state (gray/hover/active),
not theme-swappable art assets a Theme pack provides — they don't belong in `wwwroot/sprites/`.

**Leaf stays dumb (panel supplies, leaf renders):** `UserStoryInteractionButton` takes `IconPath`
(an SVG `<path d>` string) + `AccentColor` (a CSS color) as `[Parameter]`s and renders one inline
`<svg><path d="@IconPath" /></svg>`. It has no knowledge of `InteractionTypeEnum` and injects no
service. The `InteractionTypeEnum → (IconPath, AccentColor)` mapping lives in the owning composite
(`StoryInteractionPanel`, WU16) or a shared constants helper it calls — mirroring the
"owning composite maps the domain enum" pattern the sprite-key design used, just without
`ISpriteReadService` in the chain. `ISpriteReadService.GetSpriteUrl` is unaffected and unused here.
See `audit/UserStoryInteractions.md` Feature 16 and `audit/Sprites.md` Feature 3.

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
| `Relationship` | `bg-pink-100 text-pink-800` |

**`RichTextView` (WU5, 2026-06-21):** root is a single `<div>` carrying only typography inline
styles (`font-family`/`font-size`/`line-height`/`max-width`/`text-align`, from the cascaded
`ReaderDisplaySettings`, defaulting to `ReaderSettings`' own defaults when no provider is present) —
**no border, no background, no padding.** Borders/surfaces are a Container Composite concern
(`Card`), owned by whichever context composes the leaf, not the leaf itself — a chapter-reading page
wants bare content, a `CommentItem` already provides its own card and would double-box, and an
`EditorView` preview pane that wants a bordered look wraps `RichTextView` in `Card` rather than the
leaf growing one. Renders nothing when `HtmlContent` is null/empty.
