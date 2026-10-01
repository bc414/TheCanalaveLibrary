# Roadmap — The Canalave Library (Platform-First → Features → Beta → Launch)

> **Live master plan.** Phase status, the "Decisions that need you" table, and newly-resolved
> decisions going forward. Supersedes `middle_plan_v2.md` (retired 2026-07-27), which itself
> superseded `middle_plan.md` (retired 2026-07-05) and `forward_plan.md` (retired 2026-07-03).
> That chain's full historical Resolved log (every design decision from the original 2025 sessions
> through 2026-07-27) is **not duplicated here** — it stays in `middle_plan_v2.md` §Resolved,
> which itself carries forward `middle_plan.md`'s and `forward_plan.md`'s entries. This file starts
> a fresh Resolved section (see bottom) rather than re-copying 150+ historical entries. `CLAUDE.md`
> remains the source of truth for file paths, artifact names, and Stage semantics; `workplan.md`
> remains the work-unit ledger — new work-units are *sequenced* here and *recorded* there.
>
> **Why the rename (2026-07-27):** the `forward_plan → middle_plan → middle_plan_v2` chain named
> itself after its position in a sequence, with no stable next name and a version suffix baked into
> the filename — the only doc in this corpus named that way. `roadmap.md` is named for its role,
> like `status.md`/`workplan.md`, and doesn't need a `_v2` next time content is superseded — content
> gets superseded, the filename stays put.

## Where things stand (2026-07-27)

MVP build-out + platform build-out (Phases 0, 1, 5) are complete and browser-verified. `git log`
shows a bursty pattern, not a steady drip — relevant to how "Recommended next work units" below is
sequenced:

- **2026-07-05 → 07-07:** all nine Phase 1 platform items in three days (Observability,
  SignalBuffering, L6 index batch, ErrorHandling, Email, Security, DataProtection, Marts).
- **2026-07-10 → 07-13:** most of Phases 2/4/5 — Design System, seven MVP-surface features landing
  in a single day (07-11: Series, Story Lineage, Saved Tag Selections, Account Deletion UI,
  Import/Export, Open Graph, Site Daily Stat), Spotlight/Polls/Story Arcs (07-12), the L5 WASM
  global flip + Custom Lists (07-13).
- **2026-07-18:** one 20-commit day removing the desktop/mobile device-fork paradigm
  (WU-ResponsiveMerge) plus the Feature-56 cut.
- **2026-07-24 → 07-27 (most recent four days):** zero new features — entirely hidden-deferral
  closures surfaced by the 2026-07-24 audit (`hidden-deferrals-tracker.md`: WU-AccessGate2,
  WU-ChapterArcBrowserPass, WU-GroupsL5/L5b, A3, WU-RecLifecycle, WU-B2, WU39, WU-TagFanon,
  WU-MsgArchive, WU-TokenGreen, WU-ParentVisibility) plus three doc-hygiene passes and this file's
  own creation.

The pattern is build-in-bursts, then harden-in-a-burst. Only two Phase-2 items and the whole of
Phases 3, 6, 7 remain before launch. Live count/Position detail: `workplan.md`'s Position block.

## Phase status

- **Phase 0 — Hygiene + CI — DONE ✓ (2026-07-05).** Full detail: `middle_plan_v2.md` Phase 0.
- **Phase 0.5 — Convention-settling visual mini-pass — DONE**, folded into ongoing Pattern
  Accumulation (`layer4-style.md`).
- **Phase 1 — Platform build-out — DONE ✓ (2026-07-07, WU-Marts closing item 9).** All 9 items
  shipped: Observability, SignalBuffering (supersedes the dissolved Redis/L7 plan), L6 index
  batch + perf baseline, ErrorHandling, Email, Security, DataProtection, SignalR (removed —
  permanently ruled out for messaging), Marts. Full detail: `middle_plan_v2.md` Phase 1.
- **Phase 2 — MVP-surface completeness — DONE ✓ (2026-07-30, WU-AccountEnforcement closing the
  last item).** Items 2–8 are DONE (Series/Story Lineage/Saved Tag Selections, Manual+Automatic
  Tree Search, Account Deletion UI, External Link Verification, Export/Import, AccessGate) — full
  detail: `middle_plan_v2.md` Phase 2. Item 1, WU-Home, is DONE ✓ (2026-07-28) — decision row 2
  resolved and built; see §Resolved above and `workplan.md`'s DONE entry.
  **WU-AccountEnforcement (residual) — DONE ✓ (2026-07-30).** Core login-blocking + banner shipped
  inside WU38a (2026-07-11); the mid-session-responsiveness residual is closed. `RefreshSignInAsync`
  turned out not to be the tool — it reissues only the *caller's* cookie, and a moderator cannot
  reach a different user's session with it. Built instead: `AccountStatusBanner` re-reads status
  live via a new `IAccountStatusReadService`, re-queried on every in-app navigation (the
  `MessagesNavLink` unread-badge pattern), now covering Warned/Suspended/Banned rather than Warned
  alone. Pointer: `workplan.md` DONE entry; `security.md` "Account-Status Enforcement";
  `identity-and-authorization.md` §"Account Status Is Display-Only, Read Live".
- **Phase 3 — Full L4 sweep + Stage-6 freezes — not started; Phase 2 is done, so this is next.**
  Brian-driven, per-cluster render → fix → Pattern-Accumulate → 5→6 on sign-off. Surface decision
  row 1 is resolved (see Resolved below in `middle_plan_v2.md`). **WU-A11y split in two on
  2026-07-31 (decision row 12, resolved — see §Resolved):** the static/naming half
  (labelling, validation association, `Modal` primitive, image `alt`, mechanical gates) does not
  need the sweep and is sequenced ahead of it (Tier 2); **WU-A11y-Keyboard** (focus trap, Escape,
  combobox ARIA, the manual keyboard script) still pairs with this sweep — both are final
  whole-site passes over already-built surfaces, and keyboard verification needs Brian's browser
  pass regardless.
- **Phase 4 — Beta-scope decisions — DONE ✓ (2026-07-18, last verdict: Feature 56 cut).** Every
  per-feature verdict rendered. Full detail: `middle_plan_v2.md` Phase 4.
- **Phase 5 — L5 WASM enablement — DONE ✓ (2026-07-13, WU-L5Sweep + WU-GlobalFlip).** One
  Phase-5-adjacent follow-up remains — see "Recommended next work units" below.
- **Phase 6 — Beta — not started.** Small audience from the existing community. Entry gate:
  Phases 0–3 done (0/1 already are), every Phase 4 item resolved (is), email live (is).
  **Blocked on decision row 6** below. *(**WU-NotifEmail** was sequenced at this gate until
  2026-07-31, when it moved to Tier 2 of "Recommended next work units" — see that row for why.)*
