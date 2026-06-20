# Audit — Stories/

**Features:** 4, 5, 8, 9, 10, 45 (Story creation/editing, browsing/display, arcs, series, relationships,
view-count). Largest cluster.

## Shared Context

**Entities (Core/Stories/):** `Story` (hot partition — status, counts, dates, FKs), `StoryListing`
(warm — title, short desc, cover art URL; carries the FTS `SearchVector` generated column), `StoryDetail`
(cold — long desc, slug, `PostApprovalStatus`). Vertical-partition trio with 1-to-1 cascade. `Story`
implements `IEditableStoryProperties` via explicit interface implementation (`[NotMapped]` projections
across the three partitions) — a deliberate, somewhat advanced pattern that lets one object satisfy the
edit contract. Also here: `StoryArc`, `Series`/`SeriesEntry`, `StoryRelationship` (+type lookup),
`StoryTag`/`StoryCharacter` live in the model set but belong to Tags.

**DTOs/contracts (Core/Stories/):** `CreateStoryDTO`, `StoryUpdateDTO`, `StoryDetailsDTO`, `StoryTagDTO`,
`IEditableStoryProperties`, `IStoryTag`, `StoryMappers`, `StoryValidations`, `StoryValidationException`.

**Services:** `IStoryReadService` / `IStoryWriteService` (Core) + `ServerStoryReadService` /
`ServerStoryWriteService` (Server, direct-injection pattern per spec §6.6 — see RESOLVED note below) +
`HttpStoryReadService` / `HttpStoryWriteService` / `HttpStoryOverviewService` (Client). `StoryEndpoints`
(Server) maps the API.

**Components (SharedUI):** `StoryPage` (dispatcher, `/story/{StoryId:int}/{Slug?}`),
`StoryDesktop`/`StoryMobile` (stubs), `StoryPropertiesForm` + `StoryPropertiesViewModel`.

**Fluent config:** inline in `ApplicationDbContext.OnModelCreating` — Story FK fan-out (Cascade to owned
collections, Restrict to `StoryStatus`, `SetNull` author anonymization), partition 1-to-1s, FTS computed
column + GIN index `ix_story_listing_search_vector`, slug unique-filtered index.

---

## Feature 4 — Story Creation & Editing

- **L1 — Stage 5.** Partition trio + `IEditableStoryProperties` plumbing is sound and matches spec §4/§7.
  Awaiting migration + build verification (no migrations exist). *Settled:* three-table vertical split;
  slug server-generated; explicit-interface edit contract.
- **L2 — Stage 5.** **RESOLVED (2026-06-20):** this cell's prior "Stage 5, nothing to reconcile" call was
  wrong — `DbStoryWriteService`/`DbStoryReadService` injected `IDbContextFactory<T>`, which was never
  registered in `Program.cs` (only plain `AddDbContext<T>` existed), so DI container validation failed at
  app startup. Surfaced by actually running the Aspire AppHost end-to-end. Per spec §6.6 ("Why Direct
  DbContext Injection over IDbContextFactory" — superseded for thread-safety reasons that don't apply
  under scoped DI), rewrote as `ServerStoryReadService(ReadOnlyApplicationDbContext readDb)` and
  `ServerStoryWriteService(ReadOnlyApplicationDbContext readDb, ApplicationDbContext writeDb) :
  ServerStoryReadService(readDb), IStoryWriteService` — primary-constructor injection, `readDb` private or
  base. Registered via `AddScoped<>` in `Program.cs` (already scoped; only the implementation type names
  changed). *Open:* cover-art upload to R2/MinIO not implemented; slug generation not visible in the write
  path.
- **L3-Logic — Stage 4.** `StoryPropertiesForm` is a correct `EditForm` + `DataAnnotationsValidator` +
  ViewModel + server-error surfacing pattern, but: injects `ITagRetrievalService` (no impl, unregistered),
  has a `@* TODO: tags, cover art *@`, no slug/AdminControls handling. *Disagrees with:* completeness, not
  architecture. Resolution → Stage 2/3 to finish.
- **L3.5-Structure — Stage 4.** Form skeleton exists; missing `TagSelector` wiring, cover-art upload,
  `AdminControls`. Shared create/edit usage (routes `/stories/new`, `/story/{id}/edit`) not yet realized.
