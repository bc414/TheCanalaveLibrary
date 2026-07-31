# Hidden Deferrals Tracker

**What this is.** A checklist of deferred/pending work that is **not transparent from the `status.md` grid** —
items where the grid cell reads Stage 5 (or N/A), yet real work remains. Produced by a manual audit on
**2026-07-24**. Feature 53 (External Story Links & Verification) is excluded — a separate planning session owns it.

> **2026-07-26 update (WU-TagFanon).** A5 was taken up and turned out to be the visible tip of a
> substantially non-functional tag subsystem — see A5's rewritten entry. That WU also resolved
> **C1** (measured → reject), half-closed **C4**, closed **H5** in full, fixed **H6**'s tag-length
> drift, and added **B0**. The pattern worth carrying forward: a one-line tracker entry is a
> *pointer to an investigation*, not a scoped work item — three of A5's assumptions were wrong
> before any code was written.
>
> A **post-implementation review of that same WU** then added **B11** and **B12**, and caught four
> defects in the newly-written code: malformed ship input threw from inside predicate assembly
> (a 500 for what should be a 400); adoption crashed with a raw `DbUpdateException` on case-variant
> duplicates in one story; two N+1 query loops (one unbounded by paging) in a codebase that
> documents a batch-enrichment rule; and a "data-preserving" migration whose transformation SQL had
> only ever run against an empty database. Worth carrying forward: **every one of those was found by
> re-reading the diff** — the 2258-test suite was green and the browser pass was clean throughout.

**Status: snapshot, not authoritative.** This file is a hand-maintained convenience list — listed in
`CLAUDE.md`'s Project Files table (added 2026-07-27, owner-approved) so sessions can find it, but still
*not* a governed source of truth like `status.md` / audit files / skills. It can go stale.
Before acting on any item, **re-read the cited source of truth** (audit note / code seam / plan row) and check
whether a later work-unit already closed it — several items in the original report were dropped precisely because
a later WU had resolved them (see "Already-closed, do not re-report" at the bottom). Treat a checked box as "someone
believed this was handled," not proof.

**How the grid hides these (four blind spots).** The grid is a feature×layer matrix of coarse Stage numbers, so it
structurally cannot show: (1) a spec'd sub-feature cut inside an otherwise-Stage-5 cell; (2) plumbing that is built
and compiles but is wired to nothing; (3) an L6 "verified" that was asserted, not measured; (4) cross-cutting or
decision work that has no row at all.

**Label legend.**
- Type — `scope-cut` · `inert` · `index-unverified` · `latent-risk` · `off-grid` · `decision` · `doc-drift` · `polish` · `test-gap`
- Priority — `high` / `med` / `low` (suggested attention, not a mandate)
- Window — `mvp` / `beta` / `launch` / `anytime` (when it plausibly needs to be done)
- Deliberate? — where noted, the deferral is a conscious scope decision, not an oversight; taking it up is a *choice*, not a *fix*.

---

## A. Spec'd sub-features cut for MVP — cell reads Stage 5

- [ ] **A1 — Inline Pokémon-sprite editor blot (WU-EditorSprite)** `[scope-cut · med · beta]`
  - Grid: F6 & F35 L3.5=5, L4=5 (EditorView reads fully done).
  - Source: `audit/Chapters.md` (WU6 note + "WU6's two deferrals formalized as named WUs, 2026-07-15"); `workplan.md` Planned/not-yet-built.
  - Context: Spec §5.30.2 calls for embedding Pokémon sprites into rich-text bodies via a Quill blot. Never built; the HTML-sanitizer allow-list extension it needs isn't designed yet. Shared across every EditorView consumer (chapters + blog posts).
  - Next: design the blot + sanitizer allow-list additions together; this is a genuine authoring capability, not polish.

- [x] **A2 — AutoLoadNextChapter reading behavior — OFFICIALLY CUT (decided 2026-07-24)** `[scope-cut · low · anytime]` — *Decision: will not be built, not merely deferred.*
  - Grid: F7 L3-Logic=5.
  - Source: `audit/Chapters.md` Feature 7 L3-Logic note ("AutoLoadNextChapter is post-MVP"); cut decision recorded here 2026-07-24.
  - Context: Traced to `GeminiDiscussions/MyActivity September to November 2025_filtered.md` Entry #872 (2025-11-03 23:37) — Gemini free-brainstormed it unprompted in response to an open "what user-centric settings am I missing for a fanfic site?" question, drawing from a commercial binge-read UX baseline (Webtoon/RoyalRoad-style auto-advance-on-scroll). No site-specific rationale was ever given for it, and the owner determined on review that auto-advancing past a chapter boundary works against the site's mission — it denies the natural pause point for reflection and per-chapter commenting. Cutting it outright rather than leaving it "post-MVP" (which implied it was still on a future roadmap).
  - Residual: **done 2026-07-24** — the field was removed from `ReaderSettings`/`ReaderSettingsDto`/`ReaderSettingsForm.razor`/`ReaderDisplaySettings.cs` doc comment/`ServerUserSettingsService`, plus the stale example in `layer1-data-model.md` and the mention in `folder_clusters.md`'s Chapters row. Spec §~991 still lists it (spec is read-only, never edited) — `audit/Chapters.md` Feature 7 L3-Logic note now carries the divergence. No code references remain outside migrations (auto-generated, updated by a fresh `dotnet ef migrations add`).

- [x] **A3 — Full story-completion tracking → spoiler gate is hardcoded open — DONE (2026-07-24)** `[scope-cut · med · beta]` — *Owner chose to build now rather than defer further (plan-mode reassessment).*
  - Grid: F7 L3-Logic=5; F26 (Spoiler Comments) all built cells=5.
  - Source: `audit/Chapters.md` WU26 note (superseded); `audit/Comments.md` Feature 26.
  - Context: "Full completion tracking is post-MVP." `CommentSection` was wired with `UserHasCompletedStory=false` **hardcoded**, so the spoiler completion-gate could never treat a viewer as having finished the story.
  - **Residual: done 2026-07-24** — built spec §5.12's application producer (`IUserStoryInteractionWriteService.MarkCompletedAsync`, a durable direct write mirroring `MarkStartedAsync`, deliberately outside the reading-progress signal buffer; fires on reaching the final chapter of an author-Completed story only, no auto-clear) and wired the real `ViewerHasCompletedStory` into the gate via `ChapterReadingDto`. A latent `StoriesInProgress` counter underflow was found and fixed in the same session. `dotnet test` green (2096/2096); browser-verified end-to-end against seed data. Full narrative: `workplan.md` "A3" entry; `audit/Chapters.md` A3 Stage note; `audit/UserStoryInteractions.md` A3 settled note; `layer2-services.md` §"`IsCompleted` auto-producer".

- [x] **A4 — Recommendation lifecycle — REDESIGNED + BUILT (WU-RecLifecycle, 2026-07-25)** `[scope-cut · med · beta]`
  - Grid: F27 all=5; F48 (Story Approval Workflow) built cells=5.
  - Source: `audit/Recommendations.md` §"WU-RecLifecycle settled design" (authoritative); code `ServerRecommendationWriteService.cs`.
  - Context: The original entry's framing was wrong twice over. Spec §5.6's "moderator review" was a
    mis-rewording (source deliberation: *author*-approval + time auto-approve; no mod gate), and on
    first-principles review the owner rejected any pre-publication gate outright (discovery-first;
    a gate merges "fix an earnest flaw" and "remove a troll" into one harsh mechanism). The settled
    design — **publish-immediately + author Request-Revision (note, hide-until-edited, auto-relive) +
    author Remove (silent, sticky, unblockable)** — has **no `/mod/submissions` rec tab, ever**.
  - **Coupling:** **D1** folded into the same WU (the status filter becomes load-bearing the moment
    `NeedsRevision`/`Rejected` rows exist). Also folded: **D3.2** (rec-attribution validation); self-rec block.
  - Follow-ups spawned (not built): extend author checks to co-authors when the dormant `CoAuthor`
    feature is actually built; profile owners deleting `UserProfileComment`s on their own profile
    (same grievance shape as author-deletes-story-comments, deliberately excluded from WU-RecLifecycle).