- **Phase 7 — Launch readiness + Launch (DigitalOcean) — not started.** Topology settled: droplet
  (server only — Redis superseded by in-process signal buffers; Valkey joins only at the N≥2
  trigger), managed PostgreSQL, Cloudflare R2 + CDN. The checklist, each item small:
  - **Deploy mechanism** — `aspire publish` docker-compose output is the candidate path.
  - **Config/secrets promotion contract** — one documented list of every env var the droplet needs
    (connection strings, `ImageStorage__S3__*` R2 values, `Email__*` per the chosen provider,
    OTLP endpoint, Data Protection).
  - **Migration-in-production convention** — gated deploy step (backup first), not dev's
    migrate-on-startup.
  - **Backups you have restored** — managed-PG backup policy + one performed restore drill; R2
    story for blobs.
  - **Uptime & alerting** — safely-exposed health endpoint (exposure mechanism itself undecided)
    + external pinger + an alert channel that reaches Brian.
  - **Sending-domain DNS (SPF/DKIM/DMARC)** — part of decision row 8 below; must verify before
    beta invitations go out.
  - **Operational resilience group** — minimal ops runbook, DR beyond the DB, cost/billing
    alerts, the explicit no-staging-environment decision.
  - **Telemetry destination live** — self-hosted Grafana LGTM container (resolved mechanism, see
    `middle_plan_v2.md` §Resolved); deploy it, set `OTEL_EXPORTER_OTLP_ENDPOINT`.
  - **Image derivative sizing** — covers/avatars currently store one 2048px original served into
    24–144px display slots on every listing grid (tracker **B14**, surfaced scoping WU-DataSaver's
    cut). Generate derivatives at upload or front R2 with Cloudflare Image Resizing, then emit
    `srcset`/`sizes` at consumer sites; natural fit alongside this checklist's R2/CDN work.
  - **CI hardening for a live master** — add `push: master` trigger + branch protection + promote
    the vuln scan from report-only to a hard gate (all deliberately deferred from Phase 0 until
    master means "what's deployed" — see `middle_plan_v2.md` §Resolved).
  - **Clear the AngleSharp 0.17.1 mXSS at the root (CVE-2026-54570)** — the vuln-scan hard gate
    above will block on this unless cleared first. Fix: replace `Ganss.Xss` with a custom
    AngleSharp-1.5.2 sanitizer behind the existing seam. Currently risk-accepted (mitigated by the
    13-tag/`href`-only allow-list) — see `security.md` "Accepted-risk register."
  - **TLS/domain** (Cloudflare Registrar).
  - **Activate verified-crawler serving** — flip `Seo:TrustVerifiedBots` once the origin-lockdown
    item above lands, and verify the real Cloudflare verified-bot header contract at that time.
  - **Legal/policy track** — decision row 10 below; non-engineering, runs parallel, gates launch.

## Decisions that need you

Row numbers preserved from the retired chain (other docs cite them by number — see
`middle_plan_v2.md` §"Decisions that need you" header for which rows resolved when, up to
2026-07-27). Row 13 was added 2026-07-27 (promoted out of `hidden-deferrals-tracker.md` B11's
blocking question, same treatment now applied going forward: a tracker item that turns out to be a
genuine open decision gets promoted here, not left buried in the tracker) and resolved 2026-07-28.
Row 12 (accessibility scope/depth) resolved 2026-07-31 — see §Resolved. Rows 14–16 were added
2026-09-30 (WU-StoryLifecycle review fixes), promoted out of `hidden-deferrals-tracker.md` **F9** under
the same rule; F9's other two items already sit on the worksheet as pending D20 and D30. Rows 17–18
were added 2026-09-30 by WU-InertFeatures: two unruled edges its build had to take a default on.
Row 19 was added the same day by its review fixes (a third such edge, previously only labelled
"unruled" in the conventions). Row 20 was added 2026-09-30 by WU-ModerationIntegrity: two account-status
transitions its spec derived rather than took from owner text.