- **L4-Style — Stage 1.** Bootstrap (`mb-3`, `form-control`). Blocked on Tailwind/design tokens.
- **L5 — Stage 4.** `HttpStoryWriteService` exists but `StoryEndpoints` maps **no** write endpoints; the
  client calls handlers that don't exist. Reconcile by adding POST/PUT endpoints from the stable interface.
- **L6 — Stage 2.** Story search indexes deferred ("to be added by query need").

## Feature 5 — Story Browsing & Display

- **L1 — Stage 5.** `StoryListing` warm partition is the projection anchor; sound.
- **L2 — Stage 2** (reclassified from 4). `ServerStoryReadService` (renamed from `DbStoryReadService` —
  see Feature 4's L2 RESOLVED note for the `IDbContextFactory` → direct-injection fix, same cell) has
  `GetStoryByIdAsync` (→ `StoryDetailsDTO`) and `GetStoryForEditAsync`, both correct
  `ReadOnlyApplicationDbContext` `.Select()` projections — they *work* and match spec/conventions. The gap
  is unbuilt extension, not divergence: no `StoryListingDto` listing/browse/search projection exists, and
  the content-rating master filter ("mature disabled ⇒ no trace anywhere," §5) is absent. Add the listing
  read path; nothing here to reconcile.
- **L3-Logic — Stage 4.** `StoryPage` is a real dispatcher (device detection + `IStoryReadService`,
  loads `StoryDetailsDTO`, redirects to `/not-found`). Gaps: no `[PersistentState]` ⇒ prerender→interactive
  double-fetch flicker; route is `{Slug?}` not the spec's hybrid catch-all `{*StorySlug}`.
- **L3.5-Structure — Stage 4.** `StoryDesktop`/`StoryMobile` render title + author + short desc + a
  `<RandomNumberGenerator />` placeholder. None of the spec layout (cover → long desc → chapter selection →
  recommendations, §5.28), `StoryCard`, or `StoryDeck` exists.
- **L4-Style — Stage 1.** Bootstrap-ish; blocked on tokens.
- **L5 — Stage 4.** Only `GET /api/stories/{id}` mapped; `HttpStoryReadService.GetStoryForEditAsync`
  calls `/{id}/edit` which is unmapped. Listing endpoints absent.
- **L6 — Stage 2.** Story-centric filtered indexes pending.

## Feature 8 — Story Arcs
- **L1 — Stage 5.** `StoryArc` + unique indexes `(StoryId,Title)`, `(StoryId,SortOrder)`. Overlap/gap
  validation is C#-side (not yet written).
- **L2 — Stage 2.** No arc service.
- **L3 / L3.5 — Stage 1 (conceptual, §8.2).** Arc-management UI was never designed. Resolve in chat.
- **L4 — Stage 1** (blocked). **L5 — Stage 2.**

## Feature 9 — Series & Ordering
- **L1 — Stage 5.** `Series` + `SeriesEntry` (composite key `(SeriesId,StoryId)`, `OrderIndex`), unique
  `(AuthorId,Name)`. **L2/L3/L3.5 — Stage 2.** **L4 — Stage 1. L5 — Stage 2.**

## Feature 10 — Story Relationships
- **L1 — Stage 5.** `StoryRelationship` composite key `(Source,Target,Type)`, type lookup seeded
  (Inspired By/Prequel/Sequel/Companion), `StatusId` enum→short. One-way directional per §5. Cascade from
  both source and target story. **L2/L3/L3.5 — Stage 2.** **L4 — Stage 1. L5 — Stage 2.**

## Feature 45 — View Count Tracking
- **L1 — Stage 5.** `Story.ViewCount`. **L2 — Stage 2** (MVP direct increment unbuilt).
- **L3-Logic — Stage 2.** Client ping (5s timer / first scroll) unbuilt. **L3.5 — N/A** (no dedicated
  component). **L4 — N/A. L5 — Stage 2.**
- **L7 — Stage 2.** Redis `INCR` + drain worker (write-behind pattern 1). Interface unchanged from MVP.

---

### Cluster-level notes
- Delete `RandomNumberGenerator` once `StoryDesktop` gains real content.
- The commented-out `[NotMapped] IReadOnlyCollection<IStoryTag> StoryTags => StoryTags.ToList();` in
  `Story.cs` would have been infinitely recursive — correctly left disabled; the explicit-interface version
  is the live one.
