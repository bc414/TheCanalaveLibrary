# Surface Registry — every visual element, classified by role

Phase 0 deliverable of the design-solidification plan (2026-07-10). Purpose: an exhaustive
inventory of every visual element in the UI, its **current** treatment, its **assigned role**
under the ratified taxonomy, and a **mismatch flag** where current treatment contradicts the
role. This document is the checklist for the Phase B re-role sweep and the input Brian ratifies
before Phase A token values lock.

How to read: one table per component; one row per distinct element kind. "Current classes" lists
only ground/border/shadow/hover/ink/z utilities (layout omitted). Mismatch "—" = element already
consistent with its role.

## The seven roles (ratified constitution, 2026-07-10)

| # | Role | Game metaphor | Expected treatment |
|---|---|---|---|
| 1 | Canvas | Route terrain | Page background only; painted by the `body` rule; nothing else uses it |
| 2 | Wayfinding | Location plaque, signposts | Page titles/tab bars/section headers/empty states — plaque treatment or vessel edge; never bare paragraphs on canvas |
| 3 | Container | The path | Site-content vessels (cards, rows, panels): `surface` beige, border at rest (hybrid elevation) |
| 4 | Content Surface | Dialog/text box | ALL RichTextView/EditorView content: shared `ContentSurface` wrapper (Reading / Inline / Input variants), near-white ground; reader-owned typography inside |
| 5 | Control | Menu buttons, cursor | Buttons/inputs/toggles/interaction squares; focus ring = primary outline; primary distinct from canvas green |
| 6 | Indicator | HP/EXP bars, type chips, stat boxes | Semantic tokens (HP trio), type-color tag palette, stat tiles; color never sole channel |
| 7 | Overlay | X-menu over the world | Top bar, flyouts, modals, toasts: `surface-raised`, shadow allowed, one panel recipe, one z-scale, uniform dismissal |

Assignment tests: RichTextView/EditorView-rendered prose = role 4; user-*written identifiers*
(titles, usernames, taglines) = roles 2/3 text, never role 4. Site-computed facts (counts,
statuses, dates-as-status) = role 6.

## Global cross-cutting censuses (session data, 2026-07-10)

These apply across clusters and are the systemic mismatches; per-component tables below carry
the local instances.

**Hover recipes for "neutral row highlight" — 4 concurrent recipes (one role, one recipe needed):**
- `hover:bg-(--color-surface)` — UserMenu, CreateMenu
- `hover:bg-(--color-surface-raised)` — NotificationBell, mod pages, group rows, MessagesNavLink
- `hover:bg-(--color-surface-hover)` (+ bare form) — 15 files (UserCard/StoryCard dropdowns, chapter lists, notifications, bookshelves…)
- `hover:bg-(--color-primary)/10|/20` — 12 files (pagination, tag directory, dialogs, conversation list)
- Uniform & correct already: solid-button hover (24× `primary-strong`), link hover (44× `hover:underline`), focus ring (36× `focus:ring-(--color-primary)`; 2 sky-500 stragglers), disabled (33× `disabled:opacity-50`).

**Elevation — tokens losing to Tailwind defaults 17:7:**
`shadow-sm` ×8, `shadow-lg` ×6, `shadow-md` ×5, `shadow-xl` ×4 vs `shadow-medium` ×6,
`shadow-subtle` ×1; `shadow-prominent` unused. Hybrid rule (Phase C): borders at rest, token
shadow only on Overlays; retire raw defaults.

**Dropdown/flyout panel chrome — 8 variants on one role:** radius `rounded-md`/`rounded-xl`,
ground `surface`/`surface-raised`, shadow `shadow-md`/`shadow-medium`, border present/absent,
z-index 10/20/30 (drift). Confirmed bug: UserMenu has no outside-click/Escape dismissal and
renders overlapping the NotificationBell flyout.

**Raw Tailwind palette (should be tokens or documented carve-outs):** status/rating badges
(`bg-green-100`/`bg-yellow-100`/`bg-red-100`/`bg-blue-100`/`bg-purple-100` + `-800` inks) in
StoryCard/StoryDesktop/StoryMobile — accidental, must move to semantic tokens; TagChip/TagSelector
five-hue sets — documented-deliberate, slated to become Pokémon type colors; `focus:ring-sky-500`
(ResultsFilterPanel, Search*) — stragglers; `hover:bg-sky-700` ×3 — stragglers;
UserStoryInteractionButton `bg-gray-200`/`fill-gray-500` — documented WU7 pattern (Control role,
keep); DevLoginBar — dev-only, exempt.

**Dead classes:** `border-muted` (VouchButton) — references nonexistent `--color-muted`; last
one in the codebase after the 2026-07-10 sweeps.

**Browser-confirmed role violations (2026-07-10 pass):** chapter prose + Quill editors bare on
canvas; story long description + profile bio bare on canvas; comment/rec/vouch bodies on beige
Containers; tags directory chips floating on canvas; bookshelf tab bar transparent on canvas;
cover-art placeholder using `surface-hover` as ground; primary buttons white-on-leaf-green ~2:1;
Profile broken badge image (missing onerror fallback).

---

# Per-component inventory

(Sections below produced by the 2026-07-10 cluster audit; ratify or adjust role assignments,
especially ⚖️-marked rows.)

> **Assembler note on "bare spelling" flags:** single-word bare token utilities (`bg-surface`,
> `bg-primary`, `text-text`, `text-danger`, `text-accent`, and — since `--color-surface-hover`
> was declared — `bg-surface-hover`) DO compile and render; they are a *style-consistency*
> issue (two spellings for one token), not dead classes. The only genuinely dead class remaining
> is `border-muted` (VouchButton). `border-surface-hover` (ResultsFilterPanel) compiles but is
> semantically wrong (hover token as a border color).

## Cluster: Stories / Chapters / RichText / Drafts / Recommendations

#### PairingBuilder.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Existing-pairing row | `rounded-md border border-(--color-border) bg-(--color-surface)` text-(--color-text) | Container | — |
| Pairing meta (type · priority) | `text-(--color-text-muted)` | Indicator | — |
| Remove button | `text-(--color-danger) hover:underline` | Control | Destructive control as bare text link, no button treatment |
| Add-pairing panel | `rounded-md border border-(--color-border) bg-(--color-surface)` | Container | — |
| "Add Pairing" heading | `text-sm font-medium text-(--color-text)` | Wayfinding | — |
| Member toggle chips | selected: `border-(--color-primary) bg-(--color-primary) text-white`; unselected: `border-(--color-border) text-(--color-text) hover:border-(--color-primary)` | Control | — |
| Radio labels / Priority label | `text-(--color-text)` / `text-(--color-text-muted)` | Control | Native radios unstyled (no token treatment) |
| Priority select | `rounded border border-(--color-border) bg-(--color-surface)` | Control | Surface-on-surface ground; no focus ring |
| Add Pairing button | `bg-(--color-primary) text-white hover:bg-(--color-primary-strong) disabled:opacity-40` | Control | disabled:opacity-40 diverges from the opacity-50 used elsewhere |
| "Add at least 2 characters" hint | `text-xs text-(--color-text-muted)` | Wayfinding | — |

#### SettingEntry.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Wrapper card | `rounded-md border border-(--color-border) bg-(--color-surface)` | Container | — |
| "Custom Details" label | `text-sm font-medium text-(--color-text)` | Wayfinding | — |
| Name input / description textarea | token input recipe with `focus:ring-2 focus:ring-(--color-primary)` | Control | Input ground identical to enclosing container ground (surface-on-surface; border is the only separation) |

#### CharacterEntry.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Wrapper card | `rounded-md border border-(--color-border) bg-(--color-surface)` | Container | — |
| Tag name (+ sprite) | `font-medium text-(--color-text)` | Container | — (user identifier, correctly not Content Surface) |
| Priority label | `text-xs text-(--color-text-muted)` | Wayfinding | — |
| Priority select | `rounded border border-(--color-border) bg-(--color-surface)` | Control | Surface-on-surface; no focus ring (sibling inputs have one) |
| Remove button | `text-(--color-danger) hover:underline` | Control | Bare destructive text link |
| OC checkbox | `rounded border-(--color-border)` | Control | Native checkbox, minimal treatment |
| OC name input / bio textarea | `border-(--color-border) bg-(--color-surface) focus:ring-2 focus:ring-(--color-primary)` | Control | Surface-on-surface |

#### StoryPage.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Loading text | `text-(--color-text-muted)` + em | Wayfinding | — |
| Not-found text | `text-(--color-danger)` | Wayfinding | Bare text, no empty-state treatment |

(All visuals delegated to StoryDesktop/StoryMobile.)

