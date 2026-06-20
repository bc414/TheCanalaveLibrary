# Layer 4 — UI Style (Visual + Layout)

Tailwind utility classes, sprite resolution, responsive variants, images, conditional class
expressions. Blocked on design tokens being locked.

> **Tailwind version note:** the project uses **Tailwind v4, CSS-first configuration** — tokens live
> in an `@theme {}` block inside `TheCanalaveLibrary.Server/Styles/app.css` (the input stylesheet),
> not in a `tailwind.config.js`. Spec §2.1 and the original Axiom 9 phrasing describe the older v3
> `tailwind.config.js` model; the spec is a read-only historical snapshot and is not edited — this is
> the resolved, authoritative convention. See `forward_plan.md` Phase C "Resolved."

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

```razor
@* Inside a leaf component *@
@if (SpriteIdentifier is not null)
{
    <img src="@SpriteService.GetSpriteUrl(SpriteIdentifier, Theme, PrefersAnimated)"
         alt="@TagName" class="w-5 h-5 inline-block" loading="lazy" />
}
```

Sprites are `wwwroot/images/themes/{theme}/static/` or `animated/` (Animated WebP).
`ISpriteReadService` resolves the full path; the component just renders the URL.

## Theme-Swappable Interaction Icons

```razor
<img src="@SpriteService.GetInteractionIcon(InteractionType.Favorite, Theme)"
     alt="Favorite" class="w-6 h-6" />
```

Default icons: star (follow), heart (favorite). With Pokémon theme: Staryu, Luvdisc, etc.
Icons are `wwwroot` assets resolved by the sprite service, not inline SVG.

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

**Tailwind's `--font-display`/`--font-body` tokens stop at `RichTextView`/`RichTextEditor`.** Those
two components render *all* user-generated content — chapter text, comments, recommendation blurbs,
profile bios, private messages — and own their own font via the user's `ReaderSettings` override, not
the site token. Do not apply `font-*` Tailwind utilities to content rendered inside them; site chrome
(nav, labels, buttons, headings, page structure) uses the Tailwind font tokens as normal.

Reader settings (font, size, line height, text width, justify) are applied as CSS on the
`RichTextView` container element. The component receives settings as parameters and maps them
to inline styles or CSS custom properties:

```razor
<div style="font-family: @FontName; font-size: @FontSize;
            line-height: @LineHeight; max-width: @TextWidth;
            text-align: @(JustifyText ? "justify" : "left")">
    @((MarkupString)HtmlContent)
</div>
```

## Pattern Accumulation

As components are built, visual conventions emerge (e.g., "we use `rounded-xl` for card surfaces,
`rounded-md` for inputs, `rounded-full` for chips and avatars"). These must be captured in this
file after each implementation session. Without written conventions, future sessions will make
fresh choices that drift from established patterns.