- [x] **A5 — Fanonize flow — SUPERSEDED + BUILT, far larger than framed (WU-TagFanon, 2026-07-26)** `[scope-cut · low · anytime]`
  - Grid: F11/F12/F31/F41 cells all stay 5 (they were 5 and remain 5 — the hidden-deferral shape).
  - Source: `audit/Tags.md` §"WU-TagFanon Stage note" (authoritative); `workplan.md` WU-TagFanon.
  - **The one-line framing here was wrong three times over.** "Flip `IsFanon`, match `OcName` to
    `TagName`" had (a) no entry point — nothing ever showed a moderator which names had reached
    critical mass; (b) no chance of working on the owner's own `"Saura (Silver Resistance)"`
    example, since a disambiguated tag name never string-matches an `OcName` of "Saura"; and (c) no
    way to establish the `ParentTagId` the three-tier model requires. Settled shape:
    **mod-driven set selection** — the affected rows are the dashboard group the moderator was
    looking at, so the tag may be named anything.
  - **Auditing the subsystem beneath it found six defects in already-Stage-5 code**, all fixed:
    hierarchy invisible to discovery (adoption would have *removed* stories from their species'
    search results); the Setting/AU half unreachable AND unrenderable; `OcBio` write-only; the OC
    gate conflating custom naming with per-story portrayal; the character overlay disagreeing with
    itself three ways; and no ship filtering at all on a fanfiction site.
  - Built: custom-name/nuance split, `SettingDetail` folded onto `StoryTag`, hierarchy roll-up,
    ship-filter axis, public `/fanon` dashboard, link-and-notify, `/tag-adoptions` adoption flow,
    full SeedTool tag world. 2258 tests green; browser-verified with psql ground truth.

- [ ] **A6 — Explore candidate-pane tag/interaction filter axes** `[scope-cut · low · anytime]`
  - Grid: F33 (Manual Tree Search) L3.5=5.
  - Source: `audit/Discovery.md` WU40 L4.5 "Deferred."
  - Context: The Explore-mode candidate-results pane has no tag/interaction filter axes; recorded as not-built.

- [ ] **A7 — Pinned-Story edge → discovery mart / Automatic-tab integration** `[scope-cut · med · anytime]`
  - Grid: F59 L8=5, F60 (mart worker) L8=5.
  - Source: `audit/Discovery.md` WU40 settled note & L4.5 "Deferred"; `workplan.md` WU40 "Deferred follow-up (not yet sequenced)."
  - Context: The Pinned-Story edge (WU40) works in manual tree search only. Extending it to the Automatic tab needs a 7th UNION arm in the **frozen** `DiscoveryMartSchema` + chain-of-trust membership. Unnumbered WU — "flagged so it isn't lost." Both mart cells read Stage 5.

- [ ] **A8 — Mobile-compact EditorView toolbar (WU-EditorMobile)** `[scope-cut · low · post-mvp-mobile]` — *Deliberate (re-scoped to the future mobile phase).*
  - Grid: F6 L4=5.
  - Source: `workplan.md` Planned/not-yet-built (WU-EditorSprite entry); `cross-cutting.md` §Rich Text; `middle_plan_v2.md` Phase 4 (design-now-or-defer framing — historical, Phase 4 itself is DONE, this item's own build is not).
  - Context: WU6 shipped the desktop editor toolbar only. A compact mobile toolbar is re-scoped onto the adaptivity ladder's rung-3 trigger in the future mobile phase.

---

## B. Built-but-inert — plumbing exists, nothing drives it

- [ ] **B0 — `PrefersDataSaverMode` is stored, settable, and consumed by nothing** `[inert · low · anytime]` — *Found by WU-TagFanon's audit, 2026-07-26.*
  - Grid: F21/F22 (settings) cells=5 — invisible there.
  - Source: `User.PrefersDataSaverMode`; `AppearanceSettingsForm.razor`; `ServerUserSettingsService`.
  - Context: The data-saver checkbox stores, renders, and persists — and **no render path reads
    it**. `ThemeContext` carries only `(Slug, PrefersAnimated)`, so `TagChip` and the other sprite
    consumers could not honour it even if they wanted to. "Sprites disabled" is therefore not a
    reachable state today. Deliberately left out of WU-TagFanon's scope (it belongs to the sprite
    subsystem, not the tag model) — but it means any design that leans on "users with sprites off"
    is reasoning about a state that does not exist.
  - Next: decide whether data-saver suppresses sprites (then carry it on `ThemeContext`), or cut
    the setting. Either way it needs its own deliberation, not a ride-along.

- [ ] **B1 — Notification email fan-out (`EmailEnabled` is inert)** `[inert · med · beta]`
  - Grid: F41/F42/F43 L2=5.
  - Source: `layer2-services.md` §Notification settings; `audit/Notifications.md`; `roadmap.md` Phase 6 "WU-NotifEmail" / `middle_plan_v2.md` Resolved "Email mechanism."
  - Context: The per-type `EmailEnabled` checkbox stores, renders, and persists — so the settings page is a legit Stage 5 — but it drives **no mail**. WU-Email shipped *transactional* mail only (confirmation/reset). Fan-out over notification settings is WU-NotifEmail, Phase 6, unbuilt.
  - Next: WU-NotifEmail also folds in the missing `FakeNotificationWriteService` + anonymous-`NotificationBell` regression test (see H5).

- [x] **B2 — Comment & blog-follower notifications — DONE (WU-B2, 2026-07-25)** `[inert · med · beta]`
  - Grid: F23/F24/F35 L2=5 (now genuinely live, not inert).
  - Source: code `ServerCommentWriteService.cs` (four `// TODO(post-MVP comment-notifications)` at ~lines 65, 119, 165, 220); `ServerBlogPostWriteService.cs` (`// TODO(post-MVP follower-notifications)` ~line 64).
  - Context: New comments do not notify story/blog/group/profile owners or parent-comment authors; new profile blog posts do not notify followers. Seams are stubbed but unwired. (Comment-*like* notifications are deliberately omitted by design — not part of this.)
  - **Closed 2026-07-25 (WU-B2), with scope growth beyond the original item:** all five seams wired
    (group comments = replies-only by owner decision; blog fan-out fires on the publish transition
    and extends to the linked story's followers/favoriters/read-it-later users, types 13–16 with
    precedence-dedup). Pulled in during planning: the blog spoiler **content interstitial**
    (completion-gated, CommentItem pattern) + `BlogPostCard` snippet suppression; **story-link
    integrity** (write-time `StoryId` ownership validation; `GroupBlogPost.StoryId` removed
    entirely — group posts are not story-linkable, restoring the original TPT design); `PollUpdated`
    enrichment fix (profile-post polls were title-less). Narrative: `audit/Notifications.md` WU-B2
    slice, `audit/BlogPosts.md`, `audit/Groups.md`, `layer2-services.md` §"Comment & blog-post
    semantic methods".

- [ ] **B3 — UserStat counters with no producer (always read 0)** `[inert · med · beta]`
  - Grid: F22 all=5; F58 (recalc worker) L2=5.
  - Source: `audit/Profiles.md` Feature 22 deferred-counters note; `layer2-services.md` §"Counters deferred — producer not yet built"; `UserStatRecalculator.cs` (deliberately skips them).
  - Context: `SpotlightCount`, `AcknowledgedAsBetaReaderCount`, `AcknowledgedAsInspirationCount` have no producer wired; the recalc worker skips them on purpose ("recomputing to 0 would mask missing producers"). They display 0 forever. `SpotlightCount`'s definition is also unsettled; the acknowledgment source is ambiguous (`BetaReader` entity vs `StoryAcknowledgment`).

- [ ] **B4 — Badge award automation: 1 of 5 (BetaReader has no producer)** `[inert · med · beta]`
  - Grid: F50 L1/L2/L3/L3.5/L5=5.
  - Source: `audit/Badges.md` "Deferred award triggers" table + WU36 settled decisions.
  - Context: Only Recommender/RecommenderSilver auto-award. **BetaReader has no producer and no assigned WU** (`AcknowledgedAsBetaReaderCount` never populated — ties to B3). Architect/Patron/Artist are *deliberate manual grants* (settled after the Feature 56 cut) — not pending.

