# Hidden Deferrals Tracker

**What this is.** A checklist of deferred/pending work that is **not transparent from the `status.md` grid** —
items where the grid cell reads Stage 5 (or N/A), yet real work remains. Produced by a manual audit on
**2026-07-24**. Feature 53 (External Story Links & Verification) is excluded — a separate planning session owns it.

**Status: snapshot, not authoritative.** This file is a hand-maintained convenience list, deliberately kept
*outside* the governed process docs (`status.md` / audit files / skills) at the owner's request. It can go stale.
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

- [ ] **A4 — Recommendation approval lifecycle (auto-approve shortcut)** `[scope-cut · med · beta]`
  - Grid: F27 all=5; F48 (Story Approval Workflow) built cells=5.
  - Source: `audit/Recommendations.md` Feature 27 L2; `audit/Moderation.md` Feature 48 ("Rec-approval wiring deferred"); code `ServerRecommendationWriteService.cs` (`StatusId = ApprovedStatusId, // auto-approve MVP (moderation deferred to WU34)`).
  - Context: Spec §5.6 defines a Pending → author-approval → moderator-review lifecycle plus a `/mod/submissions` rec-approval tab. Recs currently write `StatusId=Approved` directly. WU34 built moderation but explicitly left rec-approval out.
  - **Coupling:** see **D1** — the moment this lifecycle goes live, `GetRecommendedStoryIdsByUserAsync`'s missing status filter becomes a real leak. Do them together.

- [ ] **A5 — Fanonize notify/migrate flow (spec §14)** `[scope-cut · low · anytime]`
  - Grid: F11/F12 L2/L3/L3.5=5.
  - Source: `audit/Tags.md` F11 & F12 settled notes.
  - Context: `IsFanon` is editable, but the workflow that notifies authors whose `OcName` matches a newly-fanonized tag and offers migration is unbuilt. Only the notification enum seam (`TagUpdateSuggestion = 26`) exists.

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
  - Source: `workplan.md` Planned/not-yet-built; `cross-cutting.md` §Rich Text; `middle_plan_v2.md` Phase 4.
  - Context: WU6 shipped the desktop editor toolbar only. A compact mobile toolbar is re-scoped onto the adaptivity ladder's rung-3 trigger in the future mobile phase.

---

## B. Built-but-inert — plumbing exists, nothing drives it

- [ ] **B1 — Notification email fan-out (`EmailEnabled` is inert)** `[inert · med · beta]`
  - Grid: F41/F42/F43 L2=5.
  - Source: `layer2-services.md` §Notification settings; `audit/Notifications.md`; `middle_plan_v2.md` Phase 6 "WU-NotifEmail" / Resolved "Email mechanism."
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

- [ ] **B5 — Private-message archive/unarchive has no UI** `[inert · low · anytime]`
  - Grid: F49 L3-Logic/L3.5/L4/L4.5=5.
  - Source: `audit/Messaging.md` L4.5 "Observation (not a defect)."
  - Context: `SetArchivedAsync` + the "Archived" label exist and are tested, but no UI control surfaces them. Capability dead-ends at the service layer; every messaging cell reads Stage 5.

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

## C. L6 cells marked verified but never measured / known-missing indexes

Context for all of C: `audit/L6-reconciliation-matrix.md` is explicitly *"evidence for a later build/measure pass, not the
pass itself,"* and a live `pg_indexes` sweep has **not** been run since the 2026-07-07 discovery that six
`user_story_interactions` filtered indexes had silently collapsed to one. Several L6=5 cells therefore assert more than
was measured. The owner's standing rule is "always measure."

- [ ] **C1 — Substring search paths unindexed (need `pg_trgm` GIN)** `[index-unverified · med · anytime]`
  - Grid: F31/F32 L6=5; F11/F12 L6=5.
  - Source: `L6-reconciliation-matrix.md` Stories/Discovery + Tags sections; `L6-intent-ledger.md` #307/#1166.
  - Context: `SearchStoriesByTitleAsync` and `SearchTagChipsAsync` run `ILIKE '%term%'` with no supporting index; leading-wildcard can't use a B-tree. `GetTagsByTypeAsync`'s composite also has wrong column order.

- [ ] **C2 — Recommendations filter+sort composite never built** `[index-unverified · med · anytime]`
  - Grid: F27 L6=5; F55 L6=N/A (masks the shared need); F33 L6=2 (transparent there).
  - Source: `L6-reconciliation-matrix.md` Recommendations; `L6-intent-ledger.md` #964/#969.
  - Context: The `(story_id, status_id, is_highlighted_by_author DESC, date_posted DESC)` composite was designed but never built. Cross-cutting: `GetForStoryAsync` (F27), `GetMyPickCandidatesAsync` (F55 Spotlight), and Manual Tree Search all rely on this shape.

