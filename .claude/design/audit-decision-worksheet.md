# Audit Decision Worksheet (2026-08-04)

> **Status: working capture document, not authority.** Sequences the owner-decision batches from
> [[db-schema-first-principles-audit]] (§3, plus the two §2 forks its §7 flags for owner sign-off)
> and [[service-layer-first-principles-audit]] (§3, including each of §3.20's individual rulings)
> into one answer-in-order queue. The audits remain the evidence — every entry cites its source
> section(s); re-verify there before acting. Three decisions appear in both audits and are merged
> into single entries. Each answer, once given, is consumed per Doc-Touch moment 1 (promulgated to
> the convention doc / audit file / `roadmap.md` Resolved list by the WU that uses it) — after
> which the entry here is history, not the record.

Fill in each **Answer:** line; tick the checklist as you go.

## Progress checklist

- [x] D1 — F48 approval queue: mandatory or cut
- [x] D2 — Published-date semantics (story + chapter content)
- [x] D3 — Recommendation provenance home
- [x] D4 — System/self-sourced notifications (nullable source)
- [x] D5 — De-identify moderation notifications
- [x] D6 — Visibility gating: raises vs clears
- [x] D7 — Sibling-report auto-resolution
- [x] D8 — Report reported-user column + takedown-reversal ratification
- [x] D9 — Mod-only read gating posture
- [x] D10 — TPT FK posture (RESTRICT flip)
- [x] D11 — Poll-owner delete behavior
- [x] D12 — Comment deletion: placeholder vs reparent
- [x] D13 — User-deletion survival set ratification
- [x] D14 — Blob cleanup ownership
- [x] D15 — Author story deletion (archive permanence)
- [x] D16 — Group fan-out RelatedEntityId
- [x] D17 — Hidden-favorite fan-out membership
- [x] D18 — Private-message email: ratify absence or reserve
- [x] D19 — UserNotificationSetting granularity
- [ ] D20 — Bounds, caps, and throttle policy (one enumeration)
- [x] D21 — Counter recompute principle
- [x] D22 — Counter transactionality wording
- [x] D23 — Check-then-act posture per family
- [x] D24 — Does creating a group count as joining
- [ ] D25 — Selection-by-id single gate rule
- [ ] D26 — Series & custom-list by-id visibility
- [ ] D27 — "My X" anonymous semantics
- [ ] D28 — FTS configuration + scope
- [ ] D29 — Case-insensitive community-facing names
- [ ] D30 — Optimistic concurrency (xmin) + edit-conflict UX
- [ ] D31 — USI dates partition fate
- [ ] D32 — Story-centric USI index gap
- [ ] D33 — group_folder_group_story join rigor
- [ ] D34 — Public integer IDs acceptance
- [ ] D35 — HTML-as-source ratification (ContentRaw)
- [ ] D36 — Canonical DI registration shape
- [ ] D37 — Server-only methods on WASM-registered interfaces
- [ ] D38 — CSRF posture as recorded policy
- [ ] D39 — CancellationToken policy
- [ ] D40 — Mark-read-elsewhere sets HasStarted (WU45)
- [ ] D41 — StoriesInProgress formula
- [ ] D42 — Archived-poll votability
- [ ] D43 — Group blog-post rating vs audience waterfall
- [ ] D44 — Account re-verification silently un-verifying
- [ ] D45 — Chapter author-draft preview surface
- [ ] D46 — Chapter version deletion
- [ ] D47 — Moderator actions available for Group-typed reports *(added 2026-08-08; sits in Block B)*

---

## Block A — Top-severity unblockers

Gates WU-StoryLifecycle, WU-InertFeatures, and parts of WU-ModerationIntegrity (service audit §7:
"items 1–5 unblock fix-WUs 1, 3 and 4 and should come first").

### D1. Is the F48 approval queue mandatory?

*Source: service §3.1; defect §2.1.1; spec §5.1.*
The code holds both positions at once: the moderation queue and Approve/Reject machinery exist,
but nothing server-side stops an author from self-publishing, un-rejecting, or entering the queue
with an illegal post-approval status. Options: enforce a server-side transition table
(author-legal: Draft↔PendingApproval; published-lifecycle moves; Rejected/approval statuses
moderation-only) — or cut the queue. The audit's ruling: the halfway state is the only wrong
answer. This decision fixes the shape of WU-StoryLifecycle and determines where D2's date stamps
land.

**Answer (2026-08-04):** **Mandatory, but only for authors who have never had a story approved.**
The queue's sole purpose is **spam prevention** — not editorial standards, not tag/rating sanity.
Everything below follows from that.

*Enforcement is not part of the fork.* A server-side transition table is required under every policy
answer including "cut" — cutting still leaves defect (d)'s unchecked enum binding and still leaves
the published-lifecycle moves unguarded. Build the guard regardless; the only open question was
policy.

*Transition table.* Author-legal: `Draft→PendingApproval` (gated on `CanSubmitForApproval`, which no
server code calls today), `PendingApproval→Draft` (withdraw), `Rejected→Draft` (revise, **uncapped**),
moves among the published set, and `published→Draft` (unpublish). Moderator-only and guarded on
current status: `PendingApproval→PostApprovalStatus` (approve) and `PendingApproval→Rejected`.
`PostApprovalStatus` is validated at both submit and approve, closing the approve-into-Draft hole.

*Trust waiver.* An author with `ApprovedStorySubmissions >= 1` and an uncleared `CanAutoApprove`
flag publishes `Draft→published` directly. The counter is **monotonic — never decremented** by
deletion, takedown, or revoke; a decrementable counter is farmable. Spam is an account-level
property, so one approved story is the whole signal and re-checking it on story N tests nothing.
Any moderator may revoke the flag; the grant is automatic. Rationale for gating first submissions
only rather than everything: the queue cannot deliver a standards guarantee anyway, because
post-approval edits are ungated under every answer (pass at T, edit to M tomorrow) — so mandatory
review buys a snapshot that expires immediately, while costing author friction and the volunteer
attention that low-yield reviews burn.

*`Rejected` is reachable only from `PendingApproval`.* Published content is removed via
`IsTakenDown`, which already exists with a reversal path. Allowing rejection of published work would
leave two overlapping invisibility mechanisms with undefined ordering ("can a taken-down story be
approved", "does reversing a takedown restore a story that was `Rejected` underneath"); confining
rejection to pre-publication makes both vacuous. Because trusted authors never enter the queue,
`Rejected` is only ever reachable by an author's first submission — which is why the resubmit cap
Fimfiction uses is unnecessary here.

*Unpublish re-enters the gate*, which the trust waiver then skips for trusted authors. Deliberate,
and recorded so it isn't read as a stronger guarantee than it is: approve-once-then-rewrite is
closed for an author's **first** story and open thereafter. That is Fimfiction's posture too (trust
is enforced by ban, not by re-gate); `CanAutoApprove` revocation is the lever if it matters.

*Import-verification submissions never take the waiver* — spec §5.23 queues imports to verify
**authorship**, a different question than "will this person post garbage."

*Prior art.* Fimfiction gates the story on first submission for untrusted authors only, auto-approves
thereafter (threshold reported as 1 or 2), keeps rejection revisable, and re-gates its "Revoke
Submission" path — while making re-approval **not** re-post to the front page. Approval has been
there since the 2011 public beta; Auto-Approve was layered on later as an earned privilege, so the
trajectory is moderate-everything → carve out a trust tier, not the reverse.

*Caveat that belongs with D20, not here.* A submission queue is a content-level answer to an
account-level problem. Fimfiction ran this queue for fourteen years and still disabled open
registration in June 2025 over spam accounts (staff-created accounts only as of February 2026). The
primary defenses are registration-side — email verification and story-creation rate/age limits,
which **D20's throttle-coverage enumeration owns**; the queue is the backstop. Keep it anyway: its
real guarantee is that **zero spam ever reaches a reader**, whereas every reactive path lets a spam
story pollute Explore, tree search, and new-stories until someone reports it — the worst failure
mode for a site whose premise is discovery.

*Discharges D2:* the publish stamp lands on the first unapproved→published transition and is never
re-stamped (matching Fimfiction, where re-approval after a revoke does not re-bump the feed).

*Open sub-edges routed, not dropped, for WU-StoryLifecycle:* in-queue edits (recommend allow, no
re-queue — the mod approves the row as it stands, and the trust tier means one shot at it);
post-approval rating raise (reactive only, ratify); author suspended/banned/deleted mid-queue
(approve must guard on a live, non-anonymized author — interacts with D13); moderator concurrency on
approve/reject (same guard shape as §2.1.2's double-resolution fix). A minimum-content floor for
submission belongs to **D20**; feed-event/anti-bump rules (first-publication-only, permanently, per
artifact) belong with the new-chapter fan-out in **D16/D17**. Chapter-level gating never happens.

### D2. Published-date semantics

*Source: schema §3.4 + service §2.9 (one ruling, two columns).*
`stories.published_date` is NOT NULL, stamped at creation (including drafts), never re-stamped;
`chapter_contents.publish_date` has the same shape. Long-drafted work sorts as stale on every
recency surface and would mis-anchor the new-chapter fan-out. Options: restore nullability
(NULL = never published — the audit calls this "the honest model") or keep NOT NULL as
"date-created-until-published" and stamp on the first false→true publish transition. Public dates
freeze the moment real data exists — genuinely now-or-never. Depends on D1 (the transition
detection is where the stamp goes).

**Answer (2026-08-04):** **Restore nullability. NULL = never published.** Stamp on the first
unpublished→published transition; **never re-stamp**. Applies to `stories.published_date` and the
chapter publish anchor alike. This is the audit's own "honest model" and the spec's original shape —
the shipped `NOT NULL` is the drift.

*Why nullable, not "date-created-until-published" — D1 supplied the deciding argument the audits
didn't have.* D1 made `published→Draft` (unpublish) a legal author transition, so `StoryStatusId` can
no longer answer "has this ever been published": a Draft story may be virgin or pulled back. Under
the NOT NULL model **no column can answer it**. Under the nullable model one already does, for free.
And D1's anti-bump rule — feed events are first-publication-only, permanently, per artifact — needs
exactly that durable fact to be enforceable at all. `published_date IS NULL` *is* the "never
published" predicate; making it carry a creation date instead destroys the only cheap place to put
it. Zero cost now, unrecoverable once real dates exist.

*Consequence, stated because it is counterintuitive:* a story published, unpublished for a year, then
republished keeps its original `published_date` and sorts as year-old on New Stories. That is
correct and is exactly Fimfiction's Revoke-Submission behavior (re-approval "goes right back to where
it was," no front-page repost). Republication is not a publication event.

*The chapter half is not symmetric with the story half — the audit's "one ruling, two columns"
framing misses a fork.* `Chapter.IsPublished` is per-**chapter**; `ChapterContent.PublishDate` is per-
content-**version** (`PrimaryContentId`, `VersionCount`, alternate versions). So "the chapter's
publish date" is currently *the primary version's* date (`ChapterListEntryDto`'s own doc comment says
so, and that DTO already types it `DateTime?` — the read layer already wants the nullable semantic
and the entity is the odd one out). **Live bump vector:** adding an alternate version and promoting
it to primary moves the chapter's publish date forward, which would re-anchor the "New" badge and
mis-fire the new-chapter fan-out.

*Ruling on that fork:* the publish anchor is a **chapter-level** fact, not a version-level one. Add
a nullable `Chapter.FirstPublishedDate` beside `IsPublished`, stamped once on the chapter's first
publish and never moved thereafter; it is the "New"-badge input and the fan-out anchor, and it makes
the invariant `IsPublished == (FirstPublishedDate != null)` checkable in one row. `ChapterContent.
PublishDate` stays (nullable) as **per-version provenance only** — when *this version* went live —
and no discovery surface reads it. Adding or promoting a version is an *update*, never a publish
event. Lower-churn alternative if the extra column is unwanted: define the anchor as
`MIN(publish_date)` over published versions — same semantics, but every recency surface pays for the
aggregate, so the column is preferred.

*Site-local vs. provenance — keep these from being conflated.* `published_date` /
`FirstPublishedDate` always mean "went live **on this site**." `original_published_date` /
`ChapterContent.OriginalPublishDate` (spec §5.23 import fields) are display-only provenance. An
import must **not** backdate `published_date` — doing so buries imported work on arrival; conversely
the import's arrival is a genuine site-local publication and sorts as one. Discovery sorts on the
site-local column, always.

*Dependency discharged:* D1's transition detection is where both stamps land — the same guarded
`TransitionStatusAsync` that enforces the story table, and the chapter publish path for the chapter
anchor. D16/D17's new-chapter fan-out anchors on `Chapter.FirstPublishedDate`, which is why that
column should exist before the fan-out producer is written.

### D3. Where recommendation provenance lives

*Source: service §3.2; defects §2.3.3, §2.4.1.*
`user_story_recommendation_sources` FK's to the USI composite PK, so the primary attribution flow
FK-fails silently (fires on chapter load, before any USI row exists) and sparse-row cleanup
destroys provenance. Options: decouple the partition's FK (FK to users + stories directly),
upsert-parent, or capture-at-MarkStarted. Audit: only decoupling fixes both failure modes, and
it is free today. Also gates the `RecordSuccessAsync` credit-gate fix (§2.4.1).

**Answer (2026-08-04):** **The coupling is correct. Keep the composite FK to the USI PK and keep
both cascades. The fault is a missing producer, not a schema fault — build the Read-It-Later button
that was specified in 2025 and never built.** The audit's three-way fork is void; do **not**
decouple the partition to users+stories.

*Why the fork is void.* Service §3.2 and defect §2.3.3 infer a schema fault from the shipped caller:
`ChapterReadingPage` reads `?rec=` on load and inserts before any USI row can exist, so the FK to
the USI composite PK looks wrong. The inference runs backwards. The 2025 design record
(`GeminiDiscussions/MyActivity September to November 2025_filtered.md`) shows provenance coupled to
the interaction deliberately at every step, and shows the intended producer — a **Read It Later
button on the recommendation card** — was specified and never built. Corroborating code facts:
`RecommendationCard.razor` has no such button or callback; `IUserStoryInteractionWriteService` has
no targeted single-bit setter a card could call; and no file in the repo generates a `?rec=` link.
The `?rec=`-on-load write is a placeholder standing in for the missing producer. Decoupling would
delete the feature's defining semantic (below) while appearing to fix a bug.

*What the attribution means — the sentence every rule below follows from.* Deliberation entry #1358
(2025-10-27, file line 32194) defines it by two cases: RIL clicked **on the recommendation card** →
store the source; RIL clicked on the story page → store nothing. The attribution is **metadata on
the `IsReadItLater` bit**: it records *how that bit came to be set*. It is not a general "where did
this reader come from" event log.

*Cascade on the recommendation FK is correct, and is not the EF accident it looks like.* The
shipped DDL has `onDelete: Cascade` on both FKs. `CanalaveDBCreation.sql:1473` shows `ON DELETE NO
ACTION` for the rec FK, which reads as a deliberate "a used recommendation cannot be deleted" — it
is not. Entry #1216 (2025-10-29, file lines 39871–39927) shows `ON DELETE SET NULL` was written
first, SQL Server rejected it for a multiple-cascade-path conflict (Stories→USI cascade racing
Stories→Recommendations→USI set-null), and NO ACTION was the workaround, paired with compensating
C# (lines 40030–40052) that nulled the references by hand before deleting. The intended rule was
always *deleting a recommendation clears the attribution and leaves the interaction alone.* In the
two-table shape, "set the attribution to NULL" and "delete the sources row" are the same operation,
so CASCADE is the faithful translation. **RESTRICT is rejected**: attribution is a niche feature and
must never block a recommender from deleting their own recommendation.

*Two entry points, both original, both to be built.*
- **Read It Later from the rec card** (entry #1359, file line 32135) — the durable path. Needs a new
  write-service method performing both writes in **one unit of work** (upsert the USI row with
  `IsReadItLater=true` + stamp `ReadItLaterDate`; insert the sources row). The card cannot use
  `SetUserStoryInteractionStateAsync`, which takes the whole six-bit set and would clobber the
  viewer's other flags unless the card first read per-story state for every rendered rec.
- **Direct link from the recommendation** (file line 17265, the *earlier* design, `?rec_source=ID`) —
  the same-session read-now path. Keep the URL as the carrier but **stop writing on page load**;
  persist the sources row at the 90%-of-Chapter-1 moment, in the same call that already runs
  `MarkStartedAsync`. The parent row is guaranteed there, so the FK-ordering hazard becomes
  structurally unreachable, both paths converge on one integrity rule ("a sources row exists"), and
  a URL-farm attempt must actually reach 90% of Chapter 1 per target. A direct-link reader who stops
  early and returns without the param gets no attribution — correct: the URL is the same-session
  path, RIL is the durable one.

*Removal — five triggers.* Because the attribution describes the RIL bit, it dies when that bit
does. Cascade-only is rejected: it lets a stale attribution outlive the RIL it describes, and in the
un-RIL→re-RIL-from-a-different-rec case the already-exists early return credits **the wrong
recommender**.
1. `IsReadItLater` true→false → delete, service-level, same unit of work.
2. USI row swept or deleted → cascade (already in place).
3. Prompt answered (either control) → delete; consume, per entry #1359 line 32209 ("prevents the
   pop-up from appearing again if the user re-reads the chapter").
4. Recommendation deleted → cascade (already in place).
5. Recommendation taken down or author-`Rejected` → delete, service-level sweep; no cascade fires on
   a flag/status change. Rationale: the prompt shows the rec as a reminder, so a rec that cannot be
   displayed cannot be reminded, and leaving the row would let an invisible rec collect credit.
   **Consequence, accepted:** `Rejected` is reversible ("blocked until the author unblocks it"), so
   unblocking does not restore destroyed attributions.

*The prompt.* `GetHelpfulPromptRecommendationIdAsync` returns `int?`; it must return a **DTO**, because
the widget shows the recommendation itself as a reminder — the RIL may have been long ago. Two
controls only: **Yes (thumbs up)** and **X**. There is no "No thanks" button; delete the one in
`RecommendationHelpfulPrompt.razor`. Yes → record success, then delete the sources row. X → dismiss
the widget and delete the sources row (X is the decline; a widget that returns after being dismissed
is what line 32209 rules out). Show the prompt only when: a sources row exists, the rec is visible,
`Recommendation.RecommenderId` is non-null (anonymous recs get no prompt — gate at read time, not
write time, so a recommender deleting their account between RIL and read is covered), and no success
is already recorded.

*Gates on the write paths.* Record no attribution when the caller is the **story's author** (allow
the RIL itself; skip the attribution, so no dormant row exists that can never be consumed).
Anonymous viewer clicking RIL on a rec card → login nudge. **First attribution wins** within a
single attribution's life; a re-RIL after a clear starts a new one. `RecordSuccessAsync` requires
the caller's sources row inside the service — this is §2.4.1's fix, unblocked by this ruling.

*Cross-decision note that belongs with D21, recorded here so it is not lost.*
`UserStats.RecommendationSuccessesEarned` is **derived, like every other counter** — D21 (answered
2026-08-08) admits no exception. `recommendation_successes` rows cascade away with the recommendation,
and again on the reader's `UserId`
([IdentityConfigurations.cs:64-67](TheCanalaveLibrary.Server/Data/Configurations/IdentityConfigurations.cs#L64-L67)),
so a recommender's count falls both when they delete a recommendation and when a crediting reader
deletes their account. Both are **ordinary ground truth, not drift**: the counter answers "successes
on your extant recommendations," and
[the recompute aggregate](TheCanalaveLibrary.Server/Profiles/UserStatRecalculator.cs#L163-L170)
already reads exactly that. No named exception, no surviving-credit ledger — see D21 for why the
no-tiers badge model removes the motive for one.

*Routing note for schema §2.1.* The `user_story_interactions.recommendation_id` shadow FK is still a
genuine defect, but it is a **fossil, not a modelling slip**: `CanalaveDBCreation.sql:436` shows
`SourceRecommendationID INT NULL` living directly on `UserStoryInteractions` before the split to the
partition table (the split reasoning is at file line 50220 — a mostly-null column on a hot table for
a feature absent from its main filtering job). Removing the unpaired collection nav on
`Recommendation` is correct; record *why*, so it is not re-litigated.

*What a WU needs to cover* (Features 30 + the USI cluster; audit files `Recommendations.md` and
`UserStoryInteractions.md`; built-but-inert class, so tick the matching
`hidden-deferrals-tracker.md` item or add one): new write-service method + interface change; the
card button and its wiring in every host that renders `RecommendationCard`; login nudge; delete the
`?rec=`-on-load write and move persistence to the 90% trigger; DTO-returning prompt read with the
four gates; prompt component reduced to Yes/X with the reminder card; `RecordSuccessAsync` sources-row
gate + consume; the two service-level removal triggers (1 and 5); drop the phantom nav + migration;
and fix `RecommendationWriteServiceTests.RecordAttributionSource_WritesSourceRow`, whose comment
asserts a production path that has never existed ("opening the story creates the USI row") — the
seeding is right for the real design, the stated reason is not.

### D4. System/self-sourced notifications

*Source: service §3.3; defect §2.3.2.*
`CreateCoreAsync` cannot express a self-caused or system-sourced notification, which is why
`ReportReceived` is silently annihilated by the drop-self rule. Proposed: nullable
`SourceUserId` (null = system; drop-self vacuous; dedup defined on null source) — the column is
already nullable in the DB. Enables D5.

**Answer (2026-08-04):** **Make it nullable. `CreateCoreAsync` takes `int? sourceUserId`; null = no
actor — system-sourced or self-caused.** No migration: `notifications.source_user_id` is already
`integer NULL` (`20260719023703_InitialSchema.cs:686`), `Notification.SourceUserId` is already `int?`,
the DTO's `SourceUserId`/`SourceUserName` are already nullable, and the read path already LEFT JOINs.
The write path was the only layer that could not express the shape the other four already assume.

*Drop-self becomes conditional, not accidentally vacuous.* Guard on `sourceUserId is int s &&
recipientId == s` — a null source never drops anyone. The invariant exists to suppress "you did this"
echoes; a notification with no actor has no self to echo.

*`ReportReceived` is restored by deleting the parameter that broke it.* `NotifyReportReceivedAsync`
loses `moderatorSourceId` — at submission time no moderator exists, which is why
`ServerModerationWriteService.cs:95` passed the reporter as their own source and drop-self annihilated
the row. Removing the parameter is what stops the call from ever being written that way again.

*Two different nulls now share one column, and the row cannot tell them apart.* "Actor deleted" (SET
NULL, pre-existing) and "no actor" (new) are indistinguishable in storage. Disambiguate by
notification **type** at display time, never by the column. Presenter rule: actor-free types compose
actor-free text ("Your account has been suspended"); the `?? "Someone"` fallback
(`NotificationPresenter.cs:41`) survives only for types that genuinely had an actor since deleted.
"Someone banned your account" must not be reachable.

*Guardrail — a null source is not a convenience.* Drop-self was silently protecting call sites from
self-notification; null-sourcing removes that protection, so a type with a real actor must always pass
it. Concretely: `ApplyAccountActionToUserAsync` (the moderator-initiated path, marked by
`ReporterUserId == ModeratorUserId`) must **not** be wired to the restored receipt — drop-self would
previously have suppressed it; under this ruling it would deliver, mailing moderators receipts for
their own actions.

*`RelatedEntityId` stays non-nullable; 0 remains "no related entity."* It carries no FK, is
interpreted per type, and 0 is never a valid id in this schema — NULL would buy only a second nullable
dedup component and a null branch in every enricher lookup. Promote the sentinel from
convention-by-accident to a documented rule in `layer2-services.md`.

*Dedup enumeration — the load-bearing rider, and broader than the audit states.* The key is
`(type, source, related, unread)`. Null-sourcing strips the source's discriminating power, and ten
types pass `related: 0` (70, 72, 73, 74, 76, 77, 80, 81, 82, 90), so the key degenerates to one unread
row per type per user. **Two of them already collapse today** with a live moderator source: one
moderator resolving two of a user's reports (81/82) or removing two of their items (70) delivers one
notification — against spec §5.21's "reporters always learn the outcome." Create-core's own doc comment
(`ServerNotificationWriteService.cs:421-424`) cites as its rationale the exact case its call site
defeats. Ruling per type:
- **70, 80, 81, 82 carry the report id.** Globally unique across reported-entity kinds. Carrying the
  *reported entity* id instead would falsely collapse a report on story 5 with a report on comment 5
  under one type.
- **72, 73, 74, 76, 77, 90 are exempt from cross-existing dedup.** No related entity exists — the
  target is the account itself. Two warnings while the first is unread must produce two rows.
- **Prerequisite:** schema §2.3's `related_entity_id` `int → bigint` widen (`Report.ReportId` and
  `SubmitReportRequest.EntityId` are both `long`). Already queued in the schema-hardening WU on
  unrelated grounds (comment ids are `bigint`); this ruling makes it a dependency. Land D4's change on
  that migration rather than touching `notifications` twice.

*What it unlocks.* The general "the system speaks" class becomes representable: badge-earned, digests,
worker-generated events, and any future automated moderation. (Private-message email was listed here
until D18 ruled it out of the notification catalogue entirely — it is a Messaging-owned path, not a
system-sourced notification.)

*Seed/test divergence to reconcile in the same WU.* `SeedGraph.cs:827` already writes
`SourceUserId: null` for `TagUpdateSuggestion` while the live path passes `moderatorSourceId`;
`NotificationCleanupTests.cs:81` seeds a null source commented "no actor needed." Both become legal
shapes — but the `TagUpdateSuggestion` divergence is a real fork, routed to D5.

### D5. De-identify moderation notifications

*Source: service §3.4.*
Sanction notifications (types 70–82) currently ship the acting moderator's id + username to the
sanctioned user over a WASM-reachable endpoint — a harassment/retaliation vector. Proposed:
null-source them (the audit Report row keeps the real moderator for the ledger). Settle before
real moderation happens. Depends on D4's mechanism.

**Answer (2026-08-04):** **Null-source them. The acting moderator's identity is never disclosed to the
sanctioned or reporting user through a notification.** The `Report` row remains the audit ledger and
keeps the real `ModeratorUserId` — accountability is preserved internally; only outward attribution is
removed. Mechanism is D4's nullable source.

*Threat model.* A user reading "ModeratorName banned your account" has a named target with a public
profile, a comment surface, a PM inbox, and usually an off-site identity. Moderation here is volunteer
labour and retaliation is the standard failure mode. The name buys the recipient nothing actionable —
an appeal goes through a channel, not a person.

*Suppressing it at the presenter is not a fix.* The id ships in `NotificationDto` over a
WASM-reachable endpoint, so it is client-visible whether or not any UI renders it. The row must not
carry it. Type-level enforcement: every `NotifyX(..., int moderatorSourceId)` on
`INotificationWriteService` loses that parameter, so no future call site can reintroduce the leak.

*Scope: all of 70–82, including the good news.* `StoryApproved` (75), `ExternalAccountVerified` (76)
and `ExternalLinkVerified` (78) are de-identified too. If only sanctions were anonymous, the presence
of a name would itself signal "you're fine" and its absence "you're in trouble" — the band would leak
by inference. Uniformity is the mechanism, not tidiness.

*Consequences, accepted.* A moderator can be neither thanked nor contacted through the notification —
correct; appeals belong on a dedicated channel. And the notification row alone cannot answer "which
moderator did this"; the `Report` join answers it. That separation is the point.

*Interaction with D4's dedup enumeration.* This ruling is what strips the source's discriminating power
across the whole band, so D4's per-type `RelatedEntityId` ruling is a **prerequisite** of this one, not
a follow-up. Landing D5 without it collapses the sanction band to one unread row per type per user.

*Open sub-edge routed, not dropped.* `TagUpdateSuggestion = 26` is moderator-sourced
(`NotifyTagAdoptionSuggestedAsync(..., int moderatorSourceId)`) but sits outside the 70–82 band, and
the seed tool already writes it null-sourced. Same exposure shape: a moderator invites authors to adopt
a fanonized tag, and the invitation names them. Rule it with the WU — extend the de-identification
(recommended; the invitation is a moderation act) or record why an invitation differs from a sanction.

### D6. Visibility gating: raises vs clears

*Source: service §3.5; defect §2.6 (over-filtering lockout).*
Today the full visibility guard runs on clears too: a mature-off user cannot un-favorite an M
story; mark-unread is refused on taken-down stories. Proposed rule: flag-raises require the full
guard; clears/lowers on an existing row are always permitted. Sub-question: do takedown/status
gates also lift for clears (audit leans yes — clearing reveals nothing)?

**Answer (2026-08-04):** **Take the audit's recommendation. A flag-raise requires the full
parent-visibility guard; a clear or lower on an existing row belonging to the caller is always
permitted — on all three axes (content rating, lifecycle status, takedown).** No axis is exempt from
the exemption.

*Why clears are safe, stated as the rule the sweep applies.* Visibility guards exist to prevent
**new disclosure** and **new entanglement** — kind (g)'s own justification
(`ServerUserStoryInteractionWriteService.cs:17-23`) is that raising a bit on a guessed id increments
*another user's* public counter and self-enrolls the actor in a hidden story's fan-out. A clear does
neither: it removes the actor's own row, reduces a counter, and withdraws them from a set. There is
nothing left for a guard to protect.

*The read plane already concedes what the guard was defending.* `GetStatesByStoryIdsAsync` is a
bare-FK query on `UserStoryInteractions` (`ServerUserStoryInteractionReadService.cs:53-55`) and
`ReadOnlyApplicationDbContext` declares no filter on that entity — so the client is *already* told
"you have story 123 favorited" for stories it cannot see. Refusing the clear withholds the write
while the read has already shipped: it protects nothing and costs the user their own data.

*The site already implements this rule everywhere it has paired verbs; the divergence is API shape,
not policy.* `FollowAsync` calls `RequireProfileVisibleAsync`, `UnfollowAsync` has no guard;
`JoinAsync` runs `GroupVisibilityGuard` ("you cannot join what you cannot see"), `LeaveAsync` has
none. USI and read-marks diverge only because they use a **whole-state setter** — there is no
"unfavorite" method to leave unguarded, so one guard at the top of a six-bit PUT covers raises and
clears alike. WU-ParentVisibility swept for *missing* guards and was not looking for over-broad
ones. This ruling is therefore a correction to a sweep artifact, not a new policy.

*All three axes, and the confidentiality pair is the load-bearing half.* The rating axis has an
escape hatch — direct-nav plus interstitial mints a reveal — so exempting rating alone would fix the
case that could already be worked around and leave the two that cannot. Takedown is never
reveal-bypassable: under the status quo a taken-down story freezes every reader's personal rows
**permanently**. And D1 made `published→Draft` a legal author transition, so the status axis now
lets any author freeze their readers' rows at will by unpublishing. Confidentiality is about what
the viewer may *learn*; deleting your own row teaches them nothing.

*Mechanism — load first, diff, then decide.* Invert the current order: load the existing row (both
services already do, and already capture pre-state for the transition-delta counters), diff it
against the payload, and invoke the guard only if some bit goes **false→true**. Riders:
- **Mixed payloads:** any raise anywhere in the payload guards the whole call. No per-bit
  partial application.
- **No row ⇒ nothing to clear ⇒ any true bit is a raise ⇒ guarded.** The enumeration attack kind (g)
  exists to stop is still blocked, and the safe default falls out of the diff rather than being
  bolted on.
- **No row + all-false payload** stays a silent no-op (the existing `AnyBitTrue` early return), so
  the reordering introduces no existence oracle for stories that do not exist.
- **Sparse cleanup is a clear.** An all-false update deleting the row outright is the intended
  terminal state and is permitted on hidden stories.
- **Authentication is untouched** — a clear still requires an authenticated caller acting on their
  own row.

*Chapter read-marks take the same rule with one asymmetry already in the code.*
`SetChapterReadAsync(id, false)` and `SetAllChaptersReadAsync(id, false)` become ungated; the
`isRead: true` paths keep the full `IsChapterVisibleAsync` / `IsStoryVisibleAsync` guard. Their
cascade into `MarkStartedAsync`/`MarkCompletedAsync` is unaffected: those two only ever *set* bits,
so they are raises by construction and keep the unconditional guard. `HasStarted` remains
non-clearable by any surface (`Has-` prefix = permanent past event, WU45) — this ruling does not
make it one.

*Counter consequence, accepted and named for D21.* Clearing a favorite on a taken-down or
status-hidden story decrements the author's `FavoritesOnStories`. That is correct — the favorite is
genuinely withdrawn — and it is the point: today the counter retains favorites nobody is permitted
to withdraw. D21's recompute answer should treat withdrawal-while-hidden as ordinary ground truth,
not drift.

*Prospective harm this closes before it exists.* The new-chapter fan-out producer is not built
(§2.3.1; recipients = `IsFollowed`). Once it is, a mature-off user who followed while mature-on
would receive alerts for an M story they cannot unfollow. Land D6 before the producer ships (D16/D17
territory) so that state is never reachable.

*Scope — enumerate before fixing, per the audit-before-cross-cutting rule.* The two confirmed
surfaces are `SetUserStoryInteractionStateAsync` and the two `ServerChapterReadMarkWriteService`
methods. The WU first walks the sibling clears and records the result rather than point-fixing:
`ServerContentRevealService.RemoveAsync`, `RemoveVouchAsync`, `UnfollowAsync`, `Group.LeaveAsync`,
`CustomList.RemoveStoryAsync`, `Series.RemoveStoryAsync`. Where a clear is already unguarded, that
is now a *recorded* conformance rather than an accident; where one is guarded, it moves.

*Doc home (Doc-Touch moment 1, before code).* `identity-and-authorization.md`
§"Parent-visibility guards" — kind (g) currently reads "child write service calls it and throws"
with no raise/clear distinction; it gains the rule and the load-first-diff mechanism.
`layer2-services.md` §Content-Rating case 2 gains a cross-reference: its verbatim "the caller's
viewer settings must not prevent the service from confirming the entity exists" is the same
principle, and the USI panel was its standing contradiction.

*Tests — the free window.* `ParentVisibilityContractTests` asserts the **raise** cases only
(`SetUserStoryInteractionStateAsync` with bits true at :536-542, `SetChapterReadAsync(id, true)` at
:554-560), so no existing test encodes the behavior being removed. The WU adds the mirrored
clear-succeeds cases — one per axis, including a taken-down story — which is what stops the sweep
artifact from reappearing.

*Sequencing.* Gates WU-AccessGateSweep2 together with D25; independent of the Block B/C rulings.

## Block B — Moderation policy

Completes WU-ModerationIntegrity's inputs.

### D7. Sibling-report auto-resolution

*Source: service §3.9; defect §2.4.4.*
Does resolve-with-removal close all other open reports on the same target? Recommended yes, in
the same unit of work — this defines what `ActiveReportCount` *means* and eliminates the
zombie-open-reports-on-deleted-targets class.

**Answer (2026-08-04):** **Yes. `ResolveWithRemovalAsync` closes every other Open|UnderReview report
on the same target, in the same unit of work — soft takedown and hard delete alike.** Closure is
keyed on the **target**, `(ReportedEntityType, ReportedEntityId)` together — never on the id alone
(story 5 and comment 5 are different targets, the same distinction D4 makes for `RelatedEntityId`)
and never on the target's *author* (a ban does not answer reports about that author's other posts).

*What this defines — the sentence the rest follows from.* `ActiveReportCount` is a cache of
`COUNT(*) FROM reports WHERE (type, id) = target AND report_status_id IN (Open, UnderReview)` —
**how many unanswered questions stand against this target**, which is exactly what the queue's
triage sort (`GetReportQueueAsync`, ordered by `TargetActiveReportCount` desc) claims to rank. Under
the status quo it is instead "how many reports were filed and not individually clicked," and those
diverge the moment two people report the same thing. Removal answers the question once, for the
target, so the count must go to zero — not to N−1.

*The zombie class is the load-bearing argument, and hard delete makes it unavoidable.* Reports carry
no FK to their polymorphic target, so they survive its deletion; `ServerModerationReadService.cs:89-94`
then **silently drops** queue rows whose target no longer materializes. Without sibling-closing, one
hard delete leaves N−1 rows that are permanently `Open`, permanently invisible, and permanently
unresolvable — while their +N sits on a counter that has no recompute path (§2.4.4). That is not
drift that a later reconciler tidies; it is a state the UI cannot reach. This is the one path where
"leave the siblings and let a moderator click through them" is not merely wasteful but impossible.

*The criterion is answerability, not irreversibility.* After a removal, a moderator opening a sibling
report sees removed content and can do exactly one thing — resolve it. The queue item has no
remaining decision in it. That is why the rule is **removal only**:
- **`ResolveNoActionAsync` does not close siblings.** The target is unchanged and every other report
  is still genuinely actionable — a different reporter, a different reason, possibly a correct one
  the first moderator's reason did not cover. One moderator's "no" is a ruling on one complaint.
- **Neither account-action path bulk-closes.** Warn/Suspend/Ban change the *account*, not the
  reported artifact; the content stays live and each remaining report still asks a live question.
  For a User-typed target this is deliberate too: `ApplyAccountActionAsync` already resolves its own
  report and decrements, and the remaining reports on that account stay open because a warned or
  suspended user's conduct is still under review (and, per §2.1.3's fix, a ban is reversible via
  Reinstate).
- **A sibling claimed by another moderator (`UnderReview`) closes anyway.** A claim is triage
  bookkeeping, not a lock; exempting claimed rows would preserve the exact zombie in the exact case
  where two moderators are working the same target.

*Siblings resolve as `ResolvedActionTaken`, not `ResolvedNoAction`.* Action **was** taken on their
target; telling a reporter "no action" about content that was just removed is a false statement to
the person the spec promises an outcome. Each sibling gets `ModeratorUserId` = the acting moderator,
`DateResolved` = the same timestamp, and `ActionTaken` = the removal reason with the resolving report
id named, so the ledger records that the row was closed *by* another report rather than reviewed on
its own. **Nothing is deleted** — every reporter's row and reason survives, which is what D8's
ban-signal reading and `GetUserModerationHistoryAsync` depend on.

*Every sibling reporter is notified.* Spec §5.21's "reporters always learn the outcome" is the whole
reason the bulk close cannot be silent — `NotifyReportResolvedAsync` per distinct sibling reporter,
in the existing best-effort try/catch, anonymous reporters (null `ReporterUserId`) excluded. This
interlocks cleanly with the two rulings around it: D4 puts the **report id** on types 80/81/82, and
§2.4.4's per-(reporter, target) open-report unique index means one reporter can hold at most one
open report per target — so "notify every sibling reporter" is exactly-once by construction, with no
dedup collapse. Land the index in the same WU; without it the notification loop can address the same
user twice for one event.

*Mechanism — derive the counter change from rows actually transitioned.* Inside one
execution-strategy transaction: (1) load the primary report and apply §2.1.2's status guard
(Open|UnderReview → else `ModerationValidationException`); (2) perform the removal; (3) set the
primary report's fields tracked, as today; (4) one `ExecuteUpdateAsync` over
`Reports.Where(target matches && status is Open|UnderReview && ReportId != thisId)` → **N rows
affected**; (5) `AdjustActiveReportCountAsync(target, −(1 + N))`; (6) `SaveChangesAsync`. Riders:
- **Deriving the delta from rows-affected is what makes the counter self-correcting.** It cannot
  double-decrement, cannot go negative, and a report filed concurrently between (4) and (5) keeps
  its own +1 and stays open — the invariant holds without a lock. Do **not** zero the column
  outright; that would swallow the concurrent report.
- **The transaction is the "same unit of work" the audit asks for.** `ExecuteUpdateAsync` executes
  immediately while the primary report's edits wait for `SaveChangesAsync`; unwrapped, a failure
  between them closes the siblings against a removal that never committed.
- **The primary row stays tracked and is excluded from the bulk update** — a tracked entity and a
  set-based update on the same row is the collision the split avoids.
- **Hard delete's counter adjustment is vacuous** (the target row is deleted in the same
  `SaveChanges`) and that is fine — the counter dies with its row. What matters is that the report
  rows, which outlive the target, are closed.
- **`Message` targets keep the no-op counter half** (no `ActiveReportCount` column) while the
  sibling-closing half applies normally.

*Consequences, accepted and stated so they are not re-litigated.* (a) **Reversing a takedown does not
reopen the closed siblings** — no pre-state is stored and D8 rules the Report row the ledger; content
that is again problematic is reported again. (b) A reporter whose report was closed by someone else's
resolution is told "resolved, action taken," which is true, without being told that a different
report drove it — correct, and it is the same de-identification posture as D5.

*Sub-edge routed, not dropped.* Sibling-closing eliminates the zombie class **created by moderation
removal**; it does not touch zombies created elsewhere — `UserDeletionService` cascades destroy
content whose reports survive with the same invisible-and-open shape. The WU must either close
reports targeting entities destroyed by user deletion at that site, or hand the case to the
reconciler below. Pick one in the WU and record it; leaving it implied is how this class re-forms.

*Two schema items this ruling makes load-bearing, both free while the table is empty.* The sibling
query and the recompute both need a **partial index on `(reported_entity_type, reported_entity_id)
WHERE report_status_id IN (0, 1)`** — land it on §2.4.4's per-(reporter, target) partial unique index
migration. And this answer hands **D21** the recompute expression `ActiveReportCount` currently lacks
(§2.4.4(b): "no recompute path at all"): the `COUNT(*)` above is its ground truth, so D21 can classify
the column as *derived* rather than authoritative, and the reconciler that closes case (b)'s
orphaned +1 falls out of the same definition.

*Doc home (Doc-Touch moment 1, before code).* `layer2-services.md` §"Moderation Model (settled WU34)"
— the target-keyed sibling rule, the `ActiveReportCount` definition, and the removal-only criterion;
`audit/Moderation.md` gains the settled note. **WU-ModerationIntegrity** owns the build, jointly with
§2.1.2's status guards and §2.4.4's dedup index — they touch the same three methods and must not be
split. **Tests (Integration tier, `ModerationServiceTests`):** three reports on one target, resolve
with removal → all three `ResolvedActionTaken`, counter exactly 0, three reporters notified; the same
shape with `hardDelete: true`; and the negative case — resolve-no-action leaves the siblings `Open`
and the counter at N−1.

### D8. Report reported-user column + takedown-reversal ratification

*Source: schema §3.7; tracker B18.*
(a) Denormalize `reported_author_user_id` onto `reports` at write time now (one nullable column +
one write-path line vs a four-table backfill later)? For ban decisions it is the primary signal,
even if the B18 UI waits. (b) Ratify that takedown reversal leaving stale/nulled takedown
metadata is acceptable *because* the Report row is the audit ledger — so nobody adds a
takedown-history table reflexively.

**Answer (2026-08-05):** **(a) Yes — denormalize now, as a nullable `ReportedUserId`
(`reported_user_id`), populated at write time for every target type. The column is not named
"author."** **(b) Ratified — a takedown reversal that leaves nulled takedown metadata is acceptable
*because* the `Report` row is the audit ledger. No takedown-history table.**

*Why "user" and not "author."* `ReportedEntityType` spans seven values — six shipped, plus `Group = 6`
added by D13 after this entry was written (see the amendment at the end of D8) — and the answerable
account is called something different across them: Story / Comment / BlogPost / Recommendation have an
author, `Message` has a sender, `User` **is** the account, and a `Group` may have **no** answerable
account at all. `reported_author_user_id` would be a
misnomer on two of six rows, and — the failure this ruling is actually guarding against — it would
invite the next reader into exactly the story-only reading that made B18 look like a stories
problem. `ReportedUserId` also lands in the vocabulary the table already speaks: `ReporterUserId`
(who filed), `ModeratorUserId` (who ruled), `ReportedUserId` (who it is about), beside
`ReportedEntityType`/`ReportedEntityId` (what it is about). Semantics, stated once: **the account
answerable for the reported artifact at the moment the report was filed.**

*Populated for every type that has an answerable account, including `User` — this is the half that
pays.* Filling it only for
content targets would leave B18's read a two-branch predicate (`type == User && entityId == uid`
OR `contentOwner == uid`) forever. Filling it always collapses the whole moderation history to
`Reports.Where(r => r.ReportedUserId == uid)` — one predicate, one index, no per-type join, and it
is a strict superset of what `UserModerationHistoryDto.Reports` returns today. So B18 does not
narrow `ModUsersPage`'s on-screen caveat ("Reports against content they wrote are not listed
here"); it **deletes** it, and the column replaces the existing user-targeted query rather than
supplementing it.