| # | Decision | Default (per spec/§0) | Why it's yours |
|---|----------|----------------------|----------------|
| 4 | **Launch-readiness mechanics** — the full Phase 7 checklist above. | Topology settled (droplet + managed PG + R2); `aspire publish` compose output is the default deploy candidate. | Operational cost/effort trade-offs. Gates Phase 7. |
| 6 | **Beta logistics** — who, how many, invite mechanism, feedback channel. | None. | Community relationships are yours. Gates Phase 6. |
| 8 | **Email provider + sending domain** — mechanism is resolved (config-only SMTP swap); which provider, the sending domain, and its SPF/DKIM/DMARC DNS records remain open. | Postmark, SES, or Resend (cheap at this scale); needs a sending domain, tying into row 4's domain work. | Cost, deliverability reputation, and the domain is yours. Gates Phase 7. |
| 10 | **Legal/policy track ownership + timing** — ToS, privacy policy, DMCA agent/process, moderation obligations for a fanfiction UGC site. | None. | Legal exposure and community policy are yours; engineering only hosts the documents. Gates Phase 7. |
| 14 | **Import rider, alternate reading (worksheet D1)** — must a *trusted* author's story that lists external ("also posted on") links still go through the approval queue? | No (as built): import verification is the decoupled per-link queue (WU38d/WU39), which never consults trust, so D1's "import-verification submissions never take the waiver" holds vacuously. | "Yes" would reopen the settled WU38d note in `audit/Moderation.md` F53 ("links don't gate story approval") — a policy reversal only you can make. Tracker **F9** item 2. |
| 15 | **Tell an author when a moderator revokes or restores their auto-approve?** | No notification (D1 is silent; nothing is sent today). The revoke/restore is still recorded as a moderator-initiated `Report` row. | Moderation transparency toward authors is community policy. Tracker **F9** item 3. |
| 16 | **When does a chapter "go live" if its story isn't live yet?** `Chapter.FirstPublishedDate` is stamped on the chapter's own first publish, even while its story is Draft/Pending/Rejected (D2's "stamped once on the chapter's first publish", invariant tied to `IsPublished`). D2 also says the anchor means "went live **on this site**", which such a chapter has not. | As built: stamp on the chapter's own first publish. Consequence: L8 `new_chapters`/`new_words` count chapters of stories that never went live (including rejected spam), while `new_stories` excludes those stories; the fan-out anchor (WU-InertFeatures) predates the story's own `PublishedDate`. | The two halves of D2's wording disagree on this edge, and the answer fixes the "New"-badge, fan-out and L8 semantics for good once real data exists. Tracker **F9** item 5. |
| 17 | **Should a new chapter notify its story's followers when the story itself isn't live?** The new-chapter fan-out (type 10) fires once, when the chapter's `FirstPublishedDate` is stamped. | As built (a default, not a ruling): suppress it when the story is not publicly published at that moment (Draft / PendingApproval / Rejected, or taken down). Consequence: a chapter first published while its story was unpublished never notifies, because its anchor is already stamped when the story later goes live; a story that is unpublished and republished does not re-announce its chapters. | Who gets told about what, and when, is reader-facing policy; it is tied to row 16 (whether such a chapter has "gone live" at all). Rule text: `layer2-services.md` §"Notification Generation" → "New-chapter fan-out". |
| 18 | **May a recommender earn a "helpful" credit from their own recommendation?** A recommender who saves a story for later from their own recommendation card (or follows its "Read now" link) gets an attribution row, and answering the prompt adds +1 to that recommendation's `SuccessfulRecCount`. | As built (status quo): allowed. Only the recommender badge counter skips the reader-is-recommender case; D3's author gate covers the *story's* author, not the recommender. | It decides what the public "N helpful" count means; D3 did not address it. Rule text: `layer2-services.md` §"Attribution (Feature 30)". |
| 19 | **Should a Spotlight slot grant name the moderator who granted it?** `SpotlightSlotGranted` (90) carries the granting moderator as its source, so the awardee's notification shows that moderator's name (and ships their id in `NotificationDto`). | As built (status quo): the source is kept. D4's dedup exemption already applies to 90. | D5 de-identifies the 70–82 moderation band "including the good news" so a name's presence cannot leak an outcome, but D5 scopes itself to 70–82 and 90 sits outside it; whether a reward from a moderator is a moderation act is policy. Rule text: `layer2-services.md` §"Notification Generation" → "Moderation-band de-identification". Filed WU-InertFeatures review fixes, 2026-09-30. |
| 20 | **Two account-status refusals the build derived (service §2.1.3).** May a moderator *warn* someone who is serving a live suspension, and may they *ban* someone already banned? | As built (derived, not owner text): both refused with a `ModerationValidationException`. A warning would lower a live suspension to Warned — the same "silent lowering" §2.1.3 names for warning a banned user — and a second ban would write a second `AccountBanned` (74) notification, since D4 exempts 74 from dedup. Everything else in the table is literal §2.1.3 (Banned leavable only via Reinstate; a suspension needs a future date). **Refined by the WU's review fixes (2026-09-30):** the second-ban refusal holds only on the moderator-initiated path. A *report-driven* Ban on a banned account resolves that report against the standing ban (`ResolvedActionTaken`, 81 to the reporter; no status write, stamp bump or second 74). Refusing it there left every other report about a banned account closable only as "no action" (82) — account actions close no siblings (D7), Warn/Suspend on Banned are refused, and a `User` report cannot be removed — which tells the reporter something false and writes `ResolvedNoAction` into D8's history. | Escalation policy is yours; "allowed" is a one-line change per row. Rule text: `layer2-services.md` §"Moderation Services" → "The account-status transition table".

## Recommended next work units (2026-07-27)

*A full-backlog sequencing recommendation, not a mandate — priorities stay yours to reweigh.
Reanalyzed 2026-07-27 against the current tracker state (unchanged since the 2026-07-24 audit
except for closures already reflected in "Already-closed" there) and the `git log` trajectory
above. Work units cluster by shared surface — the pattern WU-TagFanon/WU-RecLifecycle/WU-GroupsL5b
already proved: one WU per shared file/subsystem beats one WU per tracker checkbox — not by
tracker letter. Full narrative/rationale for every item stays in `hidden-deferrals-tracker.md`;
this table adds sequencing only, it doesn't restate content.*

**Why this order:** the debt-paydown burst (07-24 → 07-27) hasn't run dry — tracker groups A–H
still hold roughly 20 open items, two flagged high-priority security (E2, E3 — both already
deliberately gated to Phase 7/launch, not moved up by this reanalysis). Tiers 1–2 continue that
burst a little further on the cheapest, most-unblocked items rather than context-switching back to
feature work mid-burst; Tier 3 onward is the pivot back onto the Phase 2/3 critical path once it's
done.

| Tier | Proposed work unit | Tracker items closed | Why here |
|---|---|---|---|
| **0** — decisions only, chat, no code | ~~Decision row 2 (homepage sections)~~ **DONE 2026-07-28** | — | Unblocks WU-Home, the last unstarted Phase-2 item |
| **0** | ~~Decision row 13 (`/discover` URL state)~~ **DONE 2026-07-28** | — | Resolved against URL state; see "Resolved" below |
| **1** — already unblocked, no decision needed | ~~WU-AccountEnforcement residual~~ **DONE 2026-07-30** | *(G1's residual)* | Closed Phase 2's last item — see Phase status above |
| **1** | ~~WU-ErrorHandling2~~ **DONE 2026-07-30** | E1 | Unblocked since WU-GlobalFlip (2026-07-13), never picked up since |
| **2** | ~~WU-DiscoveryFilterRestore~~ **DONE 2026-07-28** | B11 | Device-local filter restore + ship seeding parity, shipped same day as decision row 13's resolution — see "Resolved" below |
| **2** | ~~WU-SelectionPermalink~~ **DONE 2026-07-28** | — | Artifact-addressed sharing for public saved selections (`/discover/selection/{id}/{*slug}`); row 13's other half, shipped same day |
| **2** — continue the debt-paydown burst, clustered by shared surface | ~~WU-ApplyFiltersPurity~~ **DONE 2026-07-30** | B12 | Cached `ITagHierarchyReadService` restored `ApplyFilters` to pure/sync — no numbered decision row; resolutions recorded directly in `hidden-deferrals-tracker.md` B12 and `workplan.md`'s DONE entry |
| **2** | WU-StatBadgeProducers (in progress, 2026-07-30) | B3 (partial), B4 | Scoped: builds the Story Acknowledgments feature (`AcknowledgedAsBetaReaderCount`) + a producer hook on the already-built `StoryLineage` approval (`AcknowledgedAsInspirationCount`); re-files `SpotlightCount` under B8 rather than building it. Surfaced a site-wide badge-tier retirement along the way — see Resolved below. |
| **2** | ~~WU-DataSaver~~ **DONE 2026-07-31** | B0 | Measured, not just decided — "suppress sprites, or cut the setting" turned out to have a wrong premise; see "Resolved" below |
| **2** | ~~WU-DiscoveryOverrideUI~~ **DONE 2026-07-31** | B7 | Per-user filter-override editing surface (§8.7) — see "Resolved" below |
| **3** | ~~WU-Home~~ **DONE 2026-07-28** | F1 | Closed Phase 2's last content item; Phase 2 fully closed 2026-07-30 (WU-AccountEnforcement, Tier 1) |
| **2** | WU-A11y (Structure) | F6 (static half) | Decision row 12 resolved 2026-07-31 (sweep by defect class, no fourth test tier, extract `Modal`, Identity in scope). Static/naming pass — labelling, validation association, `Modal` primitive, image `alt`, mechanical gates. Does not need the L4 sweep; sequenced here. See "Resolved" below. |
| **4** — Phase 3 (L4 freeze sweep) | WU-A11y-Keyboard | F6 (keyboard half, filed 2026-07-31) | Focus trap, Escape, combobox ARIA, manual keyboard script. Needs Brian's browser pass — pairs with the sweep as originally scoped. |
| **4** | ~~WU-SweepRiders~~ **DONE 2026-07-31** | H1, E4, H8 | Pulled ahead of the Phase 3 sweep rather than ridden alongside it — H8 was a decision that needed settling before the sweep styles ~1,325 LOC of Identity scaffold, not during it; H1 turned out to need no code change (stale tracker context — verified already-resolved) and E4 was small enough to travel with H8. See "Resolved" below. |
| **5** — beta window | WU-EditorSprite | A1 | Spec'd authoring capability; design the sanitizer allow-list addition alongside it |
| **5** | Decision row 6 (beta logistics) | F5 | Chat-only, shortly before beta opens. **New dependency (2026-08-01):** tracker **B19** — there is no role grant/revoke capability anywhere, so a second moderator can only be created by editing `DataSeeder` and rebuilding the DB. Needs building if beta involves anyone moderating but Brian. |
| **2** | ~~WU-UserModeration~~ **DONE 2026-08-01** | B13 (opens B17/B18/B19) | Filed as `polish · low`; was the tip of an **unusable moderation feature** — nothing could report a User, and `/mod/reports`' Warn control threw for every report that could actually exist, so nobody could be warned, suspended or banned. See "Resolved" below. |
| **2** | WU-NotifEmail (in progress, 2026-07-31) | B1 | **Pulled off the Phase-6 gate 2026-07-31.** The old placement assumed a live audience was the forcing function and that the unchosen email provider was in the way; neither holds. Provider selection is a config switch over plain SMTP (decision row 8's *mechanism* half was resolved at WU-Email) and the whole path is Mailpit-verifiable today, so there is nothing a beta audience teaches that a dev inbox doesn't. Building it before beta also means the settings page stops lying to the first real users. Does **not** close row 8 / F4 — deliverability (provider, sending domain, SPF/DKIM/DMARC) stays at Phase 7. Settled constraints: `audit/Notifications.md` §"Notification email fan-out." |
| **6** — cheap filler, anytime, no phase dependency | WU-PolishSweep | D4, D5, H2, H6 | Code-economy items, 401-mapping cleanup, StoryDeck skeleton, the by-design-gap list |
| **6** | WU-TestHygieneSweep | H3, H4 (manual-verify half) | Cover-art browser-verify, paste-from-Word manual check |
| **6** | *(fold H7 into whichever WU next touches the test suite, or its own pass)* | H7 | The 25 C-consolidate merges are mechanical, no urgency, no dependency |
| **6** | *(standalone, own WU whenever convenient)* | ~~A6~~ **DONE 2026-07-31** (WU-ExploreFilterAxes), A7 | Discovery-adjacent but distinct from Tier 2's cluster (~~Explore filter axes~~; the frozen `DiscoveryMartSchema`'s 7th UNION arm) — heavier lift, `anytime` window, no forcing function. A6 turned out to be a *design* reopen (a settled "not filtered" note) rather than the code task its one-liner implied — see "Resolved" below. A7 unchanged. |
| **6** | WU-L6MeasurePass | C2, C3, C4, C5, C6 | Moved down from Tier 2 (2026-07-30) — Recommendations/Vouches/Messaging/Series/Comments are already Stage 5 across every column and nothing else open touches their query shapes (checked against the full tracker, not inferred from the stage number — Stage 5 means "matches current spec," not "won't change"). No hard gate forces it earlier: Phase 3's freeze only mints Stage 6 on the L4-Style column (`middle_plan.md:106`), so C2–C6's L6=5 claims aren't at risk of being frozen unverified. **Soft floor this tier doesn't otherwise carry: finish before Phase 6 Beta opens** — Recommendations/Comments/Messaging/Vouches take real concurrent traffic there, the exact paths C2–C6 flag as unindexed, and re-measuring/re-indexing after the fact is cheap but discovering the gap via slow beta pages isn't a good look. |

**Deliberately not reordered** — each already has an explicit gate and a stated reason; this
reanalysis found no case for moving any of them up: A8 (post-mvp-mobile), B8 (post-beta), B9/B10
(launch), E3/E5 (launch/post-launch), E6 (pre-launch — no rate-limit abuse signal exists yet), F8
(post-launch). F2/F3/F4/F7 are already tracked above via the Decisions table and the Phase 7
checklist, not re-listed here. H4's PDF-import half stays a deliberate future-format candidate.

**E2 (AngleSharp CVE) flagged, not moved.** Tracker priority is `high`, but the risk is accepted
and mitigated (the 13-tag/`href`-only allow-list strips the attack vector) with a concrete Phase 7
root-cause fix already designed. Leaving it at Phase 7 is a decision worth your explicit
re-confirmation given the `high` label, not a silent default this reanalysis is making for you.

Group G is fully closed (2026-07-30) — G1's genuine residual (mid-session account-status
responsiveness, tracked separately as the WU-AccountEnforcement Tier-1 row above) shipped
2026-07-30; nothing left to sequence.

## Resolved

- **TPT FK posture (worksheet D10) — answered 2026-08-06, built WU-TptHardDelete 2026-09-30.** No
  decision row: it came out of the schema audit's §2.2 and the service audit's §2.2. Three blog-post
  delete sites orphaned `base_comments`/`base_polls` rows, because no cascade from a content parent can
  reach a TPT base row. Ruling: fix every site through one shared set-based helper (`TptDelete`, raw SQL
  keyed off the child table, never materialized), flip the five content-parent → TPT-child FKs to
  RESTRICT as the guardrail, **no** scheduled orphan sweep (declined, not deferred), and TPH stays
  closed on warm/cold read-shape grounds. Built with the adjacent group-post lifecycle defect: per-subtype
  update/delete methods, and the profile methods return 404 for group and site ids instead of a 500.
  Rule: `layer1-data-model.md` §"Hard-deleting a content parent"; `layer2-services.md` §"Hard deletes
  of content parents". Narrative: `audit/BlogPosts.md` F35, `audit/Comments.md` F23, `audit/Groups.md`
  F40. Still open: D43 (group-post rating on update), D47(b) (a group-delete path), tracker F16
  (`BlogPostsWritten` for group/site posts) and B25 (no blog-post delete UI).

- **Poll-owner delete behavior (worksheet D11) — answered 2026-08-06, built WU-TptHardDelete
  2026-09-30.** No decision row: it came out of the schema audit's §2.5. `base_polls.owner_id` is
  nullable `ON DELETE SET NULL`, matching `base_blog_posts.author_id`; a NULL owner is nobody in every
  ownership check. Rule: `cross-cutting.md` §"Delete Policy Summary"; `layer1-data-model.md`
  §"Relationships & Queries". Narrative: `audit/BlogPosts.md` F37. Still open: D42 (whether an
  ownerless or archived poll stays votable).

- **Sibling-report auto-resolution (worksheet D7), the report reported-user column + takedown-reversal
  ratification (worksheet D8, with its 2026-08-08 seventh-type amendment) and mod-only read gating
  (worksheet D9) — answered 2026-08-04/05/06, built WU-ModerationIntegrity 2026-09-30.** No decision
  rows: they came out of the service-layer audit's §3.8/§3.9/§2.4.4 and the schema audit's §3.7.
  - D7: `ResolveWithRemovalAsync` closes every other open report on the same `(type, id)` target in
    the same transaction, as `ResolvedActionTaken`, notifying every distinct sibling reporter; the
    counter moves by −(1 + rows closed); removal only. `ActiveReportCount` is defined as a cache of
    the open-report `COUNT(*)`. Landed with service §2.1.2's status guards (lock and guard) and
    §2.4.4's per-(reporter, target) partial unique index. **Delegated pick:** reports orphaned by
    account deletion are closed **at the source** (`UserDeletionService`, via
    `ReportLedger.CloseForDestroyedTargetsAsync`), not handed to a reconciler — D13's
    "clean up at the source" principle; the reconciler recomputes counts, not statuses. Author
    self-delete sites stay open (tracker F13).
  - D8: `Report.ReportedUserId` — the answerable account at filing time, every target type, snapshot,
    SET NULL; the per-user history reads it (tracker B18 closed, caveat deleted). D8(b): the entity
    carries current state, the report carries history; no takedown-history table; reversal (not yet
    built) nulls the metadata and reopens nothing. The `Group` arm is WU-UserDeletion's (D13).
  - D9: every mod-only read gates in the service with the shared `RequireModerator()` extension (all
    private guard copies removed); `IReportSubmissionService` split from the mod interfaces. **Delegated
    pick:** the 401-instead-of-404/400 class is fixed at the throw sites (`KeyNotFoundException`,
    `ModerationValidationException`, new `ExternalVerificationValidationException`); the
    `EndpointHelpers` table is unchanged (`InvalidOperationException → 401` stays the auth safety net).
  - Also built from service §2.1.3: suspend-date validation, the transition table, and **Reinstate**.
    Two derived refusals are decision row 20.
  Rule: `layer2-services.md` §"Moderation Services"; `identity-and-authorization.md` §"Role-Based
  (Moderator) Gating"; `security.md` §"Account-Status Enforcement". Narrative: `audit/Moderation.md`
  F46/F47 (Settled notes and Stage notes). Still open: D47 (Group-typed report actions).

- **Recommendation provenance home (worksheet D3) — answered 2026-08-04, built WU-InertFeatures
  2026-09-30.** Had no decision row: it came out of the 2026-08-03 service-layer audit's §3.2, which
  proposed decoupling the attribution partition from the USI row because the shipped `?rec=`-on-load
  write FK-failed. Ruling: the coupling is correct and the fork is void — the fault was a missing
  producer (the 2025-specified Read It Later button on the recommendation card). The attribution is
  metadata on the `IsReadItLater` bit; two entry points (card RIL, and a direct link persisted at the
  Ch.1 ≥90% moment); five removal triggers; a DTO-returning prompt with Yes/X only; `RecordSuccessAsync`
  requires and consumes the sources row; cascade (not RESTRICT) on the recommendation FK. Rule:
  `layer2-services.md` §"Attribution (Feature 30)". Narrative: `audit/Recommendations.md` F30,
  `audit/UserStoryInteractions.md` F16. Unruled edge left open: decision row 18.

- **System/self-sourced notifications (worksheet D4) and moderation-notification de-identification
  (worksheet D5) — answered 2026-08-04, built WU-InertFeatures 2026-09-30.** Had no decision rows:
  both came out of the service-layer audit's §3.3/§3.4. D4: `CreateCoreAsync` takes a nullable source
  (null = no actor); drop-self is conditional; `ReportReceived` is restored; 70/80/81/82 carry the
  report id and 72/73/74/76/77/90 are exempt from cross-existing dedup; `related_entity_id` widened to
  `bigint` on the same migration; two nulls are told apart by type, never by column. D5: the whole
  70–82 band is null-sourced, good news included, and — D5's routed sub-edge, taken per the owner's
  recommendation — so is the tag-adoption invitation (26). No band method takes a moderator id, and
  (review fixes, 2026-09-30) every band call site skips the acting moderator as a recipient — the
  D4 guardrail generalized. Type 90's attribution stays open as decision row 19. Rule:
  `layer2-services.md` §"Notification Generation"; threat model in `content-safety.md`
  §"Notification Loop". Narrative: `audit/Notifications.md` F41, `audit/Moderation.md` (cluster
  Settled note).

- **Group fan-out `RelatedEntityId` (worksheet D16) and hidden-favorite fan-out membership (worksheet
  D17) — answered 2026-08-07, built WU-InertFeatures 2026-09-30.** D16: `RelatedEntityId` names the
  single most specific entity of the event — for a group add, the `GroupStory` junction row — and a
  second id column is never added; the re-point backlog is recorded as a conformance list (tracker
  B23), not built. D17: a hidden favorite suppresses public-plane consequences only, so type 15 goes to
  `IsFavorite || IsHiddenFavorite`, and the author-plane type 20 must not fire for a hidden favorite.
  Rule: `layer2-services.md` §"Polymorphic RelatedEntityId" and §"Comment & blog-post semantic
  methods". Narrative: `audit/Notifications.md` F41, `audit/Groups.md` F39. The new-chapter fan-out
  that both rulings fed was built in the same WU (its unpublished-story default is decision row 17).

- **Visibility gating: raises vs clears (worksheet D6) — answered 2026-08-04, built
  WU-AccessGateSweep2 2026-09-30.** Had no decision row: it came out of the 2026-08-03 service-layer
  audit's §2.6/§3.5, which found the full visibility guard running on *clears* — a mature-off reader
  could not un-favorite an M story, and mark-unread was refused on a taken-down one. Ruling: a flag
  raise keeps the full parent-visibility guard; a clear or lower on the caller's own existing row is
  always permitted, on all three axes (rating, status, takedown); load first, diff, then decide, with
  no row + all-false staying a silent no-op. The D6-mandated enumeration also moved three guarded
  unlike paths (blog post, comment, recommendation) and — in the WU's review fixes — poll-vote
  withdrawal, and guarded one unguarded raise (`SetReceiveAlertsAsync(true)`). A clear's *response*
  stays gated (an unlike on a hidden parent returns no count; a withdrawal from a hidden poll returns
  null). Built in the same WU, from first principles rather than an owner
  sentence: **profile blog posts by id respect the author's `ProfileVisibility`** (blog posts are
  profile-tab data — `access-gating-first-principles.md` §5 row 1b, the F15 permalink precedent),
  and **`AllowProfileComments` is enforced in the comment write service**, not only by the profile
  page. Rule: `identity-and-authorization.md` §"Parent-visibility guards" → "Raises vs clears";
  `layer2-services.md` §"`AllowProfileComments` Gate". Narrative: `audit/AccessGate.md` F66,
  `audit/UserStoryInteractions.md` F16. Sub-edges left for the owner: tracker **F10**; the §2.6 slice
  waiting on worksheet D25/D26 (copy path, series and custom-list by-id reads, `CloneListAsync`):
  tracker **F11**.

