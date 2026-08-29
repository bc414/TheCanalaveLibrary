# Service Layer (L2) — First-Principles Foundation Audit (2026-08-03)

> **Status: evidence document, not authority.** Companion to
> [[db-schema-first-principles-audit]] (2026-08-02), which owns the logical schema; this document
> owns the **service layer** — every `I*Service` contract in Core, every `Server*` implementation,
> every `Client*` HTTP implementation, the endpoints that carry them, and the composition root.
> Method: ten parallel audit passes (one per cluster group: Stories/Series, Chapters/Export/Import,
> Discovery/Tags, Groups/Lists/Recommendations/Collaboration, Comments/Blogs/Following,
> Profiles/Moderation/Badges/Spotlight/UserDeletion, Notifications/Messaging, infrastructure
> services, UserStoryInteractions, and a whole-layer composition-root/uniformity sweep), each
> reconciling three sources — the spec (§3.5–3.27, §5.x, §6.x), the settled conventions
> (`layer2-services.md`, which supersedes the spec where it says so), and external first-principles
> reasoning about what a service layer for this exact site ought to be. Working-tree state audited
> as of 2026-08-03 (including the in-flight WU-UserModeration changes). No code, migration, or grid
> edit was made producing this report. Each finding cites its evidence (file:line) and is marked
> CONFIRMED (code path traced) or PLAUSIBLE (suspicious, not fully traced) so it can be re-verified
> before acting.
>
> **Why now:** no human has used the site; there is no data. Every service *contract* — exception
> taxonomy, null-vs-throw semantics, authorization posture, notification behavior, public counters —
> is currently free to change. The moment human testing begins, contracts get locked in by habit,
> bookmarks, and expectations, exactly as schema shapes get locked in by data. This audit separates
> (1) genuine defects to fix while free, (2) decisions to settle deliberately, (3) strengths to
> ratify so later sessions don't "fix" them, and (4) doc corrections so doctrine and code agree.

---

## 0. Verdict summary

**The foundation is architecturally sound and, in several places, exemplary.** The load-bearing
choices — CQRS-lite with inheritance, the DTO firewall, factory-per-method read contexts,
compile-time DbContext safety, named query filters as model-level invariants, the signal-buffer
trio pattern, sanitize-once-on-save with the allowlist as an interchange contract, best-effort
post-commit side effects, the composition DAG over building-block methods, and the uniform client
HTTP error-translation seam — all survive first-principles re-derivation, and the composition root
implements them with unusual discipline (zero missing endpoint gates against service-assumed auth,
complete sanitizer coverage, exact worker/test parity, no singleton-captures-scoped violations).

What remains falls into a small set of **systemic gaps** rather than random bugs — and this is the
key synthesis result: the same five failure classes recur across otherwise-excellent clusters:

1. **Lifecycle state machines exist but their transitions are not guarded** (§2.1) — the story
   approval flow can be bypassed entirely by a forged status, moderation resolves double-fire,
   account status has no legal-transition model and no reinstate path.
2. **Three features are silently inert** (§2.3) — chapter publish notifies nobody, report receipts
   are annihilated by the drop-self rule, and recommendation attribution FK-fails in its primary
   flow. All three fail without an error anywhere; none is on any open-work ledger.
3. **Nothing bounds client-supplied sizes** (§2.5) — page sizes, batch sizes, result caps, text
   lengths, and reveal rows are uncapped across nearly every cluster, mostly on anonymous
   endpoints.
4. **The TPT cascade-orphan hazard the schema audit predicted is live in exactly the two places it
   named** (§2.2), plus the moderation hard-delete path.
5. **A handful of InteractiveAuto parity breaks** (§2.7) — behaviors that differ between the
   circuit and WASM render modes, which is the one divergence class this architecture forbids and
   the one that stays invisible until the WASM pass is exercised.

Everything else is either a decision sitting on an unchosen default (§3), a doc/doctrine
correction (§4), or a ratification (§1, §5). Nothing found undermines the architecture itself.

---

## 1. First principles — what the best foundation for this site is

The audit needed a standard to judge against. This section states it, derived from the site's
actual constraints and checked against spec ∩ shipped code ∩ external reasoning. Nothing here is
aspirational; each principle is already the codebase's revealed preference — stated once so it can
be enforced deliberately instead of by precedent-matching.

### 1.1 The constraints that bound the design space

1. **Read-dominated, fanfic-archive scale.** ~90/10 read/write; entity populations in the
   thousands-to-millions, never billions. Latency budgets are human-browse budgets.
2. **Two render modes, one component tree.** InteractiveAuto means every service contract is
   consumed twice: in-process on the SignalR circuit and over HTTP from WASM. The real API surface
   is *interface × transport* — a contract that behaves differently across the hop is broken even
   if both halves individually "work." Endpoints are a second door, not an implementation detail.
3. **Per-circuit DI scopes with interleaved async.** Anything not safe under intra-scope
   concurrency is latently broken on the first authenticated page load (proven 2026-07-01; the
   factory-per-method pattern is the settled answer).
4. **Solo maintainer.** Rules enforced by vigilance decay; rules enforced by structure
   (compile-time visibility, type-level privilege, DB constraints, single choke points) survive.
5. **Pre-launch, zero data.** Contract changes are free today and breaking tomorrow.
6. **N=1 with honest N≥2 seams.** In-process state is allowed only where a documented body-swap
   seam exists (buffers, reference caches, the rate limiter — see §3.14 for the one missing
   seam sentence).

### 1.2 The ten principles

- **P1 — The service boundary is the firewall, and the firewall is total.** Entities never cross;
  DTOs live in Core; the method signature is the contract; components inject the narrowest
  interface. (Spec §3.5/§3.6; doctrine "DTO Firewall". Verified essentially clean layer-wide.)
- **P2 — Authorization is a service-layer property; UI and endpoints are affordance and
  transport.** Every ownership/role/visibility/lifecycle gate lives in the server service; the
  endpoint carries an equivalent edge gate for defense in depth; Class-A access-gate failure is
  indistinguishable from not-found. (Mostly true; the exceptions are §2.6 and the mod-read
  asymmetry, decision §3.8.)
- **P3 — Invariants are properties of the model, not call-site vigilance.** Named query filters,
  the notification create-core, single-authority counter mutation, sanitize-once at the single
  trust boundary. Any invariant that requires every future call site to remember it is a defect
  waiting to be filed — this audit found the three call sites that forgot the TPT rule (§2.2).
- **P4 — Concurrency safety by construction.** Atomic `ExecuteUpdateAsync` with transition-delta
  on toggles; unique constraints as check-then-act backstops; advisory locks where
  count-then-insert can't be constraint-backed; factory-per-method contexts.
