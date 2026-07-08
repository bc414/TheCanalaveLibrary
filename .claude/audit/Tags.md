# Audit — Tags/

**Features:** 11 (admin), 12 (story tagging), 13 (display & sprites), 14 (filtering/selection UI),
15 (saved selections).

## Shared Context

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
`StoryCharacterStoryCharacterRelationship`. Feature 10's unrelated `StoryRelationship` /
`StoryRelationshipType` (story-to-story) are unchanged.

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
    story-to-story `StoryRelationship` / `StoryRelationshipType`. Other renames in the same pass:
    field `RelationshipType` → `PairingType`; enum `CharacterRelationshipType` → `CharacterPairingType`;
    nav `StoryCharacter.StoryCharacterRelationships` → `StoryCharacterPairings`; the implicit EF
    shadow join table `StoryCharacterStoryCharacterRelationship` is replaced by a first-class named
    entity `StoryCharacterPairingMember` (`StoryCharacterPairingId` + `StoryCharacterId`, composite PK).
    Feature 10's `StoryRelationship` / `StoryRelationshipType` are untouched.

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
  server rendering. `TagSelector`'s typeahead path in WASM remains untested-in-browser (no WASM
  page hosts it yet — that's Feature 14 L5, still Stage 2).

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
  **L5 — Stage 2.**
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
- **L1 — Stage 5.** `SavedTagSelection`/`Entry` with unique constraints and Restrict-on-Tag; copy-on-write
  on share is application logic (unbuilt). **L2/L3/L3.5 — Stage 2.** **L4 — Stage 1. L5 — Stage 2.**

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
