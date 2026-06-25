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
for the `TagChip` leaf; `SpriteUrl` is a server-resolved relative path, not the raw `SpriteIdentifier`
key — see `layer2-services.md` §"Sprite URLs Are Resolved Server-Side, At Projection Time"; request-scoped,
never cached cross-user/theme).
**WU12 follow-on:** `ServerTagReadService.SearchTagChipsAsync`'s WU4/WU11-era hardcoded
`"pokemon"`/`false` theme placeholder (the per-keystroke typeahead's in-memory sprite-resolution step,
`layer2-services.md` §"Per-keystroke typeahead search...") is replaced with the real
`IActiveUserContext.Theme`/`PrefersAnimatedSprites`, now that that context exists. Small, low-risk
follow-on found while building WU12's `IActiveUserContext` — not a re-opened Feature 14 cell.

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
  tooltip description). Sound. **L2 — Stage 2** (no admin/write service). **L3/L3.5 — Stage 2**
  (mod CRUD behind `AuthorizeView` on Tag Directory). **L4 — Stage 1. L5 — Stage 2. L6 — Stage 2.**

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

## Feature 12 — Story Tagging
- **L1 — Stage 5.** `StoryTag`, `StoryCharacter`, `StoryCharacterRelationship`, `SettingDetail` all
  present with priorities and relationship-type conversion. *Note:* OC-detail enforcement is spec'd as a DB
  **trigger** (when `AllowOCDetails`), which is a manual migration edit — not present (no migrations exist).
  Tracked as a Layer-1 follow-up, not a downgrade.
  **L1 gap (WU37 add):** `AllowSettingDetails` flag (parallel to `AllowOCDetails`) is absent from model +
  spec but documented in `Tag_Design_Deliberations.md` §7 — Setting/AU tags only; gates `SettingDetail`
  creation; add in WU37.
- **L2 — Stage 2.** No tagging write service. **L3/L3.5 — Stage 4** (via `TagSelector`, see Feature 14).
  **L4 — Stage 1. L5 — Stage 2. L6 — Stage 2.**

  **Captured-for-later — design from `Tag_Design_Deliberations.md` (WU37 scope, not WU27.5):**

  - **OC workflow (§2, §6):** Characters split into a dedicated `StoryCharacters`/OCs table (stable
    per-story character IDs for relationships; OC override secondary). Columns: `IsOC`, `OC_Name`,
    `OC_Bio`. "OC Bulbasaur \*" display with tooltip when `OC_Name` populated. **Sprite always from
    the base tag** — users cannot upload custom OC sprites (keeps user images out of site-standard
    search cards). `IsOC = 1` legal only where `Tag.AllowOCDetails = true` (generic Pokémon, "OC
    Trainer"/"OC Human" — never named characters like "Ash Ketchum"). Enforcement via DB trigger
    `TR_StoryCharacters_EnforceOCLogic`. **WU27.5 sets the `AllowOCDetails` gate on Tag (Character-type
    only in `TagEditorForm`); WU37 consumes it.**

  - **Priority / "Primary vs Supporting" (§5):** `Priority` TINYINT (0=None, 1=Primary, 2=Supporting)
    on the story↔tag link (already present as `StoryTag.Priority` / `TagPriority`). User-facing labels:
    "Primary" / "Supporting". Applies to Genre, Character, Relationship — **not** ContentWarning (to be
    enforced by a trigger in WU37).

  - **SettingDetails (§7, §8):** `SettingDetail` row for custom setting name/description override (already
    present). Parallel `AllowSettingDetails` gate (Setting/AU tags only — absent from current model +
    spec; add to `Tag` entity + `TagConfigurations.cs` + migration in WU37). Uniqueness constraint
    `UNIQUE(StoryId, BaseTagId)` so a story can hold custom details for multiple settings simultaneously.

  - **Fanonize notify/migrate (§14):** when a mod flips a tag to `IsFanon = true`, the
    `TagUpdateSuggestion` notification (enum value 26, already seeded in `ModelEnums.cs`) fires to
    authors whose `StoryCharacter.OC_Name` matches the newly-fanonized tag's `TagName`. Authors get a
    one-click option to migrate their OC to the official fanon tag. **WU27.5 ships `IsFanon` as a plain
    editable field only; this workflow lands in its own future WU (depends on StoryCharacter.OC_Name +
    author-facing UI).**

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
