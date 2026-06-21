# Audit — UserStoryInteractions/

**Features:** 16 (state writes), 17 (lists & bookshelves).

## Shared Context

**Entities (Core/Models/):** `UserStoryInteraction` (the bit-packed hot table, PK `(UserId,StoryId)`),
`UserStoryInteractionDate` (warm 1-to-1 partition), `UserStoryRecommendationSource` (sparse 1-to-1).
**Fluent config:** composite keys, partition 1-to-1 cascades, and **seven filtered/covering indexes** on
`UserId INCLUDE (StoryId)` filtered by each boolean.

This is the cluster the audit flagged as the most significant case of **code staleness** (a stale-code
trap, not an intent contest — see audit-summary §0).

---

## The reading-status divergence (drives Stage 4 on L1 + L6)

`UserStoryInteraction` currently has these booleans:
`IsInProgress, IsCompleted, IsActivelyReading, IsFavorite, IsHiddenFavorite, IsFollowed, IsReadItLater,
IsIgnored`.

The revised spec model (§4, §5.12; conventions `layer1-data-model.md` naming) requires:
- **`HasStarted`** (`Has-` prefix, permanent past event, set at 90% of Ch.1) — **absent**.
- `IsCompleted`, `IsIgnored` (`Is-` mutable) — present.
- "Actively Reading" / "In Progress" is a **derived** state
  (`HasStarted AND NOT IsCompleted AND NOT IsIgnored`), **not a stored column** — yet the code stores both
  `IsInProgress` and `IsActivelyReading`.
- Zero-coupling rule (no bit drives another) — cannot be expressed because the foundational bit
  (`HasStarted`) is missing.

