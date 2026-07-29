# Audit — Tags/

**Features:** 11 (admin), 12 (story tagging), 13 (display & sprites), 14 (filtering/selection UI),
15 (saved selections).

## Shared Context

> **2026-07-18 — Desktop/Mobile fork removed (WU-ResponsiveMerge).** `TagDirectoryDesktop`/
> `TagDirectoryMobile` merged into `TagDirectoryPage` (page renders its own markup; the 33-line
> mobile stub deleted). The page remains the L5 WASM-island pilot — island behavior unchanged.
> Narrow rendering is provisional pending the future mobile phase. Desktop/mobile assertions
> elsewhere in this file are historical. Rules: `canalave-conventions/render-and-layout.md`
> §"Responsive Layout Architecture"; spec §3.9/§3.10 superseded on this axis.
> Verified 2026-07-18: full suite green post-merge (Unit 702 / Integration 727 / RazorComponents
> 510); browser smoke at desktop width clean (loads, no error banner, zero console errors);
> narrow rendering deliberately unpolished, no visual pass yet.

> **2026-07-26 — WU-TagFanon: deliberate Stage-4 reopening of the WU37 settled routing table.**
> WU-TagFanon (plan: `~/.claude/plans/i-want-to-plan-resilient-sonnet.md`) is the first rigorous
> consumer of the three-tier character-tag model (generic → specific-canon child → fanon child,
> Gemini-era Entry #1316) and found the settled shape half-built: hierarchy invisible to discovery,
> the Setting/AU overlay unreachable (gate flag dropped in `TagDirectoryPage` DTO mapping) *and*
> invisible (no reader projection), `OcBio` write-only, and the OC gate conflating custom naming
> with per-story portrayal. Changes to the "settled, do not revisit" items below, recorded as a
> deliberate reopening, not drift:
> - **`SettingDetail` deleted; overlay folded onto `StoryTag`** as nullable `CustomName`+`Nuance`.
>   The real rule is *cardinality*, not "instances are referenced": a 0-or-1 overlay fits the
>   junction row; `StoryCharacter` keeps its table because a story may hold several custom-named
>   characters of one species (original `UQ_StoryCharacters_StoryTag (StoryID, BaseTagID,
>   CharacterName)` intent restored).
> - **`OcName`/`OcBio` → `CustomName`/`Nuance`** on `StoryCharacter`; `AllowOCDetails` +
>   `AllowSettingDetails` → single `Tag.AllowCustomName`. "Bio" was never deliberated (the `Oc`
>   prefix was a mechanical side effect of adding `IsOC`, Entry #1316); "Identity" is banned as a
>   term (collides with ASP.NET Core Identity). `Nuance` is ungated and universal — every tag type.
> - **Roll-up:** filtering a parent tag matches its children (symmetric include/exclude) — the
>   query the rejected `Cache_TagHierarchy` presumed would exist.
> Full rules: `layer2-services.md` §"Structured Tag Authoring" (rewritten same date) +
> §"Tag Hierarchy Roll-Up". The WU37 notes below are historical.

**Entities (Core/Tags/ + Core/Models/):** `Tag` (`TagName`, `TagTypeId`, `IsFanon`, `ParentTagId`
self-ref one-level hierarchy, `SpriteIdentifier`, `AllowOCDetails`, `Description`), `TagType` (+ enum
mirror `TagTypeEnum`: Character/Setting/Genre/ContentWarning/CrossoverFandom/Relationship), `StoryTag`
(composite `(StoryId,TagId)`, `Priority`→`TagPriority`), `StoryCharacter` (OC unification),
`StoryCharacterRelationship` (romantic/platonic), `SettingDetail`, `SavedTagSelection` /
`SavedTagSelectionEntry`.

**Contracts:** `ITagReadService` (Core/Tags/ — renamed from `ITagRetrievalService` in WU3), `TagDropDownDTO`,
`StoryTagDTO`, `IStoryTag`, `TagPriority`, `TagChipDto` (Core/Tags/, minted WU4 — render-ready tag data
for the `TagChip` leaf). **As of 2026-06-27 (sprite redesign):** `TagChipDto.SpriteUrl` replaced by
`TagChipDto.SpriteIdentifier` (the raw key, not a resolved path). Sprite URL resolution moved from the
read service into the render component (`TagChip.razor` + `ThemeContext` cascading value). See
`layer2-services.md` §"Sprite URLs Are Resolved At Render Time." The DTO is now per-content (not
per-user/per-theme) and is freely cacheable across viewers.
**WU12 follow-on (superseded 2026-06-27):** `SearchTagChipsAsync`'s per-keystroke typeahead
sprite-resolve step previously called `ISpriteReadService` in-memory after materialization. Superseded
by the sprite-redesign: read services now project raw `SpriteIdentifier` and drop their
`ISpriteReadService` constructor dependency entirely.

**WU37 naming correction (2026-06-25):** `StoryCharacterRelationship` → **`StoryCharacterPairing`**;
`CharacterRelationshipType` → **`CharacterPairingType`** (Romantic/Platonic); new first-class join
`StoryCharacterPairingMember` replaces the EF auto-generated shadow table
`StoryCharacterStoryCharacterRelationship`. Feature 10's unrelated story-to-story link (renamed
`StoryLineage`/`StoryLineageType` in WU42, 2026-07-12 — formerly `StoryRelationship`/
`StoryRelationshipType`) was unchanged by WU37.

**Pre-integration cleanup (2026-06-26):** `CharacterRelationshipType { Romantic, Platonic }` enum in
`Core/Lookups/ModelEnums.cs` deleted — zero references repo-wide; the live pairing-type enum is
`CharacterPairingType` (Romantic/Platonic). `CharacterPairingType` backing type normalized from `: byte` to
`: short`, consistent with the project convention (magic enums use `: short` / `HasConversion<short>()`;
no migration needed — Npgsql maps both `byte` and `short` to `smallint`). Dead placeholder comment
`// ... (Keep all existing enums from Part 1 and Part 2) ...` removed from `ModelEnums.cs`.
`layer1-data-model.md` enum table example updated to `CharacterPairingType` (replaces the now-deleted
`CharacterRelationshipType`). Verified: `dotnet test` 1222 green.

**Components:** `TagSelector` (`SharedUI/Tags/` — moved out of the legacy `Components/` folder; see
`canalave-conventions/SKILL.md` "Code Organization"). The empty, unused `TagViewModel.cs` that sat
alongside it was deleted in the same move. **The relocation is folder-only — `TagSelector`'s content is
unchanged and remains the discardable scaffolding described below, scheduled for the WU11 rebuild.**

**Fluent config:** `Tag.TagName` unique (**WU27.5 changes to composite `(TagName, TagTypeId)` per
`Tag_Design_Deliberations.md` §3 — so "Paris" can be both a Character and a Setting; migration required**);
self-ref `ParentTagId` SetNull; `TagType`/`Tag` Restrict; `SavedTagSelection` cascade + unique
`(UserId,Nickname)`; `SavedTagSelectionEntry` unique `(SelectionId,TagId)` + Restrict on Tag.

**Nav rename (WU27.5):** `Tag.InverseParentTag` → `Tag.ChildTags` (EF scaffold artifact; pure C# rename,
no migration; `TagConfigurations.cs` `HasMany` call updated to match).

**L1 drift (flag, not fixed in WU27.5):** `Tag.SpriteIdentifier` is `[MaxLength(50)]` but spec says 100;
`Tag.Description` is 512 vs spec 500. Address in a future L1 pass.

---

## WU-TagFanon Stage note (2026-07-26) — F11 + F12 + F31 + F41, all touched cells keep their current Stage (F11/F12/F31 L4 remain Stage 1 — standing Phase-3 visual pass)

**What changed.** The tag model's per-story overlay was rebuilt around the custom-name/nuance
split, hierarchy roll-up was implemented, and the fanonization pipeline was built on top. Cells
were Stage 5 before and remain Stage 5 — this is the hidden-deferral shape: the grid could not
show that tiers 2–3 of the character model had storage and nothing else.

**Six defects the audit found in already-Stage-5 code** (each fixed here):
1. **Hierarchy had zero effect on discovery.** `ApplyFilters` matched tag ids exactly, so
   filtering `Bulbasaur` never returned `Ash's Bulbasaur` or a fanon child. The Gemini-era record
   rejected the `Cache_TagHierarchy` closure table *because* "finding all children is a simple
   direct query" — that query was never written. Left unfixed, fanonize adoption would have
   silently removed a story from its own species' search results.
2. **The Setting/AU half was unreachable AND invisible.** `TagDirectoryPage` dropped
   `AllowSettingDetails` from both DTOs (so no Setting tag could ever be flagged), and
   `SettingDetail` was projected only into edit hydration — never into `StoryDetailsDTO`.
3. **`OcBio` was write-only** — authored, stored, projected to the display DTO, rendered nowhere.
4. **The OC gate conflated two concepts** — one flag gated `IsOc`, `OcName` *and* the bio, making
   a per-story portrayal note ("competent Ash") impossible on any specific character.
5. **The character overlay disagreed with itself three ways** — the original schema wanted several
   OCs per species (`UQ_StoryCharacters_StoryTag`), the model permitted it, `TagSelector` forbade it.
6. **Ship filtering did not exist.** WU37 removed the `Relationship` tag type; nothing replaced its
   searchability, so a fanfiction site had no pairing filter at all.

**Also fixed (latent, found while building):** `DeleteTagAsync` omitted `StoryCharacters` from its
reference pre-check (a character tag used only as an OC base hit the Restrict FK as a raw
`DbUpdateException`); `TagEditorForm` never hydrated `SpriteIdentifier`, so editing any
sprite-bearing tag silently cleared its sprite key; H6's tag L1 length drift corrected
(SpriteIdentifier 50→100, Description 512→500).

**Settled here (do not revisit):**
- **Cardinality, not "instance-referencing", is why the two halves differ.** Gemini's "absolute
  requirement" framing for `StoryCharacter`'s surrogate PK is false as stated (composite FKs work);
  it becomes true only *conditioned on* multiple OCs per species, which the original schema
  explicitly allowed. A 0-or-1 overlay belongs on the junction row; a 1-to-many overlay needs its
  own table. Hence `SettingDetail` deleted, `StoryCharacter` kept.
- **"Identity" is a banned term** for this concept — it collides with ASP.NET Core Identity
  sitewide. The concept is **custom naming**; the columns are `CustomName`/`Nuance`.
- **"Bio" was never deliberated.** The original name was the neutral `CharacterBio`; the `Oc`
  prefix arrived as a mechanical side effect of introducing `IsOC` in the same 2025-10-27 session.
- **Fanonized tags get `AllowCustomName = false`** — a specific entity, same rule that protects
  "Ash Ketchum" from being re-declared as someone else's character.
- **Fanonization is mod-driven set selection, never string matching.** The affected rows are the
  dashboard group the moderator was looking at, so the tag may be named anything — the
  `"Saura (Silver Resistance)"` case the naive `OcName == TagName` reading could never handle.
- **Never notify an author twice per tag** — enforced by `TagAdoptionState.DateNotified`, NOT by
  notification unread-dedup (which would re-fire on anyone who read and moved on).
- **No moderator dismiss, no per-row exclusion, no undo.** A living ranking over a growing corpus
  has nothing to dismiss; moderators cannot have read every story so cannot judge rows; nothing is
  ever applied without the author acting. Author-side dismiss DOES exist and is reversible — only
  the author knows whether their "Saura" is *that* Saura.

**How verified (2026-07-26).** `dotnet build` green; `dotnet test` green — **2258 total**
(753 Unit / 593 RazorComponents / 912 Integration). Design-token check green.
- **Unit** — `TagValidationsTests` overlay bounds + the removed type-coercions.
- **Integration** — `StoryTaggingTests` rewritten for the split (gate rules both directions, nuance
  ungated on canon characters and genres, two same-species OCs with distinct names, duplicate and
  double-unnamed rejection, flat overlay persistence, index-based pairings incl. a pairing between
  two same-species OCs, edit round-trip); `FanonPipelineTests` (case-insensitive grouping across
  authors, single-author below threshold, drafts excluded from public reach, gated story list with
  complete count, non-mod rejection, link-and-notify, never-twice across a read+sweep, duplicate
  link rejection, adoption preserving nuance/priority/pairings, collision skip, dismiss round-trip,
  nudge resolving a disambiguated target); `DiscoveryRollUpAndShipTests` (parent→child include on
  both tables, independent AND terms, symmetric exclude, ship single-pairing coverage vs
  co-presence, pairing-type constraint, ship roll-up, ship exclude).
- **RazorComponents** — H5 closed in full: `FakeNotificationWriteService` added to the fakes
  catalog and the previously-unwritten anonymous-`NotificationBell` regression test written.
- **Browser (L4.5 band, extended seed + psql ground truth).** `/fanon` hub with ✦ indicators and
  sr-only parent cues; `/fanon/characters` ranked with the threshold hiding single-author groups;
  **two separate "saura" groups on different base tags** — proving grouping is `(name, base tag)`,
  not name alone; moderator link-to-existing-tag against `"Saura (Silver Resistance)"`, psql-confirmed
  (link row, 2 adoption states, 2 type-26 notifications, zero stray tags); `/tag-adoptions` index and
  per-tag page; adoption psql-confirmed to keep the stable row id, priority and nuance while clearing
  `IsOc`/`CustomName`, with the *other* author's row untouched; **roll-up proven on the adopted
  story** — it now carries only the fanon child yet still returns under a `Bulbasaur` filter; ship
  filter returning exactly the paired story; the three-tier tree rendering in `/tags` for the first
  time (`Bulbasaur` → `Ash's Bulbasaur` → `Saura (Silver Resistance)` ✦); the setting overlay
  rendering its `*` + tooltip. Zero console errors; anonymous SSR carries no mod controls.
- **Bug found and fixed during the browser pass:** the link panel pre-filled the create-new tag
  name with the group name, so a moderator who typed in the typeahead without *selecting* a result
  silently minted a duplicate tag. The create path is now disarmed until deliberately typed, is
  disabled while a pick exists, and the button states which action it will take.

**L6 — measured, not assumed (live `pg_indexes` sweep + EXPLAIN ANALYZE on the extended seed).**
`ix_tags_parent_tag_id` already exists (EF FK convention) and is what roll-up expansion needs;
at 136 tags the planner correctly prefers a seq scan (0.02 ms, 2 buffers). Dashboard grouping:
2.4 ms over 5,940 character rows × 3,012 stories. Ship probe: 0.1 ms. **No new indexes were
warranted.** **Tracker C1 resolves to REJECT** — the tag-chip `ILIKE` runs in 0.079 ms over 136
tags; F11's L6 note deferred a trigram index "until tag counts grow" and they have not grown
enough. Recorded as a measured decision, not an assumption.

## Feature 11 — Tag Administration
- **L1 — Stage 5.** `Tag` shape matches §5.16 (curated, staff-only, hierarchy, sprite key, OC flag,
  tooltip description). Sound. **L2 — Stage 5 (WU27.5, see Stage note below).** **L3/L3.5 — Stage 5
  (WU27.5, see Stage note below)** (mod CRUD behind `AuthorizeView` on Tag Directory). **L4 — Stage 1. L5 — Stage 5 (WU-L5Pilot,
  see Stage note below). L6 — Stage 5 (WU-L6, 2026-07-07 — resolved as already-covered, no DDL:
  the tag table is tiny and PK/unique-indexed; a trigram index for the leading-wildcard chip
  search was REJECTED under R4 until tag counts grow — `layer6-indexes.md` §"Rejected").**

  **Settled for sprite redesign (2026-06-27, do not revisit):** `ServerTagWriteService` gains a
  non-blocking sprite-existence warning via `ISpriteAssetProbe.ExistsAsync` (server-only write-time
  probe). When `SpriteIdentifier` is non-empty, the probe checks whether the static asset exists for
  the default theme slug. On miss, the write **still succeeds** but returns a warning alongside the
  saved tag (non-blocking — out-of-band provisioning may lag tag creation). `TagEditorForm.razor`
  surfaces the warning inline. `TagValidations.cs` is unchanged (length-only, pure, no IO).
  `ISpriteReadService` is **removed** from `ServerTagReadService`'s constructor. `TagChipDto.SpriteUrl`
  renamed to `TagChipDto.SpriteIdentifier`.

  **Settled for WU27.5 (2026-06-24, do not revisit):**
  - **Role gate — real now.** `<AuthorizeView Roles="Moderator,Admin">` for UI affordances; server
    `IActiveUserContext.IsModerator || IsAdmin` guard in `ServerTagWriteService`. Role *rows* already
    seeded via `ApplicationRoleConfiguration.HasData`; WU27.5 closes the assignment gap in
    `DataSeeder.cs` (AdminUser also assigned "Moderator"). `IsInRole` is literal — Admin-inheritance
    expressed by listing both roles.
  - **Delete — block when in use.** Pre-check `StoryTag` + `SavedTagSelectionEntry` + child-tag
    counts; throw `TagValidationException` ("in use") so the Restrict FK never fires.
  - **Uniqueness — composite `(TagName, TagTypeId)`.** From first principles (natural key = name + type)
    and `Tag_Design_Deliberations.md` §3. Index drop + recreate migration; validation checks uniqueness
    within type.
  - **`IsFanon` — plain editable field.** Fanonize notify/migrate flow deferred (seam: existing
    `NotificationTypeEnum.TagUpdateSuggestion = 26`; workflow lands in its own future WU).
  - **Edit form — full field set:** TagName, TagType, Description, SpriteIdentifier, IsFanon,
    AllowOCDetails (Character-type only — hidden + coerced `false` for other types), ParentTag
    (same-type top-level tags; may not be the tag itself; parent may not itself have a parent).
  - **Browse layout:** sections per type, parent→child nesting everywhere. Bounded types (Setting,
    Genre, ContentWarning) render expanded; unbounded types (Character, Relationship, CrossoverFandom)
    additionally get collapsibility + type jump-nav. `TagTypeLayout` helper classifies which.
  - **Mod controls:** hover ✎/✕ + WU9-shell modal hosting `TagEditorForm` / `ConfirmDialog`.
  - **`AllowOCDetails` context:** WU27.5 only sets the gate (which Character tags accept OC details);
    the OC creation/display flow (StoryCharacters OC_Name/OC_Bio, enforcement trigger) is Feature 12 /
    WU37.

  **WU27.5 Stage note — L2/L3/L3.5 (2026-06-25):**

  Built: `Core/Tags/ITagWriteService.cs` (CRUD + XML-doc exceptions); `Core/Tags/CreateTagDto.cs`,
  `Core/Tags/UpdateTagDto.cs`, `Core/Tags/TagValidations.cs` (name required/≤100, unique-within-type,
  description ≤512, sprite ≤50, parent same-type + no parent of its own + not self), `Core/Tags/TagValidationException.cs`,
  `Core/Tags/TagEditorFormResult.cs`, `Core/Tags/TagTypeLayout.cs` (bounded/unbounded classification).
  `Server/Tags/ServerTagWriteService.cs` (inherits `ServerTagReadService`; `RequireMod()` gate first;
  delete pre-checks `StoryTags`+`SavedTagSelectionEntries`+`ChildTags` count; throws `TagValidationException`
  if referenced so Restrict FK never fires). DI: `AddScoped<ITagWriteService, ServerTagWriteService>()`.
  Also extended `TagChipDto` with `IsFanon`/`AllowOCDetails`/`ParentTagId` (non-breaking, default false/null;
  accurately populated only by `GetTagDirectoryAsync`).
  UI: `SharedUI/Tags/TagEditorForm.razor` (presentational leaf, no `@inject`, bUnit-testable; `EditForm`
  over inner `TagEditorFormModel`; parent dropdown filtered to same-type top-level tags excluding self;
  `AllowOCDetails` checkbox conditional on `TagTypeId == Character`; emits `TagEditorFormResult` via
  `EventCallback`; renders `ServerError` in `role="alert"`).

  **How verified (2026-06-25):** `dotnet build` green (8 projects, 3 pre-existing warnings, 0 errors).
  - **Unit** (`TagValidationsTests.cs`, 23 tests): name required/whitespace/length boundary, uniqueness,
    description length, sprite length, parent-doesn't-exist, cross-type parent, two-level parent rejection,
    self-reference on update, `CoerceAllowOCDetails` theory (7 type/input/expected combinations).
  - **Integration** (`TagWriteServiceTests.cs`, Testcontainers Postgres, 11 tests): mod-gate (Create/
    Update/Delete throw `UnauthorizedAccessException` for non-mod); create happy path persists row; duplicate
    name in same type throws `TagValidationException`; same name different type succeeds; parent assignment
    persists `ParentTagId`; two-level parent throws; update persists renamed name + `IsFanon`; delete unused
    succeeds; delete with `StoryTag` child throws `TagValidationException`; delete with child tags throws;
    delete missing id throws `KeyNotFoundException`.
  - **RazorComponents** (`TagEditorFormTests.cs`, 9 tests): all type options present; `AllowOCDetails`
    visible only for Character type (theory, 6 types); edit mode pre-populates name; parent dropdown
    shows same-type top-level only; parent dropdown excludes self; submit emits DTO; cancel fires callback;
    server error renders in `role="alert"`.

  **WU38 Stage note — sprite redesign (2026-06-27):**

  Applied to Feature 11 as part of the wider sprite-system redesign:
  - `TagChipDto.SpriteUrl` renamed → `TagChipDto.SpriteIdentifier` (raw key, not a resolved URL).
  - `ServerTagReadService` drops its `ISpriteReadService` constructor dep; all projection sites now
    copy `tag.SpriteIdentifier` verbatim.
  - `ITagWriteService.CreateTagAsync` return type changed to `Task<TagSaveResult>` (record
    `(int TagId, string? SpriteWarning)`). `UpdateTagAsync` return type changed to `Task<string?>` (the
    warning string). `ISpriteAssetProbe spriteProbe` injected into `ServerTagWriteService`; non-blocking
    `BuildSpriteWarningAsync` probes the default theme slug and returns an advisory string on miss.
    The save always succeeds regardless of the probe result.
  - `TagDirectoryPage.razor` captures the `SpriteWarning` and shows an amber advisory block below the
    form on create.
  - **How verified (2026-06-27):** `dotnet test` green — 437 Unit + 443 RazorComponents + 348
    Integration = 1228 tests. Integration tier (`TagWriteServiceTests.cs`): all 11 existing tests
    updated to unwrap `TagSaveResult.TagId` — verified still pass. Unit tier
    (`LocalSpriteAssetProbeTests`, 4 tests): `ExistsAsync` true/false against temp dir, checks static
    `.png` not animated `.webp`, wrong theme returns false. RazorComponents tier
    (`TagDirectoryTests.cs`, `TagEditorFormTests.cs`): `FakeTagWriteService` updated to return
    `Task<TagSaveResult>` / `Task<string?>`. All cells (F11 L2/L3/L3.5) remain Stage 5 — the changes
    are additive corrections to already-Stage-5 code; no regression found.

  **WU-L5Pilot Stage note — L5 (2026-07-04):**

  Built as the project's first Layer-5 surface (the `layer5-wasm.md` battle-test pilot):
  `Server/Tags/TagEndpoints.cs` (`/api/tags` group: directory/by-type/chips reads public;
  POST/PUT/DELETE writes rely on the service's `RequireMod` gate, endpoint translates
  `TagValidationException`→400-with-`ProblemDetails.Detail`, `UnauthorizedAccessException`→403,
  `KeyNotFoundException`→404 — all as **bodied** `Results.Problem`, since body-less error results
  get re-executed by `UseStatusCodePagesWithReExecute` with the original HTTP method and surface
  as 405). `Client/Tags/ClientTagWriteService : ClientTagReadService` mirrors the server
  inheritance and rethrows the same typed exceptions from status codes, so `TagDirectoryDesktop`'s
  existing catch-and-display works unchanged in WASM.

  **How verified (2026-07-04):** **Integration** (`TagEndpointsTests`, 10 tests via
  `Factory.CreateClient()`): directory grouping, enum-from-query + repeated-`ids` binding with
  order preservation, 403 non-mod, 200 create + DB row, 400 duplicate with detail message, 400
  route/body mismatch, 404 unknown tag, 204 delete + row gone, 400 in-use delete. **Unit**
  (`ClientTagServiceTests`, 11 tests, canned `HttpMessageHandler`): URL/verb shapes, blank-term
  short-circuit, `TagSaveResult`/JSON-null round trips, 400→`TagValidationException` (message from
  `ProblemDetails.Detail`), 401/403→`UnauthorizedAccessException`, 404→`KeyNotFoundException`,
  unmapped→`HttpRequestException`. **Browser (L4.5 band):** full mod CRUD driven on the live WASM
  island — create with sprite-warning advisory, duplicate-name inline error, confirmed delete —
  each mutation checked against psql ground truth.

## Feature 12 — Story Tagging
- **L1 — Stage 5.** `StoryTag`, `StoryCharacter`, `StoryCharacterPairing` (renamed from
  `StoryCharacterRelationship` in WU37), `SettingDetail` all present with priorities and pairing-type
  conversion. *Note:* the spec's SQL-Server-era `TR_StoryCharacters_EnforceOCLogic` trigger is
  superseded by service-layer `StoryValidationException` (WU37 settled decision — see below).
  **L1 additions in WU37:** `Tag.AllowSettingDetails`; `StoryCharacterPairing` rename + `PairingType`
  field + `CharacterPairingType` enum; new `StoryCharacterPairingMember` first-class join; `UNIQUE(StoryId,
  BaseTagId)` on `SettingDetail`; `TagChipDto.AllowSettingDetails`.
- **L2 — Stage 5. L3-Logic — Stage 5. L3.5-Structure — Stage 5. L4 — Stage 1** (pending human visual
  sign-off on sub-component styling). **L5 — Stage 5. L6 — Stage 5 (WU-L6, 2026-07-07 — resolved
  as already-covered, no DDL: the tag-filter probes are correlated EXISTS on `(story_id, tag_id)`
  = the `story_tags` PK, and no probe reads `priority`; the proposed reverse composite
  `(tag_id, story_id) INCLUDE (priority)` was REJECTED under R4 — measured neutral, the PK was
  already optimal. `layer6-indexes.md` §"Rejected").**

  **WU37 Stage notes (2026-06-25):**

  **L2 — how verified:** Built write path (Phase 2): `ServerStoryWriteService` extended to route
  characters → `StoryCharacters`, settings → `StoryTag`+`SettingDetail`, pairings → `StoryCharacterPairing`+
  `StoryCharacterPairingMember`, flat types → `StoryTag`. Validation in `StoryValidations.cs`: OC gate
  (rejects `IsOc=true` on tag with `AllowOCDetails=false`), SettingDetail gate (`AllowSettingDetails`),
  ContentWarning priority coercion to Primary, pairing-member count ≥2, pairing-members in story's
  character set. `GetStoryForEditAsync` hydrates structured collections. `ApplyFilters` partitioned by
  tag type — Character ids match `s.StoryCharacters.Any(...)`, all others match `s.StoryTags.Any(...)`.
  `ServerTagReadService.GetTagChipsByIdsAsync` extended with `AllowOCDetails` and `AllowSettingDetails`
  fields for UI gating. `Story : IEditableStoryProperties` removed (was causing EF Core shadow-nav
  registration of `StoryCharacterPairings`, making `Include(s => s.StoryCharacterPairings)` throw
  `InvalidOperationException`).
  - **Unit** (`StoryTaggingValidationTests.cs` + existing mapper unit tests, 434 total green): OC gate
    reject/allow, SettingDetail gate reject/allow, ContentWarning coercion, pairing member count,
    pairing member not in story.
  - **Integration** (`StoryTaggingTests.cs`, new; 12 tests): character routing (StoryCharacters not
    StoryTags), CW priority coercion, OC gate reject/allow, SettingDetail gate reject/allow, pairing
    member count < 2 throws, pairing member not in story throws, pairing persistence (StoryCharacterPairing
    + StoryCharacterPairingMembers), GetStoryForEditAsync round-trip, discovery character filter via
    StoryCharacters, sanity-check that character is absent from StoryTags. 348 integration tests green.

  **L3/L3.5 — how verified:** Built Phase 3 authoring UI: `StoryPropertiesViewModel.cs` replaced single
  `SelectedTags` list with four structured collections (`SelectedFlatTags`, `SelectedCharacters`,
  `SelectedSettingDetails`, `SelectedPairings`). New presentational sub-components (no `@inject`,
  bUnit-testable): `CharacterEntry.razor` (Priority select, OC checkbox gated by `AllowOCDetails`,
  OcName/OcBio when IsOc), `SettingEntry.razor` (Name + Description inputs), `PairingBuilder.razor`
  (member toggle buttons, Romantic/Platonic radio, Priority select, Add/Remove). `StoryPropertiesForm.razor`
  rebuilt with per-type chip lists, structured state dictionaries, `RebuildViewModel()` pattern, `@key`
  directives on selectors for programmatic removal. `StoryEditorPage.razor` updated with 4 new init
  parameters and structured DTO mapping on submit.
  View-page display (Phase 6): `StoryDetailsDTO` extended with `Characters` and `Pairings` collections
  + display records `CharacterDisplayEntry` / `PairingDisplayEntry`. `GetStoryByIdAsync` projection
  extended to include character and pairing data with sprite resolution. `StoryDesktop.razor` and
  `StoryMobile.razor` render OC character names and ship pairing pills in the metadata section.
  - **RazorComponents** (`CharacterEntryTests.cs`, 8 tests; `PairingBuilderTests.cs`, 5 tests): chip
    name renders, priority select present, OC toggle gated by AllowOCDetails, OC fields gated by IsOc,
    Remove fires callback, priority change fires OnChanged, pairing add UI visible only with 2+ chars,
    existing pairings show member names, Remove fires OnPairingsChanged, Add button disabled with no
    members. 440 RazorComponents tests green.
  - **Visual sign-off:** pending human check (L4 stays Stage 1).

  **Settled for WU37 (2026-06-25, do not revisit):**

  - **Per-story routing table.** Every tag type's per-story association is differentiated:

    | Tag type | Per-story target |
    |---|---|
    | Genre, ContentWarning, CrossoverFandom | `StoryTag` (flat) |
    | Setting | `StoryTag` + optional `SettingDetail` side-row |
    | Character | `StoryCharacter` (replaces `StoryTag`; OC payload + pairing anchor) |
    | Pairing (ship) | `StoryCharacterPairing` + `StoryCharacterPairingMember` join |

    Character leaves `StoryTag` for one reason: pairings need a stable surrogate PK
    (`StoryCharacterId`) to anchor to. `TagTypeEnum.Relationship` is removed (last value; no renumber).

  - **Naming disambiguation (WU37 Phase 1).** The existing `StoryCharacterRelationship` entity is
    renamed `StoryCharacterPairing` to eliminate the near-collision with Feature 10's unrelated
    story-to-story link (renamed `StoryLineage`/`StoryLineageType` in WU42, 2026-07-12 — formerly
    `StoryRelationship`/`StoryRelationshipType`). Other renames in the same pass:
    field `RelationshipType` → `PairingType`; enum `CharacterRelationshipType` → `CharacterPairingType`;
    nav `StoryCharacter.StoryCharacterRelationships` → `StoryCharacterPairings`; the implicit EF
    shadow join table `StoryCharacterStoryCharacterRelationship` is replaced by a first-class named
    entity `StoryCharacterPairingMember` (`StoryCharacterPairingId` + `StoryCharacterId`, composite PK).
    Feature 10's entity was untouched by WU37 (it got its own rename later, in WU42).

  - **`AllowSettingDetails` gap (closed in WU37 L1).** `Tag_Design_Deliberations.md` §7 calls for a
    gate parallel to `AllowOCDetails`: Setting/AU tags only; gates `SettingDetail` creation. Add
    `Tag.AllowSettingDetails (bool, default false)` + `TagChipDto.AllowSettingDetails`; coerce `false`
    for non-Setting types in `TagConfiguration`; surface in `TagEditorForm` for Setting tags only.
    `UNIQUE(StoryId, BaseTagId)` on `SettingDetail` also added here (currently missing from config).

  - **Priority — 2-value, Primary default.** Keep existing `TagPriority { Primary=0, Supporting=1 }`.
    No `None` value, no renumber migration. Primary is the default. ContentWarning gets no priority
    picker and its priority is coerced to `Primary` at service layer.

  - **OC workflow.** `IsOc = true` is legal only where `Tag.AllowOCDetails = true` (the gate set in
    WU27.5). OC display: "OC Bulbasaur \*" with tooltip when `OcName` is populated. Sprite always
    from the base tag — no custom OC sprite uploads. `OcName` max 128, `OcBio` max 2048 chars.

  - **Enforcement is service-layer only.** The spec's `TR_StoryCharacters_EnforceOCLogic` trigger is
    SQL-Server-era framing — not implemented and not planned. All legality rules (`IsOc` gate,
    `SettingDetail` gate, ContentWarning priority coercion, pairing members ≥2 and from this story's
    characters) are enforced via `StoryValidationException` in `ServerStoryWriteService`. A DB CHECK
    is an optional post-MVP defense-in-depth.

  - **Discovery filter branch.** `ApplyFilters` in `ServerStoryReadService` currently filters all
    tag types through a single `s.StoryTags.Any(st => st.TagId == tid)` predicate. After Character
    leaves `StoryTag`, that branch finds nothing for character filters. Fix in WU37 Phase 2: partition
    `IncludedTagIds` / `ExcludedTagIds` by `TagTypeId` (carried on `StoryFilterDto`); Character ids →
    `s.StoryCharacters.Any(sc => sc.CharacterTagId == id)`; all others → `s.StoryTags.Any(...)`.
    See `audit/Discovery.md` Feature 31 and `layer2-services.md` "Structured Tag Authoring."

  - **Fanonize notify/migrate (§14) — deferred.** The `TagUpdateSuggestion` notification flow
    (when a mod flips `IsFanon = true`, notify authors whose `OcName` matches the newly-fanonized
    tag's `TagName` + offer migration) depends on `OcName` + author-facing UI built in WU37. The
    seam exists (`NotificationTypeEnum.TagUpdateSuggestion = 26`, already seeded); the workflow
    lands in its own future WU after WU37.

  **Open for WU37 opusplan:**
  - New sub-components for character wrapper, setting wrapper, and pairing builder (names, parameter
    contracts, ViewModel layout).
  - Edit-mode hydration DTO shape for structured collections (`StoryCharacters`, `SettingDetails`,
    `StoryCharacterPairings`).
  - `StoryPropertiesForm.razor` rebuild approach — wrapper-per-pick vs. per-type section.

## Feature 13 — Tag Display & Sprites
- **L1 — Stage 5.** `SpriteIdentifier` URL-builder key on `Tag`.
- **L2 — Stage 5 (WU3, 2026-06-20).** Renamed `ITagRetrievalService` → `ITagReadService`
  (`Core/Tags/`); added `ServerTagReadService` (`Server/Tags/`, primary-constructor DI over
  `ReadOnlyApplicationDbContext`, `.Select()` projection to `TagDropDownDTO`); registered
  `AddScoped<ITagReadService, ServerTagReadService>()` in `Server/Program.cs`. Updated the two
  existing injectors (`TagSelector.razor`, `StoryPropertiesForm.razor`) to the new type. No Client/L5
  impl yet — server-only per the MVP InteractiveServer-only decision; deferred to post-MVP L5 batch.
  **Verified:** `dotnet build` green (4 projects); zero remaining `ITagRetrievalService` references;
  live server boot clean (DI resolved, no startup throw), `/`, `/Account/Login`, `/Account/Register`
  all `200`.
  **2026-06-22 (WU12.5 backfill):** verification migrated into asserted tests — `TagReadServiceTests`
  in `TheCanalaveLibrary.Tests.Integration` (tier: **Integration**). Seeds Guid-suffixed `Tag` rows;
  all assertions relative (shared-accumulating-state safe). Covers: `SearchTagChipsAsync` — empty/
  whitespace → `[]`; ILike case-insensitive match; alphabetical order; `MaxSearchResults` cap (10);
  `SpriteUrl` null when `SpriteIdentifier` null; type-filter exclusion. Also covers
  `GetAllGenreTagsAsync` relative order and type exclusion. `dotnet test` green.
- **L3-Logic / L3.5-Structure / L4-Style — Stage 5 (WU4, 2026-06-21).** Built `TagChip`
  (`SharedUI/Tags/TagChip.razor`) as a pure leaf: `[Parameter, EditorRequired] TagChipDto Tag` +
  `[Parameter] EventCallback OnRemove` (display-only when `OnRemove` has no delegate, per §5.30.4).
  Injects no service — `SpriteUrl` arrives pre-resolved on the DTO. Visual: `rounded-full`, internal
  padding only (`px-2 py-0.5`, Outer Margin Rule honored — parent spaces chips via `gap-`),
  type-based background/text color per `TagTypeEnum` (table in `layer4-style.md` Pattern
  Accumulation), `title` tooltip from `Tag.Description`, optional sprite `<img>`, X button gated on
  `OnRemove.HasDelegate`. No producing read service exists yet (lands WU11/WU12-13), so no real
  caller — superseded the old inline Bootstrap `badge bg-primary` rendering inside `TagSelector`
  conceptually, but `TagSelector` itself is untouched (its WU11 rebuild is what will actually call
  `TagChip`).
  **Verified:** `dotnet build` green (4 projects); manual visual check via a throwaway demo harness
  on `HomeDesktop.razor` (all six `TagTypeEnum` colors distinguishable, Bulbasaur sprite renders via
  `ISpriteReadService.GetSpriteUrl("pokemon", "bulbasaur", false)`, tooltip on hover, X button only on
  the two chips given `OnRemove` and removes correctly, no doubled spacing) — user-confirmed working
  against the live server. Demo harness is throwaway, to be removed once WU11/WU13 wire a real caller.
  **2026-06-22 (WU12.5 backfill):** verification migrated into asserted tests — `TagChipTests` in
  `TheCanalaveLibrary.Tests.RazorComponents` (tier: **RazorComponents**). Covers: tag name renders;
  all six `TagTypeEnum` background classes (Theory); sprite `<img>` present/absent; `Description` as
  `title`; remove button present only with `OnRemove` delegate; click invokes callback. `dotnet test`
  green.
- **L5 — Stage 5 (WU-L5Pilot, 2026-07-04).** The tag-display read path now runs in WASM:
  `Client/Tags/ClientTagReadService` (HttpClient over `Server/Tags/TagEndpoints.cs` — supersedes
  the L2 note's "no Client/L5 impl yet"), registered in `Client/Program.cs`. Sprite resolution in
  WASM works via `ThemeContextProvider` **moved to `SharedUI/Sprites/`** (islands can't receive
  Routes.razor's cascade) + `AddAuthenticationStateSerialization(SerializeAllClaims = true)` so
  the theme claims reach the client. **How verified:** Unit tier `ClientTagServiceTests` (URL
  shapes, deserialization, blank-term short-circuit); Integration tier `TagEndpointsTests` (read
  endpoints incl. binding); browser band — `TagChip` sprites render on the live `/tags` WASM
  island with the full `onerror` fallback chain (animated 404 → static 200), byte-identical to
  server rendering. `TagSelector`'s typeahead path in WASM remained untested-in-browser at this
  point (superseded 2026-07-13: WU-GlobalFlip took F14 L5 to Stage 5 — see Feature 14's L5 line).

## Feature 14 — Tag Filtering & Selection UI
- **L1 — N/A.**
- **L2 — Stage 5 (WU3, 2026-06-20; extended WU11, 2026-06-21/22).** Shared `ITagReadService`
  contract. WU11 added `SearchTagChipsAsync(TagTypeEnum type, string term)` — a capped (`Take(10)`),
  per-keystroke search method on `ServerTagReadService`, filtering via `EF.Functions.ILike` (Npgsql
  doesn't translate the `string.Contains(string, StringComparison)` overload — caught at build/runtime,
  not assumed), returning `List<TagChipDto>` with sprites resolved post-materialization via
  `ISpriteReadService.GetSpriteUrl` (see `layer2-services.md` §"Per-keystroke typeahead search…").
  Additive — `GetTagsByTypeAsync` and friends unchanged for non-chip callers.
- **L3-Logic / L3.5-Structure — Stage 5 (WU11, 2026-06-21/22).** Rebuilt around single-select
  **Blazored.Typeahead** 4.7.0 (not the package's multi-select — chips sit *above* the input per
  §5.30.4, the package renders them inside), sourced by `SearchTagChipsAsync`. Selected chips are
  `TagChip` leaves; dropdown rows are lightweight (color dot + sprite + name). Contract is
  `EventCallback<IReadOnlyList<TagChipDto>> OnSelectionChanged` — **not** the spec's literal
  `IReadOnlyList<Tag>` (DTO Firewall forbids the EF entity crossing into UI). Selector stays
  type-scoped (one `TagTypeEnum` per instance); `Priority`/`StoryTag` mapping is the consuming form's
  job (WU24). Canonical snippet in `layer3.5-structure.md` "Third-Party Wrapper Composite".
  **Real bug found and fixed during verification:** `BlazoredTypeahead` requires a `SelectedTemplate`
  parameter — omitting it throws `InvalidOperationException` in `OnInitialized()`, which terminates
  the Blazor Server circuit immediately (symptom: page frozen on the SSR-prerendered markup forever,
  field permanently unresponsive — *not* a typing/focus bug). A secondary `NullReferenceException` in
  `BlazoredTypeahead.Dispose()` was a downstream symptom of the same half-initialized state, not a
  separate prerendering incompatibility — disappeared entirely once `SelectedTemplate` was supplied.
  Detail and the corrected canonical snippet are in `layer3.5-structure.md` "Third-Party Wrapper
  Composite" (an earlier mid-build note misdiagnosed this as a prerender-incompatibility issue
  requiring an `IsInteractive` guard; that guard was removed once the real cause was found).
- **L4-Style — Stage 5 (WU11, 2026-06-21/22).** No outer margin (the discarded `mb-4` was the
  violation that motivated the rule); dot-color table in `layer4-style.md` Pattern Accumulation;
  package's own CSS skeleton kept as-is (see `layer4-style.md` "Blazored.Typeahead Stylesheet").
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; TagSelector typeahead search/select verified under WASM for
  the first time — on the NEW in-house `CanalaveTypeahead` (Blazored.Typeahead removed after it
  crashed the WASM renderer; see workplan). Full wave narrative + the 7 bugs found/fixed:
  `workplan.md` WU-GlobalFlip.
- **Verified:** `dotnet build` green (4 projects, 0 errors); live server run (`run-server` skill),
  homepage `200`, no exceptions in server log across multiple boot/request cycles. User-confirmed
  visual + interactive check against the live server via a throwaway `HomeDesktop.razor` harness
  (two `TagSelector` instances, Character + Genre, backed by 7 throwaway fixture `Tag` rows inserted
  directly via `psql`): debounced dropdown with dot+sprite+name rows, selecting clears the input and
  adds a chip, already-selected tags excluded from further results, chip X removes and updates the
  selection callback, no doubled spacing. Harness and fixture rows removed after confirmation.
  **2026-06-22 (WU12.5 backfill):** verification migrated into asserted tests — `TagSelectorTests` in
  `TheCanalaveLibrary.Tests.RazorComponents` (tier: **RazorComponents**). Uses `FakeTagReadService`
  (empty results) + `JSInterop.Mode = Loose` (Blazored.Typeahead makes JS focus calls). Covers: label
  renders; pre-selected chips render; empty initial → no chips; removing a chip fires
  `OnSelectionChanged` with updated list; removed chip disappears from markup. The add-via-typeahead
  flow (keyboard input → debounce → server search → selection) requires JS simulation beyond bUnit's
  scope — that path is covered by `TagReadServiceTests` (Integration tier) + manual interactive check;
  the circuit-killing SelectedTemplate bug is covered by `TagSelector` rendering at all in these tests.
  `dotnet test` green.

## Feature 15 — Saved Tag Selections

**Scope settled (WU43 planning, 2026-07-11, do not revisit):** a Saved Tag Selection's only job is to
populate the tag include/exclude axis of a discovery filter. It is **not** a saved query — it never
carries free-text search, sort order, interaction exclusions, or AND/OR include-mode; those are
per-user/per-surface concerns owned elsewhere (interaction exclusions already persist via
`UserStoryInteractionFilterSetting` per `(User × SearchMode × filter-kind)`, merged by
`IDiscoveryDefaultsReadService`; sort/text are transient viewer intent — see `layer2-services.md`
§"Saved Tag Selections Persist Only the Tag Axis"). **ONE unified selection spans all tag types**
(Genre + Character + Setting + …), not a per-type artifact — `TagFilter`'s per-type `TagSelector`s are
purely a type-scoped input affordance; the emitted `TagFilterSelection`/`StoryFilterDto` already flatten
tags across types, and the frozen L1 entry is a flat `(TagId, IsExcluded)` row.

Settled additively against the Stage-5 L1 (two new columns, no other L1 change):
- `SavedTagSelectionEntry.IsExcluded` (bool, default false) — captures both the include and exclude axis,
  where the frozen model only had include (bare `TagId`).
- `SavedTagSelection.Description` (`[MaxLength(280)]`, nullable) — bounded plain text, not rich HTML (it's
  semantic metadata/caption, not authored content — no EditorView/RichTextView/sanitize pipeline). Shown
  both in the load flyout (notes-to-self) and on the profile tab (sharing caption).
- **No per-user cap** (unlike Vouch's 5) — primary use is personal bookkeeping of arbitrary tag
  combinations; unbounded is intended.
- **Load and Save are separate UI surfaces** mounted once in `TagFilter`'s header (so all four
  `ResultsFilterPanel` consumers — `/discover`, Tree Search, Bookshelves, Profile story tabs — get them
  for free): a searchable/sortable **Load flyout** (destructive replace of on-screen tags; nickname
  text-filter + sort by nickname/date-created, default DateCreatedDesc overridable via a
  `ReaderSettings.SavedTagSelectionSort` preference; full ⋯ row menu — overwrite/rename/publish/delete)
  and a compact **Save dialog** (capture current tags as new). Both hidden for anonymous viewers.
  Deliberately not combined into one surface — load (destructive) and save (additive) are opposite
  operations; a mutating save form inside the overwrite-driven list was assessed as unneeded fragility.
- **Sharing = copy-on-write** (original Gemini-era decision, carried forward): `IsPublic=true` + a
  dedicated `ProfileTab.TagSelections` tab (like Recommendations) — **no public browse/gallery surface**.
  "Add to my filters" on someone else's public selection creates an independent owned copy
  (`CopyPublicSelectionAsync`); editing/deleting either side never affects the other.
- **Permalink (decision row 13, 2026-07-28).** Each public selection is additionally addressable at
  `/discover/selection/{SelectionId:int}/{*Slug}` — the story-slug contract, so **the id is the
  source of truth and the slug is a decorative tail that is never parsed** (no slug column, no
  migration; renaming a selection never breaks a link). The permalink lands the visitor in
  `SearchPage` with the tag axis pre-seeded and fully editable, which is what makes a shared
  selection *runnable* rather than merely visible. **A permalink is not a browse surface**: there is
  still no gallery, no cross-user index, and selections stay out of the sitemap under
  WU-AccessGate2's "rearrangements/navigation out" paradigm (`audit/Seo.md`). Gated on **both**
  `IsPublic` and the owner's `ProfileVisibility`, server-side (Class A —
  `design/access-gating-first-principles.md`).
- **L6 indexes required**: the table aggregates every user's rows and queries are always
  `UserId`-scoped with sort as a first-class concern — `(UserId, DateCreated)` +
  `(UserId, IsPublic)` alongside the existing unique `(UserId, Nickname)`. See `layer6-indexes.md`.

**L1 — Stage 5** (extended additively via `WU43_SavedTagSelectionExcludeAndDescription`; the
permalink needed **no** L1 change — see WU-SelectionPermalink below).
**L2/L3-Logic/L3.5-Structure — Stage 5** (WU43, verified below; extended by WU-SelectionPermalink,
2026-07-28). **L4-Style / L4.5-Browser — Stage 1**
(pending visual/live-browser sign-off, WU8/WU13/WU23 precedent). **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13; permalink read + its client twin added 2026-07-28)** — endpoints + client impl live (WU-L5Sweep) and the
site now runs global InteractiveAuto; the saved-selection flyout fetch is interactive-only and was
not browser-driven in the flip's wave. Full wave narrative + the 7 bugs found/fixed: `workplan.md`
WU-GlobalFlip. **L6 — Stage 5** (two new indexes, see `layer6-indexes.md` "Saved Tag
Selections").

### WU-SelectionPermalink Stage note (2026-07-28) — F15 L2/L3-Logic/L3.5/L5 stay Stage 5

Decision row 13's sharing half (`roadmap.md` §Resolved). A public selection is now addressable at
`/discover/selection/{SelectionId:int}/{*Slug}` and, more to the point, **runnable**: before this,
the profile tab could show a shared selection but offered no way to see its stories, and nothing at
all to an anonymous visitor.

**No L1 change, deliberately.** The route follows the story-slug contract — `StoryPage` is
`/story/{StoryId:int}/{*StorySlug}` — so the **id is the source of truth and the slug is a
decorative tail that is never parsed**. Links render `StorySlug.Slugify(Nickname)` from the
already-loaded nickname, which means renaming a selection never breaks a link someone shared, and no
slug column, uniqueness scan or migration is needed. (Nickname→slug is not injective — "Fluff!" and
"Fluff?" both slugify to `fluff` — which is precisely why the slug must not be the lookup key.)
Unlike stories there is no canonical 301: selections are out of the sitemap, so a stale tail is
simply ignored.

**Access control — the load-bearing part.** `GetSelectionDetailAsync` is authenticated
(`RequireAuthorization()`), so the permalink needed a **new** anonymous-callable read,
`GetPublicSelectionByIdAsync`, enforcing **both** gates in the service (single enforcement point;
the endpoint only translates):
1. `IsPublic` — stricter than `GetSelectionDetailAsync`'s owner-or-public rule: a private selection
   is unreachable by link **even for its owner**, matching `GetPublicSelectionsByUserAsync`, which
   likewise excludes private rows from the owner's own tab. A link exists to be shared.
2. the owner's `ProfileVisibility` — **Class A** (`design/access-gating-first-principles.md`
   §1b: private/UsersOnly profile-tab data must respect it server-side, adversary model applies). A
   permalink is just another path to profile-tab data.

Missing, unpublished and not-visible all return the same contractual `null`, and the page renders
one neutral notice for all three — distinguishing them would make the permalink an existence oracle
for private profiles.

**Sort/text/interaction exclusions stay the viewer's own** §8.7 defaults; the artifact contributes
the tag axis alone. That is what keeps a permalink from turning a saved *tag selection* into a saved
*query* — F15's scope ruling is untouched (`layer2-services.md` §"Saved Tag Selections Persist Only
the Tag Axis", extended with this rule).

**Ships remain out**, and not merely unimplemented: `SavedTagSelectionEntry` is a flat
`(SelectionId, TagId, IsExcluded)` row unique on `(SelectionId, TagId)`, while a ship is a *group*
of 1–3 member ids plus a pairing type. Flattening degrades it to co-presence — which WU-TagFanon
ruled is **not** a ship — and the unique constraint forbids one character appearing in two ships.
Admitting ships would need new child tables and a reopened F15 scope; recorded as a known future
decision, not a silent deferral.

**Surfacing:** profile Tag Selections cards link to the permalink (title + a "See these stories →"
action). The banner offers "Add to my filters" to logged-in non-owners via the ratified
`<AuthorizeView>` DI split (`SelectionPermalinkBanner` injection-free → `SelectionAdoptButton` holds
the write service), and a log-in link to anonymous viewers rather than a missing control. Still **no
gallery, no cross-user index, and out of the sitemap** under WU-AccessGate2's
"rearrangements/navigation out" paradigm.

**How verified:** `dotnet build` green; `dotnet test` green (2,330 total). **Integration**
(`SavedTagSelectionServiceTests`, 5 new cases, denial-first): public+visible returns for anonymous;
private returns null for owner, other user and anonymous; public-but-Private-profile returns null
for anonymous and other users; `UsersOnly` hides from anonymous but not from a logged-in viewer;
missing id is indistinguishable. **RazorComponents** (`SearchPageTests`: banner renders and seeds
the tag axis, panel stays editable, unavailable notice, slug ignored, bare `/discover` has no
permalink chrome). **Manual band:** browser pass 2026-07-28 — followed the link from the profile
tab, confirmed a stale slug still resolves, confirmed the anonymous view, then flipped the owner's
`ProfileVisibility` to `Private` in Postgres and reloaded the same URL: identical neutral notice, no
nickname/description/tag leak. Full browser narrative: `audit/Discovery.md`
§"WU-DiscoveryFilterRestore + WU-SelectionPermalink note".

### WU43 Stage-5 verification note (2026-07-11)

`dotnet build` full solution green, 0 warnings/errors. `dotnet test` full suite green: 585 Unit + 564
RazorComponents + 516 Integration = 1665 total. Covering tiers:
- **Unit** — `SavedTagSelectionValidationsTests`: `CanSave` rules (empty nickname/description-length/
  empty-tag-set/duplicate-nickname), `DisambiguateCopyNickname` (first collision, escalating suffix,
  case-insensitivity, truncation at `MaxNicknameLength`).
- **Integration** (`SavedTagSelectionServiceTests`, Testcontainers Postgres) — create/update/delete
  round-trip with owner gating; `IsExcluded` persisted on both axes; `UpdateAsync` replaces entries
  wholesale (not a merge); per-user duplicate-nickname rejection (case-insensitive) vs. same-nickname-
  different-user success; all four `GetMySelectionsAsync` sort orders; `GetSelectionDetailAsync`'s
  owner-or-public visibility gate; `GetPublicSelectionsByUserAsync` filters correctly;
  `CopyPublicSelectionAsync` — independent copy ownership, editing/deleting either side never affects
  the other, nickname-collision disambiguation (verbatim-if-no-collision, `(copy)`/`(copy N)`
  escalation), rejection of a private non-owned source, and owner-copying-own-private-selection
  success; `SavedTagSelection.UserId` Cascade verified via `UserDeletionService.DeleteUserAsync`
  (entries cascade too; the referenced `Tag` survives).
- **RazorComponents** (`SavedTagSelectionLoadFlyoutTests`, `SavedTagSelectionSaveDialogTests`,
  `TagFilterTests`) — hidden for anonymous viewers; list render + nickname text-filter + no-matches
  state; Apply raises `OnApply` with hydrated chips *and* visually remounts every `TagSelector` (the
  `@key`-generation fix, proving it actually works end-to-end, not just compiles); Delete via the
  nested `ConfirmDialog`; Save disabled when the on-screen tag set is empty or nickname is blank;
  `SavedTagSelectionValidationException` surfaced via `InlineAlert` without closing the dialog.

**Real issues found and fixed during this pass, not anticipated in the plan:**
1. A first draft of the copy-on-write disambiguation test assumed nickname collision was checked
   against the *disambiguated* form ("Fluff (copy)") rather than the source's literal nickname
   ("Fluff") — the actual (correct) service behavior is verbatim-copy-unless-the-source-nickname-
   itself-collides. Fixed the test, not the service; documented the exact semantics with three
   dedicated test cases instead of one incorrect one.
2. **`@inject`-declared properties resolve at component construction time, unconditionally — a
   same-component `<AuthorizeView>` around markup does not defer it.** `SavedTagSelectionLoadFlyout`/
   `SaveDialog` originally declared their services at file-top with `<AuthorizeView><Authorized>` only
   around markup; this broke nine pre-existing bUnit files that render `TagFilter`/`ResultsFilterPanel`
   (`SearchDesktop/MobileTests`, `BookshelvesDesktop/MobileTests`, `TreeSearchDesktop/MobileTests`,
   `ResultsFilterPanelTests`, `ProfilePageTests`) with a missing-service `InvalidOperationException`,
   even though those tests default to an anonymous/unauthorized context. Fixed by splitting each into
   a thin injection-free wrapper (`<AuthorizeView><Authorized><…Inner/></Authorized></AuthorizeView>`)
   plus an `…Inner` component holding the real markup and `@inject` — the inner component is a genuine
   child of the `Authorized` `RenderFragment`, so it's only ever constructed (and only ever injected)
   when `AuthorizeView` has actually decided "authorized." The nine pre-existing files then needed only
   `this.AddAuthorization()` (defaulting anonymous), no saved-selection service fakes at all — proving
   the fix. Full mechanism recorded in `layer3-logic.md` §"Deferring DI Behind AuthorizeView (WU43)".
3. `TagFilter.ApplySavedSelectionAsync` needed a `@key`-based forced remount of every `TagSelector` —
   mutating `_included`/`_excluded` alone doesn't visually refresh a child that only seeds itself in
   `OnInitialized`. Caught by writing `TagFilterTests.ApplyingSavedSelection_ReplacesChipsAndReemitsOnChanged`
   before assuming the naive state-mutation approach would work; documented in `layer3-logic.md`
   §"Forcing a Child to Re-Seed via @key (WU43)".

**Scope note:** following the Series (WU41) / `ReaderSettings.DefaultSearchSort` precedent, no
dedicated `ProfileDesktopTests`/`ProfileMobileTests`/`ReaderSettingsFormTests` files were added for the
presentational-only Tag Selections tab body / sort dropdown — the meaningful logic (copy-on-write,
service calls) is Integration-covered; the tab/dropdown markup itself follows the same untested-by-
convention precedent as Series's tab and the pre-existing sort dropdowns in the same form.

---

### Cluster-level reconciliation
Per audit-summary §0: this was **stale code, not a design to adjudicate**. `TagSelector` was
non-working (its `ITagRetrievalService` had no impl and wasn't registered, so it threw at runtime) —
so the "unless the code is working" exception didn't apply, and the spec (§5.30.4) won outright. The
remaining Stage-4 flags below are trap-warnings, not open questions; treat the existing component as
discardable scaffolding.

`TagSelector` is the clearest example of a Gemini-era component that compiles but won't compose: native
datalist, list mutation, inline badges, outer margin. The build-to-spec path is (a) build the
`ITagReadService` impl + register it — **done, WU3** — (b) extract a `TagChip` leaf (WU4), (c) rebuild
`TagSelector` around Blazored.Typeahead + `OnSelectionChanged` (WU11). That makes it the Phase-1 atom
several other features wait on.

## L4.5-Browser verification (2026-07-01) — F11 + F12 + F13 + F14 → Stage 5, two bugs fixed same-session

- **F11:** as AdminUser on `/tags`, hover ✎/✕ mod controls appear; TagEditorForm modal opens; edited
  Adventure's description and saved (psql-verified). Two bugs fixed:
  1. `TagDirectoryDesktop.razor` passed `ServerError="_editorError"` — missing `@`, so the literal
     text "_editorError" rendered in red on every editor open (string-typed params take attribute
     text literally; the neighboring non-string params compile as expressions — the exact
     `layer3-logic.md` "Razor Attribute Quoting" pitfall recurring).
  2. `TagEditorForm`'s Tag Type `<option>` values were numeric shorts while `@bind` on a
     `TagTypeEnum` property serializes the enum NAME — no option matched, so the select rendered
     blank when editing an existing tag. Options now use `value="@type"`.
- **F12:** structured tagging driven through `/story/new` (character typeahead + priority row,
  setting, genre; persisted and displayed on the story page — see Stories audit note).
- **F13:** TagChip display verified across directory/cards/story pages (type-colored chips).
  Sprite-bearing chips not visually exercised — seeded tags carry no `SpriteIdentifier` (sprite
  URL-building itself is Unit-covered; the visual band belongs to Feature 3's pass).
- **F14:** on `/discover`, genre typeahead → Adventure chip → Apply Filters narrowed the deck to
  exactly the three Adventure-tagged stories; ✕ removal renders. AND/OR toggle + interaction
  filters present (interaction-exclusion semantics exercised under F16/F17's pass).

### WU-AuditFixPass note (2026-07-18)

`TagEditorForm`'s hand-rolled server-error `<p role="alert">` normalized to `InlineAlert`
(MA-405). Full detail: `workplan.md` WU-AuditFixPass.

### WU-AuditFixPass-2 note (2026-07-18)

Endpoint-authz sweep + MA-407, F11 (cells stay Stage 5 — defense-in-depth added): tag write routes gained
the `.RequireAuthorization()` floor they lacked (the service-layer `RequireMod` already covered them;
floor + stale-comment fix). `TagEndpoints`' private `ExecuteWriteAsync` deleted — now uses the shared
`EndpointHelpers`. Covered: `HttpRateLimitTests.TagWrites_*` updated to authenticate as a moderator (the
real caller) to reach the limiter past the new floor. Full detail: `workplan.md` WU-AuditFixPass-2.