- [ ] **C3 — `GetIncomingVouchesAsync` sort unindexed** `[index-unverified · low · anytime]`
  - Grid: F19 L6=5.
  - Source: `L6-reconciliation-matrix.md` Following/Vouches; `L6-intent-ledger.md` #1121.
  - Context: Sorts by `date_vouched` with only a plain FK index on the non-leading PK column serving it.

- [ ] **C4 — F49 & F15 L6 flipped to Stage 5 but never measured** `[index-unverified · med · anytime]`
  - Grid: F49 L6=5; F15 L6=5.
  - Source: `L6-reconciliation-matrix.md` Messaging + Saved-Tag + "Gaps found in already-Stage-5 cells."
  - Context: No SeedTool generator exists for these, so they were flipped to 5 without measurement — fails the "always measure" bar the project set. F49 additionally lacks a `conversation_participants(user_id, is_archived)` inbox composite.

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

- [ ] **D1 — `GetRecommendedStoryIdsByUserAsync` omits `StatusId==Approved` filter** `[latent-risk · med · with A4]`
  - Grid: F28/F30 L2/L3=5.
  - Source: `modernization-audit/deferred-work.md` §7 (`ServerRecommendationReadService:152-153`).
  - Context: Its sibling reads apply the status filter; this one doesn't. Latent because recs currently auto-approve (**A4**) — but the instant rec-moderation lands, pending/rejected rec story-ids leak onto the public profile tab. Audit flagged ⚠️ "worth checking whether WU34 makes this live" — never checked.