- **Story approval queue: mandatory for first submissions only (worksheet D1) — answered 2026-08-04,
  built WU-StoryLifecycle 2026-09-30.** Had no decision row: it came out of the 2026-08-03 service-layer
  audit's §3.1, which found the code holding both positions at once (a queue and approve/reject
  machinery, but nothing server-side stopping an author from self-publishing or un-rejecting). Ruling:
  the queue exists for spam prevention only and gates an author's **first** submission; an author with
  one approved story and an unrevoked `CanAutoApprove` publishes directly. A server-side transition
  table is enforced regardless of policy. `Rejected` is reachable only from `PendingApproval`;
  unpublish is a legal author move that re-enters the gate (which the waiver then skips). The approval
  counter is monotonic, and any moderator may revoke the flag (the grant is automatic). *Not part of the
  ruling:* moderator **restore** of the flag was added by WU-StoryLifecycle itself, because a one-way
  moderator lever is service audit §2.1.3(b)'s "irreversible in-app" defect class. Rule: `layer2-services.md`
  §"Story Lifecycle" (plus §"Records of a decision are not counters" and the auto-approve clause under
  §"Account actions"). Narrative: `audit/Moderation.md` F48, `audit/Stories.md` F4.

- **Published-date semantics (worksheet D2) — answered 2026-08-04, built WU-StoryLifecycle
  2026-09-30.** `stories.published_date` is nullable again, and **NULL = never published on this
  site**. It is stamped on the first transition into the published set and never re-stamped
  (republication is not a publication event). The chapter anchor is chapter-level:
  `Chapter.FirstPublishedDate`, stamped once and never moved, is the "New"-badge input and the
  new-chapter fan-out anchor. `ChapterContent.PublishDate` is nullable per-version provenance that no
  discovery surface reads. Imports never backdate. Rule: `layer2-services.md` §"Story Lifecycle";
  L8 sourcing in `layer8-data-marts.md` §`site_daily_stats`. Narrative: `audit/Chapters.md` F6/F7,
  `audit/Stories.md` F5.