- **P5 — Durability tiers are explicit and binary.** Durable intent → direct synchronous write.
  Loss-tolerant + high-frequency + coalescable (all three) → buffer/flusher/worker with the honest
  contract. No third tier; no silent drops.
- **P6 — Composition forms a DAG over building-block methods.** Foundational services own
  presentation projections; composite services own domain queries; evolution is body-swap behind
  stable interfaces (spec §3.27). (Verified: no cycles, no reverse edges — appendix in the
  composition sweep.)
- **P7 — The error contract is part of the API and must survive the transport.** Domain exception
  taxonomy, null-for-absence vs throw-for-violation, and *identical* component behavior in circuit
  and WASM modes. (The one systemic violation is the error-list round-trip, §2.7.1.)
- **P8 — Side effects are subordinate to the primary write.** Notifications, badges, emails:
  best-effort post-commit, logged, never able to roll back the durable action; fan-out work leaves
  the request path. (Verified layer-wide — every call site wrapped.)
- **P9 — Everything user-growable is bounded.** Pagination or caps on public reads, caps on
  user-created artifacts, rate limits on spam surfaces, bounded buffers that log-and-count rather
  than silently drop. (The systemic gap — §2.5.)
- **P10 — Prefer loud failure over silent corruption.** Where the DB can refuse (constraints,
  RESTRICT), let it; where only the service can, throw the domain exception. Silent drift is the
  enemy — hence recomputability as a stated requirement for every denormalized value (shared with
  schema audit §3.6).

### 1.3 Deliberate absences — ratify the negatives

The layer is also defined by what it does **not** have, and these absences are decisions, not
gaps. A future session should not "add" any of these without a first-principles case: no
repository layer (the service *is* the repository boundary), no MediatR/CQRS bus, no AutoMapper
(projections are hand-written `.Select()`s — the perf and clarity win is real), no DTO
inheritance, no generic `CreateAsync` notification escape hatch, no caching outside the
four-conditions doctrine, no lazy loading, no triggers. Each was either explicitly rejected in the
spec/deliberations or is excluded by standing doctrine.

---

## 2. Defects — fix while contracts are free

Grouped by failure class, ordered by severity within class. Every item carries its origin cluster
and confidence. File:line references are as-of 2026-08-03.

### 2.1 Lifecycle state machines without transition guards

**2.1.1 No server-side story status-transition enforcement — authors can self-publish and
self-un-reject. CRITICAL, CONFIRMED.** `StoryMappers.UpdateStoryEditableProperties`
(`StoryMappers.cs:91`) copies `StoryStatusId` verbatim from the client DTO; both
`CreateStoryAsync` and `UpdateStoryAsync` forward it with only ownership/validation gates
(`ServerStoryWriteService.cs:32,107`; `StoryEndpoints.cs:149-160`). Consequences, each one forged
WASM call away: (a) a story published directly, never entering the F48 approval queue that
`ServerModerationWriteService.ApproveStoryAsync`/`RejectStoryAsync` and the PendingApproval mod
queue exist to gate; (b) a mod-**Rejected** story revived by the author with one PUT; (c)
`StoryValidations.CanSubmitForApproval` is never called by any server code, so a story can enter
the queue with `PostApprovalStatus = Draft` and be "approved" *into Draft*; (d) out-of-range enum
shorts bind unchecked. Fix: a server-side transition table (author-legal: Draft↔PendingApproval
with `CanSubmitForApproval`; published-lifecycle moves among InProgress/Completed/OnHiatus/
Cancelled/Rewriting; Rejected reachable and leavable only via moderation; approval statuses never
author-settable). This is the moderation gate's integrity; it also determines where the
`PublishedDate` re-stamp (§2.9) lands.

**2.1.2 Moderation resolve paths have no status guard — double-resolution corrupts
`ActiveReportCount` downward. HIGH, CONFIRMED.** `ResolveNoActionAsync` (:112-138),
`ResolveWithRemovalAsync` (:140-172), and `ApplyAccountActionAsync` (:174-200) in
`ServerModerationWriteService` load via `SingleAsync` with no `ReportStatusId` check and
unconditionally decrement. Two moderators resolving the same report (or resolve-then-account-action
on one report — a plausible sequence on `/mod/reports`, which carries both control sets) decrements
twice for one +1; the counter that is the *entire* triage signal (E2 in §6) goes negative.
`ClaimReportAsync` (:105-110) shows the correct guarded shape. Fix: guard on Open|UnderReview →
`ModerationValidationException`, decrement only on the actual transition.

**2.1.3 Account-status model: no-op suspensions, no reinstate path, no transition guards.
MEDIUM ×3, CONFIRMED.** (a) A suspension with null (or past) `suspendedUntilUtc` is written
verbatim (`ApplyStatusAndNotifyAsync` :349-351) and `CanalaveSignInManager` (:45-50) blocks only a
*future* non-null date — the user signs straight back in after the stamp kill. (b) Nothing anywhere
writes `AccountStatusEnum.Active` back: an expired suspension reads Suspended forever; a wrongful
ban is irreversible in-app. (c) `ApplyStatusAndNotifyAsync` overwrites unconditionally, so
**warning a banned user silently unbans them**, and non-Suspended transitions leave stale
`SuspendedUntilUtc`. Fix as one WU: validate suspend dates, add a `ReinstateUser` action (→ Active,
clears the date, files its audit report like every action), and a legal-transition check (Banned
leavable only via Reinstate).

**2.1.4 `ApproveLineageAsync` has no Pending precondition. HIGH, CONFIRMED.**
`ServerStoryLineageWriteService.cs:128-158` increments `AcknowledgedAsInspirationCount` and
re-notifies unconditionally — a double-click double-increments. Reject/Delete both capture
`wasApproved` correctly; the approve side alone violates the transition-delta rule. One-line fix.

### 2.2 TPT cascade orphans — the schema audit's §2.2, verified live

DB cascades delete TPT *child* rows only; the abstract base rows orphan and poison every
polymorphic query. `ServerChapterWriteService.DeleteChapterAsync` (:316-319) is the one guarded
site and is verified sound (comments EF-deleted from both tables; the set is closed under replies;
likes cascade off the EF-deleted base rows). The **three unguarded live paths, all CONFIRMED
HIGH**:

- `ServerBlogPostWriteService.DeleteBlogPostAsync` (:179) and `DeleteSiteBlogPostAsync` (:408) —
  stub-delete with a comment claiming cascades handle it. `base_comments` rows orphan; polls are
  *worse*: `base_polls` rows survive **with** their options and votes (those FK the base table) —
  a fully intact ghost poll.
- `ServerModerationWriteService.ApplyHardDeleteAsync` (:564-586) — bare `Remove`; hard-deleting a
  Story orphans every base comment on every chapter; a BlogPost hits both traps above.