*The write-path line already exists.* `ResolveActionTargetUserIdAsync`
(`ServerModerationWriteService.cs:319`) resolves the owning account across all six types today —
`User` → the target, `Message` → `SenderUserId`, the four `IModeratableContent` types →
`AuthorUserId`. `SubmitReportAsync` calls a **nullable-returning** sibling of it (extract the switch;
the account-action path keeps its throw). Submit must not adopt that throw: anonymous or
deleted-author content stays reportable, and the column simply goes NULL. That asymmetry is the
whole difference between the two call sites and is why it is an extraction, not a reuse.

*Nullable, FK → `users`, ON DELETE SET NULL* — matching `ReporterUserId`/`ModeratorUserId`, for the
same reason schema §3.7 gives: reports outlive the accounts they name. A NULL therefore means
"owner unknown, anonymized, or deleted," never "this artifact has no owner."

*Snapshot at write, never re-resolved.* The column records who was answerable when the report was
filed. It is not a cache of a live join and must not be recomputed to "correct" it — a moderator
reading history needs what was true at report time. Recorded here so a later session does not file
it as drift. (Practical consequence: nothing that reassigns content ownership rewrites report
history.)

*Cost is asymmetric and the window is open.* One nullable column + one extracted resolver **now**
versus a four-table backfill join later, on a table that is still empty. Land it with D7's partial
indexes on the same migration, plus a plain index on `reported_user_id` for the B18 read — all
three are free at zero rows and none of them are afterwards.