- **Moderator reach over user accounts — resolved 2026-07-31 (Brian-ratified, tracker B13 /
  WU-UserModeration, built 2026-08-01).** Had no decision row: it surfaced as a `polish · low`
  tracker line about a dead route parameter, and the question underneath it — *may a moderator act
  on a user who was never reported?* — had never been asked. Three sub-decisions:
  1. **Yes, and via a mod-filed report, not a new audit table.** The `Report` row *is* the audit
     record (`ModeratorUserId`/`ActionTaken`/`DateResolved` live on it), so a moderator acting
     without a member report opens and resolves one in the same unit of work, marked by
     `ReporterUserId == ModeratorUserId`. Rejected: a separate `ModerationAction` entity — cleaner
     conceptually, but it forks the audit trail across two surfaces and buys nothing the `Report`
     row doesn't already carry. Cost: no new entity, no migration, no synthetic reason row.
  2. **An account action targets the reported content's author**, not only a User-report's target.
     Supersedes WU34's User-target-only rule, under which `/mod/reports`' Warn control threw for
     every report the app could actually produce.
  3. **`/mod/users` is lookup + per-user history**, `/mod/reports` carries the full
     Warn/Suspend/Ban set. This is what made the `{UserId:int?}` parameter live rather than deleted.

  **Why it mattered more than its label:** the whole account-action capability was unreachable —
  nothing in the app could report a User, so the page that lists user reports was permanently empty,
  and the one control that could act on a content author always failed. Nobody could be warned,
  suspended, or banned on a UGC site heading into beta. Rule: `layer2-services.md` §"Account
  actions — target resolution and the report-as-audit-record rule". Pointer:
  `hidden-deferrals-tracker.md` B13/B17/B18/B19; `workplan.md` WU-UserModeration;
  `audit/Moderation.md` Features 46/47. Opened **B19** (no role grant/revoke capability exists
  anywhere — moderators can only be minted by editing the seeder), which is a **decision row 6
  dependency**: recruiting a second moderator for beta needs it.

