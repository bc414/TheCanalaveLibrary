# Tag Design Deliberations — The Canalave Library

A summary of all tag-related design discussions from the Gemini 2.5 Pro design transcripts.

---

## 1. Table Structure — One Table vs. Many

The first question was whether character tags and genre tags should be separate tables. The answer was a single `Tags` table with a `TagType` discriminator, plus a `StoryTags` junction table. The justification was scalability — new tag types (Relationship, Warning, Setting, etc.) can be added by just expanding the constraint, with no schema changes.

---

## 2. What Lives in `StoryTags` (vs. Separate Tables)

The schema evolved significantly. Originally everything went into `StoryTags`, but two tag types were split out:

- **Characters** — moved to a dedicated `StoryCharacters` table. The primary reason was to handle **relationships**: a `StoryCharacterRelationships` table needs stable per-story character IDs (e.g., "Ash in Story 456"), which you can't cleanly get from `StoryTags`' composite key. The OC detail override was a secondary benefit.
- **Settings / AU Tropes** — remained in `StoryTags`, with an optional `SettingDetails` row for custom name/description overrides.

What remains in `StoryTags`: Genre, Content Warnings, Setting, and AU Trope tags.

---

## 3. The `TagName` Uniqueness Constraint

The initial schema had `TagName NVARCHAR(100) NOT NULL UNIQUE`, which was identified as wrong — it would prevent having "Paris" as both a Character and a Location. Fixed to a composite unique constraint: `UNIQUE (TagName, TagTypeID)`.

---

## 4. `TagType` and `TagOrigin` Data Types