#### StoryPropertiesForm.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Field labels | `text-sm font-semibold text-(--color-text)` | Wayfinding | — |
| Text inputs / textarea / selects | token input recipe with focus ring | Control | — |
| Validation messages | `text-sm text-(--color-danger)` | Indicator | Bare danger text, no alert vessel |
| Cover art preview img | `rounded-md shadow-sm` | Container | `shadow-sm` raw Tailwind shadow, not token |
| File-input faux button | `file:bg-(--color-primary) file:text-white hover:file:bg-(--color-primary-strong)` | Control | — |
| Cover hint text | `text-xs text-(--color-text-muted)` | Wayfinding | — |
| Long description editor | (delegated to EditorView) | Content Surface | No vessel around the editor block |
| Genre priority row | `rounded-md border border-(--color-border) bg-(--color-surface)` | Container | — |
| Genre priority select | `rounded border border-(--color-border) bg-(--color-surface)` | Control | Surface-on-surface; no focus ring |
| Submit button | primary token recipe, `disabled:opacity-50` | Control | — |

#### StoryEditorPage.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Loading text | `text-(--color-text-muted)` + em | Wayfinding | — |
| Forbidden text | `text-(--color-danger)` | Wayfinding | Bare text, no treatment |
| Page h1 | `text-2xl font-bold text-(--color-text)` | Wayfinding | — |

#### StoryCard.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Card root | `rounded-xl bg-surface shadow-medium` | Container | — (shadow-medium token; bare `bg-surface` spelling) |
| Cover fallback tile | `bg-surface-hover` text-(--color-text-muted) | Container | Container ground using surface-hover (now earth brown) |
| Title link | `font-bold text-text hover:underline` | Container | — |
| Caret button | `text-(--color-text-muted) hover:text-text` | Control | No icon-square treatment |
| Caret dropdown panel | `absolute z-10 rounded-md bg-surface shadow-medium` | Overlay | z-10; no border — same ground as card beneath, shadow is only separation |
| Dropdown menu items | `hover:bg-surface-hover` | Control | — |
| Author byline | `text-(--color-text-muted) hover:underline` | Container | — |
| Status badge (computed `StatusBadgeClass`) | `bg-blue-100 text-blue-800` / `bg-green-100 text-green-800` / `bg-yellow-100 text-yellow-800` / `bg-red-100 text-red-800` / `bg-purple-100 text-purple-800` / fallback `bg-surface-hover text-(--color-text-muted)` | Indicator | Raw Tailwind palette; fallback badge on surface-hover |
| Rating badge (computed `RatingBadgeClass`) | `bg-green-100/yellow-100/red-100 text-*-800`, fallback `bg-surface-hover` | Indicator | Raw Tailwind palette |
| Word count | `text-xs text-(--color-text-muted)` | Indicator | — |
| Short description | `text-sm text-(--color-text-muted)` | Container | — (user tagline, correctly not Content Surface) |
| Tag chips / interaction panel | (delegated) | Indicator / Control | — |

#### StoryDeck.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Loading / empty state | `text-(--color-text-muted)` | Wayfinding | — |

#### StoryDesktop.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Title h1 | `text-3xl font-bold text-(--color-text)` | Wayfinding | — |
| Edit Story link | `text-(--color-primary) hover:underline` | Control | — |
| Author link + dates row | `text-(--color-text-muted)`; link `text-(--color-primary) hover:underline` | Wayfinding | — |
| Status/rating badges (computed, same tables as StoryCard) | `bg-blue-100 text-blue-800` etc.; fallback `bg-surface-hover text-(--color-text-muted)` | Indicator | Raw Tailwind palette (switch tables duplicated across StoryCard/Desktop/Mobile) |
| Word count | `text-xs text-(--color-text-muted)` | Indicator | — |
| OC names | `text-(--color-text-muted)` / `font-medium text-(--color-text)` | Container | — |
| Pairing ("ship") pills | `rounded-full bg-(--color-surface-hover) text-(--color-text)` | Indicator | Chip ground borrowed from surface-hover (hover token as static ground) |
| Cover fallback tile | `rounded-xl bg-(--color-surface-hover)` | Container | Container ground using surface-hover |
| Long description | `prose prose-sm text-(--color-text)` wrapping RichTextView | Content Surface | UGC prose with no vessel — sits directly on canvas |
| "Chapters" h2 | `text-lg font-semibold text-(--color-text)` | Wayfinding | — |
| Chapter list wrapper | `rounded-xl border border-(--color-border) bg-(--color-surface)` | Container | — |

#### StoryMobile.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| (Same element set as StoryDesktop; smaller type scale) | identical class recipes incl. raw-palette badge tables, `bg-(--color-surface-hover)` cover fallback + ship pills, unvesseled `prose` RichTextView | as StoryDesktop | Same mismatches as StoryDesktop |

#### StoryViewStats.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Revealed view count | `text-sm text-(--color-text-muted)` | Indicator | — |
| "View stats" reveal button | `hover:bg-surface-hover` | Control | — (menu-item recipe, consistent with host dropdown) |

#### ChapterList.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Chapter row link | `border-b border-(--color-border) last:border-b-0`; `hover:bg-(--color-surface-hover)` | Container | — |
| Chapter number | `text-(--color-text-muted) tabular-nums` | Wayfinding | — |
| Chapter title | `text-(--color-text)` | Container | — |
| Draft marker | `bg-(--color-surface) ring-1 ring-(--color-border) text-(--color-text-muted)` | Indicator | Badge ground = surface (invisible on the surface container it sits in; ring is only separation) |
| Word count | `text-xs text-(--color-text-muted) tabular-nums` | Indicator | — |
| Alt-version sub-row | `pl-10 hover:bg-(--color-surface-hover) border-t border-(--color-border)/50` text-(--color-text-muted) | Container | `border-(--color-border)/50` ad-hoc opacity variant |
| "No chapters yet." | `text-sm text-(--color-text-muted)` | Wayfinding | — |

#### ChapterNavigation.razor
| Element | Current classes (incl. @code helpers) | Role | Mismatch |
|---|---|---|---|
| Prev/Next link (`NavLinkClasses(false)`) | `bg-(--color-surface-raised) border-(--color-border) text-(--color-text) hover:bg-(--color-primary)/20` | Control | — |
| Prev/Next disabled | `bg-(--color-surface-raised)/50 border-(--color-border) text-(--color-text-muted) cursor-not-allowed` | Control | /50 opacity ground variant for disabled |
| Chapter/version summary triggers | `border border-(--color-border) bg-(--color-surface-raised) hover:bg-(--color-primary)/20` | Control | — |
| Dropdown panels (×2) | `absolute z-10 border border-(--color-border) bg-(--color-surface) shadow-md` | Overlay | `shadow-md` raw shadow vs token; z-10 |
| TOC entry (`TocEntryClasses`) | current: `bg-(--color-primary)/10 font-semibold text-(--color-primary)`; published: `text-(--color-text) hover:bg-(--color-surface-hover)`; unpublished: `pointer-events-none text-(--color-text-muted)` | Control | — |
| Alt-version indicator (⋮) | `text-xs text-(--color-text-muted)` | Indicator | — |
| Version entry (`VersionEntryClasses`) | same current/idle recipe as TOC entries | Control | — |

#### ChapterReadingPage.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Loading / not-found / gated messages | `text-(--color-text-muted)` | Wayfinding | — |
| Chapter title h1 + "Chapter N" meta | `text-xl font-bold text-(--color-text)` / `text-sm text-(--color-text-muted)` | Wayfinding | — |
| Exceeds-rating heads-up | `rounded-lg border border-(--color-border) bg-(--color-surface-raised) text-(--color-text-muted)` | Indicator | — (tokened alert) |
| Top/bottom author's-note asides | `rounded-lg border border-(--color-border) bg-(--color-surface-raised)` wrapping RichTextView | Content Surface | — (vesseled; the ONLY vesseled RichTextView in these clusters) |
| Chapter body | bare article wrapping RichTextView | Content Surface | UGC prose with no vessel — directly on canvas; inconsistent with the vesseled author's notes above/below it |
| Links (skip/edit/read-default) | `text-(--color-primary) hover:underline` | Control | — |

#### ChapterPropertiesForm.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Field labels (+ optional suffixes) | `text-sm font-semibold text-(--color-text)` / `font-normal text-(--color-text-muted)` | Wayfinding | — |
| InputText / InputSelect | token input recipe with focus ring | Control | — |
| EditorView instances (×3) | (delegated) | Content Surface | No vessel around editor blocks |
| Invariant helper texts | `text-sm`/`text-xs text-(--color-text-muted)` | Wayfinding | — |
| Submit button | primary token recipe | Control | — |