- [ ] **D2 — Poll `by-blog-post` leaks draft metadata** `[latent-risk · low · anytime]`
  - Grid: F37 all built cells=5.
  - Source: `modernization-audit/deferred-work.md` §7.
  - Context: `GET /api/polls/by-blog-post/{id}` returns poll name/description for polls attached to an *unpublished draft* blog post (tallies/voters are blanked, metadata isn't).

- [x] **D3.1 — Groups: missing cross-group folder-ownership validation — RESOLVED (WU-GroupsL5b, 2026-07-25)** `[latent-risk · low · anytime]`
  - Grid: F39 all=5 (unchanged).
  - Source: `modernization-audit/deferred-work.md` §7 (split from the original **D3**, 2026-07-25 — see D3.2 below for the other half).
  - Context: `AssignStoryToFolderAsync` didn't verify `folder.GroupId == groupStory.GroupId` — an admin of group A could file A's story into a group-B folder id via direct API use, no UI needed.
  - Residual: **done 2026-07-25**, folded into the B6 fix since both landed in the exact same method (`AssignStoryToFolderInternalAsync`). Now rejects a cross-group folder id with `KeyNotFoundException` — identical to a genuinely nonexistent folder, so the response never discloses that the id exists in another group. New Integration coverage at both the service and HTTP layers (previously zero tests existed for assign/unassign at all). Detail: `workplan.md` WU-GroupsL5b; `audit/Groups.md` F39 Stage note.

- [ ] **D3.2 — Recommendations: missing recommendation↔story ownership validation** `[latent-risk · low · anytime]` — *Deliberately deferred to a future Recommendations-refinement session (split from D3, 2026-07-25).*
  - Grid: F30 all=5.
  - Source: `modernization-audit/deferred-work.md` §7.
  - Context: `RecordAttributionSourceAsync` never checks `recommendationId` exists/belongs to `storyId` (bogus self-attribution can later feed credit via `RecordSuccessAsync`). Unrelated code path to D3.1 (Recommendations, not Groups) — the user split the original combined D3 item rather than fold this half into the Groups fix, since it belongs with dedicated Recommendations work instead.

- [ ] **D4 — Code-economy items (not the disclosed "extract-or-not" seams)** `[polish · low · anytime]`
  - Grid: affected cells all Stage 5.
  - Source: `modernization-audit/deferred-work.md` §3 & §6.
  - Context: **MA-107** DI double-registration (7 clusters register the write class twice → two instances per scope); **MA-408** `SavedTagSelection` N+1 (`GetPublicSelectionsByUserAsync` loops `HydrateDetailAsync`); **MA-006** `ContentSurface` hardcodes 3 `ReadingBackground` palettes as raw hex in a `style=` attr (`layer4-style.md` itself calls this a defect the CI token-checker can't catch); **MA-007** leftover `FrameStyle` magic-int param; **MA-211** field-copy idiom divergence in `ServerStoryArcWriteService`.

- [ ] **D5 — Client 401-mapping deviations + a shipped behavior change** `[latent-risk · low · anytime]`
  - Grid: F16/F17/F38/F49 L5=5.
  - Source: `modernization-audit/deferred-work.md` §8; `fix-status.md` line 131.
  - Context: `ClientGroupWriteService`, `ClientMessagingWriteService`, `ClientUserStoryInteraction*` still use deviant 401-mapping. Separately, ~14 converted services now map bare-401 → `InvalidOperationException`, routing expired-cookie 401s to the generic-error path instead of the forbidden banner. Labeled "optional cleanup, not a bug" — but L5 reads uniformly done.

---

## E. Cross-cutting work with no grid cell at all

- [ ] **E1 — WU-ErrorHandling2 (`ProblemDetails` envelope + client HTTP translation)** `[off-grid · med · pre-launch]`
  - Grid: L5 column reads 5 sitewide (implies the client HTTP path is complete).
  - Source: `error-handling.md` §"Deferred (Phase-5-adjacent) — WU-ErrorHandling2"; `middle_plan_v2.md` Phase 5.
  - Context: WU-ErrorHandling deferred the API error-envelope + full client-service HTTP error-translation half; "design still not done." The global flip made the HTTP surface testable but the error-shaping half is unbuilt.

- [ ] **E2 — AngleSharp 0.17.1 mXSS (CVE-2026-54570) — accepted-risk live CVE** `[off-grid · high · launch]`
  - Grid: security is a global condition, no cell; F66 reads 5.
  - Source: `security.md` §"Accepted-risk register — AngleSharp"; `middle_plan_v2.md` Phase 7.
  - Context: Transitively pinned by HtmlSanitizer 9.x (can't just bump). Mitigated by the sanitizer allow-list; root-cause fix = replace `Ganss.Xss` with a custom AngleSharp-1.5.2 sanitizer behind the existing seam. Vuln scan stays report-only until then. A shipped known-vulnerable dep is invisible on a feature×layer grid.

- [ ] **E3 — Security Phase-7 Deferred Register** `[off-grid · high · launch]` — *Deliberate (deployment-time).*
  - Grid: none.
  - Source: `security.md` §"Phase-7 Deferred Register"; `middle_plan_v2.md` Phase 7.
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

From `middle_plan_v2.md` "Decisions that need you" + `middle-addendum.md` §3. None maps to a grid row; a grid-scanner sees
built rows at 5 and no signal these exist.

- [ ] **F1 — Homepage is unbuilt and design-blocked (WU-Home)** `[decision · high · mvp]`
  - Source: `middle_plan_v2.md` Decision row 2 + Phase 2 item 1; `workplan.md`.
  - Context: The homepage is still an "honest minimal placeholder." WU-Home is gated on a design decision (recently-updated / featured-tags / active-SitePolls placement / layout). The front door itself isn't done, yet nothing on the grid signals it.

- [ ] **F2 — Launch-readiness mechanics (Phase 7)** `[decision · high · launch]`
  - Source: `middle_plan_v2.md` Phase 7 + Decision row 4; `middle-addendum.md` §2.
  - Context: Deploy mechanism, config/secrets contract, prod-migration convention, backup+restore drill, uptime/alerting, telemetry-destination deploy, TLS/domain. Entire launch phase is off-grid.

- [ ] **F3 — Legal/policy track** `[decision · high · launch]`
  - Source: `middle_plan_v2.md` Decision row 10; `middle-addendum.md` §3 items 1–7.
  - Context: ToS, privacy policy, **DMCA designated agent** (addendum flags this as highest-value/lowest-cost), COPPA age assertion, mature-content interstitial-vs-verification, GDPR erasure confirmation, trademark/fan-project disclaimer. The one-line row 10 badly understates a multi-item backlog.

- [ ] **F4 — Email production path (provider + domain + deliverability DNS)** `[decision · med · launch]`
  - Source: `middle_plan_v2.md` Decision row 8 / Resolved "Email mechanism"; `middle-addendum.md` §3 #13.
  - Context: Mechanism is resolved (config-only swap), but provider + sending domain are unchosen. **SPF/DKIM/DMARC DNS is not even in the Phase-7 checklist** — directly gates whether confirmation/reset mail reaches real users. WU-Email flipped no cells, so email's production gap is fully invisible on the grid.

- [ ] **F5 — Beta logistics** `[decision · med · beta]`
  - Source: `middle_plan_v2.md` Decision row 6.
  - Context: Who/how-many testers, invite mechanism, feedback channel. Phase 6 gate.

- [ ] **F6 — Accessibility scope/depth (WU-A11y)** `[decision · med · pre-launch]`
  - Grid: F65 L4-Style=1, L4.5=1 (*partially* transparent — cells show Stage 1, but the blocking decision isn't visible).
  - Source: `middle_plan_v2.md` Decision row 12; `grid_axes.md` Feature 65; `audit/Accessibility.md`.
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

- [ ] **G1 — Login-enforcement described as unbuilt though it shipped** `[doc-drift · med · anytime]`
  - Source (stale): `content-safety.md` §"Login enforcement is staged"; `workplan.md` "Planned/not-yet-built" (~line 2171). Source (built): `security.md` §"Account-Status Enforcement (WU38a)"; `audit/Moderation.md` / `audit/Identity.md`.
  - Context: WU-AccountEnforcement actually shipped as **WU38a (2026-07-11)** — `CanalaveSignInManager.CanSignInAsync` blocks Suspended/Banned, security-stamp bump kills live sessions, `AccountStatusBanner` for Warned; browser-verified. Two docs still call it deferred. A future session trusting `content-safety.md` would think a shipped security control is missing.
  - **Genuine residual (still open):** a freshly-Warned/Suspended user only sees the banner/block at next sign-in. `workplan.md:2152` notes `RefreshSignInAsync` is the ready-made tool to make it mid-session responsive — unbuilt.

- [ ] **G2 — `audit/Lookups.md` overstates remaining work (stale Stage-4)** `[doc-drift · low · anytime]`
  - Source: `audit/Lookups.md` Feature 2 L1.
  - Context: Still documents Stage-4 divergences (SearchMode/DefaultSortOrder pre-three-axis seed, vestigial `ReadStatus`/`FavoriteStatus` enums, incomplete seed matrix) that current code already resolved (`DiscoveryConfigurations.cs`). Grid `5` is correct — the audit file is stale in the *opposite* direction (claims open work that's done).

- [ ] **G3 — Stale "deferred workers" note** `[doc-drift · low · anytime]`
  - Source: `workplan.md` Post-MVP "Deferred workers" bullet (~line 2208).
  - Context: Still lists workers 57 (Notification Cleanup) & 58 (UserStat Recalc) as deferred though both were built 2026-07-15. Grid L2=5 is correct.

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

- [ ] **H5 — Notification-UI test coverage gaps** `[test-gap · low · with B1]`
  - Grid: F42/F43 L3-Logic/L3.5=5.
  - Source: `audit/Notifications.md` WU33 notes + the 2026-07-13 anonymous-crash-fix note.
  - Context: `FakeNotificationWriteService` is absent from the fakes catalog, and the anonymous-`NotificationBell` regression test (for the crash fixed 2026-07-13) is unwritten. Folded into WU-NotifEmail (B1). Tests are advisory per CLAUDE.md.

- [ ] **H6 — Minor by-design gaps on Stage-5 cells** `[polish · low · anytime]`
  - Context (each grid-5, flagged "future pass" in its audit file):
    - Group **last-admin-leave** unhandled — `layer2-services.md:625`.
    - **Self-likes** accepted on own comment (F25) — `audit/Comments.md`.
    - **Spoiler flag not editable** after a comment is posted (F26) — `audit/Comments.md`.
    - **Tag L1 length drift** vs spec (F11–13): `SpriteIdentifier` 50 vs 100, `Description` 512 vs 500 — `audit/Tags.md`.
    - **VouchButton `IsFollowing` staleness** (F19): vouch affordance only appears after reload if follow+vouch happen in one visit — `audit/Following.md`.
    - **StoryLineage** cosmetic item "revisit pre-launch if at all" — `layer2-services.md:1022`.
    - **Layer-8 strict "no-mature routing" toggle** deferred — `layer8-data-marts.md:257` (same "hard-mode" idea as E6).

- [ ] **H7 — Open test-hygiene backlog** `[test-gap · low · anytime]`
  - Source: `test-hygiene-manifest.md` header + §5.
  - Context: 25 non-mobile consolidate merges; Integration `NotificationServiceTests` flake fix; Integration format-dupes; Unit tautology trim; deferred `*Mobile` test-file deletions (pending a holistic Desktop/Mobile split assessment). Suite is green, so nothing signals the pending cleanup.

- [ ] **H8 — MA-610 Identity scaffold prune-vs-keep** `[decision · low · pre-launch]` — *Partially disclosed (status.md line 85 🧑).*
  - Grid: F1 mostly 5 (L4=1).
  - Source: `modernization-audit/deferred-work.md` §2; `report.md` MA-610.
  - Context: ~1,325 LOC of scaffolded 2FA/passkey/external-login pages with no provider configured. A product decision (prune vs keep), untouched. Also: MA-112/608/012 just-in-time org moves (`UserDeletionService`, `MainLayout.razor` still under `Server/Components/Layout/`, `Core/Models` scaffold, `NotFound.razor`) — deferred by the "empty folders just-in-time" convention.

---

## Already-closed — do NOT re-report (checked during the 2026-07-24 audit)

These earlier deferrals were resolved by later work; the grid is correct. Listed so a future pass doesn't resurface them:
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