`TagType` was made a **lookup table** (needs a pretty display name for the UI). `TagOrigin` (whether a tag is Official, Fanon, etc.) was made a **magic byte / enum** (only the C# logical key matters; no user-facing display needed).

---

## 5. The `Priority`/`Emphasis`/`TagRole` Column Name — Extended Deliberation

The column indicating whether a tag is "Primary" or "Supporting" on a story went through many name iterations:

| Proposed Name | Verdict |
|---|---|
| `TagRole` | Original — too ambiguous |
| `Significance` | Too academic/verbose |
| `Prominence` | Good but still technical |
| `Emphasis` | Top pick for developer-facing schema |
| `Focus` | Better for creative/user-facing context |
| `Role` | Simple fallback |
| `TagWeight` / `Weight` | Good developer term ("strength") |
| `Centrality` | Literary but too niche |
| `Importance` | Plain English, zero ambiguity |
| `Priority` | **Final decision** — used in C# enum |

The final outcome was **`Priority`** as the column name (with `0 = None`, `1 = Primary`, `2 = Supporting` as a `TINYINT` enum), and **"Primary" / "Supporting"** as the user-facing labels. It applies to Genre, Character, and Relationship tags, but not Warning tags (enforced by a trigger).

It was also confirmed that `Priority` applies to genres just as naturally as characters — a story can have a "Primary Emphasis: Sci-Fi" and "Supporting Emphasis: Comedy."

---

## 6. OC Design — `StoryCharacters` Table

- The table was originally named `StoryCharacters`, which was ambiguous (could mean all characters). Alternatives considered: `OriginalCharacters`, `StoryOCs`, `CustomCharacters`. Settled on `OCs` as the table name and `myStory.OCs` in C#, because it's concise, non-redundant, and uses domain-specific language.
- Similarly, `StoryUniverses` was renamed `AUs` briefly but ultimately became `SettingDetails`.

**OC Bulbasaur logic**: A story can tag "Bulbasaur" with `IsOC = 1` in `StoryCharacters`. If `OC_Name`/`OC_Bio` are populated, the UI shows "OC Bulbasaur *" with a tooltip. The sprite comes from the base `Bulbasaur` tag — users cannot upload custom OC sprites (to prevent user-generated images appearing in site-standard search cards).

**`AllowOCDetails` bit**: Added to `Tags` to prevent `IsOC = 1` being set on specific named characters like "Ash Ketchum." Only generic Pokémon tags ("Bulbasaur") and "OC Trainer"/"OC Human" allow OC details. Enforced by a trigger `TR_StoryCharacters_EnforceOCLogic`.

---

## 7. Setting/Universe Design — `SettingDetails` Table

The naming journey: `StoryUniverses` → `AUs` → `SettingDetails`. "Setting" was chosen over "Universe" because the Pokémon fandom already has multiple fragmented canons, so "Alternate Universe" is misleading. "Setting" is accurate for "Games," "Anime," "PMD," "Original Setting."

The logic mirrors `StoryCharacters`:
- Simple tag selection (e.g., "Anime") → just a `StoryTag` entry.
- Custom details on a setting (e.g., a specific PMD variant with a name/description) → a `StoryTag` entry **plus** a `SettingDetails` entry.

**`AllowSettingDetails` bit**: Added to `Tags` (same pattern as `AllowOCDetails`) to prevent users adding setting details to nonsense tags like "Fluff." Enforced by a trigger. Only Setting and AU Trope type tags have this flag set to 1.

---

## 8. `AU` Uniqueness Constraint Fix

The early `AUs` table had `UNIQUE (StoryID)` — meaning a story could only have one AU with details. This was identified as too restrictive. Fixed to `UNIQUE (StoryID, BaseTagID)`, allowing a story to have custom details for its "Coffee Shop AU" and its "PMD AU" simultaneously.

---

## 9. Tag Sprites — Storage, URLs, and Theming

Several entries covered this:

**Initial approach (storing full URLs)**: The CDN URL (`https://cdn.thecanalavelibrary.com/tags/123-fluff-anim.webp`) is stored directly in the `Tags` table. Cloudflare R2 was recommended as blob storage (zero egress fees vs. DigitalOcean Spaces).

**In-row performance problem**: `[MaxLength(2048)]` on three fields (Description, SpriteUrl, AnimatedSpriteUrl) would push rows off-row in SQL Server (total ~12,500 bytes vs. the 8,060-byte limit), requiring slow pointer lookups on every search result. Solution: `[MaxLength(512)]` for URLs (actual URLs are under 200 chars), and `[MaxLength(500)]` for Description (tooltip use).

**Theme support — "Default In-Row with Overrides"**: Discussed putting all sprites in a normalized `TagThemeSprites` table (rejected — too many JOINs on hot path). The chosen design keeps default sprite URLs in the `Tags` row and uses a separate `TagThemeOverride` table only for users on non-default themes. This optimizes for the common case.

**Switching to sprite identifiers (later revision)**: Instead of storing full URLs, the `Tags` table stores a `SpriteIdentifier` string (e.g., `bulbasaur.png`). The URL is constructed at render time: `(cdn_base)/(user_theme)/(tag.SpriteIdentifier)`. Cloudflare R2 bucket is organized with theme-prefixed paths: `dark-theme/bulbasaur.png`, `light-theme/bulbasaur.png`. This decouples theming logic from the database entirely.

---

## 10. Tag Hierarchy Cache

A `Cache_TagHierarchy` (Closure Table) was designed to handle recursive parent-child tag queries. However, it was later decided this is **not needed** because tags are only one level deep — grandchildren are impossible. With a single-level hierarchy, finding all children of a parent tag is a simple direct query, not a recursive CTE, so the cache is unnecessary complexity.

---

## 11. Multi-Tag Filtering and Index Design

Filtering stories by multiple tags simultaneously (e.g., `character=A AND genre=B AND setting=C`):

- Uses `All()`/`Any()` in EF Core LINQ, which translates to `WHERE EXISTS` subqueries.
- Requires a reverse **covering index** `(TagId, StoryId) INCLUDE (Priority)` in addition to the PK `(StoryId, TagId)`.
- The PK is `(StoryId, TagId)` first because loading a story page (get all tags for one story) is the most frequent query. The reverse index handles search filtering (get all stories for a tag).
- With both sides sorted, the database can use the **Merge Algorithm** (O(m+n)) for intersections instead of a hash join — since both lists come out of the index already sorted by `StoryId`.
- The trade-off: the table effectively exists twice (doubled disk space for two sorted copies), which is acceptable since the table is very small (two ints + one small int per row).

---

## 12. `SavedTagSelection` — Naming Journey

Users can save a named set of tags for reuse in searches. The naming went through:

`TagGroup` → `FilterPreset` → `TagSet` → `SavedTagSearch` → `SavedTagFilter` → `SavedTagSelection`

Key rejections along the way:
- `TagGroup`: ambiguous with the `Group` entity
- `FilterPreset`: sounds like a site default, not user-created; "Filter" implies exclusion
- `TagSet`: `TagSetTag` as the join table name is unwieldy
- `SavedTagSearch`: implies searching for tags, not searching for stories *using* tags
- `SavedTagFilter`: "Filter" has negative/exclusionary connotation; tags are inclusionary

Final choice: **`SavedTagSelection`** and **`SavedTagSelectionEntry`** — "Selection" is neutral, implies inclusion, and the join table name is clean.

Sharing was deliberated: kept as one-to-many (one user owns many presets), with sharing implemented as a **copy/clone** action rather than a many-to-many subscription model. Copying avoids the problem of one user's edits affecting all subscribers. Presets can have `IsPublic = true` to make them discoverable.

---

## 13. Tag Autocomplete on Story Creation Form

How to let users type-search tags in the story editor:

- **Small categories** (Genres, Warnings, Settings — under ~100 items): load all at component init, filter client-side. Feels instant, aids discovery.
- **Medium categories** (Characters — 1,000–2,000 items for a single-fandom site): also load all client-side. At 2,000 items × ~50 bytes per DTO = ~100 KB, this is tiny. Client-side filtering is instantaneous vs. the 50–200ms latency of server-side filtering.
- **Debouncing**: if server-side filtering were used, 300ms debouncing is required to avoid flooding the server per keystroke.

`TagTypeEnum` should be **omitted from `TagDTO`** when fetching by type — it's redundant (the client called `GetCharactersAsync()`, so it already knows the type) and saves ~26% of payload size.

Separate requests per tag type (rather than one batched tuple request) were recommended for progressive rendering, HTTP/2 multiplexing, and cache granularity.

---

## 14. `TagUpdateSuggestion` Notification

A `TagUpdateSuggestion` notification type was included in the notification enum. This fires when a moderator "fanonizes" a tag (promotes a community OC to an official `Fanon` tag in the `Tags` table). Authors who have stories using the old OC tag text receive a notification and a one-click option to update their story's tag to the new official fanon tag.