#### ChapterEditorPage.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Page h1 / loading / forbidden | `text-(--color-text)` bold / muted / `text-(--color-danger)` | Wayfinding | Forbidden = bare danger text |
| Back-to-reading link | `text-(--color-primary) hover:underline` | Control | — |
| Version switcher panel | `rounded-lg border border-(--color-border) bg-(--color-surface-raised)` | Container | — |
| "Versions" h2 | `text-sm font-semibold text-(--color-text)` | Wayfinding | — |
| Version pill — current | `bg-(--color-primary)/10 font-semibold text-(--color-primary) ring-1 ring-(--color-primary)` | Control | — |
| Version pill — other | `border border-(--color-border) text-(--color-text) hover:bg-(--color-surface-hover)` | Control | No ground (transparent) on a raised panel; border-only |
| "(default)" suffix | `text-xs text-(--color-text-muted)` | Indicator | — |
| Set-as-default / Publish / Unpublish / Add-alternate | text links: primary / danger / muted with `hover:underline` | Control | Write actions (publish/unpublish) as bare text links |
| Publish status text | `text-sm text-(--color-text-muted)` | Indicator | Status with no badge treatment (contrast: ChapterList's Draft marker is a badge) |
| Section divider | `border-t border-(--color-border)` | Container | — |

#### EditorView.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Quill toolbar + editor (ql-*) | third-party classes; inline font style only | Content Surface | Editor surface has no site-owned ground/border treatment (relies on Quill defaults) |
| Preview button | `bg-primary text-white hover:bg-primary-strong` | Control | Bare spelling |
| Preview backdrop | `fixed inset-0 z-50 bg-black/50` | Overlay | z-50; raw `bg-black/50` backdrop |
| Preview modal panel | `rounded-xl bg-surface shadow-lg` | Overlay | `shadow-lg` raw shadow vs token |
| Close button | `bg-primary text-white hover:bg-primary-strong` | Control | Bare spelling |

#### RichTextView.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Prose wrapper | no classes; inline style only (typography from ReaderDisplaySettings) | Content Surface | Carries no ground of its own — vessel responsibility falls to every caller, applied inconsistently (see StoryDesktop, RecommendationCard, ChapterReadingPage) |

#### DraftAutosave.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Restore banner | `rounded-md border border-(--color-warning)/40 bg-(--color-warning)/10 text-(--color-text)` | Indicator | — (tokened alert recipe) |
| Restore button | `font-semibold text-(--color-primary) underline hover:no-underline` | Control | Inverted underline recipe unique to this component |
| Discard button | `text-(--color-text-muted) underline hover:no-underline` | Control | Same |

#### RecommendationCard.razor
| Element | Current classes (incl. computed) | Role | Mismatch |
|---|---|---|---|
| Card root — plain | `rounded-xl border border-(--color-border) bg-(--color-surface)` | Container | — |
| Card root — spotlighted | `border-2 border-[#5BB85A] shadow-[0_0_0_2px_rgba(91,184,90,0.15)] bg-(--color-surface)` | Container | Raw hex border + arbitrary-value glow; `CardShadowClass` is dead code (both branches return the same ground) |
| "Author's Pick" ribbon | `text-[#5BB85A]`, svg fill #5BB85A | Indicator | Raw hex |
| Hidden Gem badge | svg fill #1FA37A | Indicator | Raw hex |
| "[deleted user]" | `text-(--color-text-muted)` | Wayfinding | — |
| Recommendation body | RichTextView, no wrapper classes | Content Surface | UGC prose directly on the beige card, no content ground |
| Like button | `text-[#5BB85A]` / `hover:text-[#5BB85A]`, svg #5BB85A | Control | Raw hex incl. hover |
| Helpful count / date | `text-(--color-text-muted)` | Indicator / Wayfinding | — |
| Edit / Delete buttons | `hover:text-(--color-text)` / `hover:text-(--color-danger)` | Control | Bare text actions |
| Hidden-Gem / Spotlight toggles | `text-[#1FA37A]` / `text-[#5BB85A]` + hex hovers | Control | Raw hex |

#### RecommendationEditor.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| EditorView | (delegated) | Content Surface | — |
| Char meter — met | `text-[#5BB85A]` | Indicator | Raw hex accent |
| Char meter — unmet | `text-(--color-text-muted)` | Indicator | — |
| Save button | primary token recipe | Control | — |
| Cancel button | `border-(--color-border) bg-(--color-surface) hover:bg-(--color-primary)/20` | Control | Neutral button hovers with primary/20 (competing neutral-hover recipe) |

#### RecommendationHelpfulPrompt.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Banner root | `rounded-xl border border-[#5BB85A] bg-(--color-surface) shadow-sm` | Indicator | Raw hex border; raw shadow |
| Rec icon | svg fill #5BB85A | Indicator | Raw hex |
| Prompt text | `text-sm text-(--color-text)` | Wayfinding | — |
| "Yes" button | `bg-[#5BB85A] text-white hover:bg-[#4aa349]` | Control | Raw hex ground + hand-picked hex hover |
| "No thanks" button | `border-(--color-border) bg-(--color-surface) text-(--color-text-muted) hover:bg-(--color-surface-hover)` | Control | — |
| Dismiss (X) | `text-(--color-text-muted) hover:text-(--color-text)` | Control | — |

#### RecommendationSection.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Section header h2 + icon | `text-(--color-text)`; svg fill #5BB85A | Wayfinding | Raw hex icon |
| Count "(N)" | `text-(--color-text-muted)` | Indicator | — |
| Loading / empty state | `text-sm text-(--color-text-muted)` | Wayfinding | — |
| "Recommend this story" button | `border border-[#5BB85A] text-[#5BB85A] hover:bg-[#5BB85A]/10` | Control | Raw hex outline button incl. hex/10 hover |
| "Write a recommendation" h3 | `text-sm font-semibold text-(--color-text)` | Wayfinding | — |
| Error text | `text-sm text-(--color-danger)` | Indicator | Bare danger text, no alert vessel |

### Cluster notes (Stories/Chapters/RichText/Drafts/Recommendations)
- **Raw palette:** status/rating badge switch tables (`bg-blue-100 text-blue-800` etc.) **triplicated** in StoryCard/StoryDesktop/StoryMobile @code; `bg-black/50` (EditorView preview backdrop); Recommendation green family all arbitrary hex (#5BB85A, #4aa349, #1FA37A — grounds, borders, text, svg fills, hovers) across 4 components, only RecommendationEditor's icon uses the `RecommendationIcons` constant; spotlight glow `shadow-[0_0_0_2px_rgba(91,184,90,0.15)]`.
- **Shadows:** token `shadow-medium` (StoryCard + its dropdown); raw `shadow-sm` (cover img, HelpfulPrompt), `shadow-md` (ChapterNavigation dropdowns ×2), `shadow-lg` (EditorView preview modal), arbitrary glow (RecommendationCard).
- **z:** z-10 (StoryCard dropdown; ChapterNavigation dropdowns), z-50 (EditorView preview overlay).
- **Competing neutral-hover recipes within cluster:** `hover:bg-(--color-surface-hover)` (rows/menus) vs `hover:bg-(--color-primary)/20` (ChapterNavigation controls, editor cancel buttons).
- **Dead/suspect:** `RecommendationCard.CardShadowClass` (inert conditional); `StoryCard.DefaultCoverArtFallback` const never referenced (fallback renders letter tile instead); one-off opacity variants `border-(--color-border)/50`, `bg-(--color-surface-raised)/50`.
- **RichTextView vessel inconsistency:** author's notes vesseled (raised+border), chapter body bare on canvas, story description bare on canvas, rec bodies on beige card — three grounds for one role.

## Cluster: Comments / Messaging / Notifications / Following / Users / Profiles

#### CommentEditor.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| EditorView wrapper | (none — bare flex column) | Content Surface (authoring) | Editor sits bare in whatever parent ground hosts it; no content-surface ground of its own |
| Spoiler checkbox + label | `text-(--color-text)`, `border-(--color-border)`, `accent-(--color-primary)` | Control | — |
| Primary Save button | primary token recipe, `disabled:opacity-50` | Control | — |
| Cancel button | `border-(--color-border) bg-(--color-surface) hover:bg-(--color-primary)/20` | Control | Neutral hover uses primary tint — competing neutral-hover recipe |

#### CommentItem.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Comment card | `rounded-lg bg-(--color-surface)` | Container | — |
| Author link / deleted-user / timestamp | `text-(--color-text) hover:underline`; `text-(--color-text-muted)` | Container text (identifiers) | — |
| Comment body (RichTextView) | (none — inherits card) | Content Surface | UGC prose directly on the beige card, no content-surface ground |
| Spoiler-blur reveal button | `bg-(--color-surface) shadow-md` | Control | `shadow-md` raw shadow, not token |
| Like toggle | `text-(--color-accent)` / muted `hover:text-(--color-accent)` | Control | — |
| Reply / Edit buttons | `text-(--color-text-muted) hover:text-(--color-text)` | Control | — |
| Delete button | muted `hover:text-(--color-danger)` | Control (destructive) | — |
| Report button | muted `hover:text-danger` | Control (destructive) | Bare spelling (valid), inconsistent with paren form on Delete in same file |

#### CommentSection.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Loading / empty text | `text-(--color-text-muted) text-sm` | Wayfinding | — |
| "Leave a comment" heading | `text-sm font-semibold text-(--color-text)` | Wayfinding | — |
| Error slot (InlineAlert) | (delegates) | Indicator | — |

#### MessageComposer.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Send button | primary token recipe | Control | — |
| Cancel button | `border-(--color-border) bg-(--color-surface) hover:bg-(--color-primary)/20` | Control | Primary-tint hover on neutral button |
| EditorView | (none) | Content Surface (authoring) | No vessel |

#### MessagesDesktop.razor / MessagesMobile.razor / MessagesPage.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Sidebar/header rules | `border-r/b border-(--color-border)` | Container | Sidebar pane transparent over canvas (no ground) |
| "Messages" h1 / empty text | `text-(--color-text)` / muted | Wayfinding | — |
| "New" button | primary token recipe | Control | — |
| MessagesPage loading text | bare p+em | Wayfinding | Unstyled (no muted token, unlike siblings) |

#### MessagesNavLink.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Nav link | `text-(--color-text) hover:bg-(--color-surface-raised) hover:text-(--color-primary)` | Control (top-bar chrome) | — |
| Unread badge | `bg-(--color-primary) text-white rounded-full font-bold` | Indicator | Primary Control color as indicator, not a distinct token |

#### MessageThread.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| "Select a conversation" placeholder | muted | Wayfinding | — |
| Thread header | `border-b border-(--color-border)`, token inks | Container | — |
| "Load older" button | `text-(--color-primary) hover:underline disabled:opacity-50` | Control | — |
| Reply error text | `text-red-600` | Indicator | Raw Tailwind palette instead of danger token; not InlineAlert |
| Composer strip | `border-t border-(--color-border)` | Container | — |

#### ConversationListItem.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Row card (link) | `rounded-xl border border-(--color-border) bg-(--color-surface) hover:bg-(--color-primary)/10`; selected: `border-(--color-primary) bg-(--color-primary)/10` | Container | — |
| Username / subject / timestamp / preview | token inks | Container text | — |
| Unread count badge | `bg-(--color-primary) text-white rounded-full` | Indicator | Primary doubling as indicator |
| Archived chip | `bg-(--color-text-muted)/20 text-(--color-text-muted)` | Indicator | Chip ground derived from a text token opacity, not a status token |

#### ComposeConversationModal.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Backdrop | `fixed inset-0 z-50 bg-black/50` | Overlay | z-50; raw scrim; backdrop click only — no Escape |
| Modal panel | `rounded-xl bg-(--color-surface) shadow-xl` | Overlay | `shadow-xl` raw shadow |
| Header / labels | `border-b border-(--color-border)` | Wayfinding | — |
| Recipient preset chip | `border-(--color-border) bg-(--color-surface-raised)` | Container | — |
| Text inputs | `border-(--color-border) bg-(--color-surface) focus:border-(--color-primary) focus:outline-none` | Control | Focus = border-swap only, NO ring (other forms use focus:ring-2) — competing focus recipe |
| Error text | `text-red-600` | Indicator | Raw palette instead of danger token / InlineAlert |

#### MessageItem.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Own-message bubble | `rounded-xl rounded-tr-sm bg-(--color-primary) text-white` | Content Surface | UGC prose (RichTextView) on primary Control color; RichTextView inner colors may fight text-white |
| Other-message bubble | `rounded-xl rounded-tl-sm bg-(--color-surface-raised) border-(--color-border)` | Content Surface | UGC prose on raised Container ground rather than content-surface ground |
| Sender name / timestamp | `text-xs text-(--color-text-muted)` | Container text | — |

#### NotificationBell.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Bell button | `rounded-full hover:bg-(--color-surface-raised)` | Control | — |
| Unread badge | `bg-(--color-primary) text-white rounded-full` | Indicator | Primary as indicator |
| Flyout panel | `absolute top-full z-10 rounded-xl border border-(--color-border) bg-(--color-surface) shadow-medium` | Overlay | z-10 (UserMenu is z-30 for same pattern); NO outside-click or Escape close |
| Panel header / "Mark all read" / "See all" | token inks, `hover:underline` | Wayfinding / Control | — |

#### NotificationItem.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Row button | `rounded-xl hover:bg-(--color-surface-hover)`; unread: `bg-(--color-surface-raised)` | Container + Control | Unread state uses raised Container ground as an Indicator signal |
| Unread dot / category icon | inline `style` background / svg fill from AccentColor | Indicator | Site-computed color via inline style — entirely outside token system |
| Message + timestamp | token inks | Container text | — |

#### NotificationSettingsPage.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| H1 + intro / loading | token inks | Wayfinding | — |
| Category header (icon + h2) | svg fill + `style="color:@AccentColor"` | Wayfinding | Inline category accent on a Wayfinding heading — Indicator bleeding into Wayfinding |
| Settings table panel | `rounded-xl border border-(--color-border) divide-y` | Container | Border-only — no surface ground |
| Checkbox toggles | `accent-(--color-primary)` | Control | — |

#### NotificationsPage.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| H1 / loading / empty state | token inks | Wayfinding | — |
| View & sort segmented toggles | active `bg-(--color-primary) text-white`; inactive `bg-(--color-surface-raised) hover:bg-(--color-primary)/10` | Control | — |
| "Mark all read" button | `border-(--color-border) bg-(--color-surface-raised) hover:bg-(--color-primary)/10` | Control | — |
| Category details group | `rounded-xl border border-(--color-border)`; summary `hover:bg-(--color-surface-hover)`; inline accent color | Container | Border-only group (no ground); inline accent; native details — no coordinated open/close |
| Group count | muted | Indicator | — |

#### Settings forms (Appearance / Author / Privacy / Reader / Badge)
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Section heading + blurb | token inks | Wayfinding | — |
| Form panel | `rounded-xl border border-(--color-border) bg-(--color-surface-raised)` | Container | — |
| Inputs/selects/checkboxes | token input recipe with `focus:ring-2 focus:ring-(--color-primary)`; `accent-(--color-primary)` | Control | — |
| Save buttons | primary token recipe | Control | — |
| Badge rows (BadgeSettingsForm) | hidden rows `opacity-50` + `grayscale`; move buttons `hover:bg-(--color-surface-hover)` `disabled:opacity-30` | Container / Control | disabled:opacity-30 diverges from opacity-50 standard |

#### SettingsPage.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| H1 / loading | token inks | Wayfinding | — |
| Load-failure text | `text-(--color-danger)` | Indicator | Bare text, not InlineAlert |
| Success / error banners | `rounded-lg bg-(--color-success)/10 text-(--color-success)` (danger variant same shape) | Indicator | — |

#### ProfileBanner.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Banner block | (ground supplied by parent) | Container | — |
| Avatar / placeholder | `rounded-full shadow-sm`; placeholder `bg-(--color-surface-raised)` | Container | `shadow-sm` raw shadow |
| Username h1 / tagline | token inks | Wayfinding / Container text | — (tagline = identifier, correctly not Content Surface) |
| Badge icon row | (images) | Indicator | — |
| "Edit Profile" | `border-(--color-border) hover:bg-(--color-surface-hover)` | Control | — |
| Vouches details summary | `hover:text-(--color-primary)` | Control (disclosure) | — |

#### ProfilePage.razor / ProfileSettingsForm.razor / UserStatsBlock.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Loading / not-found | muted | Wayfinding | — |
| Bio EditorView (ProfileSettingsForm) | (none) | Content Surface (authoring) | Editor directly on raised Container panel, no content-surface ground |
| Stat strip (UserStatsBlock) | muted labels, strong counts | Indicator | — |

#### ProfileMobile.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Banner wrapper | `border-b border-(--color-border) bg-(--color-surface)` | Container | — |
| Tab details trigger | `rounded-md border border-(--color-border) bg-(--color-surface-raised) hover:bg-(--color-surface-hover)` | Control | — |
| Tab dropdown panel | `absolute top-full z-20 rounded-md border border-(--color-border) bg-(--color-surface) shadow-md` | Overlay | z-20; raw shadow-md; native details — no outside-click/Escape, stays open after tab navigation |
| Dropdown entries | active `bg-(--color-surface-hover) font-semibold`; else `hover:bg-(--color-surface-hover)` | Control | — |
| Section headers (About/Comments) | `text-sm font-bold uppercase text-(--color-text-muted)` | Wayfinding | — |
| Bio (RichTextView) | (none) | Content Surface | UGC prose bare on canvas |
| Filter overlay backdrop + drawer | `fixed inset-0 z-50 bg-black/50`; drawer `bg-(--color-surface) shadow-xl` | Overlay | z-50; raw scrim + shadow-xl; backdrop click closes, no Escape |
| Drawer "Filters" h2 | `text-sm font-bold` (no color token) | Wayfinding | No ink token (inherits) |

#### ProfileDesktop.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Banner wrapper | `border-b border-(--color-border) bg-(--color-surface)` | Container | — |
| Tab bar | nav `border-b border-(--color-border) bg-(--color-surface-raised)`; active `border-b-2 border-(--color-primary) text-(--color-primary)`; inactive muted `hover:border-(--color-border) hover:text-(--color-text)` | Wayfinding (tab bar) | — (grounded tab bar — contrast with Bookshelves' transparent one) |
| Section headers | `text-sm font-bold uppercase text-(--color-text-muted)` | Wayfinding | — |
| Bio (RichTextView) | (none) | Content Surface | UGC prose bare on canvas |
| Filter sidebar | `rounded-lg border border-(--color-border) bg-(--color-surface-raised)` | Container | — |

#### FollowButton.razor
| Element | Current classes (computed) | Role | Mismatch |
|---|---|---|---|
| Follow button (following) | `bg-primary text-white hover:bg-primary/80 disabled:opacity-50` | Control | Bare spelling (valid); hover via /80 opacity instead of primary-strong — competing primary-hover recipe |
| Follow button (not following) | `border-primary text-primary hover:bg-primary/10` | Control | Bare spelling |
| Alert-bell toggle | on: `text-accent hover:text-accent/70`; off: muted `hover:text-text` | Control | Bare spellings (valid); accent for a Control state |

#### VouchButton.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Vouched / vouch buttons | `border-accent text-accent hover:bg-accent/10 disabled:opacity-50` | Control | Bare spellings (valid); accent-outline button unique recipe |
| At-limit disabled button | `border-muted text-(--color-text-muted) cursor-not-allowed opacity-50` | Control | **`border-muted` DEAD** — no `--color-muted` token exists (last dead class in codebase) |
| Note EditorView (in ConfirmDialog) | (none) | Content Surface (authoring) | No vessel |

#### VouchList.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Vouch row card | `rounded-xl bg-surface shadow-sm` | Container | Bare spelling; `shadow-sm` raw shadow |
| Remove button | `text-danger hover:underline` | Control (destructive) | Bare spelling (valid) |
| Vouch note (RichTextView) | `text-sm` wrapper only | Content Surface | UGC prose directly on card ground, no content-surface treatment |

#### UserCard.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Card root | `rounded-xl bg-surface` | Container | Bare spelling |
| Username / tagline | `font-bold text-text hover:underline`; muted | Container text | — |
| Badge icons | (images) | Indicator | — (browser pass found one broken img — missing onerror fallback) |
| Caret menu button | muted `hover:text-text` | Control | — |
| Dropdown menu panel | `absolute top-full z-10 rounded-md bg-surface shadow-medium` | Overlay | z-10; no border; NO outside-click/Escape dismissal |
| Menu entries | `hover:bg-surface-hover` | Control | — |

### Cluster notes (Comments/Messaging/Notifications/Following/Users/Profiles)
- **Raw palette:** `text-red-600` (MessageThread + ComposeConversationModal errors); `bg-black/50` scrims.
- **Spelling dialects:** FollowButton, VouchButton, VouchList, UserCard concentrate the bare single-word spellings (`bg-primary`, `text-accent`, `bg-surface`, `text-text`, `text-danger` — all valid, inconsistent with paren form elsewhere). `border-muted` (VouchButton) is the one genuinely dead class.
- **Shadows:** raw `shadow-sm` (VouchList, ProfileBanner, ProfileSettingsForm), `shadow-md` (CommentItem spoiler button, ProfileMobile tab dropdown), `shadow-xl` (ComposeConversationModal, ProfileMobile drawer); token `shadow-medium` (NotificationBell, UserCard dropdowns).
- **z:** z-10 (NotificationBell, UserCard), z-20 (ProfileMobile tab dropdown), z-50 (modals/drawers).
- **Focus inconsistency:** settings forms use `focus:ring-2 focus:ring-(--color-primary)`; ComposeConversationModal inputs use border-swap with NO ring.
- **Inline-style Indicator colors:** notification dots/icons/headings colored from NotificationPresenter/NotificationCategoryVisuals AccentColor via style attributes — outside the token system.
- **Dismissal mechanics:** NotificationBell + UserCard menus: toggle only, no outside-click/Escape. ProfileMobile tab selector: native details, stays open after navigation. Modals/drawers: backdrop click only, no Escape.
- **UGC ground pattern:** every RichTextView here (comment bodies, message bubbles, vouch notes, bios) renders on Container grounds (surface/raised/primary) or bare canvas — none has a Content Surface treatment.

## Cluster: Tags / Discovery / Bookshelves / Groups / BlogPosts / Home

#### TagChip.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Tag chip | computed per-type: `bg-emerald-100 text-emerald-800` / violet / sky / rose / amber pairs; fallback `bg-surface text-text`; `rounded-full` | Indicator | Raw palette is documented-deliberate (slated for Pokémon type colors); fallback `bg-surface` chip is invisible against surface vessels |
| Remove button (✕) | `leading-none` only | Control | No hover/focus treatment at all on an interactive control |

#### TagDirectoryDesktop.razor / TagDirectoryMobile.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Jump-nav pills | `rounded-full border border-(--color-border) text-text hover:bg-(--color-primary)/10` | Control | Transparent ground — pills float directly on canvas |
| "+ New Tag" button | primary token recipe | Control | — |
| Section headers (h2 + count) | `font-semibold text-text`; count muted | Wayfinding | — |
| Section bodies (chip trees) | none (no ground/border) | Container | Directory sections have NO vessel — chip trees bare on canvas (both breakpoints) |
| Modal backdrop / panel (desktop) | `fixed inset-0 z-50 bg-black/50`; panel `rounded-xl bg-surface shadow-lg` | Overlay | z-50; raw scrim; raw shadow-lg |

#### TagDirectoryPage.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Loading text | muted | Wayfinding | — |
| Sprite warning strip | `rounded-md border border-amber-400 bg-amber-50 text-amber-900` | Indicator | Raw amber palette for a STATUS alert — should be warning token |

#### TagDirectorySection.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Mod edit/delete buttons | `hidden group-hover/row:inline-flex hover:bg-(--color-primary)/10` / `hover:bg-(--color-danger)/10` | Control | Hover-reveal only — invisible to keyboard/touch users |
| Empty state | muted italic | Wayfinding | — |

#### TagEditorForm.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Text inputs / textarea | token input recipe (bare spellings) with focus ring | Control | — |
| Selects | same but NO focus: classes | Control | Selects lack the focus-ring the inputs have |
| Cancel / Save buttons | neutral `hover:bg-(--color-primary)/10`; primary token recipe | Control | — |

#### TagFilter.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| AND/OR radios | `accent-sky-600` | Control | Raw palette accent instead of accent-(--color-primary) |

#### TagSelector.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Typeahead (BlazoredTypeahead) | third-party default styling | Control / Overlay | Unstyled third-party dropdown — flyout ground/z outside token system (documented as leave-for-MVP) |
| Result-row type dots | `bg-emerald-500` / violet / sky / rose / amber; fallback `bg-surface` | Indicator | Deliberate type family; fallback dot invisible |

#### SearchPage.razor / SearchDesktop.razor / SearchMobile.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Filter sidebar (desktop) | `rounded-lg border border-(--color-border) bg-(--color-surface-raised)` | Container | — |
| "Filters" header | `text-sm font-bold text-(--color-text-muted) uppercase` | Wayfinding | — |
| "Give me more" buttons (both) | `bg-sky-600 text-white hover:bg-sky-700 active:bg-sky-800 disabled:opacity-50` | Control | Raw sky palette as a de-facto SECOND primary; only active: usage in codebase |
| Mobile filter overlay | backdrop `fixed inset-0 z-50 bg-black/50`; panel `bg-(--color-surface) shadow-xl` | Overlay | z-50; raw scrim + shadow-xl |
| Filter toggle (mobile) | `border-(--color-border) bg-(--color-surface-raised) hover:bg-(--color-surface-hover)` | Control | — |

#### ResultsFilterPanel.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Search input / sort select | `border-surface-hover bg-surface focus:ring-2 focus:ring-sky-500` | Control | `border-surface-hover` = hover token as border color (semantically wrong); `ring-sky-500` raw palette focus ring — competing focus recipe |
| Apply Filters button | `bg-sky-600 text-white hover:bg-sky-700 active:bg-sky-800` | Control | Raw sky palette primary |
| Panel root | no ground (parents supply vessel) | Container | No ground contract of its own |

#### BookshelvesPage.razor / BookshelvesDesktop.razor / BookshelvesMobile.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Page loading text | bare p+em | Wayfinding | Unstyled |
| Tab bar (desktop) | `border-b border-(--color-border)` only — transparent | Wayfinding | Tab bar transparent on canvas |
| Tab links | inactive muted `hover:bg-(--color-surface-hover)`; active = inline style `background:{hex}22; color:{hex}; border-bottom:2px solid {hex}` + svg fill | Wayfinding (tab) | Active state entirely in inline hex (BookshelfTabVisuals) — outside tokens AND Tailwind |
| Tab dropdown (mobile) | summary `border-(--color-border) bg-(--color-surface-raised) hover:bg-(--color-surface-hover)` + inline accent; flyout `absolute z-20 rounded-md border bg-(--color-surface) shadow-md` | Control / Overlay | z-20; raw shadow-md; inline hex accents |
| Filter sidebar (desktop) | `rounded-lg border border-(--color-border) bg-(--color-surface-raised)` | Container | — |
| Mobile filter overlay | backdrop z-50 `bg-black/50`; panel `bg-(--color-surface) shadow-xl` | Overlay | z-50; raw scrim + shadow |

#### GroupsPage.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Page title / loading / empty | token inks | Wayfinding | — |
| "+ New Group" | primary token recipe | Control | — |

#### GroupCard.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Card (anchor) | `rounded-lg border border-(--color-border) bg-(--color-surface) transition-shadow hover:shadow-sm` | Container | `hover:shadow-sm` raw shadow hover (shared card recipe with BlogPostCard) |
| Group name | `font-semibold text-(--color-text)` | Container text | — |
| Audience badge | `bg-(--color-danger)/10 text-(--color-danger) ring-(--color-danger)/30` / warning variant / neutral raised | Indicator | — (token-based status badge — the CORRECT pattern; contrast StoryCard's raw palette) |
| Meta / description snippet | muted `line-clamp-2` | Container text | — (plain-text snippet, not RichTextView) |

#### GroupPage.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Loading / not-found | token inks | Wayfinding | — |
| Action-error toast | `fixed bottom-4 right-4 rounded-lg bg-(--color-danger)/90 text-white shadow-lg` | Overlay | NO z-index at all (can be occluded); raw shadow-lg; local one-off instead of ToastHost |

#### GroupCreateEditPage.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Error alert | `rounded-lg bg-(--color-danger)/10 text-(--color-danger)` | Indicator | — |
| Inputs | token input recipe with focus ring | Control | — |
| Audience radio cards | selected `border-(--color-primary) bg-(--color-primary)/5`; unselected surface; `hover:border-(--color-primary)/50` | Control | — |
| Submit / Cancel | primary recipe / `border hover:bg-(--color-surface-raised)` | Control | — |
| Form body | no vessel (fields directly on canvas) | Container | Edit form floats bare on canvas (contrast: TagEditorForm gets a modal vessel) |

#### GroupDesktop.razor / GroupMobile.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Group name h1 + meta | token inks, bare on canvas | Wayfinding | Header bare on canvas (page-header pattern) |
| Audience badge | token danger/warning/neutral with ring-1 | Indicator | — |
| Group description | `text-sm text-(--color-text)` bare on canvas | Container text | User-authored plain text (NOT RichTextView) bare on canvas — borderline; no vessel |
| Join / Leave / Edit buttons | primary recipe / neutral `hover:bg-(--color-surface-raised)` | Control | Mobile twins MISSING hover states the desktop versions have (Join, Leave, Add) |
| Add-story panel / folder tree panel | `rounded-lg border border-(--color-border) bg-(--color-surface)` | Container | — |
| Folders collapse toggle (mobile) | bare text button | Control | No hover/focus treatment |
| Section headers / empty states | token inks | Wayfinding | — |

#### GroupBlogPostEditorPage.razor / BlogPostEditorPage.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Titles / loading / forbidden | token inks | Wayfinding | — |
| Form body (BlogPostPropertiesForm) | no vessel | Container | Editor forms bare on canvas |

#### BlogPostCard.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Card | `rounded-lg border border-(--color-border) bg-(--color-surface) hover:shadow-sm` | Container | Raw shadow hover |
| Title link | `text-(--color-text) hover:underline hover:text-(--color-primary)` | Container text | — |
| Draft badge | `rounded-full bg-(--color-warning)/15 text-(--color-warning)` | Indicator | — |
| Meta chips / spoilers | muted; spoilers `text-(--color-warning)` | Indicator | — |
| Content snippet | muted `line-clamp-3` | Container text | — (plain snippet) |
| Report button | muted `hover:text-danger hover:underline` | Control | Bare spelling (valid), inconsistent form |

#### BlogPostPage.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Title h1 / meta | token inks | Wayfinding / Indicator | — |
| Draft badge | `rounded bg-(--color-surface) ring-1 ring-(--color-border) text-(--color-text-muted)` | Indicator | DIFFERENT draft-badge recipe from BlogPostCard (warning tint vs neutral ring) — inconsistent pair |
| Post body | `prose prose-sm text-(--color-text)` wrapping RichTextView, no ground | Content Surface | UGC prose bare on canvas — no vessel |
| Like button / edit link | token recipes | Control | — |

#### BlogPostPropertiesForm.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Inputs / checkboxes | token recipes with focus ring | Control | — |
| EditorView (Quill) | no local classes | Content Surface (authoring) | Delegated to Quill defaults — no vessel |
| Validation | `text-sm text-(--color-danger)` | Indicator | — |

#### HomePage.razor / HomeDesktop.razor / HomeMobile.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Placeholder heading + copy | wrapper `text-(--color-text-muted)`; h1 `font-bold` inherits muted | Wayfinding | Placeholder (future work-unit); heading rendered in muted ink |

### Cluster notes (Tags/Discovery/Bookshelves/Groups/BlogPosts/Home)
- **The sky-600 family is a de-facto second primary** across Discovery (`bg-sky-600 hover:bg-sky-700 active:bg-sky-800` on Give-me-more + Apply Filters; `focus:ring-sky-500`; `accent-sky-600` on TagFilter radios) while Groups/Tags/BlogPosts use `--color-primary` — systemic split predating the token re-pick.
- **Inline hex accents:** BookshelvesDesktop/Mobile active tabs — `{hex}22` alpha-suffix backgrounds, text colors, border-bottoms, svg fills from BookshelfTabVisuals — outside both tokens and Tailwind.
- **Raw palette:** TagChip/TagSelector type families (documented-deliberate); amber sprite-warning strip (accidental); sky family (accidental); `bg-black/50` scrims ×3.
- **Shadows:** all raw in this cluster — `shadow-lg` (tag modal, GroupPage toast), `shadow-xl` (2 slide-in panels), `shadow-md` (bookshelf tab flyout), `hover:shadow-sm` (GroupCard/BlogPostCard). Zero token shadows.
- **z:** z-50 (3 modal/drawer overlays), z-20 (bookshelf tab flyout), GroupPage toast has NO z.
- **Structural:** tag directory sections + chip trees have no Container vessel (both breakpoints); bookshelves desktop tab bar transparent on canvas; blog post body bare on canvas; all three blog/group editor forms bare on canvas while TagEditorForm gets a modal; draft badge two divergent recipes; GroupMobile drops hovers its desktop twin has; GroupCard audience badge is the canonical token-based status-badge recipe the story badges should adopt.

## Cluster: Layout / Errors / Dialogs / Toasts / Pagination / Moderation / UserStoryInteractions / Server chrome / Identity

#### DesktopLayout.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Sticky top bar | `sticky top-0 z-20 border-b border-(--color-border) bg-(--color-surface-raised) shadow-subtle` | Overlay | Token shadow here, raw shadow-lg on modals/toasts — shadow scale inconsistent across Overlay family |
| Logo link | `font-display text-(--color-primary-strong)` | Wayfinding | — |
| Nav links | `text-(--color-text) hover:text-(--color-primary)`; active `font-semibold text-(--color-primary)` | Wayfinding | — |

#### MobileLayout.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Bottom bar | none — bare flex row, literal unstyled spans | Overlay | Placeholder: no ground/border/z, not fixed — bottom bar entirely lacks Overlay treatment |

#### UserMenu.razor / CreateMenu.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| UserMenu trigger | `rounded-md hover:bg-(--color-surface) hover:text-(--color-primary)` | Control | Trigger hovers `surface` while NotificationBell trigger hovers `surface-raised` — inverted pair |
| "Write" trigger (CreateMenu) | `rounded-md bg-(--color-accent) text-(--color-text) hover:bg-(--color-accent)/80` | Control | Only accent-ground button in chrome; dark text on accent while other filled buttons use text-white; hover via /80 opacity instead of a strong shade |
| Dropdown panels (both) | `absolute z-30 rounded-xl border border-(--color-border) bg-(--color-surface-raised) shadow-medium` | Overlay | z-30 vs NotificationBell z-10 for same pattern; toggle-only — NO outside-click/Escape (browser-confirmed overlap bug) |
| Menu items / logout | `hover:bg-(--color-surface)` | Control | Third neutral-hover recipe (vs surface-hover and primary-tint elsewhere) |

#### LoginDisplay.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Username / logout / login link | none | Control | Completely unstyled — bare on whatever hosts it |

#### DevLoginBar.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Dev bar + links | `bg-yellow-50 border-yellow-300 text-yellow-700`; links `text-blue-600 hover:underline` | Container (dev) | Raw palette — deliberate dev-only signal, exempt |

#### CanalaveErrorBoundary.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Compact fallback | `rounded-md border-(--color-danger)/40 bg-(--color-danger)/10 text-(--color-danger)` | Indicator | — (matches InlineAlert recipe) |
| Full fallback panel | same recipe but `rounded-lg` | Indicator | Radius drifts within one component (md vs lg) |
| "Try again" buttons | primary token recipe (full) / underline link (compact) | Control | — |

#### InlineAlert.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Alert box | Danger/Success/Warning: `border-(--color-X)/40 bg-(--color-X)/10 text-(--color-X)`; Info: border + surface-raised | Indicator | — (THE canonical Indicator alert recipe) |

#### ConfirmDialog.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Backdrop | `fixed inset-0 z-50 bg-black/50` | Overlay | Raw scrim; backdrop click cancels; no Escape, no focus trap |
| Panel | `rounded-xl bg-surface p-6 shadow-lg` | Overlay | Raw shadow-lg; borderless + bg-surface while dropdown panels are bordered surface-raised — Overlay panel recipe diverges |
| Cancel button | `border-(--color-border) bg-surface hover:bg-(--color-primary)/20` | Control | Primary-tint neutral hover |
| Confirm button | destructive: `bg-danger text-white` (NO hover); else `bg-primary hover:bg-primary-strong text-white` | Control | Destructive branch has no hover state at all |

#### ToastHost.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Host stack | `pointer-events-none fixed bottom-4 right-4 z-50` | Overlay | Shares z-50 with modals — toast vs modal layering is DOM-order luck |
| Toast | `rounded-lg border shadow-lg` + `border-(--color-X)/40 bg-(--color-surface-raised) text-(--color-X)` | Indicator | Raw shadow-lg; surface-raised ground vs InlineAlert's X/10 tint — two alert recipes for the same levels |

#### PaginationControls.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Prev/Next + page buttons | enabled `bg-(--color-surface-raised) border hover:bg-(--color-primary)/20`; disabled `/50 muted cursor-not-allowed`; current `bg-(--color-primary) text-white` | Control | — (clean token recipe) |

#### ModReportsPage.razor / ModUsersPage.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Table rows | `border-b border-(--color-border) hover:bg-(--color-surface-raised)` | Container | Tables bare on canvas (no panel ground) |
| Status badges | `rounded` + `bg-warning/20 text-warning` / primary / success variants | Indicator | `rounded` (4px) vs rounded-md elsewhere |
| Action buttons | `rounded bg-success/warning/danger text-white hover:opacity-90`; ModUsers Warn/Suspend/Ban have NO hover; `bg-danger/70` ad-hoc mid-danger shade | Control | `rounded` not rounded-md; opacity-hover vs shade-hover split; text-white on light bg-warning (contrast risk); missing hovers |
| Action panels | `rounded-lg border bg-(--color-surface-raised)` | Container | — |
| Report reasons / mod notes | plain text/textarea | Container text | NOT rich text — correctly not Content Surface |

#### ModSubmissionsPage.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Tab bar | active `border-b-2 border-primary text-primary`; inactive muted `hover:text-text hover:border-(--color-border)` | Wayfinding | — (canonical tab-bar recipe candidate) |
| Submission card | `rounded-lg border bg-surface` | Container | Card ground flips surface here vs surface-raised panels on sibling mod pages |
| Approve/Reject buttons | `rounded bg-success/bg-danger text-white hover:opacity-90` | Control | Same mod-button divergences |

#### ReportDialog.razor
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Backdrop / panel | as ConfirmDialog (`z-50 bg-black/50`; `rounded-xl bg-surface shadow-lg`) | Overlay | Same divergences; no Escape |
| Select + textarea | token input recipe (no focus ring) | Control | Report notes are plain textarea — NOT Content Surface |
| Error / success lines | `text-danger` / `text-success` | Indicator | Bare colored text, not InlineAlert |

#### UserStoryInteractionButton.razor / Filter / Panel
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| Icon square | active `bg-[var(--accent)]` (inline per-type accent); inactive `bg-gray-200`, icon `fill-gray-500`, `group-hover:fill-[var(--accent)]` | Control | Documented WU7 pattern (keep), but raw gray + inline accent bypass tokens |
| Read-only span (active-only) | identical classes to active button | Indicator | Indicator visually indistinguishable from a clickable Control |
| Filter checkboxes | `accent-sky-600` | Control | Raw sky accent (second occurrence of the sky family) |
| Filter heading | `text-sm font-bold` no color | Wayfinding | No ink token |
| "Edit Story" link (own-story) | none | Control | Completely unstyled link amid six styled buttons |

#### NotFound.razor / Error.razor (Server)
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| 404 h3 + message | none | Wayfinding | Bare on canvas — the only user-facing 404; zero treatment |
| Error page headings | `text-danger` | Wayfinding | Bare page; template "Development Mode" boilerplate shipped user-facing |

#### App.razor / ReconnectModal.razor / MainLayout.razor (Identity host)
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| #blazor-error-ui bar | `fixed bottom-0 z-50 border-t border-(--color-danger)/40 bg-(--color-surface-raised) shadow-lg` | Overlay + Indicator | Raw shadow-lg; z-50 shared; `float-right` dismiss |
| ReconnectModal | scoped CSS using color tokens with hex fallbacks | Overlay | — (tokenized correctly via scoped CSS) |
| Identity chrome bar | flex only — no ground/border/shadow | Overlay | Zero Overlay treatment (contrast DesktopLayout header) |

#### Identity/Pages (Login, Register, ForgotPassword, 2FA, Manage/*, etc.)
| Element | Current classes | Role | Mismatch |
|---|---|---|---|
| All headings/prose | mostly none | Wayfinding | Bare |
| All grids/forms/buttons/alerts | Bootstrap classnames (`row col-*`, `form-floating form-control`, `btn btn-primary/danger/lg`, `alert alert-*`, `table`, `input-group*`, `glyphicon*`, `w-100`, `font-weight-bold`) | Control / Indicator / Container | ALL DEBRIS — no Bootstrap stylesheet is loaded; every Identity page is functionally unstyled native HTML on canvas. `text-danger`/`text-success` work only by collision with Tailwind token utilities. `text-secondary`, `text-info` dead (no such tokens). Destructive actions (DeletePersonalData submit, PersonalData delete link) render as plain text/links |

### Cluster notes (Layout/Errors/Dialogs/Toasts/Pagination/Moderation/Interactions/Identity)
- **Focus treatment gap:** NO focus:/focus-visible: styles exist anywhere in this cluster — combined with other clusters: focus rings exist ONLY on form text inputs; buttons, menu items, dropdowns, tabs, icon squares have no visible keyboard-focus treatment site-wide.
- **z-scale as found:** z-10 (NotificationBell), z-20 (sticky header, ProfileMobile/Bookshelf tab flyouts), z-30 (UserMenu/CreateMenu), z-50 (modals, toasts, drawers, #blazor-error-ui), none (GroupPage toast). Sibling header dropdowns at 10 vs 30; modals and toasts tied at 50.
- **Overlay panel recipe split:** dropdowns = bordered `surface-raised` `rounded-xl` `shadow-medium` (mostly); modals = borderless `bg-surface` `rounded-xl` `shadow-lg`; NotificationBell = bordered `bg-surface`. Three grounds for one role.
- **Two alert recipes:** InlineAlert (tint ground `bg-X/10`) vs ToastHost (`bg-surface-raised` ground) for identical semantic levels.
- **Mod-button family:** `rounded` radius, `hover:opacity-90` (or no hover), `bg-danger/70` ad-hoc shade, white-on-warning — four divergences from the Control recipes used elsewhere.
- **Dismissal:** all three header dropdowns toggle-only (no outside-click/Escape, no cross-coordination — two can be open at once); modals backdrop-click only (no Escape, no focus trap).
- **Raw palette:** DevLoginBar yellows/blue (exempt); `bg-gray-200`/`fill-gray-500` interaction squares (documented); `accent-sky-600`; `bg-black/50` scrims; `bg-danger/70`.

---

# Synthesis

## Corrections & additions to the global census (from the full audit)

1. **The focus gap is bigger than the census showed.** The 36 token focus rings live exclusively
   on form text inputs. Buttons, menu items, dropdown triggers, tabs, icon squares, and links
   have NO visible keyboard-focus treatment anywhere on the site. The "focus ring = D/P cursor"
   rule is currently implemented for typing, not for navigating.
2. **A third color system exists alongside tokens and raw palette:** the Visuals-constants
   pattern (`InteractionVisuals` --accent inline var — documented WU7; `BookshelfTabVisuals` and
   `NotificationCategoryVisuals` inline styles/svg fills; `RecommendationIcons` hex). Same
   mechanism, four features, zero token participation. ⚖️ Phase A must decide: promote these
   accents into `@theme`, or bless the constants file as the sanctioned home for per-item accent
   colors (and route ALL of them through it — Recommendations currently hardcodes the hex at
   every call site instead of using its own constant).
3. **The sky-600 family is a de-facto second primary** in Discovery (buttons, focus rings, radio
   accents) — predates the token re-pick; Phase B sweep item.
4. **Two alert recipes for the same levels:** InlineAlert (tint ground `bg-X/10`) vs ToastHost
   (`bg-surface-raised` ground). Pick one as the Indicator-alert material.
5. **Dead-class list corrected:** `border-muted` (VouchButton) plus Identity's `text-secondary`
   and `text-info` (Bootstrap names with no matching token). Everything else flagged as "dead"
   is a valid bare spelling.
6. **Identity pages are functionally unstyled.** The "keeps Bootstrap deliberately" carve-out
   assumed Bootstrap rendered; it doesn't (no stylesheet loaded). Every auth page is native
   HTML on canvas; destructive actions (delete account) render as plain links. ⚖️ The carve-out
   deserves re-ratification: it currently preserves nothing.
7. **Missing states inventory:** ConfirmDialog destructive confirm — no hover; ModUsers
   Warn/Suspend/Ban — no hover; GroupMobile Join/Leave/Add — no hover (desktop twins have them);
   TagChip remove ✕ — no hover/focus; TagDirectorySection mod buttons — hover-reveal only
   (invisible to keyboard/touch); mod buttons hover via `opacity-90` vs shade elsewhere.
8. **Structural bare spots beyond the known set:** MobileLayout bottom bar (unstyled placeholder
   spans); NotFound.razor + Error.razor (bare, template boilerplate user-facing); mod tables on
   canvas; GroupPage's local toast (no z-index) duplicating ToastHost.

## Ratification questions (⚖️ for Brian, before Phase A/B)

1. **MessageItem bubbles**: chat idiom (colored bubbles, own = primary) vs Content Surface
   uniformity. Bubbles are the one RichTextView site where the web-chat convention plausibly
   overrides the UGC rule. Keep bubbles (as a sanctioned Content Surface variant) or unify?
2. **Comment decomposition**: Container header + Content Surface body (name-plate + text-box) —
   confirmed as the intended split?
3. **Accent-constants system** (see Synthesis 2): tokens or blessed constants?
4. **Identity carve-out** (see Synthesis 6): re-style now under the role system, or keep
   deferred knowing the pages are unstyled?
5. **Group description / report notes / plain-textarea prose**: stays Container text (current
   assignment — the RichTextView test says no Content Surface), or does "user prose" deserve the
   sheet regardless of editor affordance?
6. **Read-only interaction squares**: currently identical to active buttons; give passive
   Indicator styling or keep the uniform look?

## Sweep checklist (feeds Phases B–D; ordered by user-visible impact)

1. ContentSurface wrapper + the UGC re-role (chapter body, story description, bios, comments,
   recs, vouches, messages ⚖️, blog bodies, all Quill editors).
2. Canvas/ink/border/primary token re-values (Phase A) + primary-contrast resolution.
3. Status/rating badges → semantic tokens (adopt GroupCard's audience-badge recipe; delete the
   triplicated switch tables into one shared mapping).
4. Overlay unification: one panel recipe, z-scale tokens (dropdown < sticky-bar < drawer < modal
   < toast < error-bar), uniform dismissal (outside-click + Escape + exclusive-open), focus trap
   on modals; GroupPage toast → ToastHost.
5. Neutral-hover recipe (pick one of the four) + missing hovers + focus-visible treatment for
   ALL Controls (the biggest a11y gap).
6. Recommendations hex family → tokens/constants; sky-600 family → primary; inline-accent
   features per ⚖️3.
7. Elevation: hybrid rule; retire raw shadow-sm/md/lg/xl (17 sites) + arbitrary glow.
8. Wayfinding: plaque treatment for page titles/section headers; tab bars grounded
   (ProfileDesktop's is the model); bookshelf active-tab inline hex → tokens.
9. Stragglers: border-muted; ResultsFilterPanel border-surface-hover; surface-hover-as-ground
   (cover tiles, ship pills, StoryCard badge fallbacks); TagChip fallback invisibility;
   disabled-opacity variants (30/40/50 → one); radius drift (mod `rounded`, dialog family);
   spelling dialect unification (bare vs paren) per layer4-style.md rule.
10. Bare pages: NotFound/Error/MobileLayout bottom bar (small, high-visibility).

Registry compiled 2026-07-10 from four parallel component audits + session censuses + browser
pass. Component tables above are the per-file ground truth for every sweep item.

## Ratifications received (2026-07-10, post-review)

1. **MessageItem** — Content Surface, NO chat bubbles (current colored bubbles were an unratified
   default; remove). Authorship = side (own right / other left) + avatar + name on each side.
2. **Comment decomposition** — ratified: Container header (site metadata) + Content Surface body;
   applies to recommendations and vouches too.
3. **Identity pages** — restyle NOW under the role system; the Bootstrap carve-out is revoked
   (layer4-style.md updated).
4. **Capped user text** (story short description, group description, taglines, titles) — Container
   text, not Content Surface and not a new role; the length cap co-opts the text into site
   function. Mechanical test documented in layer4-style.md "Element Roles".
5. **Feature accent colors** (bookshelf tabs, notification categories, interaction types,
   recommendation greens) — the COLORS are deliberate and stay; delivery mechanism (tokens vs
   constants) pending determination. Discovery's sky-600 family is NOT deliberate.
6. **Feature accents → @theme tokens** — Visuals classes keep enum→name mapping, carry var()
   references; raw hex becomes a defect post-sweep. Discovery sky-600 → primary (exorcise).
7. **Tag-type palette → Pokémon type colors** (5 new tokens; type→TagType mapping is Brian's
   Phase A pick). Supersedes the pastel carve-out.
8. **Read-only interaction squares → distinct passive look** (tinted ground + accent icon, no
   pointer cursor).
9. **Content Surface material → framed dialog box** (near-white + border + restrained frame
   accent; frame slot = future Theme/ReaderSettings hook).
10. **Control families renamed** — `--color-action`/`-hover`/`-ink` (everyday green; light fills
    + dark ink; `-ink` = links/active-text/focus ring — links stay green) and `--color-mission`/
    `-hover` (surf blue: creation, tree search, random search, interaction-history filtering,
    rec/gem/vouch/spotlight ACTIONS). `primary`/`accent` names retire.
11. **Identity vs action rule** — feature accents color Indicators (badges/icons/ribbons/tabs);
    mission blue colors those features' Controls. Both sky-600 accidents land in mission.
12. **Git-history finding (fracture etiology)** — toolchain was Tailwind v4 from first commit
    (2026-06-20; no tailwind.config.js ever); all components authored in v3 bracket idiom
    (987 usages, 0 paren) rendering as NOTHING until the 2026-07-01 browser pass converted
    syntax only. All role-level choices predate visible rendering — this refactor is the
    codebase's first semantics pass, not a migration.

## Sweep completion (2026-07-10, Phases B–F)

The re-role sweeps landed same-day; this registry's mismatch flags are RESOLVED except where
noted. Headlines: ContentSurface (Reading/Inline/Input, side-rails frame) wraps all 17
RichTextView/EditorView sites incl. de-bubbled MessageItem; ReaderDisplayProvider wired in
Routes (the cascade finally has a provider — reader font settings apply for the first time)
plus the Phase E ReadingBackground override (SiteDefault/Light/Sepia/Dark, JSON complex
property, migration ReaderBackgroundOverride); StatusBadges shared mapping replaced the
triplicated raw-palette switch tables; solid type-color tag chips; feature accents are @theme
tokens with *Visuals.cs carrying var() refs (follow = orange); action/mission families replaced
primary/accent everywhere (alias bridge deleted); one neutral hover (surface-hover); global
:focus-visible ring; z-ladder/backdrop/shadow tokens on every overlay; uniform dismissal
(dismiss.js: catchers + data-dropdown); mod buttons on tint recipes; Identity fully restyled
(31 pages + Shared, Bootstrap debris deleted); NotFound/MobileLayout bar/plaques/vessels done;
scripts/check-design-tokens.ps1 enforces all of it in CI. Remaining known-open: Error.razor
(Server template page, low priority); Blazored.Typeahead package chrome (documented MVP
carve-out); visual sign-off of swept pages = the standing L4 human pass.