*(b) Takedown reversal — ratified, and the verification the audit asked for is done.* Schema §3.7
said "populated-but-stale or nulled (whichever the service does — verify)". The answer is
**neither: no reversal path exists.** Nothing in the codebase writes `IsTakenDown = false`;
`ApplyRemovalAsync` (`:554`) only ever sets the trio on. So this ratification is forward-looking
rather than a description of shipped behavior: **when reversal is built, it nulls `TakedownDate`
and `TakedownReason` along with the flag** — the entity carries current state only, and the `Report`
row (which survives user deletion, per §3.7) carries the history. Do not add a takedown-history
table, and do not leave stale metadata behind as a pseudo-history. This interlocks with D7's
accepted consequence (a): reversal does not reopen closed sibling reports either, for the same
reason — the ledger is the report, and content that is again problematic is reported again.

*Doc home (Doc-Touch moment 1, before code).* `layer2-services.md` §"Moderation Model (settled
WU34)" — the `ReportedUserId` semantics (answerable account, every target type, snapshot-at-write) and
the entity-carries-current-state / report-carries-history split. `audit/Moderation.md` gains the
settled note. Tracker **B18**'s open "denormalize vs join-at-read" question is closed by this entry;
what remains under B18 is the DTO + UI work. **WU-ModerationIntegrity** owns the column and the
migration (jointly with D7's sibling-close work — same three methods, same migration); the B18 read
and caveat removal may follow separately. **Tests (Integration tier, `ModerationServiceTests`):**
submit one report against each target type and assert `ReportedUserId` resolves to the
right account; anonymous-author content → report succeeds with a NULL column; and a user-deletion
case asserting the report row survives with `ReportedUserId` nulled.

**Amendment (2026-08-08) — the seventh target type.** D13 (2026-08-06) added `Group = 6` to
`ReportedEntityType` without returning to this entry, which was written against six. Two things this
answer left undefined, settled here rather than left for the consuming WU to invent:

*`ReportedUserId` for a `Group` target is `groups.creator_id` when non-null, and NULL otherwise.* The
resolver extracted from `ResolveActionTargetUserIdAsync` gains a `Group` arm reading `creator_id`
directly — **not** a lookup of current admins, which would violate this entry's own snapshot-at-write
rule and would in any case return nothing in D13's motivating scenario (its guard requires zero
admins). A NULL here is **expected, not a write-path bug**, and it is the one target type where NULL
is reachable by design rather than by anonymization: D13's account-deletion route nulls `creator_id`
outright, so a group can be reportable while having no answerable account at any point in its life.
Recorded explicitly because this entry's rule reads "populated at write time for every target type,"
and a later reader finding NULL Group rows would otherwise file it as the producer being broken.

*The accepted consequence, stated so it is not re-litigated.* A founder's moderation history
(`Reports.Where(r => r.ReportedUserId == uid)`) will include reports about a group they founded and
no longer administer — including, after D13's mod-assign action, one whose admin is someone else.
That is correct under this entry's own semantics ("the account answerable **at the moment the report
was filed**"), and it is the snapshot rule working as designed, not attribution drift. Moderators
reading the history see the report's own `ReportedEntityType`, so a Group row is never mistakable for
a content report against that user.

*What this amendment does not settle.* Whether a moderator can **do** anything about a Group-typed
report is a separate and currently open question — D7's `ResolveWithRemovalAsync` has no Group branch
and D13 relies on there being no group-deletion path at all. That is **D47**, at the end of this
block. This amendment covers only what the ledger row records.

### D9. Mod-only read gating posture

*Source: service §3.8.*
Extend the service-layer gate rule to the three sensitive mod reads (recommended — three lines),
or record a deliberate "reads gate at the edge, writes in the service" split in
`identity-and-authorization.md`. Related riders: split user-facing report submission from the
mod-queue interface (type-level least privilege); the ExternalVerification mod-read posture is
the same question.

**Answer (2026-08-06):** **Option A — extend the service-layer gate rule. Every moderator-only read
performs its own role check inside the server read service, and the endpoint policy stays as the
edge half of the defense-in-depth pair. Do the interface split too — alongside the gate, not
instead of it.** The deliberate "reads gate at the edge, writes in the service" split is **not**
available as a ruling; see below.

*The set is five reads, not three — enumerate before fixing.* `GetReportQueueAsync`,
`GetPendingSubmissionsAsync`, `GetUserModerationHistoryAsync`
(`ServerModerationReadService`), plus `GetPendingAccountVerificationsAsync` and
`GetPendingLinkVerificationsAsync` (`ServerExternalVerificationReadService`) — the audit's "same
question" rider is folded in rather than deferred. `GetReportReasonsAsync` stays out: it feeds
`ReportDialog` for any authenticated user and `ModerationEndpoints`' class doc already records why.
The WU also sweeps the three remaining mod-only endpoint groups (SiteDailyStat, SiteSettings,
SpotlightSlotAllocator) and records the result, so a sixth case is not discovered later.

*The hole is the circuit, not HTTP — which is why this is a real gap and not a paper one.* All five
reads already carry `AuthorizationPolicies.RequireModerator` at the endpoint
(`ModerationEndpoints.cs:67-82`, `ExternalVerificationEndpoints.cs:46-52`), asserted by
`ModerationEndpointsTests`/`ExternalVerificationEndpointsTests`. Over HTTP there is no live leak. On
the SSR circuit **no endpoint exists**: `ModUsersPage` injects the server read service in-process and
the sole control is the page's `[Authorize(Roles = "Moderator,Admin")]`. Gating is therefore
transport-dependent, and only one direction is written down — `identity-and-authorization.md`'s
"endpoint-level… does not inherit from the page" exists to stop a page attribute being trusted to
protect an endpoint, and says nothing about what protects the circuit. The exposure is prospective:
the first mod widget on an otherwise-public page, dashboard tile, or non-page consumer of these
reads inherits no gate at all, and nothing fails loudly when that happens.

*Option B is foreclosed by shipped code, not by preference.* Two read services already role-gate
internally, both from the 2026-07-18 endpoint-authz sweep, both commented to say the gate is
enforced *there* precisely because the route cannot be trusted:
`ServerBlogPostReadService.GetSiteAnnouncementsAsync` (:331-336, downgrades a forged
`includeUnpublished=true`) and `GetSiteAnnouncementForEditAsync` (:367-373, throws). Recording
"reads gate at the edge" as doctrine would convert both into violations and require amending their
rationale comments. The split is not the codebase's rule — it is a Moderation/ExternalVerification
local exception — and it contradicts **P2** head-on ("the endpoint carries an *equivalent* edge gate
for defense in depth"), which is exactly why the audit lists it as one of P2's two exceptions.

*These are the worst possible reads to leave on a single non-inheriting attribute.* They are the
only reads in the codebase that turn **every** query filter off — `IsTakenDown`, `ContentRating`,
`StoryStatus`, `ProfileVisibility` (`ServerModerationReadService.cs:12-19`, :130, :155-156). The
elevation is correct (`content-safety.md` §"Moderator review surfaces are work surfaces"), but it
means an ungated caller does not get a filtered-down view — they get taken-down content, M-rated
stories, Private profiles' standing, and other users' report history, unfiltered. Maximum blast
radius behind minimum protection.

*Failure class — throw, matching `RequireModerator()`.* Unauthenticated →
`InvalidOperationException` → 401; signed-in non-mod → `UnauthorizedAccessException` → 403. **Not**
empty-collection. The Class-A "indistinguishable from not-found" rule governs *content* visibility,
not role gates; a mod queue's existence is not a secret, and returning `[]` would make a
mis-registered surface look *empty* rather than *broken*, silently masking the exact
misconfiguration the gate exists to catch. Precedent is `GetSiteAnnouncementForEditAsync`, which
already throws from a read service for this reason.

*Extract the guard; do not add copies four and five.* §5's pattern-uniformity item already stands
open — three private `RequireAuthenticatedUser`/`RequireModerator` copies with two different
unauthenticated behaviors (401 vs 403). This ruling is the moment that item is discharged: one
shared guard, one pair of exception semantics, used by the write services and the read gates alike.

*Cost, and the one piece of plumbing.* ExternalVerification is two `if` lines —
`ServerExternalVerificationReadService` already injects `IActiveUserContext` and exposes the
`ActiveUser` protected property (:14-19). `ServerModerationReadService` takes only the DB factory
(:25-26) while the write service holds `activeUser` without chaining it up
(`ServerModerationWriteService.cs:35-43`), so it gains a ctor parameter plus the same `protected
ActiveUser` property the EV read service already models. A base-class ctor change under a derived
write service — hence same-WU with anything else touching those constructors.

*The interface split is complementary, not an alternative — and the audit understates it.*
`IModerationWriteService` mixes `SubmitReportAsync` (any authenticated user) with
`ResolveWithRemovalAsync`/`ApplyAccountActionAsync` (hard delete, ban, suspend), and
`ReportDialog.razor:3` — a cross-cutting leaf consumed by StoryCard, UserCard, CommentItem,
BlogPostCard, recommendation cards and message threads, i.e. **public pages** — injects the whole
interface. Both interfaces are registered in `Client/Program.cs:96-97`, so the entire mod surface
lives in WASM DI; `Server/Program.cs:505`'s "Mod pages are server-rendered, no dispatcher/WASM"
comment is therefore stale and misleading (the §4 load-bearing-false-comment class — correct it in
this WU regardless). Split `IReportSubmissionService` (reasons + submit) from the mod-queue read/write
interfaces: **P1** ("components inject the narrowest interface") and **constraint 4** (solo
maintainer — structure-enforced rules survive, vigilance-enforced rules decay) both point at it, and
**constraint 5** makes the contract change free today. But type-level narrowing is a compile-time
discipline, **not a runtime control**: a mod-only interface still needs the gate on the circuit path.
Answering D9 with the split alone would leave the actual hole open.

*Doc home (Doc-Touch moment 1, before code).* `identity-and-authorization.md` — the seven-kinds
table's kind **(c)** currently names a UI/page mechanism only, while kind (d) names both halves; (c)
gains its service-layer half. The §"Role-Based (Moderator) Gating" sentence "the service-side
`RequireModerator()` remains the enforcement point of record" is today true for writes only — it
becomes true as written. Both endpoint class docs (`ModerationEndpoints`,
`ExternalVerificationEndpoints`) narrate "the service performs no role check of its own for these
reads" and move in the same edit. `audit/Moderation.md` gains the settled note.

*Sequencing and ownership.* **WU-ModerationIntegrity** owns it, jointly with D7 and D8. The read
gates touch different methods than D7's three, so no collision there — but the interface split
touches the same *types*, so it lands first within the WU or not at all. D8's B18 follow-up moves
consumers of `GetUserModerationHistoryAsync`; the split's shape accounts for that. Independent of
Block C and of D5.

*Sub-edge routed, not dropped.* §2.12's 401-instead-of-404 class (nonexistent report id;
ExternalVerification's `SingleAsync`) traces to the `EndpointHelpers` translation mismatch that
`ModerationEndpoints.cs:38-44` already documents as flagged/deferred. The WU either fixes the
translation table while it is in these files or records explicitly that it does not — leaving it
implied is how the deferral goes stale.

*Tests (Integration tier).* The existing endpoint tests cover HTTP and stay. Add direct
service-level calls with `SetActiveUser(userId)` (authenticated, `IsModerator = false`) asserting
`UnauthorizedAccessException` from each of the five reads, plus one anonymous case asserting the 401
branch — this is the tier that catches a circuit-path regression, and `FakeActiveUserContext`
already supports it with no new setup.

### D47. Moderator actions available for Group-typed reports

*Added 2026-08-08 (numbered out of sequence — it belongs topically with D7/D8). Source: the
D8 seventh-type amendment above; D13 edge (b); defect §2.1.2 / service §3.9.*

D13 added `Group = 6` to `ReportedEntityType`, justified by "report a group for an inappropriate
name." Nothing then defines what a moderator can **do** with such a report, and the two rulings that
would supply it point opposite ways:

- **D7** makes `ResolveWithRemovalAsync` generic over `(ReportedEntityType, ReportedEntityId)`. There
  is no Group branch and no group-removal capability to put in one.
- **D13** states flatly that "there is no group-deletion path at all (no `DeleteGroupAsync`, and
  nothing on the moderation surface)" — and *depends* on that fact, using it to argue that a mod
  assigning an admin is the only available remedy.

The gap that follows is narrow and exact: **D13's own motivating example has no answer.** Its
assign-admin action is guarded on the group having **zero** admins. A group with a live admin and an
inappropriate name is reportable, reaches the queue, and offers a moderator no action at all — no
removal, no rename authority, and the assign guard blocks. Resolve-no-action is the only reachable
outcome, which tells the reporter their complaint was reviewed and rejected when in fact it could not
be acted on.

Two questions, both open:

**(a) What does the queue offer for a Group target?** At minimum, removal must be hidden or disabled
for Group rows so a moderator cannot reach an unimplemented branch — that much is forced by D7's
mechanism regardless of how (b) lands, and should be treated as the floor rather than a decision.

**(b) What is the actual remedy for an inappropriate group name?** Three candidates, none ruled:
*moderator force-rename* (smallest new power, directly answers the reported harm, but is the first
mod capability that edits user content in place rather than removing it); *account action against
the creator* under the existing Warn/Suspend/Ban path (adds no new capability, but punishes an
account for an artifact that stays live — and does nothing when `creator_id` is NULL, which D13's
deletion route produces); or *build group removal* (closes the type-coverage hole in D7 and gives the
"mods can always delete it" fallback D13 correctly observed does not exist — but directly contradicts
the premise D13's ruling rests on, so choosing it means re-opening D13 edge (b), not merely extending
it).

*Sequencing note.* This does not block **WU-GroupAdminRescue**, which builds D13's assign action and
the `Group = 6` enum member. It blocks the *report-submission* half — until (b) is answered, allowing
users to file Group reports creates queue rows nobody can action, which is the zombie-report shape
D7 exists to eliminate. If WU-GroupAdminRescue lands first, it should add the enum member for its own
ledger rows without exposing a user-facing "report this group" control.

*Consequence to record wherever (b) lands.* If group removal is ever built, every member's
`GroupsJoined` falls for a reason they did not cause — see D21's conditional falling-count note and
D24.

**Answer:** _(pending)_

## Block C — Delete posture

Gates WU-TptHardDelete and the schema-hardening migration; the remaining items keep the deletion
philosophy coherent while it's loaded.

### D10. TPT FK posture

*Source: schema §2.2 (fix options) + service §2.2; flagged for sign-off in schema §7.*
Confirm the recommended combination: fix the three unguarded hard-delete sites now (copy the
`DeleteChapterAsync` template), AND flip content-parent → comment/blog/poll-child FKs to
RESTRICT so the DB refuses partial deletes loudly (free on an empty DB), optionally plus a
scheduled orphan-check sweep. Alternative: app-rule-only (fragile — this audit found 1 of 3
sites remembered).