- **Manual-tree-search candidate-pane filtering — resolved 2026-07-31 (Brian-ratified, tracker
  A6 / WU-ExploreFilterAxes).** Had no decision row: it lived as a *settled convention* in
  `layer3.5-structure.md` ("Explore and Deep Dive are **not** filtered by
  `TagFilter`/`UserStoryInteractionFilter`"), which made taking A6 up a reopen rather than a build.
  Adjudicated as a WU40 **scope cut, not a design objection**, and split three ways:
  1. **Explore, user anchors only — built.** Tag (+ roll-up) / ship / interaction-exclusion axes.
     Story anchors get nothing: none of their sections is story-valued, so a filter there is inert
     or blanks the pane.
  2. **Deep Dive — permanently unfiltered.** Now a standing design rule, not a deferral: filtering
     would silently drop links out of chains whose ≤5/≤1 boundedness is the whitelist's entire
     promise.
  3. **Filter persistence — deliberately not built.** Session-only, so the persisted-tree contract
     (IDs + edges only) stays untouched; a durable version rides WU40's already-deferred
     "saved trees" decision.

  Stated in `canalave-conventions/layer3.5-structure.md` §"Filter-Axis Component Pattern" (which
  also now records the Apply-batching split: breadth axes batch, edge toggles don't) and
  §"Explore tab"; narrative in `audit/Discovery.md` F33.

- **Notification-email send path and unsubscribe mechanism — resolved 2026-07-31 (Brian-ratified,
  during WU-NotifEmail planning).** Two constraints that had no decision row because the WU was
  parked at the Phase-6 gate:
  1. **Write-behind worker, not inline send** — supersedes `audit/Notifications.md`'s own
     2026-07-06 "build the inline version first and measure" note. ~22 seeded notification types
     default `EmailEnabled = true` and several fan out to every follower of a story or author, so
     inline sending would put N SMTP round-trips inside a SignalR circuit write. That is a
     known-shape problem, not an open empirical one.
  2. **RFC 8058 one-click unsubscribe** — `List-Unsubscribe` + `List-Unsubscribe-Post` headers over
     a Data-Protection-signed anonymous endpoint, plus a visible footer link. Retrofitting
     unsubscribe headers after mail is already flowing means re-touching every template, and bulk
     senders without them get spam-foldered.

  Both now stated in `audit/Notifications.md` §"Notification email fan-out" (settled constraints) and
  `canalave-conventions/layer2-services.md` §"Notification Generation". **Decision row 8 / tracker F4
  remain open** — provider, sending domain, and SPF/DKIM/DMARC are deliverability, not code.

- **Decision row 12 (accessibility scope/depth) — resolved 2026-07-31.** Sweep **by defect class,
  not by page**: the addendum's original four-page framing (search, story page, chapter reading,
  signup/login) would have missed most of the actual defect concentration — the 43 orphan `<label>`
  elements sit in authoring forms (`StoryPropertiesForm`, `ChapterPropertiesForm`, `PollEditorForm`,
  `GroupCreateEditPage`) none of which are in that list, and `ConfirmDialog`/`ReportDialog` appear
  on no single page. **No fourth test tier** — extend the existing bUnit `RazorComponents` tier +
  add mechanical gates (`scripts/check-a11y.ps1`) instead of axe-core/Lighthouse-CI. **Extract a
  shared `Modal` primitive** — the `layer3.5-structure.md` deferral note's own trigger ("until a
  third consumer's shape clarifies what the shared part actually is") had fired: 9 overlay sites
  existed by the time this was decided. **`Server/Identity/` in scope.**
  **Split into two work units, same day:** WU-A11y (Structure) lands everything statically
  verifiable — naming/association ARIA, never an interaction promise the code doesn't keep yet —
  and ships with full mechanical gate coverage. WU-A11y-Keyboard (focus trap, Escape, combobox
  ARIA, the manual keyboard script) stays paired with the Phase-3 L4 sweep, since axe-DevTools
  cannot test keyboard behavior at all and Feature 65's L4.5-Browser cell can't legitimately move
  without Brian's own browser pass. Detail: `audit/Accessibility.md`.

- **WU-SweepRiders — H1/E4/H8 closed (2026-07-31).** Pulled ahead of the Phase 3 L4 sweep (see
  Tier 4 above) rather than ridden alongside it, since H8 was a decision blocking whether the
  sweep needs to style the Identity 2FA/passkey/external-login scaffold at all.
  - **H8 (MA-610 prune-vs-keep) — keep, across all three flows.** Brian intends to support them
    going forward. Corrected the entry's own over-generalization along the way: only external
    login is provider-dependent (its `ManageNavMenu` entry is already conditional); TOTP 2FA and
    passkeys are functional today with no external config. No code changed. Opened a genuine
    follow-up rather than treating "keep" as "verified": `hidden-deferrals-tracker.md` **H9** — none
    of these flows has ever been driven end-to-end. Detail: `modernization-audit/deferred-work.md`
    §2 MA-610 (updated in place).
  - **H1 (`Error.razor` mismatch) — closed with no code change; the tracker entry was stale.**
    MA-110 (2026-07-18) had already fixed the plaque/vessel content this entry described. The one
    thing left to check — whether `/Error` gets any layout — turned out to already be yes:
    `AuthorizeRouteView`'s `DefaultLayout="typeof(MainLayout)"` ambient default wraps it in the
    real site chrome even though Error.razor has no `@layout` of its own and the Server assembly
    is excluded from `Routes.razor`'s Router (that exclusion governs client-side SPA-navigation
    matching, not which layout a statically-routed SSR endpoint gets). The wire status code is
    already correctly 500. Verified live via a forced-throw endpoint under a non-Development
    environment, `curl` + browser. Detail: `error-handling.md` §"The `/Error` HTML path";
    `design/surface-registry.md` §"Sweep completion" (corrected — Error.razor was never actually
    the remaining-open item it claimed).
  - **E4 (default OG image is SVG, not a raster) — built.** A real 1200×630 PNG
    (`wwwroot/img/og-default.png`) replaces the `default-cover.svg`/`default-avatar.svg` fallback
    across all eight `og:image`/`twitter:image` call sites, behind one new shared constant
    (`TheCanalaveLibrary.Core.SeoDefaults.OgFallbackImagePath`) rather than a literal repeated
    across seven files. The in-page `<img>` placeholders are untouched. The asset is an
    AI-generated placeholder (ImageSharp render from the site's design tokens) carrying a visible
    "AI-generated placeholder — replace before launch" caption, at Brian's request, so a real
    branded asset is still owed before launch. Detail: `audit/Seo.md` (Open item moved to
    Resolved); `hidden-deferrals-tracker.md` E4.

  Verified: `dotnet build`/`dotnet test` green; H1/E4 needed no new automated coverage (static
  markup + a constant swap); browser-verified `/Error` and the OG tags on six page types via
  prerendered-HTML `curl` (crawler-equivalent fetch). Full record: `workplan.md` WU-SweepRiders.

- **WU-DiscoveryOverrideUI — §8.7 per-user filter-override editing UI built, `UserCustomFilter` cut
  (2026-07-31, closes tracker item B7).** The read/merge half (`IDiscoveryDefaultsReadService`) had
  shipped since WU28 with no way for a user to ever change their per-search-mode defaults — every
  viewer was permanently stuck on the seeded matrix, adjustable only per-browser via
  `DiscoveryFilterStore`'s localStorage seam. Three decisions settled (Brian-ratified, do not
  revisit):
  - **Surface: a `/settings` section** (`DiscoverySettingsForm.razor`), not an inline
    `ResultsFilterPanel` affordance — `audit/Discovery.md`'s own earlier guess is superseded.
    `NotificationSettingsPage` is the precedent: instant-save per toggle, sparse upsert/delete, no
    Save button. New self-referential `IDiscoveryFilterSettingsService` (mirrors
    `INotificationWriteService.SetSettingAsync`'s contract exactly), deliberately kept separate
    from the anonymous-callable `IDiscoveryDefaultsReadService`.
  - **`UserCustomFilter` cut entirely**, not merely trimmed. It's bidirectional (group whitelist —
    "only search my groups" — as well as blacklist), so the 2026-07-13 Custom Lists ethics argument
    only ever cleared the `PersonalList`/`PublicList` half; the surviving group/folder half is
    simply unbuilt, unrequested, and undesigned. Six columns, trivially re-addable. Retires the last
    of spec §8 row 7.
  - **The two inert discovery `ReaderSettings` are wired**: `DefaultPaginationSize` replaces
    `SearchPage`'s hardcoded `RandomBatchSize = 20`; `DefaultSearchSort` seeds the initial sort,
    with a validity clamp (only `DatePublished` besides Random — `ReaderSettingsForm`'s dropdown
    offers every `DefaultSortOrder` with no per-surface restriction, so `Relevance`/`Score`/
    `RecentlyRead` fall back to Random rather than being misapplied). `CollapseCommentThreads` was
    found adjacent but deliberately **not** wired — Comments has no collapse behavior to hook into
    at all, and that's a design call for Brian to make from using the site, not a Claude session to
    pre-empt; opened as tracker **B15**. A second adjacent gap was found and deliberately **not**
    fixed in the same WU: `ResultsFilterPanel.ApplyAsync` hardcodes `PageSize = 20` for sorted-mode
    pagination across all its consumers — a genuine cross-cutting fix, not a SearchPage-local one,
    opened as tracker **B16** rather than folded in as a point fix.

  Built the same day: `dotnet build`/`dotnet test` green (Unit 776, RazorComponents 635, Integration
  1021 — 2,432 total). Migration `DropUserCustomFilter` applies cleanly to a fresh database.
  Pointer: `hidden-deferrals-tracker.md` B7/B15/B16; `workplan.md` WU-DiscoveryOverrideUI;
  `audit/Discovery.md` §"WU-DiscoveryOverrideUI Stage note"; `audit/Profiles.md` Feature 20;
  `layer2-services.md` §8.7.