Additionally, `DeleteBlogPostAsync` called with a **group** post id passes the base author check
and then issues a `profile_blog_posts` DELETE affecting 0 rows → `DbUpdateConcurrencyException` →
500: **a group-post author can never delete their post**, and `UpdateBlogPostAsync` on a group post
half-works (base fields land; Rating/HasSpoilers permanently uneditable; the publish-transition
flag never observed). CONFIRMED HIGH — group posts need explicit lifecycle methods.

Fix shape (one cross-cutting WU, per the audit-before-crosscutting rule, jointly with schema
§2.2's FK-posture flip): inside an execution-strategy transaction, pre-delete comment and poll
children through EF at all three sites, copying the chapter-delete template; add the group-post
update/delete methods; correct the false "cascades handle it" doc comments in the same change.

### 2.3 Silently inert features (the hidden-deferral class, invisible to every ledger)

**2.3.1 Chapter publish notifies nobody. HIGH, CONFIRMED.** `NewChapterOnFollowedStory` is seeded
(`NotificationConfigurations.cs:71`, `DefaultEmailEnabled = true`) and the enricher maps it — but
no producer exists anywhere; `SetPublishedAsync` flips the flag and returns. The core promise of
story-following delivers nothing, silently, and no tracker item covers it. Build the fan-out
(recipients = `IsFollowed` interactions; fire on false→true only; alternate-version adds and edits
never notify — design notes in §3.4).

**2.3.2 `ReportReceived` is annihilated by drop-self. HIGH, CONFIRMED (found independently by two
auditors).** `ServerModerationWriteService.cs:95` passes the reporter as both recipient and
source; `CreateCoreAsync` (:411) drops recipient==source. The spec-mandated receipt (§5.21) is
never inserted — no row, no email, no error (the call sits in a swallow-log try/catch). Root cause
is a *design gap*: the create-core cannot express a self-caused or system-sourced notification.
Fix via §3.3's ruling (nullable `SourceUserId`, null = system — the column is already nullable).

**2.3.3 Recommendation attribution FK-fails in its primary flow, silently. HIGH, CONFIRMED.**
`UserStoryRecommendationSource` is FK'd 1:1 to the `UserStoryInteraction` composite PK;
`RecordAttributionSourceAsync` (`ServerRecommendationWriteService.cs:552-581`) inserts without
ensuring the parent row exists — and its caller fires on chapter-page **load** with `?rec=`
present (`ChapterReadingPage.razor:349-353`), i.e. before any USI row can exist. Postgres 23503;
fire-and-forget swallow. The integration test papers over it with a false premise ("opening the
story creates the USI row" — no such path exists). Compounding: sparse-row cleanup cascade-deletes
the sources partition, destroying provenance permanently. This also underlies §2.4.1's credit
gate. Fix per §3.2's ruling — decoupling the partition's FK (to users+stories directly) is the
only option that fixes both the insert-order failure and the cascade loss, and it is free today.

### 2.4 Credit and counter integrity

**2.4.1 `RecordSuccessAsync` is an open credit faucet. MEDIUM (HIGH consequence), CONFIRMED.**
The endpoint requires only authenticated + story-visible + not-self + not-already
(`ServerRecommendationWriteService.cs:479-550`); the spec's attribution gate (§5.6 — success =
the post-Chapter-1 prompt on an *attributed* read) lives only in the UI. One account can loop
every rec id on the site and mint `SuccessfulRecCount`, `RecommendationSuccessesEarned`, and
Recommender badges for every recommender. The badge doctrine's "requires another person's
cooperation" gate is not actually satisfied. Fix: require the caller's
`UserStoryRecommendationSource` row inside the service (which requires §2.3.3 fixed first). The
only finding in the audit that inflates *permanent social credit*.

**2.4.2 `GroupsJoined` asymmetry drives the counter negative. MEDIUM, CONFIRMED.** Group creation
inserts the creator's member row with no +1 (`ServerGroupWriteService.cs:56-63`); `LeaveAsync`
unconditionally −1s (:144-145). Creator-later-leaves → negative; the recalculator (which counts
membership rows) fights the live path. Settle §3.7's "does creating count as joining" and encode
it in both places.

**2.4.3 `Story.WordCount` and `WordsWritten` count unpublished drafts. MEDIUM, CONFIRMED.**
`RefreshStoryWordCountAsync` (:402-404) has no `IsPublished` filter and the author delta applies
at draft creation; `SetPublishedAsync` never touches counts. Public listings overstate; authors
earn credit for words never published; contradicts the cluster's own "readable words" principle
and the counter map. Fix: filter published; move the delta to the publish transition.

**2.4.4 `ActiveReportCount` has three independent corruption paths and no reconciler. MEDIUM,
CONFIRMED.** (a) §2.1.2's double-resolution; (b) submit increments the counter in a separate
committed statement *before* the report row's `SaveChangesAsync` — a failure between leaves +1
with no row, and unlike `UserStats` there is **no recompute path at all** for the live
`AspNetUsers`/content counters; (c) no duplicate-report guard — one user files N reports on the
same target (+N to the triage sort, each individually resolvable). One WU: status guards +
transaction/reorder + per-(reporter, target) open-report dedup with a partial unique index while
the table is empty, plus a decision on bulk-closing sibling reports on removal (§3.9) — which also
eliminates the zombie-open-reports-on-deleted-targets class (queue rows whose target no longer
materializes are silently dropped but never resolvable).

**2.4.5 Chapter delete leaves `CommentsWritten` inflated. MEDIUM, CONFIRMED (drift class).** Bulk
comment removal in `DeleteChapterAsync` adjusts nobody's counter; the wired path then disagrees
with the F58 recompute truth. Either batch-decrement in the delete transaction or ratify "hard
deletion accepts drift until recompute" — currently neither is chosen.

**2.4.6 Smaller counter items.** `VersionCount++` tracked increment — the *only*
`ExecuteUpdateAsync` violation layer-wide (`ServerChapterWriteService.cs:149`); comment like-count
returns a stale pre-update value where the blog path re-reads (MA-705 applied to one sibling, not
the other — `ServerCommentWriteService.cs:517` vs `ServerBlogPostWriteService.cs:239-242`);
`SiteDailyStatAggregator.total_words` ignores the visibility predicate its own `total_stories`
uses (:48-50). All LOW, CONFIRMED, one-line-each.

### 2.5 Unbounded client-supplied inputs (systemic; mostly anonymous surfaces)

The single most repeated finding. Confirmed instances:

| Surface | Evidence | Failure |
|---|---|---|
| `GetRandomBatchAsync(batchSize)` | no validation at all; `StoryEndpoints.cs:103-105` | `batchSize=2_000_000` hydrates the whole library, anonymous |
| `StoryFilterDto.PageSize` | advisory doc comment only | same, on every listing surface |
| Tree search `ResultCap` / raw-reached traversal | rejects only `<1`; `GetRawReachedAsync` has **no LIMIT anywhere**; per-path fan-out bound overstated by doctrine | whole reachable catalog per anonymous POST |
| Comment/blog/notification/messaging paging | raw `page`/`pageSize` on anonymous or self endpoints | `page=0` → negative OFFSET → 500; `pageSize=2e9` dumps; blog listings materialize full `Content` per row pre-snippet |
| Comment text | no length cap (`CommentValidations` non-empty only) | multi-MB comments stored and re-served to every reader |
| Message body/subject | no cap / column-only cap | same; oversized subject → 500 not 400 |
| Chapter body (direct POST) | no cap; edits have **no rate limit at all** (no edit `WriteActionKind`) | ~28 MB sanitize+count per request |
| Authenticated content-gate reveals | no existence check, no enum validation, no cap, no throttle (`ContentGateEndpoints.cs:26-57`) | unbounded junk rows; unpaged `GetMyRevealsAsync` compounds |
| Export generation | zero throttling/auth/cache; QuestPDF synchronous on request thread | anonymous PDF-generation loop; the import cluster's own "selection by COST" doctrine applied asymmetrically |
| Upload endpoints | no `RequestSizeLimit`; `IFormFile` buffers up to Kestrel's 30 MB default before the 10 MB processor abort | inherited default nobody chose |
| Id-batch endpoints (`/by-ids`, `restrictToStoryIds`, `candidateIds` on query strings) | uncapped; large sets exceed request-line limits (WASM-only failure) | plus pathological SQL from hundreds of include-tag EXISTS terms |
| Saved-selection entries; view-count ping | no per-selection entry cap; anonymous unthrottled ping | row fan-in; view inflation |

Fix as **one bounds-and-caps WU**: a shared limits constants class per the Discovery auditor's C1
sketch (PageSize ≤ 100, RandomBatch ≤ 50, ResultCap ≤ 500, id-lists ≤ ~50, entries ≤ ~200, text
caps for comments/messages/chapters), clamped or rejected at the service layer; explicit endpoint
body-size caps; an `ExportGenerate` concurrency limiter mirroring `ImportParse`; reveal-row
validation + cap; and a ruling on the view-ping (§3.13). Every item is a behavioral contract
change that is free today and painful after UI/tests encode current behavior.

### 2.6 Access-control asymmetries (Class-A families)

- **Private-profile bypass on blog-post detail. MEDIUM, CONFIRMED.** `GetByAuthorAsync` gates on
  `ProfileVisibilityGuard`; `GetByIdAsync` never consults `ProfileVisibility` — a Private
  profile's posts (plus comments, polls, votes) are fully readable by id enumeration. The
  asymmetry, not either endpoint alone, is the defect; `access-gating-first-principles.md` is the
  authority for which side moves.
- **Story-acknowledgment by-story read has no visibility gate. MEDIUM, CONFIRMED.** Bare-FK query
  (`ServerStoryAcknowledgmentReadService.cs:21-35`, public endpoint) — credits enumerable for
  taken-down/draft/M-gated stories. The exact bare-FK class WU-ParentVisibility swept; this
  later-built cluster missed it. One-line `IsStoryVisibleAsync` guard.
- **Lineage read misses the source-story guard. MEDIUM, CONFIRMED.**
  `GetLineageForStoryAsync` joins the *target* through filtered Stories but never checks the
  *source* — an anonymous probe of a hidden story id returns its lineage (existence oracle +
  relationship leak). Same one-line fix.
- **Saved-selection copy path breaks both rules its siblings enforce. MEDIUM, CONFIRMED.**
  `CopyPublicSelectionAsync` distinguishes "no longer exists" from "not public" (the existence
  oracle its own permalink endpoint forbids) and skips the owner-`ProfileVisibility` check. Rule
  (§3.10): every path to a selection by id applies the identical gate set with one
  indistinguishable failure.
- **The inverse failure — over-filtering locks users out of their own data. MEDIUM-HIGH,
  CONFIRMED.** `SetUserStoryInteractionStateAsync` runs the full visibility guard on *clears* as
  well as raises: a mature-off user cannot un-favorite an M story favorited while mature-on
  (contradicting `layer2-services.md` §Content-Rating case 2 verbatim); mark-unread is likewise
  refused once a story is taken down. Ruling §3.5: gate raises, always allow clears.
- **`AllowProfileComments` is dispatcher-enforced only. MEDIUM (if the setting exists),
  PLAUSIBLE.** `ICommentWriteService.PostUserProfileCommentAsync`'s doc says the owner's setting
  is "enforced by the caller" — the affordance-not-control anti-pattern the 2026-07-18 sweep
  eliminated elsewhere; a direct POST bypasses it. Contrast `AllowPrivateMessages`, enforced in
  the write service by doctrine.
- **Assorted LOW oracles:** `GetChapterForEditAsync` and `RevokeAsync` distinguish
  exists-but-forbidden from absent (403 vs null/silent-success ordering); `CloneListAsync`'s two
  messages; series detail by id ignores the owner's `ProfileVisibility` that the by-author list
  respects (decision §3.11).

### 2.7 InteractiveAuto / WASM parity breaks

The class that stays invisible until the WASM flip, then changes user-visible behavior with no
code change:

1. **Validation error lists don't round-trip. MEDIUM, CONFIRMED.** Server joins `List<string>`
   errors into one `Detail` string; the shared client helper reconstructs a single-element list
   (`ClientHttpHelpers.cs:29-31`, self-documented). Three inline bullets on the circuit become one
   blob on WASM. Fix now: an `errors[]` ProblemDetails extension + translator preference — one
   seam each side.
2. **Seven read interfaces are registered to their write classes. MEDIUM, CONFIRMED.**
   SavedTagSelection, CustomList, Series, StoryLineage, StoryAcknowledgment, StoryArc,
   Notification (`Program.cs:361-495`) — the exact MA-706 shape Program.cs's own BlogPosts comment
   records as a fixed bug, with dedicated read impls present on disk for every one. Read consumers
   get the full write impl (least-privilege void); two write instances per scope. Mechanical
   sweep; pick binding-vs-forwarding as the canonical shape (§3.12).
3. **Sprite base-URL config seam doesn't reach WASM. MEDIUM (latent), CONFIRMED.** Server reads
   `Sprites:BaseUrl`; `Client/Program.cs:30` hardcodes `"/sprites/themes"`. The doctrine's
   "changing this one config value is the complete cutover" promise is false for the client half;
   at CDN cutover every WASM render breaks/flickers. Needs a client config transport before that
   day.
4. **WASM import re-split returns blank drafts. HIGH (WASM only), CONFIRMED.** The one hand-rolled
   sync JSON path deserializes camelCase with case-sensitive defaults
   (`ClientContentImportService.cs:91,145`) — `title`/`html` never bind. Two-line fix + extract a
   shared `JsonSerializerOptions(Web)` so the sync twin can't drift again.
5. **Moderation WIP un-does its own fix on the client. MEDIUM, CONFIRMED.** The new
   `ModerationValidationException` reaches moderators verbatim on the circuit, but
   `ClientModerationWriteService.cs:96-97` maps 400 → `ArgumentException`, which
   `ExceptionPresenter.IsUserFacing` excludes — WASM re-flattens to the generic message. Every
   sibling reconstructs its concrete type; finish the migration (+ the stale comments).
6. **Anonymous reader scroll produces "session expired" on WASM. MEDIUM, CONFIRMED contract.**
   `MarkStarted/MarkCompleted` are anon-no-op in the service but sit behind `RequireAuthorization`,
   and `ChapterReadingPage.OnScrollProgress` calls them without a user gate — circuit silently
   no-ops, WASM throws `SessionExpiredException` inside a `[JSInvokable]` for a user who never had
   a session (MA-302 class; `RecordProgressAsync` handled it, these two didn't).
7. **Explore user-pivot errors lose their message on WASM. LOW, CONFIRMED.** Bare
   `EnsureSuccessStatusCode` where the endpoint deliberately ships a 400 detail
   (`ClientManualTreeSearchReadService`); the sibling tree-search client reconstructs.

### 2.8 Notification correctness (beyond the inert producers)

- **Group `AddStoryAsync` re-notifies on the idempotent duplicate path** (block sits outside
  `if (!alreadyAdded)`) — a repeatable spam primitive for any member. MEDIUM, CONFIRMED.
- **The member fan-out is wrongly gated on the story having an author** (`if (storyAuthorId.HasValue)`
  wraps the whole call; authorless story → members get nothing). MEDIUM, CONFIRMED.
- **A story author who is also a group member receives both types 60 and 25 for one event**
  (`memberIds` doesn't exclude the author) — against the one-notification-per-event principle.
  LOW, CONFIRMED; one line if unintended (with §3.6's `RelatedEntityId` ruling).
- **Mid-batch SMTP death drops the batch remainder forever** — `SendOneAsync` swallows
  connection-class exceptions the transport contract says must propagate; and a full-batch restore
  would duplicate already-sent mails (sent/unsent split needed). MEDIUM, CONFIRMED.
- **Shutdown drain is one batch (≤200 mails)** where the doc claims deploys strand nothing;
  **`OldestUnreadFirst` has no id tiebreak** while fan-out rows share timestamps (pagination
  skips/dups); **dedup is check-then-insert with no partial unique index**. LOW each, CONFIRMED /
  PLAUSIBLE.
- **`StartConversationAsync` commits the bare conversation before participants** (orphaned-row
  producer; the "EF needs the row first" comment is false — navigation fixup makes it one save).
  MEDIUM, CONFIRMED.
- **Lineage re-request after rejection and acknowledgment re-request after decline are both
  unthrottled repeatable notification vectors** (the lineage rejection comment claims a spam
  guard that doesn't exist). LOW-MEDIUM, CONFIRMED — fold into §3.13's rate-limit-surface ruling.

### 2.9 Date-semantics defects (one ruling, two columns)

`Story.PublishedDate` (stamped at creation incl. drafts, never re-stamped — schema §3.4's service
half, CONFIRMED) and `ChapterContent.PublishDate` (same shape, CONFIRMED) both make long-drafted
work sort as stale on every discovery/recency surface and would mis-anchor the future new-chapter
fan-out. Fix jointly with §2.1.1's transition detection: stamp on the first false→true publish
transition (or adopt the schema audit's nullable form). Public dates are frozen by real data —
this is a genuinely now-or-never item.

### 2.10 Group folder integrity (schema §2.4's service half)

CONFIRMED: `DeleteFolderAsync`'s comment claims SET-NULL re-parenting that is configured nowhere —
children (and their story assignments) dangle and the subtree becomes permanently invisible in the
tree build; `CreateFolderAsync` accepts any `ParentFolderId` (cross-group or nonexistent); folder
names are unvalidated (empty persists, >100 chars → 500, sibling duplicates → raw 500, root-level
duplicates legal via NULLS DISTINCT); and `group_stories` has **no** unique `(GroupId, StoryId)`
index, so the idempotent-add check is advisory only. One folder-integrity WU with schema §2.4's
FK + `NULLS NOT DISTINCT` migration.

### 2.11 Reading-progress float hygiene

`RecordProgressAsync` accepts NaN/∞/out-of-range; NaN is **sticky** (C# `Math.Max` propagates it;
Postgres orders NaN above all numbers, so `GREATEST` keeps it forever). MEDIUM, CONFIRMED.
Clamp to [0,1] + reject non-finite in the service; write the invariant into the buffer contract.

### 2.12 Remaining confirmed defects (batch into nearby WUs)

Reply depth unbounded at write and invisible at read (four one-line root checks); polls: archived
site polls remain votable, single-choice vote race leaves two standing votes, site-poll edit
notifications carry `RelatedEntityId = 0`; vouch 5-limit and hidden-gem/highlight limits are
check-then-act with no backstop (ratify or advisory-lock per §3.15); `CreateGroupAsync` two
commits no transaction + raw-500 on duplicate name; `CreateChapterAsync` two-step save unwrapped
(permanent `PrimaryContentId=null` chapter on crash); Fanon write service claims takedown
filtering its unfiltered write context cannot provide (adoption invites fire for removed stories;
dead `IgnoreQueryFilters` decoration); `EditCommentAsync` — no rate limit, no takedown check
(author can rewrite a moderation-frozen comment), no edited-marker, no `IsSpoiler` in the update
DTO; structured-tag validation trusts client `TagTypeEnum` and never verifies tag existence
(forged types bypass required-Setting/Genre and priority caps; nonexistent ids → raw FK 500);
WU37 pairing rules (member count ≥2, member-in-story) are silently dropped instead of rejected —
doctrine table and code disagree; `UserDeletionService` and moderation hard-delete never delete
avatar/cover blobs (and abandoned cover uploads orphan — ownership decision §3.16); saved-selection
and tag-name app-level case-insensitive checks sit over case-sensitive DB indexes (mixed-case race
persists a duplicate the app can never recreate — schema §3.1's service half); N+1 in
`GetPublicSelectionsByUserAsync`; EPUB decompressed-size unbounded; legacy-encoding imports garble
without a warning; `banned (int?)` scalar projection shipped verbatim in `UploadCoverArtAsync`;
ExternalVerification `SingleAsync` → 401-instead-of-404 error class (same class as nonexistent
report id → 401 in moderation); mod-report queue unbounded; unresolvable zombie sibling reports on
removed targets; `GetForStoryAsync` author-probe uses the filtered set (mature-off author can't see
the NeedsRevision recs they must manage); notification-cluster `MessagingParticipantDto` fabricates
`/user/0` links; USI all-null date-partition rows retained on HasStarted-only rows.

---

## 3. Decisions to settle deliberately before lock-in

Each is a fork where the code sits on a default nobody chose. One-paragraph rulings; some spawn
small WUs. (Cluster-level "ratify as designed" items are in §5.)

1. **Is the F48 approval queue mandatory?** The code holds both positions at once (§2.1.1). Enforce
   transitions or cut the queue; the halfway state is the only wrong answer. Drives the fix shape
   for 2.1.1 and both date stamps (§2.9).
2. **Where recommendation provenance lives** (§2.3.3): decouple `user_story_recommendation_sources`
   from the USI composite FK (FK users+stories directly — survives sparse cleanup, insertable at
   any time) vs upsert-parent vs capture-at-MarkStarted. Only the first fixes both failure modes.
3. **System/self-sourced notifications**: make `CreateCoreAsync`'s source nullable (null = system;
   drop-self vacuous; dedup on null source) — restores `ReportReceived` and enables the next item.
4. **Moderation notifications should not carry the acting moderator's identity** to the sanctioned
   user (currently id + username ship in the DTO over a WASM-reachable endpoint — a
   harassment/retaliation vector; the audit Report row keeps the real moderator). Null-source for
   types 70–82; settle before real moderation happens.
5. **Visibility-gating asymmetry, raises vs clears** (§2.6): flag-raises require the full guard;
   clears/lowers on an existing row are always permitted (decide whether takedown/status also lift
   for clears — arguably yes; clearing reveals nothing).
6. **Group fan-out `RelatedEntityId`**: groupId (current — can never name the story; distinct
   stories dedup-collapse while unread) vs storyId. Ratify the digest behavior or switch; fix the
   author-double-notify either way.
7. **Does creating a group count as joining** for `GroupsJoined`? Encode the same answer in the
   live path and the recalculator (§2.4.2).
8. **Mod-only read gating**: extend the service-gate rule to the three sensitive mod reads
   (recommended — three lines) or write down a deliberate "reads gate at the edge, writes in the
   service" split in `identity-and-authorization.md`. Related: consider splitting user-facing
   report submission from the mod-queue interface (least-privilege at the type level), and the
   ExternalVerification mod-read posture is the same question.
9. **Sibling-report auto-resolution**: does resolve-with-removal close all other open reports on
   the same target? (Recommended yes, same UoW — defines what `ActiveReportCount` *means* and
   removes the zombie class.)
10. **One gate rule for all selection-by-id paths** (permalink, detail, copy): identical gate set,
    one indistinguishable failure (§2.6).
11. **Series: independent public artifact or profile-tab data?** By-id detail currently ignores
    the `ProfileVisibility` the by-author list respects. Same family: custom-list direct reads by
    id vs the F15 permalink precedent (IsPublic **and** ProfileVisibility).
12. **Canonical DI registration shape** for write-serves-both clusters: separate read binding where
    a read impl exists; forwarding delegate otherwise; then the seven-cluster sweep (§2.7.2).
13. **Bounds, caps and throttle policy as one enumeration** (§2.5 + the untouched spam surfaces:
    comment/chapter/blog edits, poll votes, acknowledgment re-requests, lineage re-requests,
    reveal inserts, view pings, list creation). The rate-limit *coverage* is currently
    principled-by-accident ("creates only" is a fact, not a decision). Enumerate the abuse-prone
    surface once, per the audit-before-crosscutting rule; ratify the view-ping's
    anonymous-unthrottled posture explicitly if kept (views are never sort keys — bot inflation
    accepted), or add the IP-partitioned limiter now.
14. **Counter transactionality wording**: every cluster runs counters post-commit in a second
    transaction; the doctrine (and spec §9.4) say "same transaction." Adopt "post-commit,
    recompute-corrected" as the stated contract (and note which counters *have* no recompute —
    `ActiveReportCount`, content like-counts — which is what makes §2.4.4 urgent), or wrap
    everywhere. Recommended: fix the doctrine sentence; it matches the code's own samples.
15. **Concurrency posture for check-then-act families**: accept-and-record (self-healing via
    recompute / next action) for hidden-gem/highlight limits, vouch limit, poll single-choice, USI
    flip-detection double-delta — or harden with `ON CONFLICT`/advisory locks. Minimum: close the
    USI create-create 500 and add the missing `group_stories` unique index. Record the ruling per
    family so drift incidents have a defined answer (schema §3.6's principle applied to L2).
16. **Blob cleanup ownership for terminal deletion** (user deletion, hard delete, abandoned cover
    uploads): inline deletes vs periodic orphan sweeper vs "orphans accepted" — currently nobody's
    job and undocumented. Related ruling: keep the cover two-step upload protocol or move
    persistence into the service like avatars.
17. **`UserNotificationSetting` granularity**: doctrine says per-field NULL sparse; columns are
    non-nullable (an override of `Collapsed` freezes `EmailEnabled` against future default
    changes). Make both columns nullable now (free) or fix the doctrine sentence.
18. **Private messages can never produce email** (no notification type exists) — for a small site
    the unread-PM nudge is the single highest-value email. Ratify the absence or reserve the enum
    value + semantics now.
19. **CSRF posture as policy**: SameSite=Lax is the effective sole CSRF control for the
    cookie-authenticated JSON API (antiforgery is form-post-only, uploads deliberately exempt).
    Defensible; currently implied. One sentence in `security.md` — and it converts the upload
    endpoints' incorrect "stateless API" rationale into the true one.
20. **Smaller now-or-never rulings** (one paragraph each): FTS config for proper nouns (schema
    §3.2 + the two hardcoded `'english'` query sites that must move in lockstep); chapter
    author-draft preview surface; chapter version deletion (build or record absence);
    HTML-as-source ratification (schema §3.9 — the export/import round-trip is the de facto
    migration path); "mark read elsewhere sets HasStarted" spec conflict (WU45's position is
    coherent — ratify it); comment deletion placeholder-vs-reparent (spec §5.9 — genuinely open,
    no note rejects the placeholder); comment length cap; group blog-post rating vs audience
    waterfall; archived-poll votability; account re-verification silently un-verifying;
    "My X" anonymous semantics discriminator (zero-state vs 401); CancellationToken policy
    (expensive-reads-only — ratify); server-only methods living on WASM-registered interfaces
    (split before the surface freezes); story deletion doesn't exist for authors (archive
    permanence — say so); `StoriesInProgress` vs Actively-Reading formula (settled — ratify with
    the visible consequence stated); hidden-favorite fan-out membership (doctrine says include
    hidden-only favoriters in type 15; code excludes them — pick one); USI dates partition
    written-never-read (schema §3.5 — build the date-sorted shelves, ratify as future-proofing, or
    cut); story-centric USI index gap (L6 matrix PENDING — ratify or build pre-data).

---

## 4. Convention drift — where doctrine and code disagree

The audit's second-order result: `layer2-services.md` itself contains stale or self-contradictory
passages that will cause future sessions to "fix" correct code. Corrections needed (doc-only
unless noted):

| # | Doctrine says | Reality | Action |
|---|---|---|---|
| 1 | §UserStats: counters "within the same transaction as the primary write" | Post-commit second statement, universally, matching the doctrine's *own code samples* | Reword per decision §3.14 |
| 2 | §Group Rating Waterfall Tier 1: write-side story load "already filtered… never bypassed" | Write context carries **no** filters post-WU38 (stated elsewhere in the same file); code correctly uses the confidentiality-only guard | Rewrite the Tier-1 row — display-side filter, not an add-time gate |
| 3 | §Notification Generation: "composes read services for recipient resolution… will inject" | Every fan-out queries `writeDb` directly (defensible, DAG-clean) | Align doc to reality; kill the stale future tense |
| 4 | §Notification Generation example `NotifyNewFollowerAsync(ActorId, targetUserId)` | Real signature is `(recipientUserId, followerUserId)` — copying the doc notifies the wrong user | Fix the example |
| 5 | WU37 validation table: pairing member-count/in-story → Reject | `StoryMappers` silently drops bad indexes and persists degenerate pairings | Enforce or amend (defect §2.12) |
| 6 | "SpriteBaseUrl is a config seam… no code changes" | False for WASM (§2.7.3) | Amend + fix |
| 7 | §Filtering semantics: "NULL for either field means use the default" | Columns are non-nullable; sparseness is row-level | Decision §3.17, then align |
| 8 | DAG rule: write→write only Notification/Badge | `ChapterReadMarkWrite → IUserStoryInteractionWriteService` (deliberate, described elsewhere in the same file) | Amend the sanctioned list |
| 9 | "GetListingsAsync two-step" sketch | Pre-TagFanon shape (no roll-up, no StoryCharacters branch) presented as current | Mark historical like the WU37 sketch |
| 10 | `IStoryReadService` doc comments on DTOs | Claim sprite URLs "already resolved by the read service" — resolution moved to render time | Fix stale comments |

**Load-bearing false code comments** found (fix with their defects, same WU — this is the exact
class the repo's CORRECTION-note convention exists for): `DeleteFolderAsync`'s phantom SET-NULL;
blog-delete's "cascades handle it" ×2 + interface docs; lineage-reject's phantom spam guard;
Fanon's phantom takedown filter + dead `IgnoreQueryFilters` on writeDb; `RejectLineageAsync`;
`VisibleStories`/Fanon "status not globally filtered" (stale since the StoryStatus filter landed —
the explicit window now suppresses the author-exception, which must be said or a future
simplification opens an author-draft leak); the USI "reject impossible combinations" comment (R3
made it fiction); `FindUserByUsernameAsync`'s ILike narration; the moderation client's "no method
produces 400 today"; `ClientSavedTagSelectionReadService`'s auth claim; stale `cross-cutting.md`
citations in the USI write service; `StoryFilterDto.Sort`'s wrong fallback claim
(LastUpdated vs shipped DatePublished — the DTO is the shared contract surface; pick one).

**Pattern-uniformity items:** three private `RequireAuthenticatedUser`/`RequireModerator` copies
with two different unauthenticated behaviors (401 vs 403) — extract the shared guard; read-return
collection types mixed (`List<T>` vs `IReadOnlyList<T>` — the written rule covers parameters only;
standardize reads); `ServerTagWriteService` names its context `db`; `UserDeletionService` predates
every convention it anchors (shared `Services/` folder, no Core interface, old-style ctor —
modernize while behavior is settled); badge curation's "My"+userId shape is a third self-referential
variant — record it as sanctioned; `IThemeReadService` ordering doc; `IContentImportService`
lifetime asymmetry.

---

## 5. What the foundation gets right (ratified — do not "fix")

Beyond the architecture-level principles (§1), these concrete implementations were verified
correct and should be treated as the reference for their pattern:

1. **The three signal-buffer trios** (ViewCount, ReadingProgress, UserActivity) — each meets every
   clause of the doctrine and adds refinements beyond it (visibility-EXISTS guards reusing
   `DiscoveryMartSchema.VisibleStory` so two SQL spellings can't drift; C#-side `is_read` to dodge
   float promotion; the `Discard` seam ordered before mark-unread saves; latest-timestamp merges).
2. **Spotlight redemption** — the reference concurrency-sensitive write: execution strategy +
   transaction + advisory lock + `ChangeTracker.Clear()` against retry replays, with the full
   doctrine validation set.
3. **The recommendation lifecycle state machine** — every transition guard verified present;
   the one cluster where §2.1's failure class does *not* apply. Freeze it.
4. **The UserStoryInteraction truth-table machinery** — transition-delta, sparse row lifecycle,
   zero-coupling flags, hidden-favorite privacy coherent across every surface checked, exact
   §8.7 enum translation. Reference implementation status.
5. **`UserStatRecalculator`** — set-based, formula-parity-commented, drift-signaling recompute;
   and **`StoryVisibilityGuard`'s two-axis consent/confidentiality model** with reveal bypassing
   rating only — never takedown or status.
6. **The upload trust boundary** (`ImageUploadProcessor` + `ImageUploadRules` + the Local↔S3 seam)
  — sniff, bomb-guard, re-encode, orient-then-strip, single-sourced rules; and the sanitizer:
   allowlist byte-for-byte the doctrine, rel/target normalized server-side, the CVE-2026-54570
   posture intact, WASM's sanitizer absence correctly scoped to self-XSS-only preview.
7. **The notification create-core containment** (drop-self + dedup with the WU34 key, no public
   generic escape hatch, three-layer generation defense), the **email drain-time eligibility
   design**, the **unsubscribe token service**, and the **precedence-dedup blog fan-out**.
8. **Messaging's measured read path** (the outer-projection `Substring` and the NULLS-LAST
   ordering are measured, not stylistic) and the sticky-archive triad; the four-tier
   `AllowPrivateMessages` gate with a fail-closed default arm.
9. **The client HTTP layer's single translation seam** (`ClientHttpHelpers`) with typed exception
   reconstruction across all 63 files, and the endpoint files' per-route auth-rationale doc
   headers — the composition sweep found **no** endpoint whose service assumes auth the edge
   doesn't enforce.
10. **The discovery engine's Source × Filter × Sort through-line** — one shared pure
    `StoryFilterPredicates` consumed by all four engines; the tag-hierarchy cache verified against
    all four caching conditions *as of today*; injection-safe rCTE parameterization; WU44
    composition exactly as settled.
11. **Per-context comment pattern** (four contexts, symmetric guards both directions, golden
    indexes), **poll engineering** (config-lock, replace-semantics, server-side results blanking),
    and the **DeleteChapterAsync TPT template** the §2.2 fix should copy.
12. **Export/import as the allowlist's second and third legs** — six writers mapping exactly the
    13 tags; normalize-then-sanitize import with warnings never silence; "export = what you can
    read."

---

## 6. Spec §3/§5/§6 vs. shipped — consolidated divergence ledger

Verdicts: ✅ shipped is right (ratify; audit-file line where noted), ⚠ finding above governs,
⬜ needs a ruling (§3).

| Spec | Shipped | Verdict |
|---|---|---|
| §3.5/§6.5 CQRS-lite inheritance, four write-side-read cases | Faithful layer-wide | ✅ |
| §6.6 direct DbContext injection | Factory-per-method (settled 2026-07-01) | ✅ superseded; universal |
| §3.14/§6.13 minimal-API endpoint organization | Faithful + auth-rationale headers | ✅ |
| §3.15/§3.16/§3.18 Redis write-behind, view INCR, LastReadDate | In-process buffer trios; L7 dissolved | ✅ improvement; recorded |
| §3.17 sprite services (File.Exists server / optimistic WASM) | Optimistic-everywhere + probe at mod-write; base-URL seam | ✅ superior — but ⚠ §2.7.3 (WASM seam) |
| §3.20 error handling "not yet fully designed" | ProblemDetails + typed reconstruction + `ExceptionPresenter` | ✅ built — ⚠ §2.7.1 (error lists) |
| §3.21 anti-forgery "via Blazor EditForm" | SameSite=Lax + selective exemptions | ⬜ ratify as policy (§3.19) |
| §5.1 Draft → PendingApproval → moderated publication | Infrastructure present, transitions unenforced | ⚠ §2.1.1 — regression; enforce |
| §5.6 rec pending/approval flow | Publish-immediately (WU-RecLifecycle) | ✅ settled supersession |
| §5.6 success = attributed post-Ch1 prompt | Open endpoint, UI-only gate | ⚠ §2.4.1 — spec wins |
| §5.7 hidden favorite | Fully honored every surface checked | ✅ (+ one fan-out sentence, §3.20) |
| §5.8 vouch 280-char / IsVouched bool | Unbounded rich text / first-class table | ✅ recorded supersessions |
| §5.9 [Deleted Comment] placeholder | Hard-delete + reparent-to-root | ⬜ genuinely open (§3.20) |
| §5.12 read-elsewhere leaves HasStarted unset | Per-chapter mark sets it (WU45) | ⬜ ratify WU45's position |
| §5.13 view counts on Story | Buffer → `daily_story_stats`; no column | ✅ improvement |
| §5.18 per-type in-app + email toggles | In-app always-on; email+collapsed only | ✅ recorded — ⚠ granularity §3.17 |
| §5.19 SignalR real-time messaging | Polling watermark; SignalR post-MVP | ✅ correct MVP call |
| §5.21 ReportReceived to reporter | Dead (drop-self) | ⚠ §2.3.2 — spec wins |
| §5.21 report auto-flag threshold | Triage sort on the counter instead | ✅ better at this scale — raises §2.4.4's stakes |
| §5.20 badge tiers/thresholds | No-tiers ≥1 + EarnedCount | ✅ ratified (WU-StatBadgeProducers) |
| §5.22 admin inherits moderator | `IsModerator \|\| IsAdmin` everywhere | ✅ mechanism differs, effect complies |
| §5.23 import verification in approval | Display-only two-tier verification, decoupled | ✅ settled 2026-07-24 |
| §5.24 "download their stories" | Export = what you can read | ✅ ratified |
| §5.26 spotlight donations | Dormant seam exactly as specified | ✅ |
| §5.26 tree-search filter composition | Post-traversal composition (WU44) | ✅ settled |
| §9.4 counters same-transaction | Post-commit layer-wide | ⬜ §3.14 wording ruling |

---

## 7. Recommended sequencing

**Fix-WU bundle (before first human data; roughly severity-ordered):**

1. **WU-StoryLifecycle** — §2.1.1 transition table + `CanSubmitForApproval` wiring + both date
   stamps (§2.9) + the §3.1 queue ruling. The single highest-stakes item.
2. **WU-TptHardDelete** — §2.2's three sites + group-post lifecycle methods + false-comment fixes,
   jointly with schema §2.2's FK flip. One cross-cutting WU.
3. **WU-ModerationIntegrity** — §2.1.2 + §2.1.3 + §2.4.4 (+ §3.9 sibling ruling, §3.8 read gates,
   the WIP client-exception finish §2.7.5, and schema §3.7's report columns per the audit-record
   gap). Moderation tooling is newly reachable; these are its sharp edges.
4. **WU-InertFeatures** — §2.3's three: new-chapter fan-out, nullable notification source
   (+ §3.4 de-identification), attribution FK redesign (+ §2.4.1's gate).
5. **WU-BoundsAndCaps** — §2.5 as one policy pass (+ §3.13's throttle enumeration, §2.11's clamp).
6. **WU-ParityAndRegistration** — §2.7: error-list extension, the seven-cluster DI sweep, resplit
   JSON fix, anon-scroll guards, sprite-seam client transport (before CDN cutover).
7. **WU-AccessGateSweep2** — §2.6's asymmetries in one pass (blog detail, acknowledgments,
   lineage source, selection copy, series ruling, clears-vs-raises, AllowProfileComments), against
   `access-gating-first-principles.md`.
8. **WU-FolderIntegrity** — §2.10 with schema §2.4. **WU-CounterSymmetry** — §2.4.2/3/5/6.
   **Notification-correctness batch** — §2.8. Remaining §2.12 items ride whichever WU touches
   their cluster next.

**Decision batch for the owner:** §3's twenty numbered rulings — most are one paragraph; items
1–5 unblock fix-WUs 1, 3 and 4 and should come first.

**Doc-correction pass:** §4's table + false-comment inventory, one WU, gated by the doc-audit
skill's probes afterward.

**Explicitly not this audit's scope:** L6 index builds (the reconciliation matrix owns them —
§3.20 flags the one story-centric USI gap it already tracks), the live `pg_indexes` sweep,
RazorComponents/browser-band verification of the L3 surfaces above the services, and any schema
finding already owned by [[db-schema-first-principles-audit]] (this report adds only their
service-layer halves).