**Answer (2026-08-06):** **Take the combination, with the fix's shape refined and one alternative
closed. (a) Fix all three unguarded sites now, and convert `DeleteChapterAsync` to the same shared
helper so one shape governs all four. (b) The helper deletes through the *base* row, set-based —
not by loading child entities. (c) Flip content-parent → TPT-child FKs to RESTRICT in the hardening
migration. (d) No scheduled orphan sweep.**

*RESTRICT is a guardrail, not the repair — state this plainly so the WU doesn't misread §2.2 as
offering a choice.* A RESTRICT FK deletes nothing; it refuses a parent delete while children exist.
The service layer does the actual cleanup under either posture. So §2.2's options 1 and 2 are not
alternatives to be picked between: **1 repairs the live defects, 2 prevents the fourth site from
recurring.** Adopting only 2 would leave three broken paths that now throw instead of corrupting —
better, but still broken. Adopting only 1 is the app-rule-only branch this audit already disproved
by finding 1 of 3 sites remembered.

*Why no cascade configuration can solve this — the structural fact behind the whole item.* The
orphan is not produced by deleting a comment; it is produced by deleting the **content parent**.
`chapter_comments` carries two FKs, both CASCADE (`InitialSchema.cs:2137-2143`):

| FK | Direction | Effect of a delete on the principal |
|---|---|---|
| `comment_id → base_comments` | base → child | Removes both rows. **This direction is already correct.** |
| `chapter_id → chapters` | parent → child | Removes the child row only. **This is the orphan.** |

For `DELETE FROM chapters` to reach `base_comments`, `base_comments` would need an FK to `chapters`.
It cannot have one: it is the polymorphic base shared by chapter / blog-post / group / profile
comments, with no column to hang a `chapter_id` on — and adding one defeats the reason the base
table exists. Cascades flow along FKs, so **no arrangement of `ON DELETE CASCADE` can make a
content-parent delete reach the base rows.** Service-layer participation is therefore not a design
preference; it is forced. Record it in `layer1-data-model.md` in those terms, beside the existing
TPT traps.

*The helper deletes through the base row, not through a loaded entity list.* The
`DeleteChapterAsync` template materializes every comment (`ToListAsync` + `RemoveRange`) purely to
make EF emit child-then-base DELETEs. The cheaper and clearer shape keys off the child table and
lets the *already correct* base → child CASCADE do the rest — one statement, no materialization:

```sql
DELETE FROM base_comments
WHERE comment_id IN (SELECT comment_id FROM chapter_comments WHERE chapter_id = @id);
```

