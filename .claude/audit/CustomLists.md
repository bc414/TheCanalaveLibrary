# Audit — CustomLists/

**Feature:** 51 (custom lists). User-created, named, shareable story collections beyond the system
lists. Public/private per list. Distinct from `Discovery/`'s search-result narrowing — this is
**personal organization + shareable curation**. Design settled 2026-07-13 (see below); building as
WU-CustomLists.

## Shared Context

**Entities (Core/CustomLists/):** `CustomList` (`UserId` Cascade, unique `(UserId,ListName)`,
`IsPublic`, `DateCreated` default), `CustomListEntry` (composite `(ListId,StoryId)`, Cascade from
both list and story, `DateAdded` default). Schema unchanged since `InitialSchema` — the settled
design required **zero migration**.

**Structural template:** Saved Tag Selections (Feature 15, `Tags/` cluster) — the same
personal-named-collection shape (owner CRUD, `IsPublic`, copy-on-write clone, profile tab, full L5
layer). Custom Lists mirrors its service/endpoint/client/validation architecture with story entries
instead of tag entries.

## Settled design (2026-07-13, Brian-ratified — do not revisit)

Resolves spec §8 Open Questions row 7 ("Custom lists detail — not yet designed"). The spec is a
read-only snapshot; this note is the authoritative record of the resolution.

- **Positioning: named shareable shelves.** The differentiator is naming + multiplicity + shareable
  curation. Privacy is NOT the pitch — Private Favorites already own the zero-effect private save
  (verified 2026-07-13: hidden favorites touch no public count, no profile, no discovery mart by
  default). New lists default private, but that's a sensible default, not the rationale.
- **Filter-template integration DROPPED.** Public lists are never usable as shared
  blacklist/whitelist search filters. The blacklist direction is shared author-suppression tooling
  (against the discovery-first ethos); the whitelist direction is redundant with view + clone. This
  dissolves §8.7's "how filter rules compose (AND/OR)" question — that complexity existed only to
  serve filters. `UserCustomFilter` is a separate Discovery entity, unaffected here (see
  `audit/Discovery.md` note).
- **Sharing = view + optional clone.** `IsPublic` makes a list viewable by others (including
  anonymous viewers — same posture as public profile tabs) and cloneable by authenticated users.
  Clone = independent snapshot copy (the concrete meaning of the old "copy-on-write on share"
  note): copy starts `IsPublic=false` (sharing is not transitive — same rule as
  `CopyPublicSelectionAsync`), name disambiguated "(copy)"/"(copy N)", no back-link to source,
  self-cloning allowed. **Clone copies only entries visible to the cloner** (content-rating /
  takedown filters apply via the read context) — never smuggles hidden content into their account.
- **Add flow:** an "Add to list" expander in `StoryCard`'s caret menu (deliberately NOT prime
  real estate beside the interaction buttons) + a `StoryTitlePicker` story search inside the list's
  own page for owners.
- **Surfacing:** separate "My Lists" section (`/my-lists`), NOT dynamic Bookshelves tabs — the
  closed `BookshelfTab` enum is not extended. Entry points: UserMenu item + a Bookshelves
  cross-link. Public lists also surface on the profile (`ProfileTab.Lists`, mirrors
  `ProfileTab.TagSelections`).
- **Ordering:** user-selectable sort at view time (`CustomListSortEnum`: DateAdded desc/asc,
  Title A–Z/Z–A; default newest-added). No manual `SortOrder` column, no content-rating sort.
- **Caps:** max **100 lists per user** (abuse guard, `CustomListValidations.MaxListsPerUser`);
  entries per list uncapped (reads paginate).