Supporting evidence of pre-revision design elsewhere: `ModelEnums.cs` still defines vestigial
`ReadStatus { Unread, InProgress, Completed }` and `FavoriteStatus { None, Favorite, PrivateFavorite }`
enums — the enum/junction model that the boolean-column axiom (Settled Axiom #3) was meant to replace.

**What's correct:** the partition strategy (tiny hot row, warm dates table, sparse rec-source), the
filtered-index *pattern*, and the favorite/follow/read-it-later/ignore bits.

**Nature of the gap:** stale code, not an intent contest. The stored columns implement a *superseded*
reading-status design; the spec is the recent, authoritative artifact and the code is non-working here (no
service, no UI, not migrated). **Direction is not open — the spec wins.** This is Stage-4-as-trap-warning
(see audit-summary §0/§3b): the misleading columns will be copied if a building session isn't told to
discard them. **Resolution (predetermined):** re-model the booleans (add `HasStarted`, drop
`IsInProgress`/`IsActivelyReading`, retire the vestigial `ReadStatus`/`FavoriteStatus` enums), then
regenerate the filtered indexes — i.e. proceed as Stage 2 build-to-spec, no diagnosis needed.

---

## Feature 16 — Story Interaction State Writes
- **L1 — Stage 4.** See above. `UserStoryInteractionDate` warm partition and sparse semantics ("no row =
  all false; date row only when completed/favorited") are sound and should survive the re-model.
- **L2 — Stage 2.** No interaction write service (MVP: direct EF; Layer-7: Redis `LPUSH`). Interface not
  yet defined.
- **L3-Logic — Stage 2 (button slice Stage 5, WU7; panel slice + 2-second debounce remain Stage 2).**
  `UserStoryInteractionButton` leaf built (EventCallback-driven; read-only when no `OnToggle`;
  rendered only when `IsActive`). The 2-second debounce and the panel's coordination state are
  unbuilt — **WU16**.
- **L3.5-Structure — Stage 2 (button slice Stage 5, WU7; panel slice remains Stage 2).**
  `UserStoryInteractionButton`'s markup/render-guard built. `StoryInteractionPanel` coordination
  composite (owns debounce VM, `IsOwnStory` swap to Edit button, maps `InteractionTypeEnum` →
  `(IconPath, AccentColor)`) unbuilt — **WU16**.
- **L4-Style — Stage 2 (Stage-1 block resolved, WU7; button styling Stage 5, panel's icon mapping
  remains Stage 2).** **Resolution (settled WU7, supersedes the sprite-key plan below):** interaction
  icons are **inline SVG shapes**, not theme-swappable sprite URLs — `Sprites/ISpriteReadService` is
  not involved at all. `UserStoryInteractionButton` takes `IconPath` (SVG `d` string) + `AccentColor`
  `[Parameter]`s and renders one inline `<svg>`; it has no domain knowledge. Three-state square button
  (gray inactive → accent-fill-on-hover → inverted accent-bg/white-shape when active) — full pattern
  in `layer4-style.md` "Interaction Icons Are Inline SVG." **Still open for WU16:** the
  `InteractionTypeEnum → (IconPath, AccentColor)` mapping table itself (which shape, which color, per
  interaction type) — the owning composite or a shared constants helper mints this when
  `InteractionTypeEnum` is minted (WU15/16). Superseded: spec §5.30.5's
  `ISpriteService.GetInteractionIcon(InteractionTypeEnum, theme)` and the WU2-era
  sprite-key/`GetSpriteUrl` plan that used to live in this note — see `audit/Sprites.md` Feature 3.

  **Settled / do-not-revisit (minted button contract, WU7):** `UserStoryInteractionButton`'s
  parameters are `IsActive` (bool), `OnToggle` (`EventCallback<bool>`, absence ⇒ read-only),
  `IconPath` (string, SVG `d`), `AccentColor` (string, CSS color), `Label` (string, drives
  `aria-label`/`title`). Read-only renders as a `<span>` (not a `<button>`) and only when `IsActive`.
  This contract is locked for WU16 to consume — do not redesign it when building the panel; only the
  `(IconPath, AccentColor)` *values* per interaction type are open.
  **How verified (WU7, 2026-06-21):** `dotnet build` green (4 projects, zero new warnings); live
  server run, homepage `200`; user-confirmed visual check of all three states (gray inactive, hover
  accent-fill, inverted accent-bg/white-shape active) plus the read-only-renders-only-when-active
  rule, via a throwaway harness on `HomeDesktop.razor` (heart + star sample shapes, removed after
  confirmation). No real consumer exists yet (`StoryInteractionPanel` is WU16).
- **L5 — Stage 2.**
- **L6 — Stage 4.** The seven filtered indexes are written but target `is_in_progress`/`is_completed`
  etc.; they must be regenerated against the revised columns (`has_started`). Follows L1.
- **L7 — Stage 2.** Write-behind buffer (pattern 1): `LPUSH interaction-queue` → 5s drain →
  consolidate `Dictionary<(UserId,StoryId),LatestState>` → batch raw SQL. The branch decision (swap the
  MVP service body vs. parallel path) is the real Stage-2 content.

## Feature 17 — Story Interaction Lists & Bookshelves
- **L1 — Stage 4.** Depends directly on the reading-status re-model: derived tabs "Actively Reading"
  (`HasStarted AND NOT IsCompleted AND NOT IsIgnored`) and "Abandoned" (`IsIgnored AND HasStarted`)
  cannot be computed without `HasStarted`.
- **L2 — Stage 2.** Tab-backing read queries unbuilt.
- **L3-Logic / L3.5-Structure — Stage 2.** `BookshelvesPage` dispatcher (`/bookshelves/{Tab}`,
  active-user-only, each tab composing `StoryDeck`) unbuilt. Not a discovery surface — no SearchMode
  entries (correctly so).
- **L4-Style — Stage 1.** **L5 — Stage 2.** **L6 — Stage 4** (same index re-model).

---

### Dependency callout
Everything in this folder past L1 is blocked on resolving the reading-status re-model **and** on the
`StoryCard`/`StoryDeck` atoms (owned by Stories/). Surface both to the user before building Bookshelves.