Two constraints the WU must carry. **(i)** `ExecuteDeleteAsync` is unsupported on TPT base-type
`DbSet`s — WU31.5 hit this and it is documented in-code at `ServerBlogPostWriteService.cs:177`. So
the helper is raw SQL or a change-tracker stub delete, never LINQ; both have in-repo precedent
(WU31's "raw SQL + CASCADE FK"; the stub at `ServerBlogPostWriteService.cs:176-180`). **(ii)** The
two mechanisms compose: RESTRICT on `chapter_id → chapters` fires only when the `chapters` row is
deleted, so clearing children by base id first satisfies it. Likes and votes continue to cascade off
the deleted base rows, and reply sets stay closed under the parent scope exactly as the chapter
template's comment already argues.

*FKs that flip.* `chapters → chapter_comments`, `base_blog_posts → blog_post_comments`,
`groups → group_comments`, `groups → group_blog_posts`, `base_blog_posts → blog_post_polls`.
`AspNetUsers → user_profile_comments` is **already** RESTRICT — it is the in-schema precedent for
the posture, not an exception to it. Free today on empty tables; not free later.

*No orphan sweep.* Once the FKs are RESTRICT, an orphaned base row is a state the database refuses
to enter, so a scheduled `LEFT JOIN`-all-children check would be polling for an impossibility. §2.2
option 3 is declined on that ground — **not** deferred, so no tracker item is owed. It becomes
live again only if the RESTRICT posture is ever loosened; whoever loosens it re-opens this line.

*TPH is closed, on read-shape grounds.* The obvious "make cascades sufficient" answer is to collapse
the hierarchy to a single discriminated table, which would delete the orphan class outright. It is
rejected: **the base/child split is load-bearing for the warm/cold vertical partition** — the narrow
base table serves the polymorphic scans, the wide child tables the detail reads, and TPH collapses
that into one wide table on the hot path. Secondary costs (four nullable parent-FK columns, loss of
per-subtype FK integrity) only add to it. Recorded here so the next reader who spots the cascade
asymmetry does not propose it a second time.

*Sequencing and ownership.* **WU-TptHardDelete** owns it, jointly with the schema-hardening
migration's FK flip (schema §7) — one cross-cutting WU per the audit-before-crosscutting rule, not
three point fixes. The WU also carries service §2.2's adjacent group-post lifecycle defect
(`DeleteBlogPostAsync` on a group-post id issues a `profile_blog_posts` DELETE affecting 0 rows →
`DbUpdateConcurrencyException` → 500: a group-post author can never delete their post; `Update`
half-works), since it lives in the same methods. Correct the false "cascades handle it" comments at
`ServerBlogPostWriteService.cs:177-178` and `:406` in the same change — leaving a doc comment that
asserts the disproved thing is how this defect propagated to three sites.

*Tests (Integration tier).* Per content parent, delete and assert **zero surviving base rows** for
the affected ids — `base_comments` after a chapter, profile blog post, site blog post, and group
delete; `base_polls` (with its options and votes) after a blog-post delete; and the moderation path,
where hard-deleting a Story must leave no base comment from any of its chapters. Plus one test that
proves the **FK posture itself** rather than the service code: raw-SQL `DELETE` of a parent row with
children present, expecting Postgres 23503 — this is the only test that catches a future migration
silently reverting RESTRICT to CASCADE. Plus the group-post lifecycle case: the author of a group
post deletes it and succeeds.

### D11. Poll-owner delete behavior

*Source: schema §2.5.*
`base_polls.owner_id` CASCADE contradicts the content-survives-anonymized policy: an anonymized
blog post keeps a hole where its poll was, and other users' votes are destroyed. Recommended:
nullable + SET NULL, matching `base_blog_posts.author_id`. Confirm — or record
poll-death-with-owner as a deliberate exception.

**Answer (2026-08-06):** **Take the audit's recommendation:
`base_polls.owner_id` → nullable + `ON DELETE SET NULL`, matching `base_blog_posts.author_id`.**
An embedded poll is part of the post it lives in, so it inherits the post's classification: content,
which survives anonymized. Poll-death-with-owner is declined as an exception — it would destroy
*other* users' votes as collateral, which no reading of "the author left" justifies. Poll **votes**
keep their CASCADE on the voting user; that is interaction data and stays as-is.

*Consequences to carry into the build.* Every owner read must tolerate NULL — render the anonymized
owner the same way an anonymized blog-post author renders, and treat a NULL owner as "nobody" in
ownership checks (`owner_id = @me` must never match, so an orphaned poll becomes editable/closable by
no one rather than by everyone). Whether an ownerless poll stays votable is D42's question, not this
one.

*Sequencing.* Rides the schema-hardening migration (schema §7) with the rest of the §2 items — empty
tables, so the column flip is free now and a data migration later.

*Test (Integration tier).* Delete a user who owns a poll on a blog post authored by someone else:
assert the `base_polls` row survives with NULL `owner_id`, its options survive, and the other users'
votes survive.

### D12. Comment deletion: placeholder vs reparent

*Source: service §3.20; spec §5.9.*
Spec calls for a "[Deleted Comment]" placeholder; shipped behavior hard-deletes and reparents
replies to root. Genuinely open — no recorded note rejects the placeholder.

**Answer (2026-08-06):** **Ratify the shipped behavior — hard delete + reparent replies to root.
The "[Deleted Comment]" placeholder is declined, and spec §5.9 is superseded on this point.** A
deleted comment leaves no trace: no tombstone row, no placeholder node, no "removed" affordance
anywhere in the thread. Nothing is built; this entry exists to close the question and to retire the
placeholder from the docs that still promise it.

*Why the placeholder loses.* It buys exactly one thing — surviving replies keep their referent, so a
promoted reply can't read as addressed to the chapter instead of to the comment it answered. That is
real but bounded, and it costs a permanent third comment state (live / taken-down / deleted) that
every read must tolerate, a render branch, a reap rule to stop dead rows accumulating on the site's
hottest read path, and the tests to hold all of it. Two justifications examined and rejected as
false: it recovers **nothing** (the placeholder holds no text), and it does **not** protect
notification integrity — every comment notification stores the *context* entity in
`RelatedEntityId` (chapterId / blogPostId / profileOwnerId,
[NotificationEnricher.cs:112-119](TheCanalaveLibrary.Server/Notifications/NotificationEnricher.cs#L112-L119)),
never the comment id, so a hard delete dangles no reference.

*Also settled: the spec's mechanism never worked.* Spec §5.9 and `grid_axes.md` Feature 24 ask for
orphans "displayed as children of [Deleted Comment]" *via* `ParentCommentId` SET NULL — but SET NULL
destroys the only record of which parent an orphan had, so two deleted threads' replies end up
indistinguishable from each other and from genuine top-level comments. Any future revival of the
placeholder needs a retained row, not the cascade. Recorded so the next reader doesn't re-propose
SET NULL as the way to get there.

*The accepted cost, stated plainly.* Deletion is invisible by design. That includes the case where a
story author deletes someone *else's* comment on their story
([ServerCommentWriteService.cs:442-455](TheCanalaveLibrary.Server/Comments/ServerCommentWriteService.cs#L442-L455)):
the replies are promoted to root with no sign the comment they answered was removed. This is the
known consequence, not an oversight. If author-side deletion ever needs accountability, the answer
is a **moderation-visible log**, not a reader-facing placeholder — the two solve different problems
and the log doesn't touch the read path.

*FK posture — the one thing D10 must not sweep up.* Self-ref `ParentCommentId` stays
`ON DELETE SET NULL`. It is load-bearing under this ruling, not vestigial: it *is* the reparent
mechanism. When the schema-hardening migration (schema §7) flips the content-parent FKs to RESTRICT
per D10, this FK is explicitly excluded.

*Doc sweep owed (Doc-Touch moment 1).* "[Deleted Comment]" is now a **retired term** and per
CLAUDE.md's retiring rule goes into `scripts/check-doc-hygiene.ps1`'s registry, with every process
doc grepped in the same WU. Known live sites: `grid_axes.md` Feature 24 (the rendering promise);
`audit/Identity.md`'s WU38a note; and the user-facing string at
[DeletePersonalData.razor:27-29](TheCanalaveLibrary.Server/Identity/Pages/Manage/DeletePersonalData.razor#L27-L29),
which tells a departing user their replies become "[Deleted Comment]" — wrong twice over, since
account deletion anonymizes comments (`UserId` NULL → "[deleted user]") and the placeholder will now
never exist at all. That razor string is the only code change this decision produces.

*Sequencing and ownership.* No build WU. The doc sweep and the one-line copy fix ride
**WU-TptHardDelete** (D10), which is already in these delete paths and already reasoning about
comment FK posture. Nothing here blocks anything.

*Left open, and no longer carried here.* One-level threading is a **UI-only** guarantee —
[CommentSection.razor:48](TheCanalaveLibrary.SharedUI/Comments/CommentSection.razor#L48) withholds
`OnReply` from replies, but `PostChapterCommentAsync` only checks the parent sits on the same
chapter, so a direct API call creates a depth-3 reply no read path renders — and under reparent,
deleting its depth-2 parent promotes that invisible reply to root, where it becomes visible. That is
an independent defect this decision neither creates nor fixes; it needs its own home in the service
audit's §2 defect list or the deferrals tracker rather than riding D12.

*Tests.* Already covered — `DeleteComment_ReparentsReplies_ToTopLevel`
([CommentWriteServiceTests.cs:232](TheCanalaveLibrary.Tests.Integration/CommentWriteServiceTests.cs#L232),
Integration tier) is now a ratified-behavior test rather than a description of an undecided default.
No new tests are owed.

### D13. User-deletion survival set ratification

*Source: schema §3.8.*
Ratify the survives-anonymized / destroyed-by-cascade / destroyed-by-service lists as the site's
de-facto GDPR/erasure answer (schema §3.8 enumerates them). Two unratified edges to rule on:
empty `conversations` rows accumulating (note or sweep), and groups left admin-less when every
admin deletes (app-layer, tracker-class).

**Answer (2026-08-06):** **Schema §3.8's three lists are ratified as the erasure policy, with one
correction: poll votes move from the destroyed list to the survives-anonymized list.** The two
unratified edges are ruled below. Deletion stays a **hard delete of the `User` row**
([UserDeletionService.cs:76](TheCanalaveLibrary.Server/Services/UserDeletionService.cs#L76)) — not a
soft-anonymize-in-place — and that is load-bearing, not incidental (see the GDPR note).

*Correction to the list: `poll_votes` survives anonymized.* The audit filed votes under "destroyed
(CASCADE)" as interaction data. They aren't, because **nothing denormalizes the tally** —
[ServerPollReadService.cs:96](TheCanalaveLibrary.Server/BlogPosts/ServerPollReadService.cs#L96)
computes `VoteCount = o.Votes.Count()` live, so a voter's deletion retroactively rewrites a closed
poll's published result. This follows D11 directly: D11 declined poll-death-with-owner because it
would "destroy *other* users' votes as collateral, which no reading of 'the author left' justifies."
The voter's own deletion destroys the same votes' *meaning* by the same logic — a result nobody can
reproduce.

*Why D12 is not the counter-precedent it looks like.* D12 has just ratified that deleting a comment
silently rewrites its thread, invisibly and with no trace. Read as a principle ("silent rewriting is
acceptable"), it decides this entry the other way — so the distinction is recorded rather than left
for the next reader to trip over. **D12 turned on cost, not on principle:** it called the
meaning-preservation benefit "real but bounded" and rejected it because the price was a permanent
third comment state, a render branch, and a reap rule on the site's hottest read path. Poll votes
have none of that. The price here is a nullable column and a surrogate key on an empty table, and the
render branch already exists (anonymized authors render "[deleted user]" everywhere). Same weighing,
opposite answer, because the cost side differs by two orders of magnitude.

*The line against "but favorites and ratings cascade, and they feed public aggregates too" is
current-state measure vs. point-in-time record.* A favorite count is a live measure of a story's
standing and *should* track the live user population. A poll is a dated artifact — "in June 2026, 37
people said X" — that the blog post's comment thread argues about. Rewriting it is amending a result,
not updating a metric. Interaction data whose only expression is a live aggregate keeps its CASCADE;
poll votes are the one member of that family that isn't.

*Mechanism (schema change).* `PollVote`'s PK is composite `(PollOptionId, UserId)`
([BlogPostConfigurations.cs:125](TheCanalaveLibrary.Server/Data/Configurations/BlogPostConfigurations.cs#L125)),
so `UserId` cannot go nullable in place. Add a surrogate `poll_vote_id` PK, make `user_id` nullable
with `ON DELETE SET NULL`, and add a unique index on `(poll_option_id, user_id)` — which in Postgres
does **not** constrain NULLs, so anonymized rows coexist freely while the live one-vote-per-user rule
is unaffected. The `PollOptionId` side keeps CASCADE (deleting an option deletes its votes).

*Read-path consequences.* The public voter list renders anonymized rows as "[deleted user]" rather
than omitting them, **so the list's length keeps matching the displayed count** — a voter list
shorter than its tally reads as a bug. `IsAnonymous` is unchanged and still governs `VoterChoice`
independently: a row that was already voter-anonymous stays hidden. Every "has the viewer voted /
retract my vote" query is `user_id = @me`, which never matches NULL — verify no read infers "not
voted" from a NULL comparison. Rejected alternative: keep CASCADE but denormalize a frozen counter
onto `poll_options` — the counter and the voter list would then disagree by construction, which is
worse than either pure answer.

*Edge (a) — conversations: the surviving participant keeps the thread.* Ratifies current behavior.
`PrivateMessage.SenderUserId` is already `int?` with SET NULL and only the leaver's
`ConversationParticipant` row cascades, so B keeps the full thread with A's messages attributed to
"[deleted user]". A message A sent is equally B's received mail; B participated, and the record is as
much hers as his. The alternatives are worse in both directions: deleting the conversation destroys
B's data because A left, and purging only A's messages leaves B's replies answering holes — a DM
thread is nothing but turn-taking, so a one-sided transcript is less honest than an attributed one,
not more private (B's own quoted replies would still carry what A said).

*Edge (a), mechanism — clean up orphans in the delete path, not with a sweeper.* Account deletion is
the **only** way a zero-participant conversation can arise, so clean up at the source: after
`SaveChangesAsync` and still inside the existing transaction in `UserDeletionService`, one
`ExecuteDeleteAsync` removing `conversations` rows with no remaining `conversation_participants`
(messages cascade with them). It must run *after* the save — the participant rows only vanish when
the user row is removed — so it is raw SQL, not a tracked delete. This retires the schema audit's
"linger as unreachable rows" acceptance; no worker, no schedule, no accumulating garbage.

*Edge (b) — admin-less groups: a moderator assigns a new admin on request.* Auto-promotion and
member self-claim are both **rejected** — each hands authority to someone unvetted, automatically:
auto-promote conscripts an arbitrary member who never asked, and first-come claiming is a race that
rewards whoever is watching. Ratifying permanent dormancy is also rejected, and the codebase is why:
**there is no group-deletion path at all** (no `DeleteGroupAsync`, and nothing on the moderation
surface), so a dormant group whose name or description turns inappropriate would have *no* remedy —
the "mods can always delete it" fallback does not exist. A moderator action is therefore the only
available remedy, and it puts human judgment exactly where it is needed. Until a request arrives the
group is unadministered, but that is a waiting state, not a ratified end state.

This is **not a new problem introduced by account deletion.** `layer2-services.md` §"Group Membership
and Role Model" already produces the same state via the leave path and defers it ("post-MVP: warn on
last-admin leave or auto-promote"). This ruling covers **both routes**; the consuming WU rewrites
that bullet and the companion "no way to transfer Admin status" line to point at the mod action
instead of at a deferral.

*Edge (b), mechanism.* One write method on `IModerationWriteService`, shaped exactly like
`ApplyAccountActionToUserAsync` — moderator-initiated, opens and resolves a `Report` row in the same
unit of work, `ReporterUserId == ModeratorUserId`, `ActiveReportCount` untouched — because Report
rows ARE the moderation ledger (§3.7 / D8). Three server-side guards:
- **The group must actually be admin-less** (zero `GroupMember` rows with `Role == Admin`). Without
  this guard the method is a mod override of a functioning group's leadership, a far larger power
  than the one being granted. Member-to-member transfer of a *living* admin role remains absent, and
  stays absent.
- **The target must already be a member** — promoting a non-member would have to invent a
  membership, and handing a group to someone who never joined is not a rescue.
- **Caller must be a moderator**; `UnauthorizedAccessException` otherwise, per the standard shape.

The promoted user is notified (they did not ask for this and must be able to find out). Two riders:
`ReportedEntityType` ([ModelEnums.cs:25-33](TheCanalaveLibrary.Core/Lookups/ModelEnums.cs#L25-L33))
gains `Group = 6` so the ledger row can name its subject — independently justified, since "report a
group for an inappropriate name" is the very situation that motivates this action — and the reason is
the seeded `Other` with mandatory notes, since no misconduct reason fits an administrative
assignment. Optional UI rider, recommended because it prevents the state rather than curing it: warn
on last-admin leave.

*GDPR posture, recorded because it is the question this policy answers.* Erasure (Art. 17) attaches
to **personal data**, not to content. A comment's text is personal data *because it is linked to its
author*; sever that link and — per Recital 26 — the record is anonymous data, outside the Regulation's
scope entirely. Anonymization-in-place is therefore not "retaining personal data under an exception,"
it is ceasing to process personal data at all; Art. 17(3)(a)'s freedom-of-expression carve-out would
independently cover a thread others participated in, but the policy does not need to lean on it.
Three things do bind: (i) the anonymization must be *genuine* — severing the FK does not help if the
content itself identifies the author, which is why authors must be able to delete their own content
**before** deleting the account, and why the deletion screen must say so; (ii) **no retained
mapping** — any `deleted_user → old_user_id` table kept for support purposes converts this from
anonymization to pseudonymization and collapses the whole analysis, which is why the hard delete of
the `User` row must not be softened later; (iii) the account-level fields (email, password hash,
external identity links, settings, any auth/IP logs) go unconditionally.

*Consequence — the copy is the compliance surface, not the schema.* D12 already owns the edit to
[DeletePersonalData.razor:27-29](TheCanalaveLibrary.Server/Identity/Pages/Manage/DeletePersonalData.razor#L27-L29)
and calls it the only code change that decision produces. D13 **expands what that edit must say**:
under Art. 13/14 transparency it is the one place where this whole ruling is legally visible, so the
page must enumerate the full survival set (stories, chapters, comments, blog posts, recommendations,
poll votes, sent messages — all anonymized) and point at the delete-your-content-first path, not
merely stop being wrong about comments. Same edit, same WU — larger scope than D12 alone implies.

*Sequencing and ownership.* Two halves, deliberately separate. **WU-DeletionSurvival** owns the
deletion path: the `poll_votes` PK/nullability/index change (rides the schema-hardening migration,
schema §7, with the D10/D11 flips — empty tables, free now), the poll read/write-path NULL tolerance,
and the orphan-conversation cleanup. It coordinates with WU-TptHardDelete on the
`DeletePersonalData` copy per the paragraph above. **WU-GroupAdminRescue** is its own small WU for
the moderator action — a Moderation-cluster feature addition, not a deletion-path change, and
WU-ModerationIntegrity is already loaded. It has no schema dependency beyond the enum member and can
land in either order.

*Tests (Integration tier).* Delete a user who voted in another user's poll → the `poll_votes` row
survives with NULL `user_id`, the option's `VoteCount` is unchanged, and the voter list keeps its
length; a "has voted" query no longer finds that user. Delete user A from a two-person conversation →
B's participant row, the `conversations` row, and every `private_messages` row survive, with A's
messages carrying NULL `sender_user_id`. Delete *both* participants → the conversation and its
messages are gone. Delete the sole admin of a group → the group survives with NULL `creator_id` and
zero admins; a moderator then promotes a member, whose `GroupMember.Role` becomes `Admin` with a
resolved `Report` row naming the group; promoting on a group that still has an admin, promoting a
non-member, and calling as a non-moderator each throw. *(RazorComponents tier.)* The poll voter list
renders an anonymized row as "[deleted user]" with the tally unchanged.

### D14. Blob cleanup ownership

*Source: service §3.16; defect §2.12 (avatar/cover blobs never deleted).*
Terminal deletion (user deletion, hard delete, abandoned cover uploads) leaves blobs behind:
inline deletes vs periodic orphan sweeper vs "orphans accepted" — currently nobody's job and
undocumented. Related ruling: keep the cover two-step upload protocol or move persistence into
the service like avatars.

**Answer (2026-08-07):** **Blob cleanup is owned in two places, deliberately: an inline delete at
every terminal deletion path, and a daily sweeper as the backstop for fault-class residue.
"Orphans accepted" is rejected in full — for any class.** The paired sub-ruling resolves the other
way from current code: **cover upload persists its own reference** (the avatar shape), and
`CoverArtRelativeUrl` leaves the story-update write path entirely.

*Why both owners, and why neither substitutes for the other.* The two leak classes have different
shapes. Terminal deletion needs **promptness**, because that is the entire content of the
requirement: erasure means the avatar photograph is gone when the account is gone, and the
illegal-content hard delete means the image is gone when the row is gone. "A sweep will collect it
within 24 hours" is a materially weaker answer to both, so the sweeper must never be the *mechanism*
of erasure. Fault-class residue is the opposite: a blob write that succeeds while its column write
fails leaves nothing to hook — there is no delete event, and there cannot be a transaction, because
the blob store sits outside the database. Inline deletes cannot reach that class at any level of
diligence. Two jobs, two owners.

*Why this does not contradict D13's "clean up in the delete path, not with a sweeper."* D13 rejected
a worker for orphan `conversations` rows on two facts that do not hold here: account deletion is the
**only** producer of a zero-participant conversation, and the cleanup is a set-based `DELETE` inside
the same transaction as the cause, so it is exhaustive by construction. Blobs have a **second
producer** with no delete event (a faulted or abandoned write), and no blob delete can join the
transaction that makes it correct. The D13 principle is intact — *clean up at the source wherever
the source is knowable and transactional* — and the sweeper exists only for the residue that
principle provably cannot reach.

*Sub-ruling: cover upload persists its own reference.* Three reasons, in ascending order of weight.
(i) The invariant "a stored blob is referenced by exactly one row" is a write-side data invariant,
and the two-step hands half of it to a Razor page — so every future caller (import, moderation tool,
admin backfill, a second editor surface) must reimplement the second half or leak. That is the same
structural defect that let `DeleteAsync` sit with zero callers for a month. (ii) Under the two-step
an orphan is produced by **success**, not failure: `UpdateStoryAsync` throws validation *after*
[UploadCoverArtAsync](TheCanalaveLibrary.Server/Stories/ServerStoryWriteService.cs#L165) has written
the blob, the page renders errors, and each failed save leaks another image on a path users hit
routinely. A sweeper is the right tool for orphans-by-fault and the wrong tool for
orphans-by-design. The countervailing argument — "the cover should commit with the form" —
describes an unreachable state: `_form.SelectedCoverFile` never leaves the browser until submit, and
the upload runs *inside* `HandleValidSubmit`
([StoryEditorPage.razor:239-248](TheCanalaveLibrary.SharedUI/Stories/StoryEditorPage.razor#L239-L248)),
so no picked-then-cancelled cover was ever uploaded. The two-step buys nothing in save/cancel
semantics; it only splits one submit into two independently-failing calls. The interface's stated
rationale ("so the WASM boundary never needs a client impl of `IImageStorageService`") does not
distinguish the shapes either — avatars satisfy it identically. (iii) **The decisive reason is the
inverse hazard, which is worse than any orphan.**
[StoryMappers.cs:92](TheCanalaveLibrary.Core/Stories/StoryMappers.cs#L92) assigns the cover
unconditionally from the DTO, and
[ServerStoryWriteService.cs:153-162](TheCanalaveLibrary.Server/Stories/ServerStoryWriteService.cs#L153-L162)
then best-effort-deletes the old blob on `oldCoverPath != dto.CoverArtRelativeUrl` — so an
`UpdateStoryAsync` whose DTO omits the cover **nulls the column and destroys the live blob**.
Carrying the cover in the general-purpose update DTO conflates three intents (*set*, *clear*, *don't
touch*), and over the wire
([StoryEndpoints.cs:178](TheCanalaveLibrary.Server/Stories/StoryEndpoints.cs#L178)) the third is
indistinguishable from the second. Latent only because one UI is the sole caller and faithfully
round-trips the field — correctness maintained by caller diligence, which is what service-owned
invariants exist to eliminate.

*Sub-ruling, mechanism.* `UploadCoverArtAsync` patches `StoryListing.CoverArtRelativeUrl` itself,
in the order **write blob → persist column → best-effort delete the old blob** (never delete first;
[UploadProfilePictureAsync](TheCanalaveLibrary.Server/Profiles/ServerUserSettingsService.cs#L237-L273)
is the reference implementation for both the shape and the ordering). `CoverArtRelativeUrl` comes off
`IEditableStoryProperties`, which removes it from both `StoryUpdateDTO` and `CreateStoryDTO` and
deletes the `StoryMappers.cs:92` assignment plus `UpdateStoryAsync`'s cover-change delete branch;
the read-side projections keep theirs. Clearing a cover becomes its own explicit operation rather
than a null in an update DTO, so the three intents become three distinct messages and none is
expressible by accident. **Accepted consequence:** cover upload is an immediate commit, so
form-cancel no longer reverts a cover — if that is wanted it is an explicit revert action to build,
not an emergent property to preserve.

*Mechanism — inline sites, and why they run post-commit.* A blob delete cannot be rolled back, so it
must never precede the commit it depends on; if the commit fails, no blob was touched, and if the
blob delete fails, the sweeper collects it. That ordering is what makes the two owners compose
instead of overlap.
- **`UserDeletionService`** — collect the avatar path and the cover paths of every story the user
  authored **before** `_context.Users.Remove(user)`
  ([UserDeletionService.cs:76](TheCanalaveLibrary.Server/Services/UserDeletionService.cs#L76)), since
  the cascade removes the rows that name them; delete the blobs after
  `transaction.CommitAsync()` returns, outside the execution-strategy retry (a retried delegate must
  not re-delete). Rides D13's transaction work in the same method.
- **`ResolveWithRemovalAsync`** — `ApplyHardDeleteAsync` must return the cover path alongside the
  author id so the caller can delete it in the existing post-commit best-effort block at
  [ServerModerationWriteService.cs:161-171](TheCanalaveLibrary.Server/Moderation/ServerModerationWriteService.cs#L161-L171),
  after the `SaveChangesAsync` on line 159 — same block, same swallow-and-log discipline as the
  notifications already there.
- **D15's author story deletion** — ruled buildable — is a third site and inherits this rule by
  citation rather than rediscovery.
- **Flagged, not designed here:** `/uploads/{**key}` is served with `max-age=31536000, immutable`
  ([ImageEndpoints.cs](TheCanalaveLibrary.Server/Images/ImageEndpoints.cs)), so deleting the object
  does not evict CDN copies. The illegal-content path therefore needs a cache-purge step once
  Cloudflare sits in front of R2 — a launch-readiness item (`roadmap.md` row 4 territory), not a
  code gap in this WU.

*Mechanism — the sweeper.* Worker/sweeper split per
[NotificationCleanupWorker.cs](TheCanalaveLibrary.Server/Notifications/NotificationCleanupWorker.cs)
(daily cadence, `TestAppFactory` removes the worker, integration tests call the sweeper directly).
This **amends the frozen `IImageStorageService`** — recorded as an amendment, not a violation — with a
streaming enumeration member (`IAsyncEnumerable<string>` of stored relative paths; `ListObjectsV2`
paging on S3, `Directory.EnumerateFiles` on Local) so a sweep never materializes the bucket. Three
constraints, the first non-negotiable:
- **Read the reference set unfiltered** — the anti-join must run against the write context or
  `IgnoreQueryFilters`. A global query filter (takedown, story status, confidentiality) hides
  referenced rows and makes live covers look orphaned. This is the one failure mode that destroys
  real user data, and it is the regression test that matters most.
- **Grace window** — never delete a blob whose store-reported last-modified is under 24h, so the
  sweep cannot race an in-flight upload whose column write has not landed.
- **Log-only first cycle plus a per-cycle delete cap** — bounds the blast radius of a wrong
  predicate before it is trusted.
Direction is fixed: anti-join **the store against the database**, never the reverse. A row pointing
at a missing blob is a different and non-destructive defect — log it, never act on it.

*What this closes, and one doc correction it forces.* Closes service §2.12's blob clause and §3.16
in full. Tracker **B14** (no derivative sizing) is adjacent and untouched. `audit/ImageStorage.md`'s
**"Orphan bug (fixed 2026-06-27)"** header must be rewritten by the consuming WU: it describes the
*replacement* case only, and as written it reads as if orphans were a solved problem — which is a
meaningful part of why this went unowned for six weeks.

*Sequencing and ownership.* Two halves. **WU-BlobCleanup** owns the cleanup rule end to end — the
enumeration member, the sweeper, **both** inline sites, and the doc corrections. The inline sites
stay in one WU rather than riding WU-DeletionSurvival and WU-TptHardDelete separately, because
"delete the blob at every terminal path" is a cross-cutting invariant and splitting enforcement
across WUs is exactly how the third site gets missed; it coordinates edit ordering with those two
where the same methods are open. **WU-CoverPersistence** owns the sub-ruling — a Stories-cluster
change (F4 L2 plus the editor's L3), independently landable in either order.

*Tests (Integration tier).* Delete a user with an avatar and two covered stories → all three blobs
are gone from the store and a subsequent sweep finds nothing to do. Hard-delete a covered story →
the blob is gone and a GET of its URL 404s (real Garage via `GarageFixture`, and the filesystem impl).
Inject a throwing `IImageStorageService` → the user is still deleted and a warning is logged, and the
sweeper then collects the blob. Sweeper: a referenced blob survives **including one whose story is
taken down or non-public** (the query-filter trap); an unreferenced blob past the grace window is
deleted; one inside it is kept; a row naming a missing blob is logged and not acted on. Cover
persistence: `UploadCoverArtAsync` alone, with no follow-up update, leaves the story referencing the
new blob with the old one deleted; a later `UpdateStoryAsync` that says nothing about the cover
leaves it intact — the regression test for the inverse hazard. *(RazorComponents tier.)* The editor
renders the newly uploaded cover without a save.

### D15. Author story deletion (archive permanence)

*Source: service §3.20.*
Story deletion doesn't exist for authors. If archive permanence is the position, say so
explicitly.

**Answer (2026-08-07):** **Archive permanence is rejected. Build author-facing hard delete: an
author has absolute control over their own stories** — self-service, no reason required, no
moderator in the loop, irreversible. Everything below follows from that.

*The position, stated for the docs.* Deletion is a true delete, not a withdrawal state: no grace
window, no tombstone, no "[deleted story]" placeholder page, no mod approval, no cooldown. The
standing objection to author deletion — that the record is lost — is **answered outside TCL**:
third-party archives, the Wayback Machine, and crawlers will inevitably hold copies, and anyone who
wants the fossil of a story its author withdrew can reach for it through those channels. TCL does
not undertake to be that fossil record against the author's wishes. This also settles the softer
alternatives that were on the table (permanence-plus-unpublish, a self-service `IsWithdrawn` soft
state): unpublish (D1's `published→Draft`) remains available as the *reversible* option and is
offered in the confirm dialog, but it is not the ceiling on what an author may do.

*Why D11/D13's collateral principle does not transfer, so those rulings stand unamended.* D11
refused poll-death-with-owner and D13 kept a departing user's poll votes and DM thread, both on the
same test: **does the record retain meaning once the actor is gone?** A poll result is a dated
artifact the thread argues about; a DM is equally the other participant's mail. Nothing attached to
a story passes that test — a comment, a reading-progress row, a favorite, a recommendation are all
*about* the story and do not outlive their subject. The ownership claim is also different in kind: a
poll vote is one user's act on **someone else's** post, whereas a story is the author's own work.
"Absolute control over what I wrote" is not a claim over other people's records, so the two rulings
do not collide.

*The two accepted casualties, named rather than discovered later.* **(i) Recommendations** are other
users' authored prose and cascade with the story
([StoryConfigurations.cs:52-53](TheCanalaveLibrary.Server/Data/Configurations/StoryConfigurations.cs#L52-L53)).
Accepted: a recommendation of a story nobody can open is not a readable artifact, and preserving it
would require the tombstone this ruling rejects. **(ii) `StoryLineage`** rows naming the story as
source cascade too — a derived work keeps existing but loses its attribution link upward. Accepted
on the same ground; the surviving child story is untouched.

*Scope of "own" — primary author only.* `AuthorId` gates the delete, matching every existing story
write gate ([ServerStoryWriteService.cs:99](TheCanalaveLibrary.Server/Stories/ServerStoryWriteService.cs#L99),
[ServerChapterWriteService.cs:301-302](TheCanalaveLibrary.Server/Chapters/ServerChapterWriteService.cs#L301-L302)).
`CoAuthor` is credit and access, not ownership — it carries no role or permission column
([CoAuthor.cs](TheCanalaveLibrary.Core/Collaboration/CoAuthor.cs)) — and `BetaReader` less so. A
co-author therefore cannot delete, and the primary author can delete work a co-author contributed
to. That edge is real and is **not** a deferral: joint deletion rights presuppose a joint-ownership
model that does not exist, and inventing one inside a delete button is the wrong place to design it.

*Deletion is unconditional, including under moderation.* An open report, a pending-approval status,
or an active `IsTakenDown` does not block it, and no "you can't delete evidence" gate is added. The
moderation record survives without the content: `Report` has no FK to `stories`, D8 added the
reported-**user** column, and sanctions are account-level — so an author who deletes a reported story
stays exactly as attributable and as sanctionable as before. Adding a gate would buy nothing and
would contradict the position on its most load-bearing case (the author who wants the work gone
*because* it drew attention).

*Mechanism.* `DeleteStoryAsync(int storyId)` on `IStoryWriteService`, client impl, `DELETE`
endpoint; ownership check then execution-strategy + transaction, per `DeleteChapterAsync`'s shape.
Four things the WU must get right:
- **Use D10's shared base-row helper once, scoped to the whole story** — one statement over
  `base_comments` for the comment ids of *all* the story's chapters. Explicitly **not** a loop
  calling `DeleteChapterAsync` per chapter: that path also renumbers survivors and refreshes the
  word count ([ServerChapterWriteService.cs:328-358](TheCanalaveLibrary.Server/Chapters/ServerChapterWriteService.cs#L328-L358)),
  which is pure waste when the parent is going away, and it is O(chapters) transactions.
- **Everything else cascades at the DB** — fifteen `Cascade` FKs off `stories` plus
  `ProfileBlogPost.StoryId → SetNull`
  ([StoryConfigurations.cs:17-100](TheCanalaveLibrary.Server/Data/Configurations/StoryConfigurations.cs#L17-L100)),
  including listing/detail, chapters, tags, arcs, USI, series entries, custom-list entries, group
  stories, external links. No hand-enumeration in service code.
- **`daily_story_stats` must be deleted explicitly** — it is the migration-managed raw table with PK
  `(story_id, stat_date)` and **no FK to `stories`**
  ([InitialSchema.cs:3243-3247](TheCanalaveLibrary.Server/Migrations/20260719023703_InitialSchema.cs#L3243-L3247)),
  so its rows survive the cascade. `UserStatRecalculator`'s `ViewsOnStories` joins `stories` and
  self-corrects, but `SiteDailyStatAggregator`'s site-wide view total sums the table with **no join**
  ([SiteDailyStatAggregator.cs:78](TheCanalaveLibrary.Server/Moderation/SiteDailyStatAggregator.cs#L78))
  and would keep counting a deleted story's views forever. One raw `DELETE` inside the transaction.
- **Cover blob** — this is D14's third inline site, inherited by citation: collect the path before
  the row delete, delete the object post-commit, best-effort, sweeper as backstop.

*Counters.* `ApprovedStorySubmissions` is **monotonic per D1** — deleting an approved story never
decrements it and never revokes `CanAutoApprove`; a decrementable counter is farmable and that
reasoning is unaffected by who does the deleting. Every other affected number is a recomputed
aggregate and self-corrects, including third parties' `RecommendationsWritten`.

*Sub-ruling — the spotlight slot is returned, not consumed.* `CommunitySpotlight` cascades off the
story ([StoryConfigurations.cs:57-58](TheCanalaveLibrary.Server/Data/Configurations/StoryConfigurations.cs#L57-L58)),
but `SpotlightSlot` is `Restrict` and carries its own `Status` — so a sponsor who redeemed a slot on
someone else's story would silently lose the entitlement to an author's unilateral act. **If the
booked block has not started, the slot returns to redeemable; if it has already aired, it is
consumed.** This is the one item in the cascade that is a third-party *entitlement* rather than a
record, and entitlements are exactly what the "records don't outlive their subject" argument does not
cover. The delete path therefore calls into the spotlight write service rather than relying on the
cascade alone.

*Accepted dangles.* Notifications name the story through `RelatedEntityId` with no FK, and
`DeleteChapterAsync` already sets the precedent of tolerating that rather than sweeping. Ratified:
the presenter must render a dangling reference as an inert row and never throw — verify, don't
assume. Resolved `Report` rows pointing at the vanished story are the same class and are wanted (see
the moderation paragraph).

*UX.* A destructive control on the story edit surface, typed-title confirmation, copy that states
irreversibility and that comments, recommendations, and readers' progress go with it. The dialog
offers two outs first: **unpublish** (D1's `published→Draft`, fully reversible, already invisible via
the read filter at [ReadOnlyApplicationDbContext.cs:54-56](TheCanalaveLibrary.Server/Data/ReadOnlyApplicationDbContext.cs#L54-L56))
and an **export** link (F54) so the author leaves with their own copy — the same ownership position
this decision rests on.

*Sequencing.* Strictly **after WU-TptHardDelete** (D10), whose shared helper it consumes, and it
inherits D14's inline-blob rule by citation. Lands in **WU-StoryLifecycle** (D1's WU — same service,
same author-gate work) unless that ships first, in which case it is its own small WU. Promulgation on
build, per this worksheet's preamble: `layer2-services.md` (the delete-path rule + the
`daily_story_stats` no-FK trap), `audit/Stories.md` Features 4–5, `layer1-data-model.md` beside the
TPT traps, and the ToS/help copy stating the author-control position.

*Tests (Integration tier).* Delete a story with chapters, comments on those chapters, and replies →
**zero surviving `base_comments`** for those ids (the D10 regression, at story scope); a foreign
user's USI, favorite, custom-list entry, series entry, group-story row, and recommendation are all
gone; `daily_story_stats` rows for the story are gone **and** the site aggregator's view total no
longer counts them (the one test that catches a cascade-only implementation); a `ProfileBlogPost`
that referenced the story survives with NULL `StoryId`; a non-owner and a co-author each throw
`UnauthorizedAccessException`, anonymous throws; deleting a story with an open report and with
`IsTakenDown = true` both succeed and leave the `Report` row intact; `ApprovedStorySubmissions` is
unchanged after deleting an approved story; a future-dated spotlight placement returns its slot to
redeemable while an aired one does not. *(RazorComponents tier.)* The confirm dialog requires the
typed title before enabling the destructive action, and offers unpublish and export.

## Block D — Notification design remainder

Completes WU-InertFeatures' design inputs (new-chapter fan-out) and the notification-correctness
batch.

### D16. Group fan-out RelatedEntityId

*Source: service §3.6; defects §2.8.*
`RelatedEntityId` = groupId (current: can never name the story; distinct stories dedup-collapse
while unread) vs storyId. Ratify the digest behavior or switch; fix the author-double-notify
(types 60 + 25 for one event) either way.

**Answer (2026-08-07):** **Neither — `RelatedEntityId` = `groupStoryId`, the junction row's own id.**
Both types 60 and 25 carry it; a new `RelatedEntityKind.GroupStory` resolves it by joining
`GroupStories → Groups` and `→ Stories`, yielding the group name and the story title from one id.
The fork as posed was false: it assumed the row can name only one of the two entities, which is true
only if it points at a leaf instead of at the pairing.

*The general rule this establishes* (the reason the answer is not "storyId"). `RelatedEntityId` names
the **single most specific entity of the event** — the node from which every other entity the display
needs is reachable by FK join. If an event appears to need two entities, the pairing *is* an entity:
point at the junction row, and give it a surrogate PK if it lacks one. Genuinely polymorphic targets
anchor on a TPT root (`BaseComments`, `BlogPosts` — the `BlogPostDirect` precedent from WU-B2), never
on a discriminator column. **A second id column is never added to `notifications`.** A type that
cannot name one anchor is a type that has not been designed yet.

*Why the rule, and why it stops at one rather than at two.* A notification row is already the triple
(`RecipientUserId`, `SourceUserId`, `RelatedEntityId`) — subject, actor, object. A "second entity"
would have to be a second *object of the same verb*, and no event in `NotificationTypeEnum` has one:
where two objects appear, either one owns the other (chapter ⊂ story, recommendation ⊂ story,
comment ⊂ chapter) or a junction owns both. So the count needed is not how many things the event
mentions but **how many disjoint FK roots it has**, which is 1 everywhere. Nothing structural would
have stopped a widened column at two rather than three — the stop has to be a stated rule, because
the cost curve is silent: each extra id column means another `KindFor` arm per type, a second
`BatchLoadEntitiesAsync` pass, a ruling on whether it joins the dedup key, a NULL-semantics answer
per type, and a presenter phrasing per *combination* of resolved targets (2 ids → 4 branches per arm,
3 → 8). `NotificationEnricher`'s own docstring already forbids forking its switch; a second column
is that fork.

*What the ruling buys concretely.* Distinct stories stop dedup-collapsing (the key becomes
`(recipient, type, source, groupStoryId)` — unique per group×story), while re-adding the *same* story
still collapses, which is the digest behavior actually worth keeping. Presenter text can name both
objects and deep-link either. Deleting the `GroupStory` row (`RemoveStoryAsync`) leaves the
notification title-less and non-navigating — already the enricher's designed graceful path, same as a
taken-down blog post, and acceptable.

*Rider fixes, same WU — the producer is one method and should be corrected once, not in three passes:*
exclude `storyAuthorId` from `memberIds` (the author-double-notify, types 60 + 25 for one event);
move the notification block **inside** `if (!alreadyAdded)` (today it sits outside — any member can
re-add an already-added story and re-fire the whole fan-out); and unwrap the member fan-out from
`if (storyAuthorId.HasValue)` (an authorless story currently silences the member notification too).

*Forcing case routed, not assumed.* The only event in the enum with two genuinely disjoint roots is
story lineage (types 50/51): `StoryLineage` is keyed on the composite `(SourceStoryId, TargetStoryId)`
with no surrogate PK, so no single int addresses the pair, and the recipient currently cannot learn
which of their own stories is being cited. Under the rule the fix is a surrogate PK on that junction
(one migration, free pre-launch) — **not** a notification-table column. This is a schema item; it
belongs with the lineage WU / the schema audit's decision batch, and is recorded here as an
implication of the rule, not as work already decided.

*Re-point backlog, enumerated here so it is one sweep rather than point fixes* (audit-before-
cross-cutting): several types point at the wrong node today and cost display fidelity —
`HiddenGem` (23) stores the recommender's user id, a duplicate of `SourceUserId`, so the slot is
wasted where `recommendationId` would name the story; the recommendation family (22/27/40/41/42/43)
and `RecommendationSpotlighted` (92) store `storyId` where `recommendationId` joins up to the story
anyway and adds a rec anchor; the comment types (24/31/33) store the *context* entity where
`commentId` gives context plus an in-page anchor; `CommentReply` (34) has no target at all and is the
`BaseComments` TPT-root case; `ExternalLinkVerified`/`Rejected` (78/79) store `storyId` where the link
row is the real object; `PollUpdated` (100) stores the owning blog post and leaves site polls at `0`
where `pollId` resolves both. `ContentRemoved` (70) is the one true polymorph with no TPT root —
it needs kind+id and interacts with D5. None of these are urgent; they are recorded so the rule
arrives with its own conformance list instead of being asserted over code that quietly violates it.

*Precedent set for the not-yet-built fan-outs.* The new-chapter fan-out (WU-InertFeatures, gated by
D17) already stubs `NewChapterOnFollowedStory` as `Chapter` — conformant, since chapter joins up to
story. Whatever else that producer mints follows the rule above rather than re-deciding it.

*Promulgation (Doc-Touch moment 1, owed by the consuming WU, not done here):* the rule goes into
`canalave-conventions/layer2-services.md` §"Polymorphic RelatedEntityId — Two-Pass Batch Enrichment";
`audit/Notifications.md` records the 60/25 semantics change; `roadmap.md`'s decision row moves to
Resolved pointing at the convention doc.

### D17. Hidden-favorite fan-out membership

*Source: service §3.20.*
Doctrine says include hidden-only favoriters in the type-15 fan-out; code excludes them. Pick
one — this feeds the new-chapter fan-out build (§2.3.1: recipients = IsFollowed interactions),
so it should be settled before that producer is written.

**Answer (2026-08-07):** **Include them. The doctrine is right and the code is the drift** —
`NewBlogPostOnFavoritedStory` (15) recipients become `IsFavorite || IsHiddenFavorite`
([ServerNotificationWriteService.cs:343](TheCanalaveLibrary.Server/Notifications/ServerNotificationWriteService.cs#L343)).
No schema change, no migration, no doc reversal — [layer2-services.md:787-788](../skills/canalave-conventions/layer2-services.md#L787-L788)
and [INotificationWriteService.cs:313-316](TheCanalaveLibrary.Core/Notifications/INotificationWriteService.cs#L313-L316)
already state the outcome; they gain the *principle* below so the next fan-out inherits it.

*The principle, stated generally because D17 is one instance of it.* **A hidden favorite suppresses
public-plane consequences only; it never suppresses personal-plane ones.** Spec §5.7 enumerates what
the flag withholds — public profile display, the public favorite count, tree-search/Also-Favorited
edges absent `AllowDiscoveryFromHiddenFavorites` — and every item on that list is something *other
people* see. A type-15 notification is delivered to the favoriter and to no one else; the author
never sees a recipient list, and the type's `DefaultEmailEnabled = false` means not even an
outbound message is implied. Withholding it leaks nothing and buys nothing — it just gives the
privacy-conscious user a worse product as the price of choosing privacy.

*Mirror ruling, decided here to stop it being re-opened.* `NewStoryFavorite` (20 — "Someone
favorited one of your stories") is **author-plane**, so a hidden favorite must **not** fire it. That
producer does not exist yet (the type is seeded, enriched, and presentable; nothing in
`ServerNotificationWriteService` creates one — only `SeedGraph`), so the rule is recorded before the
producer rather than discovered after. Same principle, opposite outcome; the plane, not the flag,
decides.

*The standalone-vs-modifier fork the audit didn't name — both readings are legitimate and neither is
a defect.* `IsFavorite` and `IsHiddenFavorite` are zero-coupled ([UserStoryInteraction.cs:29-33](TheCanalaveLibrary.Core/UserStoryInteractions/UserStoryInteraction.cs#L29-L33) —
all 8 combinations legal, `ValidateCombination` deliberately empty), and the UI's two toggles set
their own bit only, so three distinct favoriter states exist:

| State | Meaning | Where it shows |
|---|---|---|
| `IsFavorite` only | public favorite | profile Favorites tab, everyone |
| both true | favorite hidden from visitors | profile Favorites tab, owner's own view only |
| `IsHiddenFavorite` only | private favorite | Bookshelf "Private Favorites" tab only, never the profile |

All three are favoriters for fan-out purposes. The profile query's narrower
`IsFavorite && (includePrivate || !IsHiddenFavorite)`
([ServerUserStoryInteractionReadService.cs:97-100](TheCanalaveLibrary.Server/UserStoryInteractions/ServerUserStoryInteractionReadService.cs#L97-L100))
is a **public-plane display filter**, not the definition of favoriting, and correctly stays as it is —
it coexists with the Bookshelf tab's standalone `IsHiddenFavorite` predicate (:72, matching spec
§5.15) because the two surfaces sit on different planes. The WU changes neither read path.

*Consequence for the unbuilt new-chapter fan-out (§2.3.1).* Its recipient set stays `IsFollowed` as
specified — there is no private-follow flag, so hidden favorite does not enter. What the ruling
supplies is the answer if that build widens membership to favoriters: it would take
`IsFavorite || IsHiddenFavorite`, never the bare `IsFavorite`. D6 still gates the producer
(mature-off recipients).

*Doc home (Doc-Touch moment 1, before code).* `layer2-services.md` §Notification Generation — the
existing "Hidden-favorite users are included in 15" bullet is promoted from an assertion about one
type to the public-plane/personal-plane rule, with the type-20 mirror stated in the same breath.
`INotificationWriteService`'s XML doc keeps its clause and gains the type-20 sentence when that
producer lands.

*Sequencing.* Rides **WU-InertFeatures** with D16 (one notification WU, per Block D's framing).
Independent of every schema decision — nothing here touches DDL.

*Tests (Integration tier).* Publish a story-linked profile blog post with three favoriters seeded one
per state above → all three receive exactly one type-15 row. A user who is both an author-follower
and a hidden-only favoriter receives only type 13 (precedence 13 > 14 > 15 > 16 is unaffected by the
widened predicate). A hidden-only favoriter who is also on Read Later receives 15, not 16.

### D18. Private-message email

*Source: service §3.18.*
No notification type exists for PMs, so they can never produce email — and the audit notes the
unread-PM nudge is the single highest-value email for a small site. Ratify the absence or
reserve the enum value + semantics now.

**Answer (2026-08-07):** **Ratify the absence in the notification catalogue — PMs never produce
`Notification` rows and never take a `NotificationTypeEnum` value. Messaging owns private-message
email end-to-end; the only thing shared with Notifications is the mail transport.** The email itself
gets built (the audit is right that it is the highest-value email on the site); what is rejected is
routing it through the notification cluster.

*The pipeline is welded to the type, and the type means two things.* Email eligibility inner-joins
`NotificationTypes` (`NotificationEmailFlusher.cs:92`) for `DefaultEmailEnabled`, the subject line,
and the category; the preference is the sparse `UserNotificationSetting` override at `:105`; the RFC
8058 `List-Unsubscribe` pair at `:164-171` carries a token whose payload is literally
`"{userId}:{(int)notificationType}"` (`UnsubscribeTokenService.cs:54`). So "reserve the enum value as
a preference-and-unsubscribe key without ever producing rows" is a real option, and it was the
leading candidate before the coupling was examined directly. It is rejected because
`NotificationType` (`Core/Notifications/NotificationType.cs`) is already doing two jobs that are
coextensive only by accident: it is the **FK discriminator for `Notification` rows** *and* the
**catalogue of things a user can be told about and can mute**. A PM entry would be the first row
where those come apart, and it would make three currently-true statements false — the
`Notifications` navigation collection permanently empty for a row nothing enforces as empty,
`DefaultCollapsed` meaningless (Collapsed is a panel-display concern and there is no panel row), and
the enricher/presenter/cleanup sweep over the catalogue no longer safe to reason from. A tacit
invariant weakened by one special case, with the exception living only in prose.

*The governing distinction: sharing a transport is not coupling two features; sharing an identity
is.* Depending on `IMailTransport` and the buffer/worker is two domains depending on a shared
lower-level service — no cycle, no shared vocabulary, no invariant either can falsify. Putting a
Messaging row in the Notifications catalogue is Messaging depending on another domain's identity, and
it requires weakening that domain's meaning to fit. Record this as the general rule for any future
"feature X wants email" question, not just this one.

*What a `Notification` row means, restated so the boundary is checkable.* An event you may not have
seen, surfaced in a feed, pruned by `NotificationCleanupWorker` at 60 days. A PM is durable
correspondence with its own inbox, its own read model, and no expiry. Different things that want the
same pipe. This ruling **strengthens** `cross-cutting.md:32` ("two unread systems by design — do not
unify") and leaves `audit/Messaging.md:70` ("the Notification cluster is never touched") true; that
line gains the reason it previously asserted without one.

*Ownership split.*
- **Messaging owns:** the preference (`EmailOnNewMessage`, a column beside `AllowPrivateMessages` —
  where a user looks for it); unread-run suppression (`ConversationParticipant.LastNotifiedTimestamp`,
  nullable); body composition; its own unsubscribe token (a second Data Protection purpose,
  `TheCanalaveLibrary.MessagingUnsubscribe.v1`, payload = userId, one action, never enables).
- **Shared infrastructure:** `IMailTransport`, and the buffer/worker generalized from
  `ConcurrentQueue<long>` to a `(kind, id)` payload. The buffer's ids-only rationale carries over
  unchanged and is in fact load-bearing here — eligibility must be resolved against live rows at
  drain time, because "is it still unread?" has to be checked late.
- **Shared convention, not shared code:** the unsubscribe pattern (signed token, exactly one action,
  can only ever disable) and the RFC 8058 header pair. One statement in `security.md`; both paths
  conform.

*Suppression rule — one email per unread run, never a re-nag.* Mail when a message from the other
party is newer than the recipient's `LastReadTimestamp` **and** newer than `LastNotifiedTimestamp`,
then stamp `LastNotifiedTimestamp`. A 20-message burst while the recipient is away produces one
email; read-then-new-message produces a second, correctly; a standing unread state is never re-poked.
Gating on the watermark rather than on a proxy row's `IsRead` is what makes the read-state desync
structurally impossible rather than patched.

*Why not the notification-row shape (the option this replaces).* `MarkConversationReadAsync`
(`ServerMessagingWriteService.cs:154`) stamps `LastReadTimestamp` and nothing else, so a
notification-backed PM email would mail the recipient about a message they had already read, and the
bell and messages badges would disagree permanently. Fixable only by syncing two read states across a
domain boundary — the exact unification the doctrine forbids — plus a `RelatedEntityKind.Conversation`
arm, a presenter arm, and a new Messaging→Notifications DAG edge. Note for anyone re-opening this: the
create-core's `(type, source, related, unread)` dedup (`ServerNotificationWriteService.cs:425-438`)
*would* have given correct anti-flood behaviour for free with `source = sender`,
`related = conversationId`. That mechanism was not the problem; the identity coupling was.

*Why not a nudge sweeper.* Rejected on product grounds: a recurring reminder about standing unread
state is a workplace-tool mechanism, wrong for a community-entertainment site. It is also degenerate
under D4 — a system-sourced nudge carries `related: 0`, so dedup collapses it to one unread row per
user until read, meaning it either fires once (a worse notification) or needs a re-fire timer (the
mechanism being rejected). The deferred send in the suppression rule above is not this: it fires once
per unread run and is then silent forever.

*The one user-visible cost, resolved at the UI layer.* Separation splits "email preferences" across
two domains, and users will look in one place. The notification settings **page** renders a second
section for messaging email, sourced from Messaging's own service — a page is a composition surface,
not a domain artifact, and may read two sources. Standing rider: every outbound non-transactional
email must appear on that page. Enforced by review, deliberately not by a shared registry — a shared
registry is precisely the coupling this ruling rejects.

*Open rider for the build WU (not settled here).* Drain interval. The shared worker runs a 30 s
`PeriodicTimer`; inheriting it means a recipient who reads within 30 s is never mailed, which is
already a decent "don't bother someone who is here" property. A messaging-specific timer of a few
minutes would strengthen it at the cost of a second knob. Decide when the composer is written.

*What a WU needs to cover* (Feature 33; audit file `Messaging.md`, plus `Notifications.md` for the
buffer generalization): the two schema columns; generalize `NotificationEmailBuffer` to `(kind, id)`
and update `NotificationEmailWorker`/`NotificationEmailFlusher` call sites and tests; a messaging
composer + drain-time watermark query; the messaging unsubscribe purpose, endpoint, and its
one-action write; the settings-page section; Integration coverage for the suppression rule
(one-per-unread-run, re-fire after read, no mail when the preference is off or `EmailConfirmed` is
false). Schema footprint on the hardening migration is **two columns** — no enum member, no category,
no seed row, no `RelatedEntityKind` arm, no presenter arm, no new DAG edge.

### D19. UserNotificationSetting granularity

*Source: service §3.17; drift §4.7.*
Doctrine says per-field NULL sparse; the columns are non-nullable (an override of `Collapsed`
freezes `EmailEnabled` against future default changes). Make both columns nullable now (free) or
fix the doctrine sentence.

**Answer (2026-08-07):** **Make both columns nullable and collapse each field to NULL
independently. Doctrine is right; the schema is the drift.** `UserNotificationSetting.EmailEnabled`
and `.Collapsed` become `bool?`
([UserNotificationSetting.cs:9-10](TheCanalaveLibrary.Core/Notifications/UserNotificationSetting.cs#L9-L10));
effective value = the override when non-NULL, else the type default, per field. The row is deleted
only when **both** fields are NULL. The wire contract is unchanged — `SetSettingAsync(type,
emailEnabled, collapsed)` keeps taking two effective `bool`s and the collapse-to-NULL happens
server-side — so there is **no UI, endpoint, or client change**. Explicitly *not* taken: a
three-state "default / on / off" control that would let a user pin a value against future default
changes (see the accepted cost below).

*What the ruling protects, stated as the principle.* The sparse-override model exists for exactly
one reason, already written into
[NotificationSettingUpsert](TheCanalaveLibrary.Server/Notifications/NotificationSettingUpsert.cs#L9-L14):
"no row" is the canonical representation of "default," so a later change to a type's
`DefaultEmailEnabled` propagates to every user who never expressed a preference. **Row-level
sparseness delivers that guarantee only for users who expressed *no* preference on the type — which
is not the population it was designed for.** The settings page submits both effective values on
every toggle
([NotificationSettingsPage.razor:76,85](TheCanalaveLibrary.SharedUI/Notifications/NotificationSettingsPage.razor#L76-L85)),
so collapsing a category's display writes an `EmailEnabled` the user never chose, and that value is
pinned from then on. Flip `NewChapterOnFollowedStory` to `DefaultEmailEnabled = false` after seeing
real send volume and every such user keeps receiving mail forever, indistinguishably from someone
who opted in. The general form, which is what goes in the doc: **a sparse-override row must be
sparse at the granularity at which the user actually expresses preferences.** Two independently
settable fields sharing one row's existence is a category error.
`IDiscoveryFilterSettingsService`'s mirror-of-this-contract
([IDiscoveryFilterSettingsService.cs:33-36](TheCanalaveLibrary.Core/Discovery/IDiscoveryFilterSettingsService.cs#L33-L36))
is **correct as-is and stays row-level** — its rows carry one settable field, so row-level *is*
per-field there. That is the distinction, not a second exception.

*Why now rather than as a doc fix.* Pre-data, `bool → bool?` is a nullability relaxation on a table
holding only dev rows. Post-launch it is not a migration at all: deciding, per existing row, whether
a stored value was a real choice or an artifact of the write path requires information that exists
nowhere. The freeze is also not a hypothetical — ~22 seeded types default `EmailEnabled = true` and
several fan out to every follower of a story or author, so retuning email volume once traffic is
real is close to certain, and partial overrides (someone collapsing a panel section without ever
thinking about email) are the *typical* interaction, not an edge case.

*Accepted cost, recorded so it is not re-litigated as a defect.* Under this rule "explicitly chose
the value that happens to be today's default" is stored as NULL — identical to "never chose." A user
who deliberately wants email on for a type whose default later flips to off loses it silently. That
is the price of not building the three-state control, and it is the right trade at this scale: the
pinning affordance costs UI, vocabulary, and test surface to serve a case that a `SiteAnnouncement`
about the default change covers adequately. Note the DTO's existing claim that the two states are
"behaviourally identical"
([NotificationSettingDto.cs:12-14](TheCanalaveLibrary.Core/Notifications/NotificationSettingDto.cs#L12-L14))
is *made true* by this ruling rather than contradicted by it.

*Mechanism, per site.* **Write** —
[ApplyAsync](TheCanalaveLibrary.Server/Notifications/NotificationSettingUpsert.cs#L27-L72) computes
`storedEmail = emailEnabled == type.DefaultEmailEnabled ? null : emailEnabled` and the same for
`Collapsed`; both NULL → `ExecuteDeleteAsync` (unchanged path), otherwise upsert. **Unsubscribe** —
[UnsubscribeAsync](TheCanalaveLibrary.Server/Notifications/NotificationSettingUpsert.cs#L87-L106)
*simplifies*: it no longer reads the effective `Collapsed` to avoid clobbering it, because it now
writes only its own column. Its careful "reading `Collapsed` first matters" paragraph becomes a
load-bearing false comment the moment the columns change and must be rewritten in the same edit —
the §4 class. **Reads** — three coalescing sites, all keeping the file's existing explicit
`s != null ? … : …` style rather than relying on `??` translation:
[GetSettingsAsync:189-190](TheCanalaveLibrary.Server/Notifications/ServerNotificationReadService.cs#L189-L190),
`GetNotificationsAsync`'s Collapsed LEFT JOIN, and the flusher's eligibility predicate
([NotificationEmailFlusher.cs:105](TheCanalaveLibrary.Server/Notifications/NotificationEmailFlusher.cs#L105)).
**DTO** — `IsDefault` is replaced by per-field `EmailIsDefault` / `CollapsedIsDefault`; row-level
"no override row exists" stops being a meaningful answer once a row can be half-NULL. Cheap: the
only consumers are three assertions in `NotificationServiceTests`, and the UI gains a free
affordance ("following the site default") it may or may not use.

*Doc home (Doc-Touch moment 1, before code).* [layer2-services.md:705-711](../skills/canalave-conventions/layer2-services.md#L705-L711)
§Filtering semantics — the "NULL for either field means use the type's default" sentence is
**retained**, not rewritten, and gains the mechanism (row deleted when both NULL) and the general
principle above, with the single-field `IDiscoveryFilterSettingsService` contrast named so the next
sparse table gets it right. Also updated in the same WU: `NotificationSettingUpsert`'s class summary
and `UnsubscribeAsync`'s doc paragraph, `NotificationSettingDto`'s `IsDefault` paragraph,
[ServerNotificationReadService.cs:22-23](TheCanalaveLibrary.Server/Notifications/ServerNotificationReadService.cs#L22-L23)
plus its two inline LEFT-JOIN comments, and the flusher's eligibility list item 2. Drift-table row 7
(service §4.7) closes as "schema aligned to doctrine," not "doctrine corrected."

*Sequencing.* Two halves with one ordering constraint. The **DDL rides the schema-hardening
migration** (schema §7) with the D10/D11/D13 flips — empty tables, free now. The **L2/DTO change
rides WU-InertFeatures** with D16/D17 (one notification WU, per Block D's framing) and *requires*
the migration; the reverse order is safe because nullable columns receiving concrete values behave
exactly as today, so the migration is inert until the write path changes. No dependency on any other
Block D decision.

*Tests (Integration tier).* The freeze regression is the point and gets a dedicated test: seed a
user, override **only** `Collapsed` on `NewChapterOnFollowedStory` → the row exists with
`email_enabled IS NULL`; mutate that type's `DefaultEmailEnabled` to `false` in-test →
`GetSettingsAsync` reports `EmailEnabled = false` for that user *and* the flusher drops a pending
notification for them (the same assertion at both read sites, because a coalesce fixed in one and
missed in the other is the realistic failure). Setting one field back to its default NULLs that
column while the other override survives (row not deleted). Setting both back deletes the row —
[`SetSettingAsync_DeletesOverrideRow_WhenValuesMatchDefault`](TheCanalaveLibrary.Tests.Integration/NotificationServiceTests.cs#L301)
stays green unchanged. Unsubscribe on a type the user had collapsed leaves `collapsed` NULL and its
effective value still tracking the default, and re-unsubscribing is idempotent. *(RazorComponents
tier: untouched — no UI change, which is itself the claim being asserted.)*

## Block E — Bounds, caps, throttles

Gates WU-BoundsAndCaps.

### D20. Bounds, caps, and throttle policy as one enumeration

*Source: service §3.13 + §2.5; includes the comment length cap from §3.20 and reading-progress
clamp §2.11.*
Approve the shared limits constants (PageSize ≤ 100, RandomBatch ≤ 50, ResultCap ≤ 500,
id-lists ≤ ~50, selection entries ≤ ~200, text caps for comments/messages/chapters), clamp-vs-
reject semantics, endpoint body-size caps, the export concurrency limiter, and the throttle
*coverage* enumeration (edits, poll votes, re-requests, reveals, list creation — "creates only"
is currently a fact, not a decision). Explicit sub-ruling: the anonymous unthrottled view-ping —
ratify (views are never sort keys; bot inflation accepted) or add the IP-partitioned limiter.

**Answer — throttle-coverage half only (2026-08-07); the bounds/caps half remains open:**
**Extend selectively and wire the remainder to existing kinds.** `security.md`'s "select by cost,
not by write-vs-read" rule stands and is *not* being replaced by a blanket per-write budget — the
gap is that the rule was never run against the full surface list, not that the rule is wrong.

*New `WriteActionKind` values (each needs a `DefaultLimits` row and a `security.md` limits-table
row — `DefaultLimits_CoverEveryWriteActionKind` fails the build otherwise):* `Reveal`
(content-gate reveal inserts, which today have no existence check, no enum validation, no cap and
no throttle); `RepeatRequest`, covering both re-request vectors as one kind — acknowledgment
re-request after a decline and lineage re-request after a rejection; `Vote` (poll voting — poll
*creation* is already `ContentCreate`).

*`RepeatRequest` limits are report-tier, deliberately harsh (5 burst / 1 per 3 min or tighter).*
These are the only surfaces in the set whose harm is not server load: one user repeatedly reaching
a *named* other user's notification tray after being told no. A hard state guard (one re-request
per decline) is acceptable in place of the bucket if the WU finds it cleaner — the ruling is that
the vector closes, not the mechanism. Discharges §2.8's lineage/acknowledgment items and the false
"spam guard" comment on the lineage rejection path.

*Wire to the existing `ContentCreate` kind, no new value:* custom-list create, series create,
story-arc create. They are user-visible content creates that shipped without the call.

*Edits stay exempt — but the exemption becomes conditional, and `security.md` must say so.* The
doctrine currently states both "select by cost" and "edits/deletes are deliberately unthrottled,"
which disagree while chapter bodies have no length cap: an edit re-runs sanitize + word-count on
an unbounded body (~28 MB per §2.5) at exactly a create's cost. The exemption is sound **only once
the text caps land**, so both must ship in the same WU; if the caps half is descoped, the edit
exemption is void and an `Edit` kind is required.

*Exports are the concurrency-limiter class, not the token bucket* — an `ExportGenerate` policy
mirroring the live `"ImportParse"` one, per `security.md`'s own two-class split (expensive
authenticated work that never commits).

*View-ping sub-ruling: ratified as anonymous and unthrottled.* Views are never sort keys, so
inflation cannot distort discovery; and pings land in `ViewCountBuffer` keyed by story id, so a
bot loop grows a dictionary bounded by story count, not by ping count. The accepted consequence —
displayed view counts are inflatable and authors read them — is recorded rather than defended. No
IP-partitioned limiter; the proxy-aware client-IP policy it would need does not exist yet.

*The WU completes `security.md`'s exemption sentence in the same pass*, naming every remaining
user-reachable unthrottled surface with its rationale, not just the four it names today. The
sweep found eighteen `*WriteService.cs` files with no `EnsureAllowed`; the ones classified here as
already-covered are the named toggles (`ServerUserStoryInteractionWriteService`,
`ServerFollowingWriteService`), the HTTP-policy-covered `ServerTagWriteService`, the buffered
signals (reading progress, read marks, user activity), system-internal fan-out
(`ServerNotificationWriteService`), and the admin/mod surfaces (Badge, Spotlight, SiteSettings,
Fanon) — the last group classified by name, so the WU confirms each is genuinely mod-gated before
writing it down as exempt. `ServerSavedTagSelectionWriteService` is covered by the caps half's
per-selection entry cap, not by a bucket.

*Still open in D20:* the shared limits constants and their numbers, clamp-vs-reject semantics,
endpoint body-size caps, the §2.11 reading-progress clamp, and the registration-side throttles
routed here from D1 (email verification, story-creation age/rate limits). Do not tick D20 until
those are answered.

## Block F — Counter & concurrency doctrine

Gates WU-CounterSymmetry and defines the drift policy other WUs cite.

### D21. Counter recompute principle

*Source: schema §3.6.*
Ratify: every denormalized counter must be recomputable from ground truth, and the recompute must
exist as code; classify which columns are authoritative vs derived. Today only `user_stats` has a
recompute path. Sub-ruling (service §2.4.5): hard-delete counter drift — batch-decrement in the
delete transaction, or ratify "accepted until recompute" (currently neither is chosen).

**Answer (2026-08-08):** **Every denormalized counter is *derived*. A counter caches the answer to a
*current-state* question; its ground truth is the live rows answering that question; a recompute over
them must exist in code. A count falling because its underlying rows were legitimately deleted is
correct behavior, not drift. There is no authoritative counter class and no named exception** — the
one D3 reserved for `RecommendationSuccessesEarned` is overturned, and D3's note is rewritten in
place to say so.

*Why no exception survives.* An exception would only be needed for a counter that caches a
*lifetime-earned* fact whose evidence a third party can destroy — the shape that argues for
non-cascading evidence or a credit ledger. Nothing in this system caches such a fact, because
**badges carry no tiers and no thresholds** (settled WU-StatBadgeProducers, 2026-07-30: earned at
≥1, displays `UserBadge.EarnedCount`). A badge is a *number*, not a preserved credential. With no
credential to protect there is no motive to make evidence outlive its parent, so the ledger option is
rejected — and with it the surrogate-key rework it would have forced, since all three candidate
evidence tables carry the vanishing FK inside their primary key
([`RecommendationSuccess`](TheCanalaveLibrary.Server/Data/Configurations/RecommendationConfigurations.cs#L58),
[`StoryAcknowledgment`](TheCanalaveLibrary.Server/Data/Configurations/StoryConfigurations.cs#L384),
[`StoryLineage`](TheCanalaveLibrary.Server/Data/Configurations/StoryConfigurations.cs#L351)). Those
three tables are also *operational* — the story page and lineage graph read them — so surviving rows
with a null parent would have forced an "and not orphaned" filter onto every live read to preserve a
statistic.

*The five falling-count cases, named so they are not rediscovered as bugs.* All correct, all
requiring no code: `RecommendationSuccessesEarned` (recommendation deleted, or crediting reader
deletes their account); `AcknowledgedAsBetaReaderCount` and `AcknowledgedAsInspirationCount` (the
crediting author deletes the story — `story_acknowledgments` and `story_lineages` both cascade);
`ChaptersRead` / `WordsRead` (the author deletes a chapter — `user_chapter_interactions` cascades,
so a *reader's* lifetime stat falls for a reason they did not cause); `CommentsWritten` (same
chapter-delete cascade).

*One conditional sixth case, recorded now so it is not rediscovered as a bug later.* `GroupsJoined`
has no falling case today **only because no group-deletion path exists** (D13 edge (b) establishes
this and relies on it). If one is ever built — D47(b) holds that question open — then
`group_members` cascades on `groups` ([GroupConfigurations.cs:17-20](TheCanalaveLibrary.Server/Data/Configurations/GroupConfigurations.cs#L17-L20))
and every member's `GroupsJoined` falls for a reason they did not cause, exactly like the
chapter-delete cases above. It joins this list by default rather than needing a fresh ruling: the
counter answers "how many groups am I currently a member of," a deleted group is not one, and the
recompute already reads exactly that. Whoever answers D47(b) ticks this over from conditional to
live; no counter code changes when they do.

*Sub-ruling — service §2.4.5 dissolves; it was a false choice.* Neither batch-decrement nor "accepted
until recompute." `DeleteChapterAsync` leaving `CommentsWritten` high is not drift: the recompute
counts extant `base_comments` rows, that count is truth, and it is *supposed* to fall. The item
appeared to need a ruling only because the counter had no stated question. **WU-CounterSymmetry loses
this item**; §2.4.2/3/6 remain.

*Zero-count badges — display-layer fix, not a recalc behavior change.* A badge whose count recomputes
to 0 is incoherent under "the badge is a number," but
[`SyncBadgeEarnedCountAsync`](TheCanalaveLibrary.Server/Profiles/UserStatRecalculator.cs#L364-L380)
deliberately never awards or removes rows. Keep that boundary and **hide zero-count badges at the
display layer**. Letting the recalc delete rows would be symmetric but hands a background worker the
power to destroy user-visible state, and would erase the trace a broken producer leaves behind; the
surviving 0-row costs nothing visually and doubles as a diagnostic signal.

*Explicitly out of scope: manual grants.* Patron / Architect / Artist have no backing counter (they
are absent from `BadgeCounterSpecs` by design — settled after the Feature 56 cut). The grant row *is*
the fact, there is no ground truth to recompute from, and nothing here licenses touching them. "No
authoritative counter class" is a statement about counters, not about records of a decision.

*Build scope.* `user_stats` is already compliant
([`UserStatRecalculator`](TheCanalaveLibrary.Server/Profiles/UserStatRecalculator.cs), F58) — its
aggregates already read current state, so this ruling requires **no change to existing code**. What
remains is the seven counters with ground truth but no reconciler: `like_count` ×3
(comments/blog posts/recommendations), `chapters.version_count`, `stories.word_count`, and
`active_report_count` ×4 — whose expression D7/D8 already supplied
(`COUNT(*) WHERE report_status_id IN (0, 1)`), closing schema §2.4.4(b)'s "no recompute path at all".
Ratify the expressions here; the workers land with WU-CounterSymmetry or lazily.

*Unblocks.* **D22** — "post-commit, recompute-corrected" is now an honest contract rather than a
promise about recomputes that do not exist. **D23** — "accept-and-record, self-healing via recompute"
becomes an available posture per family. Promulgation to `layer2-services.md` is the consuming WU's
job (Doc-Touch moment 1), not this file's.

### D22. Counter transactionality wording

*Source: service §3.14; drift §4.1; spec §9.4.*
Every cluster runs counters post-commit in a second transaction; doctrine and spec say "same
transaction." Adopt "post-commit, recompute-corrected" as the stated contract (recommended —
matches the code and the doctrine's own samples), or wrap everywhere.

**Answer (2026-08-08):** **"Post-commit, recompute-corrected" is the contract. A counter is mutated
in a second, separately-committed statement after the primary write's `SaveChangesAsync`; the pair
is deliberately *not* atomic, and a failure between them is **transient** drift that D21's recompute
heals. Wrapping is declined layer-wide. The "same transaction" wording in `layer2-services.md`
§UserStats Updates and spec §9.4's cross-cutting row is wrong about the code and is superseded.**
The shipped shape — e.g.
[ServerFollowingWriteService.cs:65-71](TheCanalaveLibrary.Server/Following/ServerFollowingWriteService.cs#L65-L71)
— is ratified as-is; **no existing code changes** under this ruling except as noted below.

*This rests on D21 and is only honest because of it.* Before D21, "recompute-corrected" promised a
correction that did not exist for seven counters, which made the failure window *permanent* drift
dressed up as eventual consistency. D21 having ratified that every counter is derived and must have
a reconciler, the window's cost drops from "wrong forever" to "wrong until the reconciler runs."
That is the whole basis of the trade — if a future ruling ever exempts a counter from recompute,
that counter is *not* covered by this contract and must be reasoned about separately.

*What is not changing: the atomicity rule.* The `ExecuteUpdateAsync` mandate
([layer2-services.md:1674-1693](.claude/skills/canalave-conventions/layer2-services.md#L1674-L1693))
is untouched and still absolute. The two rules answer different questions and conflating them is how
the wrong sentence survived: `ExecuteUpdateAsync` makes a counter mutation atomic against
**concurrent callers** (the lost-update class); transactionality would make it atomic against **its
own primary write's failure** (the drift class). This ruling declines the second and keeps the first.

*Consequence for the one layer-wide violation.* `chapter.VersionCount++`
([ServerChapterWriteService.cs:149](TheCanalaveLibrary.Server/Chapters/ServerChapterWriteService.cs#L149),
service §2.4.6) is the sole tracked read-modify-write left, and it is also the *only* counter in the
codebase that genuinely rides the primary transaction. It is **not** thereby compliant — it is the
one shape this ruling still rejects, because the atomicity rule outranks and the `++` form loses
concurrent increments. Fix it to a post-commit `ExecuteUpdateAsync` like every sibling; do not read
its same-transaction placement as the doctrine's surviving example.

*Order is load-bearing — pre-commit increments are not covered.* Report submit increments
`active_report_count` in a committed statement **before** the report row saves (§2.4.4(b)), leaving
`+1` with no row. That is the mirror image of what this ruling ratifies, not an instance of it: the
recompute heals a *missing* increment on retry, but a counter incremented for a row that never
existed is invented data. The reorder §2.4.4 already wants is therefore **required** by this ruling
rather than optional. Stated as a rule: primary write commits first, counter second, never the
reverse.

*Why wrapping loses.* It buys strict consistency for display aggregates nothing gates on, and costs
an explicit transaction at every write site — which under the retrying execution strategy means the
full `CreateExecutionStrategy().ExecuteAsync` + `ChangeTracker.Clear()` ceremony that today only
Spotlight redemption carries (service §5's reference concurrency-sensitive write). Dozens of sites
inherit retry-replay hazards to eliminate a window that a reconciler already closes.

*The accepted cost, stated plainly.* A crash or a failed counter statement between the two commits
leaves a visibly wrong number until the reconciler runs. That is a known, ratified consequence, not
a bug — a report of "my story count is off by one" is checked against the reconciler's last run
before it is treated as a defect.

*Doc corrections owed (drift §4.1, doc-only).* Reword `layer2-services.md` §UserStats Updates
([:1663-1664](.claude/skills/canalave-conventions/layer2-services.md#L1663-L1664)) — only the
sentence is wrong; the code sample beneath it already shows the ratified post-commit shape and stays.
Record spec §9.4's row as superseded in the convention doc (the spec itself is read-only).
Promulgation is the consuming WU's job (Doc-Touch moment 1) — WU-CounterSymmetry.

*Unblocks.* **D23** — "accept-and-record, self-healing via recompute" is now a coherent posture to
choose per family, since this entry establishes that the healing path, not the write path, is where
counter correctness lives.

### D23. Check-then-act posture per family

*Source: service §3.15.*
Accept-and-record (self-healing via recompute/next action) vs harden (`ON CONFLICT`/advisory
locks) — per family: hidden-gem/highlight limits, vouch limit, poll single-choice, USI
flip-detection. Stated minimum regardless: close the USI create-create 500 and add the missing
`group_stories` unique index. Record the ruling per family so drift incidents have a defined
answer.

**Answer (2026-08-08):** **Neither posture uniformly. One principle, applied per family:** enforce in
the database every invariant a *reader* depends on; where that isn't declaratively expressible,
change the model until it is; where no reader depends on it, state the weakened invariant honestly
and own the recompute. Blanket-hardening is rejected — not on cost grounds, but because for three of
the five families it buys a weaker guarantee than it appears to.

*The classification test (this is the reusable part).* For any check-then-act site, ask: **if this
were violated, would a consumer be wrong, or only the writer's intent be disappointed?** Six hidden
gems instead of five breaks no reader — nothing queries a cardinality, no arithmetic depends on it.
That is a **policy**, and check-then-act is a legitimate implementation of a policy. One voter with
rows on two options of a single-choice poll makes every tally wrong, with no way to tell from the
data which row is the real vote. That is a **constraint**, and it belongs where it cannot be
violated — because "readers rely on it" means *all* readers, including future code, an import, or an
admin's manual SQL. Apply this test to new write paths rather than re-deriving a posture each time.

*Declarative and procedural hardening are not the same thing, and the difference is the whole
ruling.* A unique/partial index, FK, or `ON CONFLICT` puts the guarantee **in the data**: it holds
against every writer, forever, including ones not yet written. An advisory lock puts it **in the
code**: it holds only while every present and future writer remembers to take it. Postgres has no
assertions, so "at most N rows matching a predicate per group" has no declarative form — meaning
hardening the three cardinality limits would adopt a *convention*, not a constraint, while the docs
would begin claiming a guarantee the system does not hold. That converts a known-soft invariant into
an unknown-soft one. False confidence is the worse failure, and it is the exact class this audit
keeps finding elsewhere (`DeleteFolderAsync`'s phantom SET-NULL, the lineage "spam guard that doesn't
exist").

*Per-family ruling — the lookup table for drift incidents.*

| Family | Reader depends? | Ruling | Mechanism |
|---|---|---|---|
| USI create-create (visible 500) | n/a — it's a crash | **Harden** | Upsert / `ON CONFLICT DO NOTHING`, then re-read. Declarative; not part of the fork. |
| `group_stories` idempotent add | yes — membership is set semantics | **Harden** | Unique `(GroupId, StoryId)`. Declarative; not part of the fork. |
| `group_members` idempotent join (added 2026-08-08) | n/a — it's a crash | **Harden** | `JoinAsync`'s `AnyAsync`-then-`Add` behind PK `(UserId, GroupId)`: concurrent double-join 500s. Same shape and same ruling as the USI row above — `ON CONFLICT DO NOTHING`, then return. |
| Poll single-choice | **yes** — tallies are published numbers | **Restructure**, not lock | See below. |
| USI flip-delta counters | no — derived data | **Accept**, recompute-corrected | Locking to protect a cache is backwards; see below. |
| Hidden gem (5/user) | no | **Accept-and-record** | Overshoot bounded at concurrent-request count; recompute corrects. |
| Highlight (5/story) | no | **Accept-and-record** | Same. |
| Vouch (5/user) | no | **Accept-and-record** | Idempotency half gets a unique `(VouchingUserId, VouchedUserId)` key — that one *is* declarative and is free. |

*Poll votes: change the model so the constraint becomes expressible.* Single-choice is inexpressible
today only through a modelling accident — `PollVote`'s key is `(PollOptionId, UserId)` and the row
carries no `PollId` at all (it lives one hop away on `PollOption`), so the vote row cannot see the
scope its own uniqueness is defined over. Put `PollId` on the vote row, keep it honest with a
composite FK `(PollId, PollOptionId) → poll_options` (needs a unique index on that pair — cheap), and
add a nullable guard column set to `PollId` for single-choice polls and NULL otherwise, unique on
`(guard, UserId)`. NULLs-distinct leaves multi-choice untouched. The race then dies structurally, at
zero runtime cost, against every future writer. **Two caveats recorded so this isn't read as
stronger than it is:** it depends on `AllowMultiple` being immutable once votes exist — which
`ConfigLocked` asserts, but `ConfigLocked` is *derived* (`TotalVoterCount > 0`), so the
first-vote-vs-config-edit boundary is itself an unguarded check-then-act and needs the same
treatment in the same WU. And this is a schema change: free now, awkward after real votes exist.

*Counters are derived data; do not lock to defend a cache.* `FavoritesOnStories` and friends are a
materialized `COUNT(*)`. Under D21's own principle — ground truth is the rows, the counter is an
optimization, the recompute must exist as code — the incremental delta is *allowed* to be
approximate. Hardening the flip-detection would pay serialization on the hottest write path in the
app (every favorite toggle) to defend a property the architecture explicitly does not need. The
correct fix is the recompute, which is owed regardless.

*Advisory locks are not banned.* They remain the right tool where a genuine constraint is
non-declarative **and** unrestructurable. No family here qualifies after the poll restructure. If a
future WU reaches for one, that is the bar it must clear, and it must be recorded — a lock-based
invariant is a convention, and undocumented conventions decay.

*What this decision costs elsewhere.* Answering "accept" for three families **writes a bill D21 must
pay**: accept-and-record is only honest if the recompute exists, and today only `user_stats` has one
(§3.6). D21 therefore owes recompute paths for `IsHiddenGem`, `IsHighlightedByAuthor`, and vouch
counts, not merely the counter columns. If D21 declines to fund them, this ruling flips those three
families to "stated-soft with no corrective," which is acceptable but must be *written down that
way* rather than left implied.

*Routing.* The `group_stories` unique index lands with §2.10's folder-integrity WU (it is already
that WU's schema half). The poll-vote restructure and its `ConfigLocked` rider land with the Block H
migration wave, not with WU-CounterSymmetry — flagging the sequencing so the schema change isn't
stranded behind a service-layer WU. The USI upsert, the vouch idempotency key, and `JoinAsync`'s
upsert land in WU-CounterSymmetry itself — the last of these alongside D24's `CreateGroupAsync`
increment, since both touch the same method pair and must not be split. *(The `group_members` row was
added 2026-08-08: the family table's original five were drawn from service §3.15's enumeration, which
did not include it — an instance of the "principled-by-accident coverage" this audit keeps finding,
and the reason the classification test above is promulgated as a standing rule rather than a one-time
sweep.)* The classification test above is promulgated to
`layer2-services.md` as the standing rule for new write paths (Doc-Touch moment 1), and D22's
"post-commit, recompute-corrected" wording is what makes the four *accept* rows coherent — the two
answers must ship as one doctrine paragraph or neither reads correctly.

### D24. Does creating a group count as joining?

*Source: service §3.7; defect §2.4.2.*
Creation inserts the creator's member row with no +1; leave unconditionally −1s → negative
counter, recalculator fights the live path. Pick an answer and encode it in both places.

**Answer (2026-08-08):** **Yes — creating a group counts as joining it. `CreateGroupAsync` gains the
same `+1` that `JoinAsync` already carries; the recalculator and `LeaveAsync` are correct as shipped
and do not change.** One statement at
[ServerGroupWriteService.cs:63](TheCanalaveLibrary.Server/Groups/ServerGroupWriteService.cs#L63),
after the creator's member row saves.

*This was not actually a fork — D21 had already decided it, and the entry survives only to say so.*
D21 rules that every counter is derived, that its ground truth is the live rows answering a
**current-state** question, and that the recompute over those rows is the definition. It then
ratified `user_stats` as already compliant, requiring no change to existing code — and
`GroupsJoinedAgg` is one of the aggregates that ratified:
`COUNT(*) FROM group_members GROUP BY user_id`
([UserStatRecalculator.cs:146-147](TheCanalaveLibrary.Server/Profiles/UserStatRecalculator.cs#L146-L147)).
The creator's row **is** a `group_members` row and that aggregate does not distinguish, so D21 had
already defined the question as "how many groups is this user currently a member of" and answered it
inclusively. The defect is therefore not an undecided policy but a **wired path that fails to
implement an already-ratified formula**. Nothing here overrides D21; it applies it.

*The alternative is foreclosed by D13, independently of the argument above.* "Creating does not
count" would require the recompute to exclude the founder — `WHERE m.user_id <> g.creator_id`. D13's
account-deletion ruling leaves the group alive with **NULL `creator_id`** (its own test says so), at
which point the exclusion silently stops applying and every surviving member's counter changes
retroactively for a reason unrelated to their membership. A recompute whose answer depends on whether
some *other* user has since deleted their account is not a recompute over ground truth. The chosen
answer never reads `creator_id` at all and is immune to this.

*Neither of the two postures this block just established covers it — recorded because a later reader
routing by table-lookup would misfile it.* **D22** ratifies drift *between two commits*, healed by
the reconciler; here there is no window, the increment was simply never written, so the wired path
computes a permanently different function than the recompute and each pass oscillates rather than
converges. D22's own carve-out logic applies in mirror image: it excluded the pre-commit report-count
increment because a counter incremented for a row that never existed is invented data; a counter
permanently missing an increment for a row that always existed is the same category of error, not an
instance of the contract. **D23** would file `GroupsJoined` in its *accept* column on the reader test
(display-only, backs no badge — it is absent from
[`BadgeCounterSpecs`](TheCanalaveLibrary.Server/Profiles/UserStatRecalculator.cs#L273-L277) — and
gates nothing), but D23's subject is **concurrent** check-then-act races where "accept" means
tolerating a bounded overshoot under load. This defect is deterministic and single-threaded.
Accepting it would mean shipping a write path known to disagree with its own recompute, which is the
false-confidence failure D23 spends three paragraphs rejecting.

*It is not an edge case, and the framing mattered.* Create a group, then leave it: two first-class UI
actions, no concurrency, no failure required, `GroupsJoined = −1` on a public profile. The column is
a plain `int` and [ServerUserProfileReadService.cs:98](TheCanalaveLibrary.Server/Profiles/ServerUserProfileReadService.cs#L98)
projects it raw with no clamp. A floor-at-zero would hide the symptom while leaving the oscillation,
and is rejected for the same reason D23 rejects convention-as-constraint.

*What this does not touch.* The group left admin-less by that same leave path is **D13 edge (b)'s**
subject, not this entry's: D13 rules that a moderator assigns a new admin on request, explicitly
rejects auto-promotion and self-claim, and explicitly covers the leave route as well as the
account-deletion route. D24 makes the counter correct across that transition (create `+1`, leave
`−1`, net 0 — no negative, no residue) and takes no position on the group's administration. D13's
optional "warn on last-admin leave" rider stands on its own merits.

*Also unchanged: the mod-assign action has no counter effect.* WU-GroupAdminRescue promotes an
existing member by setting `GroupMember.Role = Admin`. That is a role change on an extant row, not a
membership change; `GroupsJoined` must not move, and the recompute agrees by construction since it
counts rows without reading `Role`. Stated so the WU does not add a sympathetic increment.

*Doc corrections owed (Doc-Touch moment 1).* `layer2-services.md`'s counter map row
([:1708](.claude/skills/canalave-conventions/layer2-services.md#L1708)) currently reads
`GroupsJoined | member | ServerGroupWriteService join/leave | ±1` — it becomes **create/join/leave**.
The same claim appears in [`audit/Profiles.md:148`](.claude/audit/Profiles.md#L148) ("`GroupsJoined`
±1 on Join/Leave") and must move with it. **Three separate rulings now edit `layer2-services.md`'s
counter sections** — this one, D22's "same transaction" rewording at
[:1663-1664](.claude/skills/canalave-conventions/layer2-services.md#L1663-L1664), and D23's
classification test — and all three belong to WU-CounterSymmetry, so they land as one doc pass rather
than three; D13's separate rewrite of §"Group Membership and Role Model" belongs to
WU-GroupAdminRescue and does not collide.

*Routing.* **WU-CounterSymmetry**, jointly with D23's `JoinAsync` upsert (same method pair). The WU's
scope is now smaller than service §7 describes: D21 removed §2.4.5 and D22 re-scoped §2.4.6 into "fix
`chapter.VersionCount++` to post-commit `ExecuteUpdateAsync`," leaving §2.4.3 (`WordCount` counting
unpublished drafts) as the only substantial item beside these mechanical ones. Whoever writes the WU
should read this paragraph rather than the audit's sequencing list, which predates Block F.

*Tests (Integration tier, `GroupServiceTests`).* Create a group → the creator's `GroupsJoined` is
exactly 1 (the class currently asserts only `MemberCount`, at
[:56](TheCanalaveLibrary.Tests.Integration/GroupServiceTests.cs#L56)). Create then leave → 0, never
negative. Create, leave, rejoin via `JoinAsync` → 1. And the convergence assertion this entry exists
for: run `UserStatRecalculator` after each of those and assert it corrects **nothing** — a pass that
changes `groups_joined` means the wired path and the recompute have diverged again, which is the only
regression that matters here.

## Block G — Access-gate rules

Gates WU-AccessGateSweep2 (with D6, answered in Block A).

### D25. Selection-by-id single gate rule

*Source: service §3.10; defect §2.6 (copy-path break).*
Every path to a saved selection by id (permalink, detail, copy) applies the identical gate set
with one indistinguishable failure. Confirm as the rule.

**Answer:** _(pending)_

### D26. Series & custom-list by-id visibility

*Source: service §3.11.*
Is a series an independent public artifact or profile-tab data? By-id detail currently ignores
the `ProfileVisibility` the by-author list respects. Same family: custom-list direct reads by id
vs the F15 permalink precedent (IsPublic **and** ProfileVisibility).

**Answer:** _(pending)_

### D27. "My X" anonymous semantics

*Source: service §3.20.*
Anonymous callers of self-scoped reads: zero-state vs 401 — one discriminator rule for the whole
surface.

**Answer:** _(pending)_

## Block H — Pre-data schema lock-ins

The remaining schema §3 forks; all free on an empty DB, expensive after.

### D28. FTS configuration + scope

*Source: schema §3.2 + service §3.20 (the two hardcoded `'english'` query sites move in
lockstep).*
`'english'` stems/drops stopwords — questionable for a proper-noun-heavy corpus ("Cynthia",
"Sinnoh"). Options: keep `'english'`, switch `'simple'`, or dual-vector (the usual
fanwork-archive answer; one more generated column + GIN). Also ratify the scope exclusion
(title + short description only; chapter bodies deliberately excluded) so it isn't "discovered"
as a gap. (Trigram/substring search is separate and already tracked by the L6 matrix.)

**Answer:** _(pending)_

### D29. Case-insensitive community-facing names

*Source: schema §3.1; service §2.12 (app-check-over-case-sensitive-index races).*
All non-Identity name uniqueness is byte-wise case-sensitive. The one that matters:
`groups.group_name` (site-global namespace; impersonation vector). Recommended: `lower()`
expression index there at minimum; decide the per-user scopes (lists, selections, series,
folders) cheaply in the same breath and record the choice either way.

**Answer:** _(pending)_

### D30. Optimistic concurrency + edit-conflict UX

*Source: schema §3.3.*
Map Postgres `xmin` as a concurrency token on `chapter_contents`, `stories`/`story_details`,
`base_blog_posts` (zero schema cost) — plus the real decision: do edit surfaces detect conflicts
(409 "someone else saved") or stay last-write-wins? The UX contract is what locks in by habit
once humans are editing.

**Answer:** _(pending)_

### D31. USI dates partition fate

*Source: schema §3.5 + service §3.20.*
`user_story_interaction_dates` is diligently written and read by nothing. Pick one: (a) build the
date-sorted bookshelf surfaces, (b) keep writing as recorded cheap future-proofing, (c) cut it.
The incoherent position is the current one. (Service §2.12 rider: all-null date rows retained on
HasStarted-only rows.)

**Answer:** _(pending)_

### D32. Story-centric USI index gap

*Source: service §3.20; [[L6-reconciliation-matrix]] conflict row.*
The "Rejected-vs-live conflict" story-centric partial indexes: ratify the absence or build
pre-data. (The L6 matrix owns the build; this ruling is whether it happens before first data.)

**Answer:** _(pending)_

### D33. `group_folder_group_story` join rigor

*Source: schema §3.10.*
The EF-generated join permits a folder of group A to contain a story row of group B. Options:
explicit join entity with composite FKs `(group_id, ...)` both sides (+ the two cheap unique
indexes), or record the app-invariant. The naming cleanup alone argues for the explicit entity
while the table is empty.

**Answer:** _(pending)_

### D34. Public integer IDs acceptance

*Source: schema §3.11.*
Sequential int IDs in public URLs — enumeration is trivial, and the access-gate design already
treats direct-nav as a consented plane. Keep ints; record the acceptance in one sentence so it's
a decision, not a default.

**Answer:** _(pending)_

### D35. HTML-as-source ratification

*Source: schema §3.9 + service §3.20.*
`ContentRaw` (editor source) was never built; sanitized HTML is the canonical source. Either add
the column now (unrecoverable later — source never stored can't be backfilled) or ratify
HTML-as-truth in `audit/Chapters.md` (the export/import round-trip is the de-facto editor
migration path).

**Answer:** _(pending)_

## Block I — Platform conventions

Gates WU-ParityAndRegistration and the doc-correction pass.

### D36. Canonical DI registration shape

*Source: service §3.12; defect §2.7.2.*
For write-serves-both clusters: separate read binding where a dedicated read impl exists;
forwarding delegate otherwise. Confirm, then the seven-cluster sweep is mechanical.

**Answer:** _(pending)_

### D37. Server-only methods on WASM-registered interfaces

*Source: service §3.20.*
Split them out before the interface surface freezes. Confirm the split as the rule.

**Answer:** _(pending)_

### D38. CSRF posture as recorded policy

*Source: service §3.19.*
SameSite=Lax is the effective sole CSRF control for the cookie-authenticated JSON API
(antiforgery form-post-only; uploads exempt). Defensible; currently implied. Ratify with one
sentence in `security.md` — which also corrects the upload endpoints' "stateless API" rationale.

**Answer:** _(pending)_

### D39. CancellationToken policy

*Source: service §3.20.*
Expensive-reads-only is the shipped shape. Ratify it as the written rule.

**Answer:** _(pending)_

## Block J — Quick ratifications

Each is a confirm-or-override with a coherent shipped position; recording the answer is the work.

### D40. Mark-read-elsewhere sets HasStarted

*Source: service §3.20; spec §5.12 conflict.*
WU45's position (per-chapter mark sets HasStarted) is coherent — ratify it over the spec line.

**Answer:** _(pending)_

### D41. StoriesInProgress formula

*Source: service §3.20.*
`StoriesInProgress` vs the Actively-Reading formula — settled per the audit; ratify with the
visible consequence stated.

**Answer:** _(pending)_

### D42. Archived-poll votability

*Source: service §3.20; defect §2.12 (archived site polls remain votable).*
Should archived polls accept votes? Rule, then the §2.12 fix follows it.

**Answer:** _(pending)_

### D43. Group blog-post rating vs audience waterfall

*Source: service §3.20.*
How a group post's rating interacts with the group audience-rating waterfall — one-paragraph
ruling.

**Answer:** _(pending)_

### D44. Account re-verification silently un-verifying

*Source: service §3.20.*
Changing email (or re-verifying) silently drops verified status. Intended or defect — rule it.

**Answer:** _(pending)_

### D45. Chapter author-draft preview surface

*Source: service §3.20.*
Should authors get a reader-faithful preview of unpublished chapters? Build or record the
absence.

**Answer:** _(pending)_

### D46. Chapter version deletion

*Source: service §3.20.*
No way to delete an alternate chapter version exists. Build or record the absence.

**Answer:** _(pending)_