- **WU-DataSaver — `PrefersDataSaverMode` cut, image derivative sizing opened as B14 (2026-07-31).**
  Tracker item B0 framed the choice as "suppress sprites, or cut the setting." Measurement before
  building showed that framing was wrong on the numbers, not just under-specified: sprites render
  at 16px across 3 sites in low-KB static PNGs, and the one sprite saving that was ever material —
  animated `.webp` → static `.png` — is already delivered by `PrefersAnimatedSprites = false`, so
  "suppress sprites" could never have made the checkbox's "(reduces image quality)" promise true.
  The actual weight is cover art/avatars: `ImageUploadProcessor.MaxStoredDimension = 2048` stores
  exactly one size per upload, served into 24–144px display slots on 20-item listing grids (up to
  ~200×–7,000× more pixels than displayed) — no `srcset`/thumbnail mechanism exists anywhere in the
  app. That's a real gap, but it helps 100% of traffic with no opt-in and no visual downgrade once
  built, making a user-facing toggle nearly redundant — so it's opened as its own item (tracker
  **B14**) sequenced with Phase 7's R2/Cloudflare work above, not built as a ride-along here.
  **Settled: `PrefersDataSaverMode` is removed end to end** — Core entity, `UserSettingsDto`,
  `IUserSettingsService`/both impls, the `/api/user-settings/appearance` endpoint, the
  `AppearanceSettingsForm` checkbox, `SeedTool`'s binary-COPY column, and a real `DropColumn`
  migration (`20260731152702_DropPrefersDataSaverMode` — a hot scalar column, not jsonb, so unlike
  `RemoveAutoLoadNextChapter` this is genuine DDL). No grid cell flips (F20/F21/F22/F3 all stay
  Stage 5 — removing an inert setting doesn't change any layer's soundness). Built the same day:
  `dotnet build`/`dotnet test` green (776 Unit + 626 RazorComponents + 1,012 Integration = 2,414,
  unchanged), migration applied and column absence confirmed against local Postgres, `SeedTool` run
  verified by value (not just success — a positional-COPY misalignment fails silently), endpoint
  round-trip confirmed live. Pointer: `hidden-deferrals-tracker.md` B0/B14; `workplan.md` WU-DataSaver;
  `audit/Profiles.md` Feature 20 Stage note; `audit/Sprites.md` Feature 3 L3-Logic note.