- [x] **B5 — Private-message archive/unarchive UI — DONE (WU-MsgArchive, 2026-07-26)** `[inert · low · anytime]`
  - Grid: F49 L3-Logic/L3.5/L4/L4.5=5 (unchanged — this filled in inert plumbing under already-Stage-5 cells).
  - Source: `audit/Messaging.md` L4.5 "Observation (not a defect)" (now struck; superseded by that file's WU-MsgArchive slice).
  - Context: `SetArchivedAsync` + the "Archived" label exist and are tested, but no UI control surfaces them. Capability dead-ends at the service layer; every messaging cell reads Stage 5.
  - **Residual: done 2026-07-26.** Provenance traced first: `IsArchived` has **no design deliberation
    anywhere in the record** — it appears in the Gemini log (Entry #1539, 2025-10-25) already present
    in a pasted SQL script, and the one first-principles PM design turn (#1409) never mentions
    archiving; spec §5.19 describes the column, not a user story. So the capability was **ratified
    deliberately** rather than inherited — built, not cut, because with no delete and no block for an
    established thread, archive is the only disposal gesture a user has. Settled semantic: **archive
    is sticky (mutes), not filing** — a reply never auto-unarchives (raise-on-reply explicitly
    rejected: it would let a persistent unwanted correspondent drag the thread back forever), the
    global badge excludes archived threads, but the per-conversation unread count stays visible in
    the Archived tab so nothing is silently lost. Needed zero service change. Shipped: thread-header
    Archive/Unarchive button, Inbox|Archived segmented toggle with per-tab on-demand fetch, the
    now-redundant per-row "Archived" chip removed (its ratified `surface-registry.md` row struck),
    `ConversationThreadDto.IsArchived` added, and the inbox sort moved from C# into SQL preserving
    the message-less-sorts-LAST contract against Postgres's `NULLS FIRST` default. `dotnet test`
    green (2213/2213); browser-verified end-to-end including a real reply into an archived thread.
    Full narrative: `workplan.md` WU-MsgArchive; `audit/Messaging.md` §WU-MsgArchive;
    `layer2-services.md` §"Conversation Archiving Is Sticky".
  - **Not touched:** no index work — **C4** stays exactly as written (all index work is a later pass).

- [x] **B6 — Story→folder assignment has methods but no UI — RESOLVED (WU-GroupsL5b, 2026-07-25)** `[inert · low · anytime]`
  - Grid: F39 L3.5=5 (unchanged — already Stage 5; this closed the inert plumbing under it).
  - Source: `audit/Groups.md` Feature 39/40 L3.5 notes.
  - Context: `AssignStoryToFolderAsync`/`UnassignStoryFromFolderAsync` exist and are tested. WU-GroupsL5 (2026-07-24) built the folder-management page but **pointedly excluded** story-assignment — there was no UI anywhere to file a story into a group folder.
  - Residual: **done 2026-07-25.** Investigating the fix surfaced that admin-gating the read side too (the first-draft design) would have shipped a display gap affecting *every* viewer, not just admins — the folder tree had never rendered folder contents at all, for anyone, since WU32. Root-fixed by retyping `GroupDetailDto.StoryIds`/`GroupFolderDto.StoryIds` (bare `int`) to `IReadOnlyList<GroupStoryDto>` (`GroupStoryId` + `StoryId`) at the source rather than bolting on a parallel admin-only endpoint. `GroupPage` now renders folder contents (story titles, linked) for every viewer unconditionally, and admin-only controls (assign/reassign via a per-story `StoryDeck` overlay, per-folder unassign, and — closing a second dead handler found in the same investigation — the previously-unwired `RemoveStoryAsync`) on top. Folds in **D3.1** (see below). Browser-verified both directions (admin: assign/reassign/unassign/remove, all `psql`-confirmed; non-member viewer: sees folder contents, zero admin controls). `dotnet test` full suite green. Detail: `workplan.md` WU-GroupsL5b; `audit/Groups.md` F39/F40 Stage notes.

- [ ] **B7 — Discovery per-user filter-override editing UI (§8.7)** `[inert · low · anytime]`
  - Grid: F31 L2=5 (no dedicated cell for the override UI).
  - Source: `audit/Discovery.md` "Note on search-result narrowing"; WU28 "Deferred."
  - Context: The §8.7 defaults *read/merge* exists (`IDiscoveryDefaultsReadService`), but there's no surface for users to edit per-search-mode overrides. Per-user random batch size is stubbed to a constant 20. The `UserCustomFilter` entity's purpose stays unresolved pending this UI.

- [ ] **B8 — Spotlight donation/payment pipeline** `[inert · low · post-beta]` — *Deliberate (deferred past beta).*
  - Grid: F55 L1/L2/L3/L3.5/L4.5/L5=5 (L4=3).
  - Source: `audit/Spotlight.md` "Deferred"; code `ServerSpotlightSlotAllocator.cs` (throws `NotSupportedException` on donation-sourced slots), `SpotlightEnums.cs`, `SiteSettingKeys.cs`.
  - Context: The mod-grant slot path works. The *second* `ISpotlightSlotAllocator` source — donation/payment — is a reserved seam that throws. Unbuilt: `SpotlightSlot.PaymentId` population, payment provider, the activity/cost-scaled N formula, Patron/Spotlighter badge, and slot-redemption expiry. A whole sub-system absent under a near-complete feature row.

- [ ] **B9 — Verified-crawler serving is inert (config-gated OFF)** `[inert · low · launch]` — *Deliberate (Phase-7 trust boundary).*
  - Grid: F64 L2=5; F66 all=5.
  - Source: `audit/AccessGate.md` Stage-5 note + "Open"; `audit/Seo.md` Feature 64.
  - Context: `VerifiedBotMiddleware` is built and tested but `Seo:TrustVerifiedBots` defaults OFF, and the `cf-verified-bot` header name is a stub pending the real Cloudflare product-tier contract. Until Phase 7, crawlers get only the interstitial (Pattern A), never full content (Pattern B).

- [ ] **B10 — Sprite R2/cloud existence-probe** `[inert · low · launch]` — *Deliberate (prod-only, non-blocking).*
  - Grid: F3 L2=5.
  - Source: `audit/Sprites.md` "Shared Context" (`ISpriteAssetProbe` — "R2 impl deferred").
  - Context: Only `LocalSpriteAssetProbe` (dev) exists. Write-time sprite-existence validation (a non-blocking warning) has no cloud/prod backend.

---

- [x] **B11 — Ship filter has no restore path — DONE (WU-DiscoveryFilterRestore, 2026-07-28)** `[inert · med · anytime]` — *Found by WU-TagFanon's own post-review, 2026-07-26. The blocking question below was promoted to **decision row 13** (2026-07-27, then in `middle_plan_v2.md`, now `roadmap.md`) so it lives in a decision ledger; **row 13 was resolved 2026-07-28** — see "Resolution" below, then built same-day.*
  - Grid: F31 L2/L3-Logic/L3.5=5 — invisible there; the axis works, it just cannot be reconstructed.
  - Source: `audit/Discovery.md` §"WU-TagFanon note" → "Settled vs. open"; `ShipFilter.razor`;
    `ResultsFilterPanel.razor` `OnParametersSet`; `SearchPage.razor`.

  **What exists.** `StoryFilterDto.IncludedShips`/`ExcludedShips` are real filter axes, applied
  server-side with roll-up, and `ShipFilter.razor` builds them. Within one page session ships
  behave correctly: `ShipFilter` owns `_included`/`_excluded`, `OnShipFilterChanged` sets
  `_userHasInteracted` so the panel's re-seed loop stops overwriting them, and pagination preserves
  them. **There is no user-visible bug today.**

  **What was missing (historical — as of 2026-07-26, before the build below).** Ships were the ONLY
  axis with no reconstruction path:

  | Mechanism | Tags | Interactions | Text/Sort | Ships |
  |---|---|---|---|---|
  | Carried on `StoryFilterDto` | yes | yes | yes | yes |
  | `Initial*` seed param on `ResultsFilterPanel` | `InitialIncludedTags`/`InitialExcludedTags` | via `InitialFilter` | via `InitialFilter` | **none** |
  | Re-hydration of display data from bare ids | `ResolveTagChipsAsync` | n/a | n/a | **none** |
  | `[PersistentState]` across prerender→interactive | chip lists persisted | n/a | n/a | **none** |
  | Seeded in `OnParametersSet` | yes | yes | yes | **no** |

  (The `[PersistentState]` row was itself a wrong assumption, corrected below — the eventual build
  used the localStorage seam, not `[PersistentState]`, for cross-navigation restore.)

  So ship state can only ever be *created by live interaction* and can never be rebuilt from a
  `StoryFilterDto`. It is lost on navigation, on any parent re-render that recreates `ShipFilter`,
  and would be lost across a prerender handoff if it could exist there.

  **Why this is worth doing, not just noting.** `/discover` currently round-trips NO filter state
  through the URL — only `TreeSearchPage` uses `SupplyParameterFromQuery` + `NavigateTo(replace:
  true)`, and that page is the in-repo pattern to copy. So today ships are consistent with
  everything else on that page. But the moment `/discover` gains URL state — and for a fanfiction
  site a shareable filtered view is close to a defining feature; "everything for this pairing" is
  the canonical community link in the medium — **every axis will come along except ships**, because
  ships are the one axis with no id→state path. Fixing it later means touching the DTO, the panel,
  the component and the page; fixing it alongside the URL work means one parameter and one seed block.

  **Resolution (decision row 13, 2026-07-28).** `/discover` never carries filter state in its URL.
  The option list above is superseded — its option 1 rested on "follow `TreeSearchPage`'s pattern,"
  but that page carries *control* state (small legible scalars), which is not precedent for
  serialising arbitrary id lists. What replaces it:
  1. **Sharing → the artifact**, via a permalink on the public selection
     (`/discover/selection/{SelectionId:int}/{*Slug}`, story-slug contract: id is truth, slug is a
     never-parsed decorative tail). WU-SelectionPermalink.
  2. **Return integrity → device-local restore** through the ratified localStorage seam, not a URL.
     WU-DiscoveryFilterRestore.
  3. **Ships → seeding parity only.** Non-shareable and non-persisted by design; F15's
     tag-axis-only scope untouched. Ships cannot enter a `SavedTagSelection` without new L1 child
     tables — the flat `(SelectionId, TagId, IsExcluded)` row unique on `(SelectionId, TagId)`
     degrades a ship into co-presence, which WU-TagFanon ruled is *not* a ship.

  **Explicitly NOT the same question as F15.** Ships are settled as *never persisted in
  `SavedTagSelection`* — a saved selection is a curated artifact the user names and shares as an
  object, and its scope is tag-axis-only (WU43). That says nothing about URL/seed round-tripping:
  a saved artifact and the address of a view are different concerns. An earlier
  `audit/Discovery.md` revision blurred the two; that note has been corrected. **Do not let the F15
  decision be cited as covering this.**

  **Implementation sketch (per the resolution) — superseded by the build below; kept for the
  provenance trail, not as pending instructions.** Add `InitialIncludedShips`/`InitialExcludedShips`
  to `ResultsFilterPanel` and seed them in `OnParametersSet` beside the tag block, reusing the
  existing `_userHasInteracted` re-seed-until-first-interaction guard (MA-402); add matching seed
  parameters to `ShipFilter` (it currently initializes its lists empty with no `OnInitialized`
  seeding). Note a real snag: `ShipFilter` keeps parallel `_includedNames`/`_excludedNames` display
  strings built at pick time from the chips the user selected — those **cannot be rebuilt from
  member tag ids**, so seeding needs a name-resolution step (the `ResolveTagChipsAsync` equivalent,
  via `ITagReadService.GetTagChipsByIdsAsync`). That resolution belongs to the **page dispatcher**,
  not the panel or the axis — seed state is dispatcher-resolved and passed down; only
  fetch-on-user-input lives in the component (`layer3.5-structure.md` §"Seed state vs. live fetch
  in filter components"). **Do not use `[PersistentState]` for cross-navigation restore** — an
  earlier revision of this sketch proposed it, but `error-handling.md` rejects it as
  prerender-handoff-only; use the localStorage seam instead.

  **Blast radius.** All four `ResultsFilterPanel` consumers (`/discover`, Tree Search, Bookshelves,
  Profile story tabs) inherit `ShowShipFilter` defaulting true, so seeding work benefits all four
  at once — and any URL decision should be checked against all four, not just `/discover`.

  **Built (2026-07-28), matching the sketch above almost exactly.** `ResultsFilterPanel` gained
  `InitialIncludedShipNames`/`InitialExcludedShipNames` (ships themselves ride on `InitialFilter`);
  `ShipFilter` seeds under the `_userHasInteracted` guard; `ShipFilterDto.JoinMemberNames` became
  the single label implementation so pick-time and seed-time labels can't diverge. Cross-navigation
  restore used `DiscoveryFilterStore` + `js/discovery-filter.js` (the localStorage seam, third
  instance after `DraftStore`/`ManualTreeStore`) — **not** `[PersistentState]`, exactly as this
  sketch warned. **One thing the sketch missed:** the browser pass found `TagFilter` seeded only in
  `OnInitialized`, so a late-arriving restored tag seed left the sidebar rendering empty while the
  query behind it *was* filtered (`TagFilter` wasn't even part of this tracker item — B11 was
  ships-only — but shares the same seed-timing class of bug). Fixed same-session with an
  `OnParametersSet` re-seed guarded by a signature; two regression tests added. Repeats this
  tracker's own lesson from the WU-TagFanon post-review: a sketch is a starting point, not a
  guarantee — read the diff. Full record: `workplan.md` §"WU-DiscoveryFilterRestore +
  WU-SelectionPermalink"; `audit/Discovery.md` §"WU-DiscoveryFilterRestore + WU-SelectionPermalink
  note". `dotnet test` green (2,330); browser-verified.

- [x] **B12 — Hierarchy roll-up made `ApplyFilters` impure; expansion is uncached and unshared — DONE (WU-ApplyFiltersPurity, 2026-07-30)** `[latent-risk · low · anytime]` — *Found by WU-TagFanon's own post-review, 2026-07-26.*
  - Grid: F31 L2=5; F59/F60 (marts) L8=5 — invisible on all of them.
  - Source: `ServerStoryReadService.ApplyFiltersAsync` + `ExpandWithChildrenAsync`;
    `layer2-services.md` §"Tag Hierarchy Roll-Up"; `layer6-indexes.md` §"Measured, no DDL needed".

  **What changed, and why it matters beyond performance.** Roll-up turned a **pure, synchronous**
  predicate builder (`StoryFilterDto` → predicate, no I/O) into an **impure, async** one that takes
  a `ReadOnlyApplicationDbContext` and whose output depends on live database state. Three call
  sites changed signature (`GetListingsAsync`, `FilterCandidateIdsAsync`, `GetRandomBatchAsync`)
  and every future caller must now have a read context in hand. Three consequences a benchmark
  will not show:

  1. **A filter is no longer reproducible from its DTO.** Two identical `StoryFilterDto`s can
     return different result sets if a moderator re-parents a tag between them. That is *intended*
     — hierarchy is meant to be live — but it means filter results cannot be cached by DTO, logged
     and replayed, or reasoned about from the DTO alone.
  2. **Layer 8 cannot share the semantics.** The discovery marts are raw SQL with no EF model
     (`DiscoveryMartSchema`, documented as frozen). If a mart ever needs the same filter behaviour
     it must reimplement expansion — a second copy of a correctness rule, which is precisely how
     the two halves of the tag model drifted apart in the first place.
  3. **The measurement that justified it captured the cheap half.** The recorded 0.02 ms is
     *database execution* for a seq scan over a 136-row `tags` table sitting in `shared_buffers`
     **on localhost**. Production is a droplet plus managed Postgres — network-separated — where an
     extra round-trip costs roughly 0.5–2 ms of latency regardless of query speed. The honest
     statement: **filtered** searches pay one extra round-trip; **unfiltered** browse pays nothing
     (`ExpandWithChildrenAsync` early-returns on an empty id set, and `ApplyFiltersAsync` only
     populates that set from the tag/ship axes).

  **The obvious cheaper design, not taken.** The parent→children map is tiny (one row per child
  tag), identical for every viewer, and changes only when a moderator writes a tag — close to an
  ideal in-memory cache with write-invalidation from `ServerTagWriteService`. It was not considered
  during the build; the query was simply added. Precedent runs both ways in this codebase:
  `ISiteSettingsReadService` is *deliberately* uncached and documents why (a mod edit must take
  effect on the next read), while sprite/theme resolution happens at render time. A tag hierarchy
  leans cacheable: edits are rare, one cycle of staleness is harmless, and the invalidation point
  is a single write service.

  **Questions to settle before building anything — settled 2026-07-30, then built same-day:**
  - Cache the expansion map, or keep the per-request lookup? **Cached.** Process-local snapshot
    (`ServerTagHierarchyCache`, `volatile` field + `SemaphoreSlim` double-checked reload) — not
    `IMemoryCache` (no precedent, buys nothing for one entry). Survives N≥2 via a 60 s absolute TTL
    layered on top of write-invalidation, so every node converges independently with **zero shared
    store** (`horizontal-scaling.md` §5) rather than needing something that "survives N≥2" in the
    Valkey sense — the question's own framing assumed a shared-store answer that turned out
    unnecessary.
  - Invalidation trigger: any `Tag` write, or specifically `ParentTagId` changes? **Broad — any
    `Tag` write.** Trivially correct and nearly free, exactly as this entry predicted.
  - Should expansion be *exposed* (e.g. `ITagReadService.GetChildIdsAsync`) so Layer 8 and any
    future consumer share one implementation instead of copying the rule? **Exposed, but via its
    own `ITagHierarchyReadService`, not `ITagReadService`.** `ITagReadService` has 5 implementers
    (server, client/WASM, both write services by inheritance, a test fake); adding a server-only
    concern there ships it over HTTP for no client consumer. A dedicated interface serves B12's
    actual goal — one shared implementation — without that cost.
  - **Re-measure on a network-separated database before concluding it does not matter.** **Obviated
    by removal, not performed.** That measurement existed to justify *keeping* a per-read
    round-trip; this WU removes the round-trip instead, and its cost is monotonically non-negative
    under any network topology, so eliminating it cannot be the wrong call regardless of what a
    future measurement would show. **If the cache is ever removed and per-read expansion restored,
    this measurement becomes necessary again** — the existing 0.02 ms figure is a localhost EXPLAIN
    number and cannot answer the production-latency question on its own.

  **Not urgent, and not a defect.** Roll-up is correct and load-bearing — without it, fanonize
  adoption removes stories from their own species' search results. This entry is about the
  architectural debt the fix introduced, so a future session can weigh it deliberately instead of
  rediscovering it.

  **Built (2026-07-30).** `ApplyFilters` reverted to pure/synchronous — `TagExpansionMap` (Core) and
  `ITagHierarchyReadService`/`ServerTagHierarchyCache` (Server) landed exactly per the resolutions
  above; `ServerTagWriteService`'s three write methods invalidate the cache post-commit. Full
  record: `workplan.md` §"WU-ApplyFiltersPurity"; `audit/Discovery.md` §"WU-ApplyFiltersPurity
  note"; `audit/Tags.md` §"WU-ApplyFiltersPurity Stage note"; convention:
  `layer2-services.md` §"Reference-Data Caching". `dotnet test` green (2,374), run twice.

## C. L6 cells marked verified but never measured / known-missing indexes

Context for all of C: `design/L6-reconciliation-matrix.md` is explicitly *"evidence for a later build/measure pass, not the
pass itself,"* and a live `pg_indexes` sweep has **not** been run since the 2026-07-07 discovery that six
`user_story_interactions` filtered indexes had silently collapsed to one. Several L6=5 cells therefore assert more than
was measured. The owner's standing rule is "always measure."

- [x] **C1 — Tag-chip substring search — MEASURED, trigram REJECTED (WU-TagFanon, 2026-07-26)** `[index-unverified · med · anytime]`
  - Grid: F11/F12 L6=5 (unchanged, now genuinely measured).
  - Source: `audit/Tags.md` §"WU-TagFanon Stage note" → L6 paragraph.
  - Context: F11's L6 note deferred a `pg_trgm` GIN index "under R4 until tag counts grow". The
    WU-TagFanon seed generator grew the vocabulary and made it measurable for the first time.
    **Measured: 0.079 ms over 136 tags, 2 buffers** — a trigram index would be pure overhead.
    Recorded as a measured decision, not an assumption; re-measure if the vocabulary grows an order
    of magnitude. **Still open:** `SearchStoriesByTitleAsync`'s `ILIKE` (F31/F32 — untouched here)
    and `GetTagsByTypeAsync`'s composite column order.

- [ ] **C2 — Recommendations filter+sort composite never built** `[index-unverified · med · anytime]`
  - Grid: F27 L6=5; F55 L6=N/A (masks the shared need); F33 L6=2 (transparent there).
  - Source: `L6-reconciliation-matrix.md` Recommendations; `L6-intent-ledger.md` #964/#969.
  - Context: The `(story_id, status_id, is_highlighted_by_author DESC, date_posted DESC)` composite was designed but never built. Cross-cutting: `GetForStoryAsync` (F27), `GetMyPickCandidatesAsync` (F55 Spotlight), and Manual Tree Search all rely on this shape.

- [ ] **C3 — `GetIncomingVouchesAsync` sort unindexed** `[index-unverified · low · anytime]`
  - Grid: F19 L6=5.
  - Source: `L6-reconciliation-matrix.md` Following/Vouches; `L6-intent-ledger.md` #1121.
  - Context: Sorts by `date_vouched` with only a plain FK index on the non-leading PK column serving it.

- [ ] **C4 — F49 L6 never measured (F15 half CLOSED, WU-TagFanon 2026-07-26)** `[index-unverified · med · anytime]`
  - Grid: F49 L6=5; F15 L6=5.
  - Source: `L6-reconciliation-matrix.md` Messaging + Saved-Tag + "Gaps found in already-Stage-5 cells."
  - **F15 half closed:** SeedTool now generates saved tag selections + entries over the seeded tag
    vocabulary (745 selections / 2,926 entries at default scale), so the Saved-Tag L6 claim is
    measurable rather than asserted.
  - **Still open — F49 Messaging:** no SeedTool generator exists for conversations/messages, so its
    L6=5 remains flipped-without-measurement, and it still lacks a
    `conversation_participants(user_id, is_archived)` inbox composite.

- [ ] **C5 — F9 Series L6=N/A hides a wrong index** `[index-unverified · low · anytime]`
  - Grid: F9 L6=N/A.
  - Source: `L6-reconciliation-matrix.md` Series & Story Lineage (WRONG row).
  - Context: `GetSeriesByAuthorAsync`'s sort is served by `ix_series_author_id_name` (keyed by name, not date). Matrix says the N/A should be *reconsidered*, not skipped.

- [ ] **C6 — Comment "golden" indexes proven by similarity only** `[index-unverified · low · anytime]`
  - Grid: F23/F24 L6=5.
  - Source: `L6-reconciliation-matrix.md` Comments + "Gaps found in already-Stage-5 cells."
  - Context: Only the chapter comment shape was measured (−98.8%); blog/group/profile goldens inherited the claim by analogy.

---

## D. Latent correctness/security — documented but never acted on

All from `modernization-audit/deferred-work.md` (repo root, **not** under `.claude/`) §7 ("Informational — none was acted on")
unless noted. All sit under Stage-5 cells.

- [x] **D1 — `GetRecommendedStoryIdsByUserAsync` omits `StatusId==Approved` filter — RESOLVED (WU-RecLifecycle, 2026-07-25)** `[latent-risk · med · with A4]`
  - Grid: F28/F30 L2/L3=5.
  - Source: `modernization-audit/deferred-work.md` §7 (`ServerRecommendationReadService:152-153`).
  - Context: Its sibling reads apply the status filter; this one doesn't. The "worth checking whether
    WU34 makes this live" question was settled 2026-07-24: **latent, not live** — WU34 added recs as
    report targets but never wired status rejection; nothing writes non-Approved rows yet. Fixed
    inside **A4**'s WU-RecLifecycle (where `NeedsRevision`/`Rejected` rows start existing), with the
    regression test the method never had.

- [x] **D2 — Poll `by-blog-post` leaks draft metadata — RESOLVED, and it was the tip of a 38-surface class (WU-ParentVisibility, 2026-07-26)** `[latent-risk · low → the class was med/high · anytime]`
  - Grid: F37 all built cells=5 (unchanged — no cell moved anywhere in the sweep).
  - Source: `modernization-audit/deferred-work.md` §7.
  - Context: `GET /api/polls/by-blog-post/{id}` returns poll name/description for polls attached to an *unpublished draft* blog post (tallies/voters are blanked, metadata isn't).
  - **Resolution: the item was real but understated on three axes.** (1) More data than name/description — the full option-text list, the owner's username, and `ConfigLocked`, a boolean side-channel disclosing *whether anyone had voted* even with tallies zeroed. (2) More gates than "draft" — the same missing join bypassed the mature-rating/reveal gate and the `IsTakenDown` filter too. (3) More surfaces — `GET /api/polls/{pollId}` had the identical defect and is enumerable by integer id, and `VoteAsync` let any authenticated user vote on a draft's poll, setting `ConfigLocked` and freezing the author's config *before publication*.
  - **Scoping it revealed a class, not a bug.** A sweep of all 29 read services and 26 write services found **38 violating surfaces** across 12 clusters. Notable siblings: an M-audience group's member roster and blog posts readable anonymously; a mature-off account able to **join** an M group (unlocking membership-gated writes and M-content notification fan-out); `RecordSuccessAsync` farming real site badges off guessed ids; `SubmitReportAsync` with no existence check at all; and a Private profile's contents reachable anonymously through manual tree search — the last being a surface `ProfileVisibilityGuard`'s own doc lists as protected, missed by the earlier WU-AccessGate sweep.
  - **Now conditionality kind (g)** in `identity-and-authorization.md`, with three new guards and a 27-test `ParentVisibilityContractTests` suite as the enforcement mechanism (docs alone had already failed once here). Full narrative: `workplan.md` WU-ParentVisibility; `status.md` Global Conditions.

- [x] **D3.1 — Groups: missing cross-group folder-ownership validation — RESOLVED (WU-GroupsL5b, 2026-07-25)** `[latent-risk · low · anytime]`
  - Grid: F39 all=5 (unchanged).
  - Source: `modernization-audit/deferred-work.md` §7 (split from the original **D3**, 2026-07-25 — see D3.2 below for the other half).
  - Context: `AssignStoryToFolderAsync` didn't verify `folder.GroupId == groupStory.GroupId` — an admin of group A could file A's story into a group-B folder id via direct API use, no UI needed.
  - Residual: **done 2026-07-25**, folded into the B6 fix since both landed in the exact same method (`AssignStoryToFolderInternalAsync`). Now rejects a cross-group folder id with `KeyNotFoundException` — identical to a genuinely nonexistent folder, so the response never discloses that the id exists in another group. New Integration coverage at both the service and HTTP layers (previously zero tests existed for assign/unassign at all). Detail: `workplan.md` WU-GroupsL5b; `audit/Groups.md` F39 Stage note.

- [x] **D3.2 — Recommendations: missing recommendation↔story ownership validation — RESOLVED (WU-RecLifecycle, 2026-07-25)** `[latent-risk · low · anytime]` — *The "future Recommendations-refinement session" this was deferred to was WU-RecLifecycle (split from D3, 2026-07-25). `RecordAttributionSourceAsync` now verifies the rec exists and belongs to the claimed story; Integration-tested.*
  - Grid: F30 all=5.
  - Source: `modernization-audit/deferred-work.md` §7.
  - Context: `RecordAttributionSourceAsync` never checks `recommendationId` exists/belongs to `storyId` (bogus self-attribution can later feed credit via `RecordSuccessAsync`). Unrelated code path to D3.1 (Recommendations, not Groups) — the user split the original combined D3 item rather than fold this half into the Groups fix, since it belongs with dedicated Recommendations work instead.

- [ ] **D4 — Code-economy items (not the disclosed "extract-or-not" seams)** `[polish · low · anytime]`
  - Grid: affected cells all Stage 5.
  - Source: `modernization-audit/deferred-work.md` §3 & §6.
  - Context: **MA-107** DI double-registration (7 clusters register the write class twice → two instances per scope); **MA-408** `SavedTagSelection` N+1 (`GetPublicSelectionsByUserAsync` loops `HydrateDetailAsync`); **MA-006** `ContentSurface` hardcodes 3 `ReadingBackground` palettes as raw hex in a `style=` attr (`layer4-style.md` itself calls this a defect the CI token-checker can't catch); **MA-007** leftover `FrameStyle` magic-int param; **MA-211** field-copy idiom divergence in `ServerStoryArcWriteService`.

- [x] **D5 — Client 401-mapping deviations + a shipped behavior change — CLOSED (WU-ErrorHandling2, 2026-07-30)** `[latent-risk · low · anytime]`
  - Grid: F16/F17/F38/F49 L5=5 (unchanged).
  - Source: `modernization-audit/deferred-work.md` §8; `fix-status.md` line 131.
  - Context: `ClientGroupWriteService`, `ClientMessagingWriteService`, `ClientUserStoryInteraction*` still use deviant 401-mapping. Separately, ~14 converted services now map bare-401 → `InvalidOperationException`, routing expired-cookie 401s to the generic-error path instead of the forbidden banner.
  - **Resolution: both halves closed together, folded into WU-ErrorHandling2** (the "shipped behavior change" turned out to be the same root cause as the deviant mappings — no real per-service 401 semantics, just staleness predating a proper session-vs-permission distinction). New `SessionExpiredException` (401) is now distinct from `UnauthorizedAccessException` (403) everywhere; ten private per-service translators (including `ClientGroupWriteService`, `ClientUserStoryInteractionReadService`'s shared bookshelf/write translator) collapsed onto the shared `ClientHttpHelpers` helpers, which now construct `SessionExpiredException` for every 401. `ClientMessagingWriteService` keeps its documented 403-disambiguation deviation (unrelated to the 401 question) but delegates the 401/404/5xx arms. A new `ErrorAlert` component gives the expired-session case an actual affordance (inline Sign-in link) instead of the old generic-error path. Detail: `workplan.md` WU-ErrorHandling2; `error-handling.md` §"The API error envelope".

---

## E. Cross-cutting work with no grid cell at all

- [x] **E1 — WU-ErrorHandling2 (`ProblemDetails` envelope + client HTTP translation) — DONE (2026-07-30)** `[off-grid · med · pre-launch]`
  - Grid: L5 column reads 5 sitewide (unchanged — the cross-cutting shape this tracker exists for).
  - Source: `error-handling.md` §"The API error envelope" (was §"Deferred (Phase-5-adjacent)");
    `roadmap.md` §"Recommended next work units" (row struck).
  - Context: WU-ErrorHandling deferred the API error-envelope + full client-service HTTP error-translation half; "design still not done." The global flip made the HTTP surface testable but the error-shaping half was unbuilt.
  - **Resolution: built in full — envelope, endpoint audit, client translation, and a session-expiry UI affordance.** `AddProblemDetails()` + a `/api`-scoped `ApiExceptionHandler` (traceId-carrying 500s); `EndpointHelpers.ExecuteWriteAsync` renamed `ExecuteAsync` and applied to every typed-exception-throwing read endpoint (found and fixed a live gap: `StoryEndpoints`' filter/random-batch/filter-candidates reads were still 500ing on malformed ship input); new `SessionExpiredException`/`ServerFaultException` Core types; `ClientHttpHelpers` gained `ThrowIfReadFailedAsync`, ten private write translators unified, ten gated read services translated; new `ErrorAlert.razor` adopted across 19 SharedUI components (8 SOLO editor pages named as a follow-up, not silently dropped). Also closed **D5** in the same pass. Detail: `workplan.md` WU-ErrorHandling2.

- [ ] **E2 — AngleSharp 0.17.1 mXSS (CVE-2026-54570) — accepted-risk live CVE** `[off-grid · high · launch]`
  - Grid: security is a global condition, no cell; F66 reads 5.
  - Source: `security.md` §"Accepted-risk register — AngleSharp"; `roadmap.md` Phase 7.
  - Context: Transitively pinned by HtmlSanitizer 9.x (can't just bump). Mitigated by the sanitizer allow-list; root-cause fix = replace `Ganss.Xss` with a custom AngleSharp-1.5.2 sanitizer behind the existing seam. Vuln scan stays report-only until then. A shipped known-vulnerable dep is invisible on a feature×layer grid.

- [ ] **E3 — Security Phase-7 Deferred Register** `[off-grid · high · launch]` — *Deliberate (deployment-time).*
  - Grid: none.
  - Source: `security.md` §"Phase-7 Deferred Register"; `roadmap.md` Phase 7.
  - Context: Cloudflare TLS Full-Strict, origin firewall to CF ranges, `ForwardedHeaders`, serving uploads from a separate origin (+ CSP `img-src` tightening), Turnstile on registration, HSTS tuning, real-domain CSP-enforce verification, and promoting the vuln scan from report-only to a hard gate.

- [ ] **E4 — Default OG/social image is an SVG, not a raster** `[off-grid · low · launch]`
  - Grid: `Seo/` cluster has no row; consuming features (F4/F6/F20/F35/F38) read 5.
  - Source: `audit/Seo.md` "Open."
  - Context: Default social image reuses `default-cover.svg`. Crawlers often won't rasterize SVG — a real 1200×630 raster is needed before OG rollout is launch-ready.

- [ ] **E5 — N≥2 horizontal-scale work** `[off-grid · low · post-launch]` — *Deliberate (activates only at N≥2).*
  - Grid: none (L7 dissolved; buffers are Stage 5 at N=1).
  - Source: `horizontal-scaling.md` (~lines 88–90).
  - Context: Load-balancer session affinity for Blazor Server circuits + the signal-buffer → Valkey body-swap are "designed, not yet built." Distinct from the buffer work the docs already cover.

- [ ] **E6 — Access-gate un-hardened edges** `[off-grid · med · pre-launch]`
  - Grid: F66 all=5; F55 L2=5.
  - Source: `audit/AccessGate.md` "Open (deferred, tracked)" + WU-AccessGate2 "Deliberately deferred, recorded."
  - Context: No rate limiting on `/content-gate/*` consent endpoints ("revisit on abuse"); interim AO3-style willingness wording (final copy + any age-assertion element is a counsel/row-10 item — legally load-bearing); optional hard-mode preference ("M URLs 404 instead of interstitial") not built; spotlight non-M pool floor.

---

## F. Off-grid open decisions & whole phases

From `roadmap.md` "Decisions that need you" + `middle-addendum.md` §3. None maps to a grid row; a grid-scanner sees
built rows at 5 and no signal these exist.

- [x] **F1 — Homepage is unbuilt and design-blocked (WU-Home)** `[decision · high · mvp]` —
  **CLOSED 2026-07-28 (WU-Home):** decision row 2 resolved (`roadmap.md` §Resolved) and built —
  home = the community page (Welcome blurb → Spotlight → active-poll-if-open → community-discourse
  link cluster). No story discovery, no personal strip. Companion WU-SiteNews closed the
  site-announcement gap the resolution surfaced.
  - Source: `roadmap.md` Decision row 2 + Phase 2 item 1; `workplan.md`.

- [ ] **F2 — Launch-readiness mechanics (Phase 7)** `[decision · high · launch]`
  - Source: `roadmap.md` Phase 7 + Decision row 4; `middle-addendum.md` §2.
  - Context: Deploy mechanism, config/secrets contract, prod-migration convention, backup+restore drill, uptime/alerting, telemetry-destination deploy, TLS/domain. Entire launch phase is off-grid.

- [ ] **F3 — Legal/policy track** `[decision · high · launch]`
  - Source: `roadmap.md` Decision row 10; `middle-addendum.md` §3 items 1–7.
  - Context: ToS, privacy policy, **DMCA designated agent** (addendum flags this as highest-value/lowest-cost), COPPA age assertion, mature-content interstitial-vs-verification, GDPR erasure confirmation, trademark/fan-project disclaimer. The one-line row 10 badly understates a multi-item backlog.

- [ ] **F4 — Email production path (provider + domain + deliverability DNS)** `[decision · med · launch]`
  - Source: `roadmap.md` Decision row 8 / `middle_plan_v2.md` Resolved "Email mechanism"; `middle-addendum.md` §3 #13.
  - Context: Mechanism is resolved (config-only swap), but provider + sending domain are unchosen. **SPF/DKIM/DMARC DNS is not even in the Phase-7 checklist** — directly gates whether confirmation/reset mail reaches real users. WU-Email flipped no cells, so email's production gap is fully invisible on the grid.

- [ ] **F5 — Beta logistics** `[decision · med · beta]`
  - Source: `roadmap.md` Decision row 6.
  - Context: Who/how-many testers, invite mechanism, feedback channel. Phase 6 gate.

- [ ] **F6 — Accessibility scope/depth (WU-A11y)** `[decision · med · pre-launch]`
  - Grid: F65 L4-Style=1, L4.5=1 (*partially* transparent — cells show Stage 1, but the blocking decision isn't visible).
  - Source: `roadmap.md` Decision row 12; `grid_axes.md` Feature 65; `audit/Accessibility.md`.
  - Context: Full WCAG-AA audit vs. targeted axe pass; whether to add an a11y test tier — undecided, and it gates the work.

- [ ] **F7 — Operational-resilience gaps (no WU, no row, no decision)** `[off-grid · med · launch]`
  - Source: `middle-addendum.md` §3 items 8–14.
  - Context: Incident-response runbook (#8); LB/session affinity distinct from the Valkey swap (#9); health-check production-exposure *mechanism* undecided (#10); backup/DR beyond the DB incl. Grafana LGTM config (#11); cost/capacity monitoring (#12); **no staging environment** (#14). Each documented as a gap with no owner/phase.

- [ ] **F8 — Growth items deliberately un-formalized** `[off-grid · low · post-launch]` — *Deliberate (revisit opportunistically).*
  - Source: `middle-addendum.md` §3 items 19–21.
  - Context: RSS/Atom feeds (#19), traffic analytics (#20), PWA `manifest.json` (#21). Conscious skips, no WU/row.

---

## G. Doc contradictions & stale files (drift already present)

These matter most for *this* doc's purpose: they make the prose surfaces untrustworthy, which is where everything above hides.

- [x] **G1 — Login-enforcement described as unbuilt though it shipped** `[doc-drift · med · anytime]` — **CLOSED 2026-07-27 (WU-DocHygiene3):** `content-safety.md` §"Login enforcement" retensed to shipped-WU38a; the `workplan.md` "Planned" entry was rewritten 2026-07-27 (WU-DocHygiene) to core-shipped + residual.
  - Source (was stale, now fixed): `content-safety.md`; `workplan.md` "Planned/not-yet-built" WU-AccountEnforcement entry. Source (built): `security.md` §"Account-Status Enforcement (WU38a)".
  - **Residual CLOSED 2026-07-30 (WU-AccountEnforcement):** `RefreshSignInAsync` turned out not to be usable (caller-scoped, can't reach a moderator's target). Built instead: `AccountStatusBanner` re-reads status live via `IAccountStatusReadService` on every in-app navigation (the `MessagesNavLink` pattern), now covering Warned/Suspended/Banned. `NotificationBell`'s identical mid-session staleness (found during this WU, not previously tracked) closed the same way. See `security.md` §"Account-Status Enforcement", `identity-and-authorization.md` §"Account Status Is Display-Only, Read Live".

- [x] **G2 — `audit/Lookups.md` overstates remaining work (stale Stage-4)** `[doc-drift · low · anytime]` — **CLOSED 2026-07-27 (WU-DocHygiene):** the Feature 2 section was rewritten as an L1 Stage-5 record; all five divergences verified resolved against code (see its 2026-07-27 Stage note).

- [x] **G3 — Stale "deferred workers" note** `[doc-drift · low · anytime]` — **CLOSED 2026-07-27 (WU-DocHygiene3):** the Post-MVP "Deferred workers" bullet now records both workers built 2026-07-15 (WU-NotificationCleanup / WU-UserStatRecalc, archive).

---

## H. Design/polish & test-hygiene (lower priority)

- [ ] **H1 — `Error.razor` server error page mismatch** `[polish · low · pre-launch]`
  - Grid: `Errors/` cluster has no row.
  - Source: `design/surface-registry.md` §"Sweep completion (Phases B–F)" + Synthesis 8.
  - Context: Last surviving Surface-Registry mismatch — bare on canvas, and ships the template "Development Mode" boilerplate user-facing. Listed "remaining known-open, low priority."

- [ ] **H2 — `StoryDeck` skeleton + `UserMenu`/`CreateMenu` flyout browser-verify** `[polish · low · anytime]`
  - Grid: cross-cutting chrome, no row.
  - Source: `layer4-style.md` §"StoryDeck (WU14)" and §"Top bar (MainLayout)."
  - Context: (a) `StoryDeck` loading-skeleton upgrade (gray placeholder cards) is a deferred additive swap behind the `Stories is null` branch. (b) The click-driven flyout-open was never explicitly browser-verified; likely exercised incidentally by later browser passes but no note confirms it.

- [ ] **H3 — Cover-art upload never browser-verified** `[test-gap · low · anytime]`
  - Grid: F4 L4.5=5.
  - Source: `audit/Stories.md` "L4.5-Browser verification (2026-07-01) … Coverage exception."
  - Context: The story cover-art `InputFile` → `IImageStorageService` path couldn't be driven during the F4 browser pass ("re-verify when a browser pass with working file upload is available"). No later wave records driving it — the L4.5=5 implies whole-feature browser verification that one path didn't get.

- [ ] **H4 — Import: paste-from-Word fidelity + PDF import** `[test-gap / scope-cut · low · anytime]` — *PDF is a deliberate future format.*
  - Grid: F63 all built cells=5.
  - Source: `audit/Import.md` Stage-5 note + "Settled" + "Open."
  - Context: Paste-from-Word fidelity is an outstanding *manual* verification (explicitly non-gating). PDF import is a deferred future format candidate.

- [x] **H5 — Notification-UI test coverage gaps — CLOSED IN FULL (WU-TagFanon, 2026-07-26)** `[test-gap · low · with B1]`
  - Grid: F42/F43 L3-Logic/L3.5=5 (unchanged).
  - Source: `audit/Notifications.md` §"WU-TagFanon slice".
  - Both halves done: `FakeNotificationWriteService` joined the fakes catalog (it was needed to
    component-test the new notification surface at all), and the anonymous-`NotificationBell`
    regression test is written — it asserts an anonymous render resolves NO notification services,
    which is the actual failure mode behind the crash fixed 2026-07-13. No longer folded into B1.

- [ ] **H6 — Minor by-design gaps on Stage-5 cells** `[polish · low · anytime]`
  - Context (each grid-5, flagged "future pass" in its audit file):
    - Group **last-admin-leave** unhandled — `layer2-services.md:625`.
    - **Self-likes** accepted on own comment (F25) — `audit/Comments.md`.
    - **Spoiler flag not editable** after a comment is posted (F26) — `audit/Comments.md`.
    - ~~**Tag L1 length drift** vs spec (F11–13): `SpriteIdentifier` 50 vs 100, `Description` 512 vs 500~~ — **FIXED WU-TagFanon 2026-07-26** (rode that WU's migration; `TagValidations` constants + the editor `maxlength`s follow).
    - **VouchButton `IsFollowing` staleness** (F19): vouch affordance only appears after reload if follow+vouch happen in one visit — `audit/Following.md`.
    - **StoryLineage** cosmetic item "revisit pre-launch if at all" — `layer2-services.md:1022`.
    - **Layer-8 strict "no-mature routing" toggle** deferred — `layer8-data-marts.md:257` (same "hard-mode" idea as E6).

- [ ] **H7 — Open test-hygiene backlog** `[test-gap · low · anytime]`
  - Source: `test-hygiene-manifest.md` (retired 2026-07-27 — this item is now the ledger of record; the manifest's §5 table lists the individual merge targets).
  - Context, all that remains open: the 25 non-mobile **C-consolidate** merges (manifest §5); Integration `NotificationServiceTests` flake fix; Integration format-dupes; Unit tautology trim. The formerly-deferred `*Mobile` test-file deletions were discharged by WU-ResponsiveMerge (2026-07-18) — no `*Mobile*` test file exists. Suite is green, so nothing signals the pending cleanup.
  - Note: `CanalaveTypeaheadTests.Escape_ClosesDropdown_WithoutSelecting` is a known pre-existing intermittent flake (passes on isolated re-run).

- [ ] **H8 — MA-610 Identity scaffold prune-vs-keep** `[decision · low · pre-launch]` — *Partially disclosed (the 🧑 deliberately-not-done list in `workplan.md`'s WU-AuditFixPass-2 entry).*
  - Grid: F1 mostly 5 (L4=1).
  - Source: `modernization-audit/deferred-work.md` §2; `report.md` MA-610.
  - Context: ~1,325 LOC of scaffolded 2FA/passkey/external-login pages with no provider configured. A product decision (prune vs keep), untouched. Also: MA-112/608/012 just-in-time org moves (`UserDeletionService`, `MainLayout.razor` still under `Server/Components/Layout/`, `Core/Models` scaffold, `NotFound.razor`) — deferred by the "empty folders just-in-time" convention.

---

## Already-closed — do NOT re-report (checked during the 2026-07-24 audit)

These earlier deferrals were resolved by later work; the grid is correct. Listed so a future pass doesn't resurface them:
- Parent-visibility across polls, comments, groups, recommendations, arcs, interactions, following, reports and the buffered writes → **WU-ParentVisibility (2026-07-26)**. With D2 closed, `modernization-audit/deferred-work.md` §7 has **no live code items left** — its only remaining entries are the dev-diagnostics exposure (environmental, not a code path) and the avatar-URL CDN model (explicitly accepted).
- Groups L5 rows 38–40 → **WU-GroupsL5 (2026-07-24)**.
- Chapter/arc/version L4.5 (F6/F7/F8, set 5→2 on 2026-07-12) → **WU-ChapterArcBrowserPass (2026-07-24)**.
- Desktop/Mobile page merges → **WU-ResponsiveMerge (2026-07-18)**.
- Systematic endpoint-authz sweep + Tier-2/Tier-3 batches + MA status-code seams → **WU-AuditFixPass / -2 (2026-07-18)**.
- `bg-surface-hover` dead classes + global keyboard focus-visible ring → **WU-DesignSystem Phases B–F (2026-07-10)**.
- Workers 57 & 58 → built **2026-07-15**.
- SEO robots/sitemap/canonical/OG tags + mature-`noindex` → **WU-Seo / WU-AccessGate (2026-07-23)**.
- Account-status *core* enforcement → **WU38a (2026-07-11)** (but see G1 for the doc-drift + responsiveness residual).
- 11 inflated L5 cells with no client code → legitimately earned via **WU-GlobalFlip (2026-07-13)**.
- ImageStorage L5 upload path (stale "not designed" note) → **WU-L5Sweep / WU-GlobalFlip**.
- Blazored.Typeahead chrome → replaced by in-house `CanalaveTypeahead` at the global flip.
- Ship filter restore path (B11) → **WU-DiscoveryFilterRestore (2026-07-28)**, alongside the
  selection permalink (**WU-SelectionPermalink**, same day) that answered decision row 13's sharing
  half.
