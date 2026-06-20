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
- **L3-Logic — Stage 2.** `UserStoryInteractionButton` leaf (EventCallback-driven; read-only when no
  `OnToggle`; rendered only when `IsActive`) and the 2-second debounce are unbuilt.
- **L3.5-Structure — Stage 2.** `StoryInteractionPanel` coordination composite (owns debounce VM,
  `IsOwnStory` swap to Edit button) unbuilt.
- **L4-Style — Stage 1** (blocked; icon concept Star/Staryu, Heart/Luvdisc).
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