- **Badge tier paradigm retired site-wide (2026-07-30, WU-StatBadgeProducers).** Scoping B4 (the
  BetaReader badge) surfaced that the Bronze/Silver tier model (`Recommender`@10 /
  `RecommenderSilver`@50, WU36) has no design provenance — traced to a single unrequested Gemini
  transcript turn (Entry #1577, 2025-10-25 11:59, in response to a pure document-transcription
  request) whose own column headers read "Badge Name (Suggestion)" / "Tiers (Example)". An identical
  synthesis run four minutes earlier over the same source produced zero tier data, and the tiers are
  never revisited or affirmed anywhere else in the ~75,000-line corpus — the same shape as the
  retired `AutoLoadNextChapter` feature (tracker A2). **Settled: a badge is earned at ≥1 and displays
  its count; `RecommenderSilver` is retired outright** (pre-production, so removal is a clean seed-row
  + constant + literal deletion, no data migration). Anti-farm protection moves from the threshold to
  the *gate* — every badge built under this model requires another person's cooperation per increment.
  Rule now stated in `layer2-services.md` §"Synchronous Inline Badge Awards"; provenance record in
  `audit/Badges.md` §"Tier paradigm — RETIRED site-wide"; `RecommenderSilver` added to
  `scripts/check-doc-hygiene.ps1`'s retired-name registry. **Build in progress** — this entry records
  the decision; `workplan.md`'s WU-StatBadgeProducers entry carries build/verification status.

- **Decision row 13 — `/discover` URL state round-tripping (2026-07-28).** **`/discover` never
  carries filter state in its URL.** The row's original framing ("follow `TreeSearchPage`'s
  pattern") was itself wrong and is superseded: `TreeSearchPage` carries *control* state
  (`?degrees=2&sort=…` — small legible scalars), which is not precedent for serialising arbitrary
  id lists. No surface in this codebase has ever done that; every addressable surface uses clean
  paths (`/story/{id}/{slug}`, `/user/{id}/tag-selections`). Three separable calls settled:
  - **Sharing is the artifact's job.** A public `SavedTagSelection` gets a permalink,
    `/discover/selection/{SelectionId:int}/{*Slug}`, following the story-slug contract exactly —
    **the id is the source of truth, the slug is a decorative tail that is never parsed** (so no
    slug column, no migration, and renaming a selection never breaks a link). It lands the visitor
    in `SearchPage` pre-seeded and fully editable. Requires a new anonymous-callable read gated on
    **both** `IsPublic` **and** the owner's `ProfileVisibility` (Class A —
    `design/access-gating-first-principles.md`). → WU-SelectionPermalink.
  - **Return integrity is device-local, not a URL and not server state.** `/discover` restores the
    last-applied filter through the ratified localStorage seam (`layer3.5-structure.md` §"The
    shared tree canvas": ids only, display data rehydrated via the existing batch reads, entities
    the viewer can no longer see pruned silently). `[PersistentState]` is **not** usable for this —
    `error-handling.md` rejects it as prerender-handoff-only, and B11's own sketch was wrong to
    propose it. → WU-DiscoveryFilterRestore.
  - **Ships get seeding parity only.** `Initial*` params + re-seed + dispatcher-resolved display
    names. Ships stay non-shareable and non-persisted; F15's tag-axis-only scope is untouched.
    Making ships shareable would need new L1 child tables, because `SavedTagSelectionEntry` is a
    flat `(SelectionId, TagId, IsExcluded)` row unique on `(SelectionId, TagId)` while a ship is a
    *group* — flattening it degrades to co-presence, which WU-TagFanon ruled **is not a ship**, and
    the unique constraint forbids one character appearing in two ships. Recorded as the known cost
    of a future decision, not a silent deferral.

  Conventions now stating the rules: `layer2-services.md` §"Saved Tag Selections Persist Only the
  Tag Axis" (permalink ≠ saved query; artifact vs. device-local restoration) and
  `layer3.5-structure.md` §"Seed state vs. live fetch in filter components".

  **Built the same day.** Both WUs shipped 2026-07-28, not merely decided — `dotnet test` green
  (2,330), browser-verified end to end (filter+ship restore across a navigation, permalink follow,
  stale slug, missing id, anonymous view, owner-turned-Private no-leak check). Full record:
  `workplan.md` §"WU-DiscoveryFilterRestore + WU-SelectionPermalink"; `audit/Discovery.md`
  §"WU-DiscoveryFilterRestore + WU-SelectionPermalink note"; `audit/Tags.md` §"WU-SelectionPermalink
  Stage note".

- **Decision row 2 — Homepage design (2026-07-28, WU-Home).** The home page is the community
  page: a focused surface, not a broad discovery one. Composition: a "Welcome" mission blurb
  (expanded when Spotlight is empty, collapsed once a spotlight is live) → Community Spotlight →
  the active SitePoll inline when one is open → a community-discourse link cluster (Polls, Fanon,
  Spotlight explained, Site News) surfacing what the persistent nav/UserMenu deliberately omit.
  Deliberately excluded: any story discovery (Recently Updated and a random draw were both
  considered and rejected — spec §5.3.3's "no sort by last updated" reasoning and the "focused
  purpose, not broad" framing) and any personalized/signed-in strip (Continue Reading/follows/
  bookshelves are already one click away via NotificationBell/UserMenu). This decision's only
  stated purpose for `GetRecentListingsAsync`/`GET /api/stories/recent` is gone, so both were
  removed as dead code rather than left in place. Surfaced a second gap along the way — no
  site-announcement channel — closed
  by the companion WU-SiteNews (`SiteBlogPost`, extends Features 35/36) on the same date. Detail:
  `audit/BlogPosts.md`, `audit/Seo.md`.

*(Empty as of 2026-07-27 — this file was just created; the entry above is the first addition.
New entries go here going forward, newest first, each pointing at the doc that now states the
rule, same convention as the retired chain. The full historical Resolved index — every decision
from 2025's design sessions through 2026-07-27 — lives in `middle_plan_v2.md` §Resolved.)*