- **No per-list email alerts** (the design log's `ListAlerts` was already obsolete; tracking-email
  intent is a system-list concern). **No collaboration** (Groups own shared curation). **No
  notifications on add** — filing a story into a personal list is silent, deliberately unlike
  `GroupStory` adds (part of the feature's appeal vs. solo groups).
- **`ListName` max length 256** (keeps the existing schema; the design log's 100 is superseded).
- **Rate limiting:** none — matches Saved Tag Selections (the closest precedent); creates are
  bounded by the 100-list cap.

## Feature 51 — Custom Lists

Built as WU-CustomLists (2026-07-13). Verification summary per layer (`dotnet test` green all
tiers: Unit 712, RazorComponents 632, Integration 680; token check clean — only Import's
pre-existing in-flight finding):

- **L1 — Stage 5.** `CustomList`/`CustomListEntry` with unique-name constraint and cascade entries.
  Sound; migration-verified; unchanged by the settled design (zero migration). Entities moved
  `Core/Models/` → `Core/CustomLists/` (legacy-folder retirement — no model change, verified by
  clean build + green Integration tier against the real migration).
- **L2 — Stage 5.** `ICustomListReadService`/`ICustomListWriteService` +
  `ServerCustomList{Read,Write}Service` (`Server/CustomLists/`), SavedTagSelections-shaped
  (both-interfaces DI registration). Covered by the **Integration** tier
  (`CustomListServiceTests`, ~28 tests): CRUD + owner-gating, case-insensitive duplicate-name,
  100-list reject-at-cap (natural-count), entry add/remove idempotence, visibility gates
  (private → null/empty to non-owner + anonymous), all four sorts, rating-filtered ids AND counts
  (no phantom counts), clone (visible-entries-only proven at the ROW level via write-context
  ground truth, private-start, name disambiguation, self-clone), story-delete cascade.
  `CustomListValidations`/`DisambiguateCloneName` are **Unit**-covered
  (`CustomListValidationsTests`). `CustomListValidationException` registered in
  `ExceptionPresenter`.
- **L3-Logic / L3.5-Structure — Stage 5.** `MyListsPage` (`/my-lists`), `CustomListPage`
  (`/lists/{id}`, route-param dispatcher with `[PersistentState]` restore-or-fetch),
  `AddToCustomListMenu` (StoryCard caret composite, `StoryViewStats` self-contained discipline),
  profile Lists tab (`ProfileTab.Lists = 7`, slug `lists`, mirrors TagSelections), UserMenu item +
  Bookshelves cross-link. **RazorComponents** tier covers the caret composite's `@code` logic
  (`AddToCustomListMenuTests`: anonymous-hidden, on-demand load, toggle args, create-private-then-add
  two-step, InlineAlert on validation error); owner remove rides StoryDeck's `CardOverlay` slot.
- **L4 — Stage 5.** Established Container/Control recipes only (no new element kinds; mission
  family on Create/Clone per the "New X"/copy-on-write CTA precedents); token check green;
  rendered clean in the browser pass. Human visual sign-off (→6) is the standing pass.
- **L4.5-Browser — Stage 5 (2026-07-13, standing dev DB kept).** Full loop driven in a real
  browser under **InteractiveAuto — the later flows ran on the real WASM runtime** (231
  `_framework` resources, zero `_blazor` WebSocket on the delete/My-Lists loads), psql ground
  truth after every write: create (My Lists page) → caret "Add to list" toggle (✓ flip) → inline
  "+ New list" create-and-add → list page (picker add via `StoryTitlePicker`, sort re-order
  Z–A, rename incl. `PageTitle`, make public/private) → mature-off viewer (ReaderGamma) sees
  1-of-2 stories with MATCHING count → clone under WASM (toast, nav to `/lists/{new}`, row-level
  proof the M entry was not copied, private start) → profile Lists tab (public only) → per-card
  ✕ Remove → delete with `ConfirmDialog` → `/my-lists`. Anonymous (cookie-less) checks: public
  list detail/ids 200 with viewer-visible data; private list returns JSON null. Zero console
  errors. TestUser's two lists (`Cozy Canon Comfort` public, `Sinnoh starter pack` private)
  remain in the dev DB as ordinary fixture data.
- **L5 — Stage 5.** `CustomListEndpoints` (`/api/custom-lists`, canonical naming; auth mirrors
  page posture — `/mine`+`/memberships` gated, detail/ids/public-by-user anonymous;
  `EndpointHelpers.ExecuteWriteAsync` translation; `Results.Json` empty-body-null on the nullable
  detail) + `ClientCustomList{Read,Write}Service` (nullable read via `GetNullableFromJsonAsync`;
  status→exception translation). **Unit**-covered (`ClientCustomListServiceTests`: URL/verb
  shapes, empty-body → null, 400/401/403/404/5xx translation) and live-proven under the real
  WASM runtime in the L4.5 pass above (create/toggle/clone/delete all flowed through the client
  impls). Meets the WU-L5Pilot Stage-5 bar (built + WASM-verified end-to-end).
