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
`StoryTagDTO`, `IStoryTag`, `TagPriority`.
**Components:** `TagSelector` (`SharedUI/Tags/` — moved out of the legacy `Components/` folder; see
`canalave-conventions/SKILL.md` "Code Organization"). The empty, unused `TagViewModel.cs` that sat
alongside it was deleted in the same move. **The relocation is folder-only — `TagSelector`'s content is
unchanged and remains the discardable scaffolding described below, scheduled for the WU11 rebuild.**

**Fluent config:** `Tag.TagName` unique; self-ref `ParentTagId` SetNull; `TagType`/`Tag` Restrict;
`SavedTagSelection` cascade + unique `(UserId,Nickname)`; `SavedTagSelectionEntry` unique
`(SelectionId,TagId)` + Restrict on Tag.

---

## Feature 11 — Tag Administration
- **L1 — Stage 5.** `Tag` shape matches §5.16 (curated, staff-only, hierarchy, sprite key, OC flag,
  tooltip description). Sound. **L2 — Stage 2** (no admin/write service). **L3/L3.5 — Stage 2**
  (mod CRUD behind `AuthorizeView` on Tag Directory). **L4 — Stage 1. L5 — Stage 2. L6 — Stage 2.**

## Feature 12 — Story Tagging
- **L1 — Stage 5.** `StoryTag`, `StoryCharacter`, `StoryCharacterRelationship`, `SettingDetail` all
  present with priorities and relationship-type conversion. *Note:* OC-detail enforcement is spec'd as a DB
  **trigger** (when `AllowOCDetails`), which is a manual migration edit — not present (no migrations exist).
  Tracked as a Layer-1 follow-up, not a downgrade.
- **L2 — Stage 2.** No tagging write service. **L3/L3.5 — Stage 4** (via `TagSelector`, see Feature 14).
  **L4 — Stage 1. L5 — Stage 2. L6 — Stage 2.**

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
- **L3-Logic / L3.5-Structure — Stage 4.** No `TagChip` leaf; tags are rendered as inline Bootstrap
  `badge bg-primary` spans inside `TagSelector`, with no sprite resolution and no type-based coloring.
- **L4-Style — Stage 1** (blocked).

## Feature 14 — Tag Filtering & Selection UI
- **L1 — N/A.**
- **L2 — Stage 5 (WU3, 2026-06-20).** Same fix as Feature 13 — shared `ITagReadService` contract.
- **L3-Logic — Stage 4.** `TagSelector` diverges from §5.30.4 on several counts: uses a native HTML
  `<datalist>` autocomplete instead of **Blazored.Typeahead** (not referenced anywhere); **mutates the
  passed `AllSelectedStoryTags` list directly** instead of raising
  `EventCallback<IReadOnlyList<Tag>> OnSelectionChanged`; no 300ms debounce; switches to a checkbox grid
  under a threshold (its own invention). It is a coordination composite injecting a read service for
  user-input-driven queries (legitimate), but the contract is wrong.
- **L3.5-Structure — Stage 4.** Dropdown rows should be lightweight (color dot + sprite + name), not
  full chips; selected chips should be `TagChip` leaves. Also carries `mb-4` on its root `<div>` —
  **outer-margin-rule violation** (`layer4-style.md`).
- **L4-Style — Stage 1** (blocked). **L5 — Stage 2.**

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
