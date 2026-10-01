# Audit — Moderation/

**Features:** 46 (reporting), 47 (queue & actions), 48 (approval workflow), 53 (import & verification),
62 (SiteDailyStat worker). Routes `/mod/reports`, `/mod/submissions`, `/mod/users`. Desktop-only, **no
dispatcher pattern** (§3.10).

## Shared Context
**Entities (Core/Moderation/ — relocated in WU34):** `Report` (`ReportReasonId`/`ReportStatusId` Restrict,
polymorphic `ReportedEntityType`→short + `ReportedEntityId` widened to **long** in WU34 migration,
`DateReported` default, `(ReportStatusId)` and `(ReportedEntityType, ReportedEntityId)` indexes added),
`ReportReason`/`ReportStatus` (seeded, Restrict delete). **`StoryImport` stays in `Core/Models/` until WU39.**
`Story.ActiveReportCount` for mod-triage ordering (not auto-hiding — see WU34 settled decisions).
**No services or components built prior to WU34.**

**Schema additions in WU34:** `Report.ReportedEntityId int→long`; `ReportedEntityType` +`Message = 5`;
soft-delete columns on Story/BaseComment/BaseBlogPost/Recommendation (renamed in pre-integration cleanup
2026-06-26: `IsTakenDown bool`, `TakedownDate DateTime?`, `TakedownReason string?`; formerly
`IsHidden`/`DateModeratedRemoved`/`ModerationRemovalReason`); `User.AccountStatus` + `SuspendedUntilUtc` +
`ActiveReportCount`; `NotificationType` seed for `StoryApproved = 75` (category `YourStories=2`, deep-link
`KindFor → Story`).

**Not EF entities:** `SiteDailyStat` (PK `StatDate`) is a raw-SQL data mart (no `DbSet`, no migration —
see Feature 62 below); `DailyStoryStat` was dropped entirely, never modeled.

**Settled — the moderation notification band, F46/F47/F48/F53 (owner rulings D4 + D5, answered
2026-08-04, consumed WU-InertFeatures 2026-09-30; do not revisit).** Rule text: `layer2-services.md`
§"Notification Generation"; `content-safety.md` §"Notification Loop (§13 Transparency)".
- **De-identified, all of 70–82, good news included** (plus tag-adoption 26): no moderator id or name
  reaches the recipient; no `INotificationWriteService` method in the band takes a moderator id. The
  `Report` row (and `ReviewedByModeratorUserId` on verification rows) is the internal ledger.
- **`ReportReceived` (80) is restored** — it was annihilated by drop-self because the submit path
  passed the reporter as their own source. It now fires null-sourced with the report id.
- **Report ids, not zeros:** 70/80/81/82 carry `Report.ReportId` (two removals or two resolutions for
  one user no longer collapse to one unread row — spec §5.21 "reporters always learn the outcome").
  72/73/74/76/77 (and 90) are exempt from cross-existing dedup.
- **Guardrail:** the moderator-initiated account action (`ApplyAccountActionToUserAsync`) never sends
  80 or 81. *Routed, not built here:* the report-driven `ApplyAccountActionAsync` sending 81 to a member
  reporter is a report-lifecycle defect owned by WU-ModerationIntegrity.
- **The acting moderator is never a recipient of their own act** (D4's guardrail generalized,
  WU-InertFeatures review fixes 2026-09-30). Null-sourcing gave up drop-self, so every band call site
  skips the acting moderator explicitly: no 81/82 for a report they filed and resolved, no 70 for their
  own removed content, no 75/71 for their own story, no 76–79 for their own account or link, no 72–74
  for a report-driven action on themselves, no 26 for a fanon name they used. This restores what
  drop-self did before D5. 80 is the exception: filing is the member's own act, and D4 restored that
  receipt on purpose.
- **Attribution sweep (D3 trigger 5):** a Recommendation takedown through the removal path deletes
  every attribution row naming that recommendation (`layer2-services.md` §"Attribution (Feature 30)").

## Feature 46 — Content Reporting

**Stages (updated 2026-09-30, WU-ModerationIntegrity browser verification):** L1–L3.5 = 5, L4 = 3,
L4.5 = 5, L5 = 5, L6 = 5. L1 stays 5 with the WU_ModerationIntegrity migration applied. L4.5 went 5→1
at the review fixes (the duplicate refusal and the WASM rate-limit message changed undriven) and back
to 5 at the browser pass the same day (tracker **H19** closed; note below).

**WU-ModerationIntegrity browser verification Stage note (2026-09-30) — F46 L4.5 1→5.** `ReportDialog`
on both render phases, `psql` after every submit:
- **Duplicate:** ReaderGamma, whose seed report on story 5 was still open, reported it again from
  `/discover`. The dialog read "You've already reported this — a moderator will review your open
  report." on the circuit (no `/api` call) and on WASM (`POST /api/moderation/reports` 400). No row.
- **A different user still can:** LurkerDelta (WASM) and AuthorAlpha (circuit) reported story 5;
  TestUser, ReaderGamma and AuthorBeta reported a comment through `CommentSection`'s dialog. Each got
  one row with `reported_user_id` = the author, counter +1 and one 80 receipt. Reasons loaded through
  `IReportSubmissionService` on both phases.
- **Throttle (WASM):** LurkerDelta's sixth report inside the 5-burst `Report` bucket read "You're doing
  that a little too fast — please wait 180 seconds and try again." (429), not the generic error; five
  rows, none for the sixth.
Detail and the rest of the pass: F47's browser-verification note.

**WU-ModerationIntegrity review-fixes Stage note (2026-09-30) — F46 L4.5 5→1.** The build kept L4.5 at
5 on the grounds that the dialog's markup was unchanged; the review pointed out that its behavior
changed undriven, which is what L4.5 records (`grid_axes.md`: "driven in a real browser and behaves as
its audit file intends"). Lowered until H19's pass; nothing else in F46 changed.

**WU-ModerationIntegrity Stage note (2026-09-30) — no flip at the build (owner rulings D8/D9, service
§2.4.4); L4.5 lowered by the review fixes (note above).**
- **Interface split (D9):** `IReportSubmissionService` (reasons + submit) is its own interface;
  `ReportDialog` injects it. `ServerModerationWriteService` implements all three moderation interfaces
  and is registered once with three forwards; `ClientReportSubmissionService` is the WASM twin (its
  `rateLimitedAction: Report` turns a 429 into `WriteRateLimitExceededException`).
- **Submission integrity (§2.4.4(b)(c)):** the row is saved before the counter moves (D22 order); one
  open report per reporter per target — the service checks first, and the partial unique index
  `ix_reports_open_reporter_target` refuses a race, both as the same 400 ("You've already reported
  this…"). The allow-set rejection is a `ModerationValidationException` (400), not the old 401.
- **`ReportedUserId` (D8):** resolved at submit for every target type through
  `ResolveAnswerableUserIdAsync` (nullable form — authorless content stays reportable).
- **Schema (L1):** migration `WU_ModerationIntegrity` adds `reports.reported_user_id` (FK SET NULL) and
  the three indexes; its data steps backfill the column, close zombies and same-reporter duplicates, and
  recompute every `active_report_count`. Run against a dirtied clone of the dev workbench DB (a duplicate,
  a zombie, a wrong counter, two anonymous rows): all handled; Down and re-Up both clean. Integration
  applies it on every run.
- **Verified:** Integration `ModerationIntegrityTests` (dedup, the index race via
  `InterleavingCommandInterceptor` with `interceptReaders`, re-file after resolution, anonymous, the raw
  index, `ReportedUserId` for all six types, authorless, SET NULL); `ModerationEndpointsTests` (400 for the
  allow-set); `ParentVisibilityContractTests` (now resolves `IReportSubmissionService`). Unit
  `ModerationIntegrityClientTests` (routes, 400, 429). bUnit: the eight `ReportDialog` hosts register
  `FakeReportSubmissionService`. The dedup catch and the counter order were mutation-checked.

**Earlier F46 Stage note (WU-InertFeatures, owner rulings D4/D5; tracker B21 closed):** `SubmitReportAsync`'s
receipt was never delivered — it passed the reporter as their own source and the create-core's
drop-self rule deleted it. It now calls `NotifyReportReceivedAsync(reporter, report.ReportId)`, which
sends `ReportReceived` (80) null-sourced with the report id (id populated by the preceding save), and
the presenter words it as a receipt ("Thanks — we received your report"). Verified by Integration
`ModerationServiceTests` (`SubmitReportAsync_DeliversANullSourcedReceipt_CarryingTheReportId`,
`SubmitReportAsync_TwoReports_TwoReceipts`) and Unit `NotificationPresenterTests`. **Browser-driven
2026-09-30** by the WU-InertFeatures browser pass (tracker H14 closed), on both render phases:
- the receipt in the bell;
- 81, 82 and 70, all NULL-sourced and carrying the report id;
- a moderator who resolved their own report got the receipt and no 82.
Detail: `audit/Notifications.md` F41's browser note. Band-wide rules: the cluster Settled note above.

**WU34 settled constraints:**
- Report targets: Story, User, Comment, BlogPost, Recommendation, PrivateMessage (`ReportedEntityId` is long).
- `SubmitReportAsync` validates the allow-set, stamps `ReporterUserId`/`Open`, increments target's
  `ActiveReportCount` (uniform `AdjustActiveReportCount` switch; skips PrivateMessage), best-effort
  `NotifyReportReceivedAsync`. No auto-hide or auto-flag logic.
- `ReportDialog.razor` is a reusable modal (reuses `ConfirmDialog`/modal-shell pattern, WU9). One host per
  consuming page; wired via `HasDelegate`-gated `OnReport` callbacks.
- **Open for opusplan:** specific `ReportDialog` state shape (selected reason + notes field); whether the
  reasons dropdown is a radio group or select; StoryCard/UserCard caret integration specifics.

**Settled — report submission (owner rulings D8/D9 + service §2.4.4, answered 2026-08-05/06; recorded
2026-09-30 at WU-ModerationIntegrity's Doc-Touch moment 1; do not revisit).** Rule text:
`layer2-services.md` §"Moderation Services" → "Report submission".
- **`IReportSubmissionService` is its own interface** (`GetReportReasonsAsync` + `SubmitReportAsync`);
  `ReportDialog` injects it, never a mod interface (D9's type-level split). One concrete class serves
  all three moderation interfaces, registered once and forwarded (the current layer2 rule; D36 pending).
- **One open report per reporter per target** — partial unique index
  `ix_reports_open_reporter_target`; the service refuses a duplicate with a user-facing 400 and also
  catches the index's race. Anonymous reports are not deduped.
- **Primary write first, counter second** (D22): the report row commits, then `ActiveReportCount +1`.
- **`ReportedUserId`** (D8) — the account answerable for the artifact when the report was filed, for
  every target type; snapshot, never re-resolved; NULL = unknown/anonymized/deleted. Anonymous or
  deleted-author content stays reportable (the submit path never throws on NULL).
- **The allow-set rejection is a `ModerationValidationException` (400)**, not the
  `InvalidOperationException` (401) it was.

**Stage note (WU34 — 2026-06-25):** L1=5, L2=5, L3=5, L3.5=5 (all verified: `dotnet test` green,
298 integration tests + 417 unit tests). `IModerationReadService`/`IModerationWriteService` implemented in
`Core/Moderation/` + `Server/Moderation/`. `ServerModerationWriteService.SubmitReportAsync` validated by
`ModerationServiceTests.SubmitReportAsync_CreatesReportRow_IncrementsStoryActiveReportCount` (Integration).
`ReportDialog.razor` + `OnReport` wired in all StoryDeck composites (BookshelvesDesktop/Mobile,
GroupDesktop/Mobile, ProfileDesktop/Mobile) and `CommentSection`. L4=3 (functional Tailwind applied; not
design-reviewed). L5=2 (WASM client service + API endpoint deferred — no `ClientModerationWriteService` yet;
superseded, see the L5 note below).
L6=5 (composite index `ix_reports_reported_entity_type_reported_entity_id` added in migration
`20260625140459_WU34_Moderation`).

**L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
site now runs global InteractiveAuto. The browser-wave verification of `/mod/reports`,
`/mod/submissions`, `/mod/users` as AdminUser is Features 47/48's queue surface (their L5 cells
were mismarked N/A until 2026-07-27 — see their Stage notes); F46's own client surface is report
submission via `ClientModerationWriteService` (not driven in the wave). Full wave narrative + the
7 bugs found/fixed: `workplan.md` WU-GlobalFlip.

**Stage note (WU-UserModeration — 2026-08-01) — F46 cells unchanged (L1–L3.5=5, L4=3, L5=5, L6=5).**
Recorded here because this feature's *reach* changed, not its stage: `ReportedEntityType.User` was in
`SubmitReportAsync`'s allow-set from WU34 and no surface ever opened `ReportDialog` with it, so no
user report could exist. A "Report user" control now lives on `ProfilePage`'s banner (its single
`ReportDialog` moved from the Stories tab to page level so every tab can reach it), and
`UserCard.OnReport` — declared and `HasDelegate`-gated since WU34, wired by nobody — is now wired at
`VouchList` and both tree-search tabs. Full narrative + verification: Feature 47's
WU-UserModeration Stage note below. **Still unreachable, filed as tracker item B17:** BlogPost,
Recommendation and PrivateMessage remain in the allow-set with no report entry point.

## Feature 47 — Moderation Queue & Actions

**WU-TptHardDelete Stage note (2026-09-30; owner ruling D10) — no flip; L2 stays 5.** The illegal-content
hard delete (`ResolveWithRemovalAsync(…, hardDelete: true)` → `ApplyHardDeleteAsync`) orphaned TPT base
rows: removing a story cascaded to its chapters and their `chapter_comments` child rows but never
reached `base_comments`, and removing a blog post did the same to its comments and its `base_polls`.
It now runs `TptDelete.StoryCommentsAsync` (Story) or `TptDelete.BlogPostDependentsAsync` (BlogPost)
before the EF `Remove`, inside `InResolveTransactionAsync`'s transaction — the helper's SQL lands at
once, the `Remove` at the delegate's save, and a retry re-runs both (the tracker is cleared first and
the SQL is idempotent). With the parent → child FKs now RESTRICT, skipping the helper would fail the
resolve rather than orphan rows. Comment, Recommendation and Message removals are unchanged (a loaded
entity's removal deletes every row it owns). The spec's K1 risk did not materialize: the story cascade
passes the Restrict `chapters.primary_content_id` FK, because the referencing chapters are deleted
before their contents. Tier: Integration — `TptHardDeleteTests`: a story with two chapters, three
comments (a reply among them) and a like leaves no base comment, story, chapter or content row and the
report `ResolvedActionTaken`; a blog post with comments and a voted poll leaves no base row of either.
The story case was mutation-checked (it fails without the helper call). Still open: the reports on the
TPT child comments a hard delete destroys stay Open (tracker F13, owner-open). WU-BlobCleanup edits this
method next (D14).

**WU-TptHardDelete browser verification (2026-09-30) — no flip; L2 and L4.5 stay 5.** No UI sends
`hardDelete=true`: the queue's removal is the takedown. So the pass drove the hard delete over HTTP as
ModUser (`POST /api/moderation/reports/{id}/resolve-removal?…&hardDelete=true`, 204), with `psql`
before and after. TestUser filed each report as Illegal Content, through `POST /api/moderation/reports`.
- **A blog post.** AuthorBeta's post carried TestUser's comment, the author's reply, a comment like, a
  poll TestUser voted on, and a like.
  - The post and every dependent row were gone, and the `base_comments`/`base_polls` orphan counts
    stayed at 0.
  - The report closed `ResolvedActionTaken` by ModUser.
  - The reporter got one 81, and the author one 70.
  - The author's `BlogPostsWritten` stayed at 1. The hard delete moves no counter, before this WU as
    after it, and the nightly recompute heals the drift. Annotated on tracker F16.
- **A story.** AuthorAlpha's story, built for the pass, had two published chapters and three comments
  (a reply among them) with a like.
  - The story, both chapters, their contents, all three comments and the like were gone, and the
    orphan counts stayed at 0.
  - The report was resolved the same way: one 81 and one 70.
- **Logs:** no `fail:` or `crit:` line in the server log.

**WU-ModerationIntegrity browser verification Stage note (2026-09-30) — F46, F47 and F53 L4.5 1→5;
tracker H19 closed.**
- **Setup:** server-only path.
  - The workbench DB first took `WU_ModerationIntegrity` in place: the three seed reports backfilled
    `reported_user_id` 5/1/6, all five `reports` indexes were present, and the counters were recomputed.
  - It was then reset, so the edited `DataSeeder` ran on a fresh DB (the same three values).
  - `psql` after every write. The phase was read from the network log (`_blazor/negotiate` against the
    action's `/api` call); the circuit was forced by removing the Auto-mode localStorage hash.
  - Actor: ModUser unless named.
- **D7 sibling closing, on both phases.** WASM: story 5 with three reports (ReaderGamma, LurkerDelta,
  AuthorAlpha), "Hide content" on the first. Circuit: a comment with two reports.
  - Every sibling closed `ResolvedActionTaken` with the primary's moderator and timestamp, and
    "Closed with report #N: …".
  - The counter went to exactly 0.
  - One 81 went to each reporter, carrying their own report id; one 70 went to the author.
  - GIF: `e2e-WU-ModerationIntegrity.gif`.
- **Stale resolve (§2.1.2), on both phases.** A panel was left open while its report was resolved
  elsewhere: by a direct API call on WASM, and on the circuit by a second tab's removal, which closed it
  as a sibling. The stale panel read "This report has already been resolved." inline; the counter moved
  once and one notification went out.
- **"Hide content"** is absent from a User report's panel and present on Story and Comment reports.
- **Account-status table, on both phases.** Each refusal shows inline:
  - Suspend with a past date: "Choose a suspension end date in the future.";
  - Warn on a live suspension: the "suspended until … UTC" refusal;
  - Ban on a banned account from `/mod/users`: "This account is already banned.";
  - Warn on Banned: the Reinstate-only message.
  Ban from Suspended cleared `suspended_until_utc`.
- **A standing ban answers a report, on both phases.** Ban from `/mod/reports` on a report about an
  already-banned account (a User report on WASM, a story report on the circuit) resolved it as
  `ResolvedActionTaken`. The counter went −1 and the reporter got one 81; there was no second 74, and
  `security_stamp` was unchanged.
- **Reinstate, on both phases.**
  - It is hidden for an Active user and shown for Suspended and Banned.
  - The panel has no date field and a success-tinted Confirm.
  - The user returned to Active with a null date. One `reports` row was written: User type, reason 1
    "Other", reporter = moderator, `reported_user_id` set, `ResolvedActionTaken`. The counter, stamp
    and notifications were untouched.
- **History (B18/D8).**
  - The caveat is gone.
  - The Target column shows type plus label/link for story and comment reports about an author's
    content, and the sibling notes read in "Action taken".
  - A comment its author deleted shows "Comment / [deleted Comment]" with no link. That report stays
    Open and is absent from the queue (tracker F13).
- **D9 read gates over HTTP.** The moderation queue, submissions and history, both EV queues, both
  SiteDailyStat reads and the allocator's capacity answer 200 to a moderator, 403 to a member and 401
  to an anonymous caller. An unknown user's history and an unknown report's resolve answer 404 (the
  resolve used to be 401). `/mod/submissions` (both tabs), `/mod/stats` and `/mod/spotlight` render for
  a moderator on both phases.
- **Not driven:** account deletion's zombie closure (no UI changed; Integration covers it).
- **No bug in this WU's code.** Filed from the pass:
  - **H20:** comment previews show raw `<p>` markup in both Target columns, and `AccountActionPanel`
    keeps a typed reason across verbs, so a ban reason can carry into Reinstate;
  - **E8:** hand-built API bodies (an enum sent as a string, `reasonId` 0) answer 500, not 400.
- **Logs:** the console was clean. The server log's only `fail:` lines were the post-wipe database
  probe, the antiforgery key, and the two hand-built requests.

**WU-ModerationIntegrity review-fixes Stage note (2026-09-30) — no further flip; L4.5 stays 1, L2 stays
5.** Three reviews of the build; the F47 fixes:
- **A standing ban answers a report** (derived — `roadmap.md` decision row 20, refined). The build's
  "no second Ban" refusal also applied to the report-driven path, so once an account was banned, every
  other report about it could close only as "no action" (82): account actions close no siblings (D7),
  Warn/Suspend are refused on Banned (literal §2.1.3), and a `User` report cannot be removed. The
  reporter was told no action was taken against a banned account, and D8's history recorded
  `ResolvedNoAction`. `ApplyAccountActionAsync(report, BanUser)` on a banned account now resolves the
  report against the standing ban (`ResolvedActionTaken`, −1, 81 to the reporter) with no status write,
  stamp bump or second 74; the moderator-initiated path keeps the refusal (no report to answer).
- **Tests the build owed:** the report-driven decrement surviving the stamp bump (Suspend and Ban on a
  `User` report — the counter fix the build made had no test); the transition table refusing on the
  report-driven path (report stays Open, counter and status untouched — the rollback); the moderator
  never receiving 81 for a sibling report they filed, nor for a report they filed and resolved with an
  account action; the D7 hard-delete case in the owner's shape (three reporters, three 81s); the
  `/reinstate` route for a moderator (204, the query-bound reason) and its 400.
- **Verified:** Integration `ModerationIntegrityTests`
  (`AReportDrivenEjection_OfAReportedUser_KeepsTheDecrement_PastTheStampBump` ×2,
  `AReportDrivenActionTheTableRefuses_LeavesTheReportOpen_AndChangesNothing`,
  `AReportDrivenBan_OnABannedAccount_ResolvesTheReportAgainstTheStandingBan`,
  `ARemoval_NeverSendsTheActingModeratorAReceiptForTheirOwnSiblingReport`,
  `AReportDrivenAccountAction_OnAReportTheModeratorFiled_SendsThemNoReceipt`, the widened hard-delete
  test) and `ModerationEndpointsTests` (`Reinstate_Moderator_BannedUser_Returns204_AndTheUserIsActive`,
  `Reinstate_ActiveUser_Returns400_WithTheMessage`). Mutation-checked: the decrement moved back above the
  bump, both moderator skips, the report-driven table call, the standing-ban branch and the sibling 81s
  each fail their tests when reverted. No browser was available — tracker H19 gains the standing-ban
  step.

**WU-ModerationIntegrity Stage note (2026-09-30) — F47 L4.5 5→1 (UI changed, no browser available);
every other cell unchanged.** Rules: the F47 Settled note "report lifecycle integrity" below and
`layer2-services.md` §"Moderation Services".
- **Resolve paths (§2.1.2):** each runs in one execution-strategy transaction that locks the report row
  (`FOR UPDATE`) and refuses a non-open one (400) or an unknown id (404 — it was 401). Notifications run
  after commit, each in its own `try/catch` (one failure used to drop the rest).
- **D7:** `ResolveWithRemovalAsync` closes every other open report on the same `(type, id)` target as
  `ResolvedActionTaken` ("Closed with report #N: …"), moves the counter by −(1 + rows closed), and sends
  81 to every distinct sibling reporter with their own report id. A `User` report cannot be removed.
- **Account actions (§2.1.3):** the transition table in both entry points (future suspension date;
  Banned leavable only via Reinstate; no Warn over a live suspension and no second Ban — derived,
  `roadmap.md` row 20; the second-Ban refusal narrowed to the moderator-initiated path by the review
  fixes, note above); `SuspendedUntilUtc` cleared on every non-Suspend status; new
  `ReinstateUserAsync` (+ `POST /api/moderation/users/{id}/reinstate`, client twin,
  `ModeratorActionType.ReinstateUser`). The report-driven action now sends 81 to the member reporter
  (§5.21, the routed InertFeatures item) and decrements after the stamp bump (`UserManager` rewrites
  every user column, so a `User`-target decrement made before it was lost — found building this WU).
- **D9:** the three moderation reads gate in the service (`RequireModerator()`, the shared extension
  every moderator guard now uses — the private copies in Moderation, ExternalVerification, SiteSettings,
  the Spotlight allocator, Fanon, Tags, site posts and site polls are gone, and the six private
  `RequireAuthenticatedUser` copies became `RequireUserId()`). The read handlers wrap in `ExecuteAsync`.
- **B18 / D8:** `GetUserModerationHistoryAsync` reads `ReportedUserId` across every target type and
  keeps rows whose target is gone (`[deleted {type}]`); `SetCanAutoApproveAsync`,
  `ApplyAccountActionToUserAsync` and Reinstate set `ReportedUserId` on their audit rows.
- **UI:** `/mod/users/{id}` drops the caveat, adds a Target column and a Reinstate button (non-Active
  only, through `AccountActionPanel`'s new Reinstate verb — no date field, non-destructive Confirm);
  `/mod/reports` hides "Hide content" for a User report. **No browser was available**, so L4.5 drops to
  1 until tracker **H19**'s pass (both render phases).
- **Verified:** Integration `ModerationIntegrityTests` (D7 soft/hard/no-action/account-action/
  same-number/claimed/Message; the status guards on all three paths, unknown ids, the `Task.WhenAll`
  race; the account-status table and Reinstate incl. `CanSignInAsync`; the 81 to the reporter; the D9
  read gates for a non-moderator and an anonymous caller; the history), `ModerationServiceTests`
  (history flipped to include content reports; a second reporter where the index requires it),
  `ModerationEndpointsTests` (404 for an unknown report, 403 for a non-mod reinstate, the history 404
  inside the wrapper). bUnit `ModUsersPageTests` (no caveat, Target column, Reinstate hidden/shown and
  submitting), `AccountActionPanelTests` (Reinstate verb), new `ModReportsPageTests`. Unit
  `ModerationIntegrityClientTests` (`RequireModerator`, the reinstate route). Mutation-checked: sibling
  closing, the status guard, the race catch, a read gate, the transition table, the reporter's 81, the
  history predicate, the Hide-content guard — each fails its tests when reverted.

**Review-fixes Stage note (WU-InertFeatures, 2026-09-30) — no flip; L2 stays 5.** D5 null-sourced the
band and so dropped drop-self's protection: a moderator who filed a report through the ordinary Report
button and then resolved it received the resolution receipt (81/82), and a moderator removing their own
content received 70. Before D5 the moderator was the source and drop-self stopped both. The resolve
paths now skip the acting moderator explicitly, and so does `ApplyStatusAndNotifyAsync` (which takes
the moderator id back) when a report-driven action lands on the moderator's own account. Integration
`ModerationServiceTests` covers it: `AModeratorResolvingAReportTheyFiled_GetsNoResolutionReceipt` (the
two 80 receipts still arrive), `AModeratorRemovingTheirOwnContent_GetsNoContentRemoved_ButTheReporterHearsTheOutcome`
and `AReportDrivenAccountActionOnTheActingModerator_SendsThemNothing`. All three fail with the skips
removed. Rule: the cluster Settled note above.

**WU-InertFeatures Stage note (2026-09-30) — no cell flips; L2 stays 5 (owner rulings D3/D4/D5).**
Every outcome notification the queue sends is now null-sourced (the moderator is never named to the
recipient) and anchored per D4: `ResolveNoActionAsync` → 82 and `ResolveWithRemovalAsync` → 81 + 70
carry the report id; the account actions' 72/73/74 are dedup-exempt, so a second warning while the
first is unread is a second row; `ApplyStatusAndNotifyAsync` lost its moderator parameter; the
moderator-initiated `ApplyAccountActionToUserAsync` carries the D4 guardrail comment and sends no
80/81. A Recommendation takedown through `ResolveWithRemovalAsync` sweeps every reader's attribution
naming that rec in the same save (D3 trigger 5). Verified by Integration `ModerationServiceTests`
(+8 — the seven listed in `audit/Notifications.md` F41's Stage note plus
`ResolveWithRemoval_OnARecommendationReport_SweepsItsAttributions`); mutation-checked. Routed, not
built: the report-driven `ApplyAccountActionAsync` sending 81 to a member reporter
(WU-ModerationIntegrity).

**WU-CounterSymmetry Stage note (2026-09-30; owner ruling D21) — no flip; L2 stays 5.** The
reconciler D7 handed D21 is built: `ContentCounterRecalculator` recomputes `active_report_count` on
all five carriers (`AspNetUsers`, `stories`, `base_comments`, `base_blog_posts`, `recommendations`)
as `COUNT(*) FROM reports WHERE reported_entity_type = <type> AND report_status_id IN (0, 1)`, reading
the partial index `ix_reports_open_target`. It runs nightly, ahead of the UserStat pass. A drift in the
triage sort's key (a crash between a report's commit and its counter, or a manual fix) now heals within a
day, where before it persisted until someone noticed. `AdjustActiveReportCountAsync` stays the single
live mutator. The resolve paths' in-transaction counter move satisfies D22's order rule (it commits
with the status flip, never ahead of it). Detail and tests: `audit/Profiles.md` F58
(`ContentCounterRecalculatorTests` — Open/UnderReview counted, resolved and Message reports ignored,
bigint comment ids; Integration).

**Stages (updated 2026-09-30, WU-CounterSymmetry — the `ActiveReportCount` reconciler, beneath L2, no
flip; WU-TptHardDelete before it — the hard delete clears TPT dependents first, an L2
change beneath the cell, no flip, HTTP-driven by its browser pass the same day; Stage notes at the top
of this section):** L1–L3.5 = 5, L4 = 3,
L4.5 = 5 (WU-ModerationIntegrity lowered it to 1 for its undriven UI and the review fixes'
standing-ban resolve; its browser pass the same day drove both on both render phases and returned it
to 5, tracker H19 closed — the browser-verification note at the top of this section; the earlier 1→5
by the WU-StoryLifecycle browser pass is recorded in F48's browser-verification Stage note), L5 = 5,
L6 = 5 (Stage notes at the top and end of this section).

**WU34 settled constraints:**
- ~~`/mod/reports` and `/mod/users` — server-rendered, mod-gated (`RequireModerator` policy), no dispatcher.~~
  *Corrected 2026-09-30 (WU-ModerationIntegrity review fixes): false since the Global Flip (2026-07-13)
  — both pages are `InteractiveAuto` with client twins (`ClientModerationRead/WriteService`). They stay
  mod-gated at the page and the endpoint, and the service now gates every moderator read itself (owner
  ruling D9 — the circuit has no endpoint to stop a caller).*
- Report queue ordered by `ActiveReportCount` desc (most-reported first) — triage sort only, never an
  automation trigger. Report counts are mod-only (no public-facing badge).
- Content removal: soft-takedown default (`IsTakenDown = true`, ~~reversible~~ *reversible by design, but no
  reversal path is built yet — owner ruling D8(b) rules only how a future reversal behaves; corrected
  2026-09-30, WU-ModerationIntegrity review fixes*, author notified with `TakedownReason`);
  separate explicit hard-delete for illegal content (CSAM/piracy). `LoadModeratableAsync` single loader switch
  + interface mutation via `IModeratableContent` in `ServerModerationWriteService` (pre-integration cleanup
  2026-06-26 collapsed the prior triple switch).
- Account actions: `AccountStatus` enum (Active/Warned/Suspended/Banned — **no Shadowbanned**) +
  `SuspendedUntilUtc` set on `User`. Status + notification + `Report` record set together.
  **Login-blocking enforcement landed in WU38a** (`CanalaveSignInManager.CanSignInAsync` +
  security-stamp bump on Suspend/Ban in `ApplyAccountActionAsync` — see
  `canalave-conventions/security.md` "Account-Status Enforcement" and this file's WU38a Stage note
  below). **Mid-session disclosure landed in WU-AccountEnforcement (2026-07-30):** the target now
  learns of a Warn/Suspend/Ban within one in-app navigation, not just via the notification and not
  just at next sign-in — see this file's WU-AccountEnforcement Stage note below and
  `audit/Identity.md`'s.
- Polymorphic target label + deep-link resolved via two-pass `BatchLoadEntitiesAsync` pattern (one query
  per present target type — same pattern as `GetNotificationsAsync`).
- `User.ActiveReportCount` added (symmetric with other targets); `AdjustActiveReportCount` switch skips
  `PrivateMessage` (no display surface for DM report count).

**WU-UserModeration settled constraints (2026-08-01) — supersedes two WU34 rules.** Recorded as
Doc-Touch moment 1, before implementation, because the plan contradicts settled WU34 notes:

- **Account actions resolve their target user; they no longer require the report target to be a User.**
  WU34 settled `ApplyAccountActionAsync` as User-target-only. That rule made the Warn control on
  `/mod/reports` throw `InvalidOperationException` for every report the app can actually produce
  (only Story and Comment have report entry points), and `InvalidOperationException` is not in
  `ExceptionPresenter.IsUserFacing`, so moderators got the generic error. Settled instead: the
  target is *the report's User target, or the reported content's `AuthorUserId`* — see
  `canalave-conventions/layer2-services.md` §"Account actions — target resolution and the
  report-as-audit-record rule" for the full mapping.
- **Moderators may act on users who were never reported** (Brian, 2026-07-31 — the decision tracker
  item **B13** asked for). Implemented as a **mod-filed report**, not a new audit entity: the action
  opens and resolves a `Report` in one unit of work with `ReporterUserId == ModeratorUserId`, which
  is what marks it moderator-initiated. No new entity, no migration, no new seeded `ReportReason` —
  the moderator picks from the six existing rows. Rejected alternative: a separate
  `ModerationAction` table (cleaner conceptually, but forks the audit trail across two surfaces and
  buys nothing the `Report` row does not already carry).
- **`/mod/users` is a per-user lookup and history view**, not the sole escalation surface;
  `/mod/reports` carries the full Warn/Suspend/Ban set. This is what makes the `{UserId:int?}` route
  parameter live — B13 closes by *wiring* the parameter, not deleting it.
- **Still open, deliberately excluded:** ~~a user's history shows reports *targeting* them, not reports
  against content they authored (needs author-resolution across four content tables — its own WU)~~
  **closed by WU-ModerationIntegrity (2026-09-30)** — D8's `ReportedUserId` makes the history one
  predicate across every target type and the on-screen caveat is deleted (tracker B18);
  BlogPost/Recommendation/PrivateMessage still have no report entry point; there is no role
  grant/revoke capability anywhere.

**Settled — report lifecycle integrity (owner rulings D7/D8/D9 + service §2.1.2/§2.1.3/§2.4.4,
answered 2026-08-04..06; recorded 2026-09-30 at WU-ModerationIntegrity's Doc-Touch moment 1; do not
revisit).** Rule text: `layer2-services.md` §"Moderation Services";
`identity-and-authorization.md` §"Role-Based (Moderator) Gating".
- **`ActiveReportCount` is a cache of the open-report `COUNT(*)` for the target** (D7) — derived, so
  recomputable; the reconciler is `ContentCounterRecalculator` (built WU-CounterSymmetry, 2026-09-30).
- **Every resolve path locks and guards** (`FOR UPDATE` on the report row inside an execution-strategy
  transaction; not Open|UnderReview → `ModerationValidationException`; missing → 404), so nothing
  decrements twice. Notifications run after commit, each in its own `try/catch`.
- **Removal closes every sibling report on the same `(type, id)` target** (D7), as
  `ResolvedActionTaken` with the acting moderator, the same timestamp and "Closed with report #N";
  the counter moves by −(1 + rows actually closed), never zeroed; every distinct sibling reporter gets
  81 with their own report id. No-action and account actions never bulk-close. A `User` target cannot
  be removed (an account action resolves it).
- **The report-driven account action tells the member reporter the outcome** (81, skipped for the
  acting moderator) — §5.21; the moderator-initiated path still sends none.
- **Account-status transition table** (§2.1.3): a suspension needs a future end date; Banned is left
  only via the new **Reinstate** action (a moderator-filed `Report` row, reason Other, no notification);
  no Warn over a ban or a live suspension (derived — `roadmap.md` decision row 20); a second Ban is
  refused on the moderator-initiated path (derived, row 20), while a report-driven Ban on a banned
  account resolves that report against the standing ban — 81 to the reporter, no status write, stamp
  bump or second 74 (row 20 as refined by the review fixes); `SuspendedUntilUtc` is cleared on every
  non-Suspend status.
- **Mod-only reads gate in the service** (D9): `RequireModerator()` (the shared extension) opens the
  three moderation reads, both ExternalVerification queues, both SiteDailyStat reads and the allocator's
  capacity read. The SiteSettings `GetIntAsync` read is the one recorded non-gate.
- **The per-user history reads `ReportedUserId`** (D8/B18) across every target type; rows whose target
  is gone stay, labelled `[deleted {type}]`.
- **Zombie reports from account deletion are closed at the source** (D7 sub-edge — the WU's recorded
  pick: at source, not a reconciler) by `ReportLedger.CloseForDestroyedTargetsAsync`, as
  `ResolvedNoAction` with a NULL moderator and a note, silently. Author self-delete sites are owner-open
  (tracker F13).
- **401-instead-of-404/400 is fixed at the throw sites** in the Moderation and ExternalVerification
  services (the WU's recorded pick); the `EndpointHelpers` table is unchanged.

**Stage note (WU34 — 2026-06-25):** L1=5, L2=5, L3=5, L3.5=5. `ModReportsPage.razor` + `ModUsersPage.razor`
built at `/mod/reports` + `/mod/users` — server-rendered, mod-gated. Claim/resolve/soft-remove/warn-user
flows implemented and covered by `ModerationServiceTests.ResolveNoActionAsync_*` and
`ResolveWithRemovalAsync_SoftHides_*` (Integration). `AdjustActiveReportCount` switch verified — Message
type is no-op (unit test). L4=3 (functional styling, not design-reviewed). L5 was N/A at WU34 —
**corrected to Stage 5 (2026-07-27):** `ClientModerationReadService`/`WriteService` + endpoints exist
(WU-L5Sweep) and `/mod/reports` + `/mod/users` were verified in a real WASM runtime in WU-GlobalFlip's
browser wave (see the F46 L5 note above). L6=5 (same migration as Feature 46).

**Stage note (WU38a — 2026-07-11):** L2 stays Stage 5, re-verified (additive). `ApplyAccountActionAsync`
now calls `UserManager.UpdateSecurityStampAsync(targetUser)` after setting `Suspended`/`Banned` (not
`Warned`) so an already-open session dies via the existing 30-min
`IdentityRevalidatingAuthenticationStateProvider` stamp revalidation, closing the "login-blocking
enforcement deferred" gap this section used to point at — see `audit/Identity.md` WU38a Stage note
and `canalave-conventions/security.md` "Account-Status Enforcement" for the full mechanism (the
sign-in-side half, `CanalaveSignInManager`, lives in Identity, not here). **Verified:** Integration
(`AccountStatusEnforcementTests.ApplyAccountActionAsync_SuspendUser_BumpsSecurityStamp`/
`_BanUser_BumpsSecurityStamp`/`_WarnUser_DoesNotBumpSecurityStamp`) — stamp changes on Suspend/Ban,
unchanged on Warn. `dotnet test` 1483/1483 green.

**Stage note (WU-AccountEnforcement — 2026-07-30):** L2 stays Stage 5, unchanged — this WU touched
nothing in `ServerModerationWriteService`/`ApplyAccountActionAsync` itself; the read-side fix lives
in Identity (`audit/Identity.md`'s WU-AccountEnforcement Stage note has the full mechanism).
Recorded here because it closes this feature's outward-facing gap: previously a target's *only*
mid-session signal was the WU34 notification (and, for Suspend/Ban, an up-to-30-minute silent
ejection); now `AccountStatusBanner` also surfaces the new status within one in-app navigation, and
the notification itself is no longer subject to the identical staleness bug (`NotificationBellInner`
never actually subscribed to `NavigationManager.LocationChanged` despite claiming to — fixed in the
same WU). **Verified:** `AccountStatusEndpointsTests.GetMyStatus_ReflectsALiveWriteThroughApplyAccountActionAsync`
drives the real `ApplyAccountActionAsync` path (not a `psql` shortcut) and confirms the change is
visible through `GET /api/account-status` on the same HTTP client immediately afterward — proof
this feature's write path needed no change for the read side to go live.

**Bug found and fixed live in the same WU (2026-07-30) — L3-Logic stays Stage 5, no Stage
change.** Unrelated to WU-AccountEnforcement's own scope, but surfaced by its browser-verification
pass and fixed in the same session per `debugging.md`'s "fix same-session" discipline: driving a
real `SuspendUser` action through `ModUsersPage.razor`'s form for the first time (every prior
verification of Suspend, including WU38a's, set `SuspendedUntilUtc` directly via `psql`/fixture,
never through this UI) crashed with `ArgumentException: Cannot write DateTime with Kind=Unspecified
to PostgreSQL type 'timestamp with time zone'`. The `<input type="datetime-local">` bound to
`_suspendUntil` via `@bind` produces `DateTime.Kind=Unspecified`; the label reads "(UTC)" but
nothing tagged the Kind before handing it to `ApplyAccountActionAsync`. Fixed by re-tagging (not
shifting) the value with `DateTime.SpecifyKind(local, DateTimeKind.Utc)` at the call site in
`ConfirmAccountActionAsync`. **Verified:** new RazorComponents `ModUsersPageTests.SuspendUser_SubmitsUtcKindDateTime`
drives the real form (Suspend → fill date/reason → Confirm) against a recording fake write service
and asserts the passed `DateTime.Kind == Utc`; mutation-sanity confirmed (reverting the
`SpecifyKind` call fails the test). Live re-verified via the real browser + `psql` ground truth
after the fix: Suspend through the mod UI now succeeds (`ResolvedActionTaken`), `suspended_until_utc`
lands correctly in Postgres, and the target's `AccountStatusBanner` shows the live-corrected date on
its next navigation.

**Settled 2026-07-18 — report-target rating routing (decision row 1), "work surface, show all."**
Superseded the WU34 "moderator's ContentRating reach = personal ShowMatureContent, mirrors
browsing" framing. Row 1 asked how to extend per-target rating scoping — which in practice only
ever covered Story reports (the sole arm with a live `ContentRating` filter) — out to
Recommendation/BlogPost/Comment. Resolved by removing scoping instead of extending it: the report
queue and pending-submissions queue are now moderator work surfaces, exempt from
`ShowMatureContent` entirely. Reasoning + full record: `middle_plan_v2.md` Resolved "Non-story
report-target rating routing"; rule: `canalave-conventions/content-safety.md` §"Moderator review
surfaces are work surfaces". L3=5 stays (behavior change, not a stage change) — **verified:**
Integration (`ModerationServiceTests.GetReportQueueAsync_ShowsMRatedStoryReport_ToModWithMatureOff`,
`GetPendingSubmissionsAsync_ShowsMRatedSubmission_ToModWithMatureOff`); `dotnet test` full suite
green (see Feature 48 note below for the pending-submissions half of this same change).

**Stage note (WU-UserModeration — 2026-08-01) — no cell flips; F47 stays L1–L3.5=5, L4=3, L5=5,
L6=5, and F46 likewise.** Nothing here changed stage: this WU closed gaps *beneath* already-sound
cells, the same shape as tracker items B0/B4/B12. What it changed is that the feature was reachable
at all.

**What the investigation found** (tracker item **B13** was filed as `polish · low` — "`ModUsersPage`'s
`{UserId:int?}` route parameter is declared and never read"; the parameter was the least of it):

1. **Nothing in the app could report a User.** Every `ReportDialog.OpenAsync` call site passed
   `Story` (5 sites) or `Comment` (2) — there were no others in the repo. `ModUsersPage` filters the
   queue to `EntityType == User`, so it rendered "No reported users." permanently, for every
   moderator. `UserCard` had carried an `OnReport` callback since WU34, `HasDelegate`-gated, wired by
   no consumer — its own comment said Report "stays dark until those features land."
2. **`/mod/reports`' "Warn user" control threw every time.** It passed a content report's id into
   `ApplyAccountActionAsync`, which required a User-targeted report (WU34's rule). Since Story and
   Comment were the only reports the app could produce, that button always failed — and
   `InvalidOperationException` is not in `ExceptionPresenter.IsUserFacing`, so the moderator saw
   "Something went wrong on our end" while the server logged at Error.
3. **Therefore the whole account-action capability was unreachable** — built WU34, sign-in-blocked
   WU38a, surfaced by `AccountStatusBanner` at WU-AccountEnforcement, covered by integration tests,
   and not usable through any in-app path. Nobody could be warned, suspended, or banned.
4. Two smaller defects alongside: `ApplyAccountActionAsync` never decremented `ActiveReportCount`
   (its two sibling resolve paths do), leaking upward the counter the triage sort orders on; and
   `UserMenu`'s mod block linked only `/mod/reports`, leaving `/mod/users`, `/mod/submissions`,
   `/mod/stats` and `/mod/spotlight` URL-typed-only.

**Built.** Target resolution (`ResolveActionTargetUserIdAsync`) so an account action lands on the
report's User target *or* the reported content's author; unresolvable author now throws the new
user-facing `ModerationValidationException` instead of the flattened-to-generic
`InvalidOperationException`. The counter decrement added. The status/stamp/notification tail
extracted to `ApplyStatusAndNotifyAsync` and shared with the new
`ApplyAccountActionToUserAsync` (moderator-initiated, files its own audit `Report` — see the settled
constraints above). New read `GetUserModerationHistoryAsync` + `UserModerationHistoryDto`, two
endpoints, two client impls. `AccountActionPanel.razor` extracted from `ModUsersPage` — load-bearing,
not tidiness: it owns the 2026-07-30 `DateTime.SpecifyKind` Npgsql fix, and giving `/mod/reports` a
Suspend control would otherwise have meant re-deriving that fix in a second place.
`ModUsersPage` rewritten around the now-live route parameter (lookup mode with `UserPicker`; detail
mode with standing + history + actions), following `MessagesPage`'s `_initialized`/`_loadedUserId`
guard pattern. `/mod/reports` gained Warn/Suspend/Ban. Report-a-user lit up on `ProfilePage` (its
single `ReportDialog` moved out of the Stories tab to page level so a banner control on every tab
can use it) plus `UserCard.OnReport` at `VouchList` and the two tree-search tabs via
dispatcher-pattern pass-throughs. `DataSeeder` gained a User-targeted report.

**Verified.** `dotnet test` green across all three tiers: Unit 793, RazorComponents 662 (+12),
Integration 1063 (+8) — 2,518 total. New coverage: Integration `ModerationServiceTests` (action on a
Story report warns the story's author; on a `UserProfileComment` report bans the comment's author;
unresolvable author → `ModerationValidationException`; counter decrements;
`ApplyAccountActionToUserAsync` files a report with `ReporterUserId == ModeratorUserId == modId`;
non-mod → `UnauthorizedAccessException`; self-action rejected; history returns user-targeted rows
only and null for an unknown id). RazorComponents: new `AccountActionPanelTests` (the retargeted
UTC-Kind regression guard + three validation cases) and `ModUsersPageTests` rewritten for both route
modes. Gates green: `check-design-tokens.ps1`, `check-doc-hygiene.ps1`, `check-a11y.ps1`.

**Browser-verified end to end** (server-only path, AdminUser, `psql` ground truth after each step):
reported ReaderGamma from their profile → report row `type=User, reporter=2`, `active_report_count`
1 → appeared on both `/mod/reports` and `/mod/users` → `/mod/users/6` rendered standing + history;
then claimed the *Story* report on `/mod/reports` and Suspended — **AuthorBeta, the story's author,
went `account_status=2` with `suspended_until_utc` persisting the entered clock value unshifted**
(the path that threw before), and the story's `active_report_count` fell 1 → 0; then `/mod/users`
with no id → typeahead-found LurkerDelta (zero reports) → Ban → `account_status=3` plus a
mod-filed report row with `reporter_user_id == moderator_user_id == 2` and `active_report_count`
correctly untouched at 0. `AccountSuspended`/`AccountBanned` notifications both fired. Finally,
`/dev/wu12/login-as/AuthorBeta` left the session signed out — `CanalaveSignInManager` blocking a
suspended account, the WU38a mechanism reachable from the UI for the first time. Zero `fail:`/`crit:`
lines in the server log across the pass.

**Stage note (WU-StoryLifecycle — 2026-09-30) — no cell flips at the build** *(L4.5 was flipped 5→1
later the same day by the review fixes — next note)*.
`UserModerationHistoryDto` gained two trailing optional fields, `ApprovedStorySubmissions` and
`CanAutoApprove` (appended with defaults so existing constructors keep compiling), populated by
`GetUserModerationHistoryAsync`. `/mod/users/{id}` shows "N approved submissions · auto-approve
on/off" under the standing line, and the Account-action card gained a Revoke/Restore auto-approve
button that reuses the page's reason category and asks for a reason in an inline confirm row; it
calls the new `SetCanAutoApproveAsync`, which files the same moderator-initiated audit `Report` as
the account actions (`layer2-services.md` §"Account actions", rule 1, now covers it). **Verified:**
Integration (`ModerationServiceTests` — revoke writes flag + audit row, unchanged writes nothing,
non-mod/self refused, history carries the trust fields); RazorComponents (`ModUsersPageTests` —
the trust line, Revoke calls the service with `(42, false, reasonId, reason)`, an empty reason never
calls it). **Not browser-verified** (no browser available) — tracker **H12** item 4.

**Stage note (WU-StoryLifecycle review fixes — 2026-09-30) — F47 L4.5 5→1; restore path tested.**
**L4.5 flipped 5→1** for the reason in the headline (`grid_axes.md` L4.5: Stage 5 means the feature
was driven in a real browser as its audit file intends; this section now describes an undriven
control). **L2 coverage gap closed:** only the revoke direction of `SetCanAutoApproveAsync` was
tested, so a service that ignored its `canAutoApprove` argument and always wrote `false` passed.
**Verified:** Integration `ModerationServiceTests.SetCanAutoApproveAsync_RevokeThenRestore_*` (flag
back to true, a second audit row "Auto-approve restored: …", and the author's next submit takes the
waiver again) and `…_UnknownUser_UnknownReason_AndOverLongReason_AreRefused` (`KeyNotFoundException`;
two `ModerationValidationException`s; nothing written). Owner question promoted: whether to notify an
author of a revoke/restore is `roadmap.md` decision row 15.

## Feature 48 — Story Approval Workflow

**Stages (updated 2026-09-30, WU-ModerationIntegrity — L2 changes beneath the cell, no flip; the
WU-StoryLifecycle browser pass before that):** L1–L3.5 = 5, L4 = 3, **L4.5 = 5**
(the review fixes dropped it to 1 because `/mod/submissions` and the D1 approval workflow had changed
undriven; the browser pass drove them on circuit and WASM and returned it to 5 — browser-verification
Stage note at the end of this section), L5 = 5, L6/L8 = N/A. WU-ModerationIntegrity's two F48 changes
(the queue read's service gate, `ReportedUserId` on the trust-waiver audit row) are in its Stage note,
the last note of this section. The D1 guards, trust
waiver, `SubmittedDate` and the takedown freeze landed beneath the other cells (Stage notes at the end
of this section). **WU-InertFeatures (2026-09-30), no flip:** `StoryApproved` (75) and
`StoryRejected` (71) are null-sourced — `NotifyStoryApprovedAsync`/`NotifyStoryRejectedAsync` lost
their moderator parameter and `ApproveStoryAsync`/`RejectStoryAsync` now only gate on the role — and
75 has its own actor-free presenter arm ("{story} was approved for the library"; it previously fell to
the catch-all). Covered by Integration `ModerationServiceTests` (the band sweep and the renamed
story-outcome dedup tests) and Unit `NotificationPresenterTests`. **WU-InertFeatures review fixes
(2026-09-30), no flip:** approve and reject read the moderator id again, solely to skip a moderator
who approves or rejects their own story (no 75/71 about their own act — drop-self did this before D5).
Integration `AModeratorApprovingOrRejectingTheirOwnStory_GetsNoOutcomeNotification`.

**WU34 settled constraints:**
- `StoryDetail.PostApprovalStatus` (live field, enforced by `StoryValidations.CanSubmitForApproval`) is the
  submission mechanism — **not** `RequestedStatusId` (that was a deliberations-doc artifact, never built).
- Approve: `StoryStatusId = PostApprovalStatus` + `NotifyStoryApprovedAsync`.
- Reject: `StoryStatusId = Rejected` + `ActionTaken` reason + `NotifyStoryRejectedAsync`.
- **Amended by owner ruling D1 (worksheet 2026-08-04), before the WU-StoryLifecycle build
  (2026-09-30) — settled, do not revisit:** the queue is **mandatory only for an author's first
  submission** (spam prevention, not editorial review). A trusted author
  (`User.ApprovedStorySubmissions >= 1 && User.CanAutoApprove`) submitting lands straight at
  `PostApprovalStatus`, never entering the queue; any moderator may revoke/restore `CanAutoApprove`
  (moderator-initiated `Report` row as the audit record). Approve and reject act **only on
  `PendingApproval`**, via a conditional update (a second moderator or an author withdraw in between →
  "already handled", nothing written); approve re-validates `PostApprovalStatus` as an entry status
  (`InProgress`/`Completed`/`OpenBeta` — closes approve-into-`Draft`), refuses a non-live author
  (deleted, banned, or suspended with a null/future end — reject stays unguarded), stamps
  `PublishedDate` on first publication (D2), and commits the status flip and the monotonic `+1` to
  `ApprovedStorySubmissions` in one transaction. `Rejected` is reachable only from `PendingApproval`.
  The queue orders by, and shows, `Story.SubmittedDate` (stamped on each →`PendingApproval`), because
  `PublishedDate` is NULL for every never-published story. Rule of record: `layer2-services.md`
  §"Story Lifecycle".
- `/mod/submissions` is a tabbed shell in WU34; import-verification tab drops in with WU39.
  **Superseded (WU-RecLifecycle, 2026-07-25):** the WU34-era "rec-approval wiring deferred; tab added
  later" expectation is void — there is **no rec-approval tab, ever**. Recommendations are
  author-controlled (publish-immediately + author Request-Revision/Remove/Unblock; see
  `audit/Recommendations.md` §"WU-RecLifecycle settled design"); moderators act on recs only
  reactively via the existing report→takedown path. `/mod/submissions` stays Stories + Imports.

**Stage note (WU34 — 2026-06-25):** L1=5, L2=5, L3=5, L3.5=5. `ModSubmissionsPage.razor` built at
`/mod/submissions` with tabbed shell (Stories tab active, Imports tab placeholder for WU39). Approve/reject
flows implemented and covered by `ModerationServiceTests.ApproveStoryAsync_*` /
`RejectStoryAsync_*` (Integration). `StoryApproved` notification wired end-to-end
(`NotifyStoryApprovedAsync` → `CreateCoreAsync` → notification row). L4=3 (functional styling).
L5 was N/A at WU34 — **corrected to Stage 5 (2026-07-27):** the approve/reject queue rides the same
`ClientModerationReadService`/`WriteService` + endpoints as F47, and `/mod/submissions` was verified
in a real WASM runtime in WU-GlobalFlip's browser wave (see the F46 L5 note above).

**Stage note (pre-integration cleanup — 2026-06-26):** Features 46/47/48, all L1-L3 cells updated.
Soft-delete columns renamed from `IsHidden`/`DateModeratedRemoved`/`ModerationRemovalReason` → `IsTakenDown`/
`TakedownDate`/`TakedownReason` across `Story`, `BaseComment`, `BaseBlogPost`, `Recommendation`; EF named
filter key `"ModeratedVisibility"` → `"IsTakenDown"`; `IModeratableContent` interface added in
`Core/Moderation/` with `AuthorUserId` projection; `ServerModerationWriteService` triple switch collapsed to
`LoadModeratableAsync` + interface mutation (one per-type loader switch remains; `AdjustActiveReportCountAsync`
stays as set-based `ExecuteUpdateAsync`); all moderation service filter bypasses changed from parameterless
`IgnoreQueryFilters()` to `IgnoreQueryFilters(["IsTakenDown"])` so `ContentRating`/`GroupAudience` stay
live — a moderator's rating reach equals their `ShowMatureContent` setting; report rows for entities filtered
by `ContentRating` are dropped (not placeholder-labelled); no-op `IgnoreQueryFilters()` on `ReadDb.Reports`
removed. Verified: `dotnet test` green (see verification section below). Note: integration tests for the new
per-mod rating-scoping behavior still to be added (see plan).

**Stage note (filter revamp — 2026-06-27):** `ServerModerationWriteService` — all 11 `IgnoreQueryFilters(
["IsTakenDown"])` on `writeDb` removed. Write context sees ground truth by architectural rule (no filters);
no bypass is needed when mods load entities to act on them. Moderation *read* bypasses in
`ServerModerationReadService` (`~6 calls`) kept — these are legitimate elevated reads (mod queue must see
taken-down content); each annotated `// elevated read:`. `ModerationServiceTests.ResolveWithRemovalAsync_
SoftHides_DropsFromPublicQuery_VisibleWithIgnoreFilter` corrected to use `ReadOnlyApplicationDbContext` for
the public-visibility assertion (was using write context, which is now unfiltered). Tests: Integration tier,
all 1232 pass. See `audit/Stories.md` §"Filter revamp Stage note" for the full cross-cutting narrative.

**Stage note (L4.5-Browser verification — 2026-07-02, Features 46/47/48 → L4.5=5):** full
report→claim→resolve and approve/reject cycles driven in a real browser against the seeded dev DB.
*(F47 and F48's halves were superseded on 2026-09-30: the WU-StoryLifecycle review fixes dropped both
cells to L4.5 = 1 because the surfaces changed under D1. The WU-StoryLifecycle browser pass the same
day re-drove them and returned both to 5 — F48's browser-verification Stage note.)*
- **F46:** report filed on a chapter comment via `ReportDialog` (reason select + notes + submit);
  `reports` row verified in psql (reporter/status/notes correct).
- **F47:** `/mod/reports` as ModUser listed all three open reports; Claim → `UnderReview` +
  `ModeratorUserId` stamped; Act panel enforced the removal-reason validation ("A removal reason is
  required"), then Hide content → report `Resolved` (`ActionTaken` + `DateResolved` stamped), target
  comment `IsTakenDown=t` with `TakedownReason`, `ActiveReportCount` decremented to 0, and the
  comment no longer renders in the chapter thread. Takedown also fired the account-action
  notification to the comment author (type 70).
- **F48:** per-mod ContentRating scoping verified in both directions — ModUser
  (`ShowMatureContent=f`) saw only the E-rated pending story; AdminUser saw both. Approve →
  `StoryStatusId = PostApprovalStatus` (InProgress) + `StoryApproved` notification (type 75) to the
  author; Reject → validation requires a reason, then `Rejected` + `TakedownReason`/`TakedownDate`
  stamped + `StoryRejected` notification (type 71).
- **Seeder bug found & fixed same-session:** `DataSeeder` stamped `PostApprovalStatus = status` for
  every story, making the two PendingApproval seeds self-referential (approval would have been a
  silent no-op). Seeder now maps PendingApproval → InProgress; live rows patched via psql. The
  production submit path was already sound (`CanSubmitForApproval` requires a resolved status —
  Unit-covered; approve semantics Integration-covered by `ApproveStoryAsync_SetsPostApprovalStatus_
  NotifiesAuthor`).
- L4 stays 3 (functional styling, not design-reviewed) — nothing unusable found.

**Stage note (2026-07-18) — supersedes the F48 bullet above.** The 2026-07-02 browser verification
("ModUser saw only the E-rated pending story; AdminUser saw both") documented the *old* per-mod
`ContentRating` scoping on `GetPendingSubmissionsAsync`. That scoping is now removed — see Feature
47's 2026-07-18 Stage note above for the full decision-row-1 reasoning ("work surface, show all"),
which covers both queues. `GetPendingSubmissionsAsync` now bypasses `IsTakenDown` and `ContentRating`
alike; a T-only or mature-off moderator sees every pending submission regardless of rating.
**Verified:** Integration (`ModerationServiceTests.GetPendingSubmissionsAsync_ShowsMRatedSubmission_
ToModWithMatureOff`); `dotnet test` full suite green.

**Stage note (WU-StoryLifecycle — 2026-09-30) — no cell flips at the build; F48 stayed L1–L3.5=5,
L4=3, L4.5=5, L5=5** *(L4.5 was flipped 5→1 later the same day by the review fixes — next note)*. Owner ruling D1 (worksheet, answered 2026-08-04) made the queue mandatory for an
author's **first** submission only, and its moderator half was unguarded: approve/reject loaded with
`SingleAsync` and threw `InvalidOperationException` for "not pending" (→ **401** "session
expired" over HTTP), approve accepted any `PostApprovalStatus` (approve-into-Draft), nothing
checked the author was still live, and two moderators — or a moderator and the author withdrawing —
could both act on one row.

- **L1:** `User.ApprovedStorySubmissions` (int, default 0) + `User.CanAutoApprove` (bool, default
  true, `HasSentinel(true)`) on `AspNetUsers` — records of a decision, monotonic, deliberately
  outside `user_stats` so `UserStatRecalculator` can never "recompute" them; `Story.SubmittedDate`.
  Migration `WU_StoryLifecycle` backfills trust for every existing author of a published story.
- **L2:** approve/reject use `SingleOrDefault` (unknown → 404) and throw
  `ModerationValidationException` (400) for "already handled", a non-entry `PostApprovalStatus`, and
  a non-live author (null `AuthorId`, Banned, or Suspended with a null/future end — stricter than
  `CanalaveSignInManager` on the null date; reject is never author-guarded). Approve runs the
  conditional status flip (`WHERE PendingApproval`, `PublishedDate ?? now`) and the author's `+1`
  in one execution-strategy transaction — 0 rows → throw, nothing incremented. Reject is one
  conditional update. New `SetCanAutoApproveAsync` (revoke **and** restore — a one-way lever would
  be the "irreversible in-app" defect class) files a moderator-initiated audit `Report`. The
  pending queue orders by, and returns, `SubmittedDate` (`PublishedDate` is NULL for every pending
  story now). `ModerationEndpoints` dropped approve/reject from its "Known EndpointHelpers mismatch"
  note and gained `POST /users/{id}/auto-approve`.
- **L3/L3.5 (`ModSubmissionsPage`):** the "submitted" column renders the nullable date ("—" when
  absent); approve failures now have a queue-level `ErrorAlert` (before, the only error slot lived
  inside the reject panel, so an approve failure showed nowhere); a `ModerationValidationException`
  from approve or reject reloads the queue so a row handled elsewhere disappears.
- **L5:** `ClientModerationWriteService` maps 400 → `ModerationValidationException` (was
  `ArgumentException`, which `ExceptionPresenter` treats as non-user-facing — every guard message
  would have collapsed to the generic error on WASM). Pulled forward from service audit §2.7.5,
  owned by WU-ModerationIntegrity, which now only verifies it.

**How verified:** `dotnet test` green (counts in `audit/Stories.md` §"WU-StoryLifecycle Stage
note"). **Integration** — `ModerationServiceTests` (approve stamps the date and records exactly one
approval; an earlier publish date survives; double approve and approve-after-withdraw both throw and
count nothing; Draft/OnHiatus targets refused with status unchanged; banned / future-suspended /
null-dated-suspended / deleted authors refused while reject still clears the row; an expired
suspension is live; unknown story → `KeyNotFoundException`; reject on Draft/published/Rejected
refused; queue ordered by `SubmittedDate`; auto-approve revoke writes the flag and a
moderator-initiated report, unchanged writes nothing, non-mod and self refused; a new user defaults
to `CanAutoApprove = true`), `ModerationEndpointsTests` (not-pending approve → **400**, not the old
401; unknown → 404; auto-approve non-mod → 403). **RazorComponents** —
`ModSubmissionsPageStoriesTests` (nullable date; already-handled approve/reject reloads; a
live-author refusal keeps the row). **Unit** — `ClientStoryLifecycleServiceTests` (400 →
`ModerationValidationException` with the server's text). Smoke-tested over HTTP on a freshly seeded
scratch DB (approve 204, second approve 400 "already handled", queue JSON carries
`submittedDate`, auto-approve revoke wrote the audit row). **Not browser-verified** (no browser
available): tracker **H12**. L4 stays 3. Rule of record: `layer2-services.md` §"Story Lifecycle".

**Stage note (WU-StoryLifecycle review fixes — 2026-09-30) — F48 L4.5 5→1; takedown freeze; the
conditional writes are now tested themselves.** Three independent reviews of the WU-StoryLifecycle
commit found:

- **L4.5 flipped 5→1** (headline): the 2026-07-02 browser pass drove a queue and an approval workflow
  that no longer behave as this section describes.
- **L2 — taken-down rows reached approve/reject.** The queue lifted the `IsTakenDown` filter and
  neither method checked it, so a taken-down story an author had unpublished and resubmitted over the
  API could be approved (+1 trust) or rejected (overwriting the takedown's `TakedownReason`/
  `TakedownDate` and leaving a `Rejected` story under the takedown) — exactly the overlap D1 confines
  rejection to pre-publication to avoid, while `layer2-services.md` claimed it could not happen. Now:
  the queue keeps the `IsTakenDown` filter on; approve and reject refuse an `IsTakenDown` row
  (`ModerationValidationException`); both conditional updates also carry `!IsTakenDown`. Paired with
  the author-side freeze in `TransitionStatusAsync` (`audit/Stories.md` F4 review-fixes note). Rule:
  `layer2-services.md` §"Story Lifecycle" — a taken-down story's status is frozen until the takedown
  is reversed (derived from D1's rationale; flagged).
- **L2 tests — the guard nobody exercised.** Every "already handled" test was stopped by the pre-read
  status check before it reached the conditional `WHERE`, so deleting the `WHERE` predicate — or
  moving the trust `+1` out of the transaction — left the suite green. New Integration tests drive the
  conditional write itself with `InterleavingCommandInterceptor` (`testing.md` §"Testing a
  check-then-act guard"): the author withdraws between approve's read and its update (throws, status
  stays Draft, counter 0); another moderator approves between reject's read and its update (throws,
  no reason written); the trust-record write fails inside approve's transaction (the status flip rolls
  back). Each was mutation-checked.
- **L3/L3.5 (`ModSubmissionsPage`):** the reload after a guard refusal ran unguarded inside the catch
  block — a failing reload escaped the event handler (circuit kill / error boundary) and stranded the
  queue on "Loading…". It now runs through `ReloadAfterRefusalAsync` (failure appended to the
  queue-level message, stale queue kept), and `LoadAsync` resets `_loading` in a `finally`. The page
  and read-service comments no longer claim the queue holds only first-ever, never-published
  submissions: a revoked author's resubmission lands here and keeps its old `PublishedDate`.
- **L1 test honesty:** the sentinel test only proved the default. Probing showed EF Core 10 infers
  `Sentinel = true` from `HasDefaultValue(true)` on its own, so `HasSentinel(true)` is a statement of
  intent and the `= true` initializer is the load-bearing half; `layer1-data-model.md` corrected, and
  a new test inserts an explicit `false` and reads it back.

**How verified:** `dotnet test` green (counts in `workplan.md`'s WU-StoryLifecycle entry, review-fixes
bullet). **Integration** — `ModerationServiceTests.ApproveAndReject_TakenDownPendingStory_*` (queue
hides it; both refuse; takedown reason/date intact; counter 0),
`ApproveStoryAsync_StatusChangesBetweenReadAndWrite_*`,
`ApproveStoryAsync_TrustRecordWriteFails_RollsBackTheStatusFlip`,
`RejectStoryAsync_StatusChangesBetweenReadAndWrite_*`,
`NewUser_InsertedWithCanAutoApproveFalse_KeepsFalse`. **RazorComponents** — `ModSubmissionsPageStoriesTests` (a refusal whose reload then fails,
for approve and reject: message kept, no "Loading…", queue kept — mutation-checked against the old
unguarded reload). **Not browser-verified** — tracker **H12**.

**Stage note (WU-StoryLifecycle browser verification — 2026-09-30) — F47 and F48 L4.5 1→5.** This
closes tracker **H12**'s moderator half; the author half is in `audit/Stories.md` F4. The dev DB was
freshly reseeded. Users: AdminUser as the moderator (he sees the M-rated pending story); TestUser,
AuthorAlpha and AuthorBeta as authors. Each phase was confirmed from the network log: the circuit
showed `_blazor/negotiate` and no `/api/moderation` call; WASM showed no negotiate and the `/api`
POST plus the reload GET. `psql` was checked after every write.
- **`/mod/submissions` (F48):**
  - The queue is ordered and dated by `SubmittedDate`: the two seeded rows (2026-09-01 and 09-03),
    then the new submission (2026-10-01).
  - With story 9 marked taken down by a temporary `psql` fixture, it was absent from the queue, and
    an anonymous GET of it 404'd. The flag was restored afterwards.
  - Approve on WASM: Completed, `published_date` stamped, TestUser's `approved_story_submissions`
    0 → 1, and one type-75 notification.
  - The same row approved from a stale circuit tab: "This submission was already handled.", the
    queue reloaded, the counter stayed 1.
  - Reject with an empty reason: "A rejection reason is required."
  - Reject with a reason on the circuit: Rejected, the reason stored in the takedown columns
    (tracker D6), `is_taken_down` false, one type-71 notification.
  - Approve of that rejected row from a stale WASM tab: 400 → the same readable message and a reload.
    This confirms the client 400 → `ModerationValidationException` mapping.
  - Reject on WASM: a revoked author's resubmission that had been published before.
- **`/mod/users/4` (F47):**
  - Trust line "4 approved submissions · auto-approve on".
  - Revoke on the circuit:
    - With an empty reason: "A reason is required."
    - With a reason: `can_auto_approve` false and a moderator-filed audit report (reporter =
      moderator = 2, entity User 4, resolved, "Auto-approve revoked: …"). The line and the button
      flipped, and the author's next submit queued.
  - Restore on WASM: the flag back to true, a second audit row "Auto-approve restored: …", and the
    author's next submit published directly.
  - A 1,500-character reason on WASM: "That reason is too long."; nothing written.
- **Logs:** zero `fail:`/`crit:` lines from these surfaces, and no console errors.

The pass's three bugs were all on the author editor (`audit/Stories.md` F4); none were on these
pages. Observed but not fixed, filed as tracker items:
- The card's "Approve → publishes as InProgress" prints the raw enum name. A queue-level "already
  handled" message also lingers when the next row's reject panel opens. Both are polish under L4 = 3
  (tracker **H15**).
- The reject and auto-approve reasons travel in the query string (tracker **D9**). Every moderation
  write endpoint that takes free text has this shape; the new auto-approve endpoint followed it.

**WU-ModerationIntegrity Stage note (2026-09-30; recorded by its review fixes) — no flip; L2 stays 5.**
Two F48 changes landed beneath L2 and the build recorded neither here:
- **The queue read gates in the service** (owner ruling D9): `GetPendingSubmissionsAsync` opens with
  `ActiveUser.RequireModerator()` — a signed-in non-moderator gets `UnauthorizedAccessException` (403),
  an anonymous caller `InvalidOperationException` (401) — and its handler runs inside `ExecuteAsync`.
  `/mod/submissions`' page `[Authorize]` no longer stands alone on the circuit.
- **The trust-waiver audit row carries `ReportedUserId`** (D8; amendment U5): `SetCanAutoApproveAsync`'s
  moderator-filed `Report` sets it to the target user, so the revoke/restore rows stay in that user's
  `/mod/users` history now that the history reads `ReportedUserId` (B18).
Verified by Integration `ModerationIntegrityTests.EveryModeratorOnlyRead_RefusesASignedInNonModerator`
(the queue read among the eight) and `ModerationServiceTests.SetCanAutoApproveAsync_Revoke_WritesFlagAndAModeratorInitiatedReport`
(asserts `ReportedUserId`). Nothing a moderator sees on `/mod/submissions` changed (a moderator passes
the gate), so L4.5 stays 5. **Browser-driven 2026-09-30** by the WU-ModerationIntegrity pass:
`/mod/submissions` (both tabs) renders for a moderator on both phases, and an EV account was approved
from it on the circuit. `/api/moderation/submissions` answers a moderator 200 (F47's
browser-verification note).

## Feature 53 — External Story Links & Verification (reframed 2026-07-11)

**Reframe (settled 2026-07-11, WU38d plan — supersedes the "Story Import & Verification" scope
below):** the feature is **"Also posted on" external story links**, plain language, display-first.
A story lists the other sites it's also live on (X, Y, Z — *multiple* links), shown low-key on the
story page (after the chapter list, before recommendations — awareness, not an invitation to click
away). Each link has a `VerificationStatus`, flipped through WU39's two-tier verification workflow
(settled 2026-07-24, see below); the reader-facing display is a muted "reviewed" indicator, not a
checkmark.
**Purpose (anti-theft):** anyone can pull a story off AO3/FFN via FicHub and re-upload it under
their own account — community members who recognize a story and see unverified links report it
(Feature 46, existing flow) for takedown. The site is anti-predatory even toward non-users: it
protects the wider Pokémon author population, including inactive authors. File-format *content*
ingestion (the other thing "import" used to mean here) is now **Feature 63** (`audit/Import.md`).

**Settled (WU38d — author-facing half, do not revisit):**
- **Data model:** `StoryImport` (one row, single source) remodeled to `StoryExternalLink` (many per
  story: `StoryExternalLinkId`, `StoryId`, `ExternalPlatformId` FK, `Url`, `VerificationStatus`
  enum-mapped `short` (`Unverified`/`Verified`/`Rejected`), `DateAdded`) + seeded
  `ExternalPlatform` lookup (`Name`, nullable `DomainPattern` for paste-a-URL auto-detect;
  "Other" row displays the URL's host). **Deliberately a lookup table, NOT a hybrid C# enum** —
  no compile-time branching on platform; the fanfic world's long tail of small archives should be
  seed rows, not code changes; matches the `ReportReason`/`ReportStatus` pattern. WU39 hangs
  per-platform verification properties off this table, not code branches. Entities live in
  `Core/Stories/` (story-page display is the primary use).
- **WU38d ships:** the remodel migration, story-page "Also posted on" row (checkmark only when
  `Verified` — **display superseded 2026-07-24 by WU39's "reviewed" sub-line, see below; the
  underlying `VerificationStatus == Verified` gating is unchanged**), `StoryPropertiesForm`
  repeatable link rows + original-dates edit surface,
  write-service sync (new links start `Unverified`; editing a verified link's URL resets it to
  `Unverified`). `Story.OriginalPublishedDate`/`OriginalLastUpdatedDate` already exist — no
  migration for those.
- **Dropped by the reframe:** "route the story into `PendingApproval`" — links don't gate story
  approval (Feature 48 untouched); verification is per-link, display-only.

**WU39 settled design (2026-07-24 — resolves the "still open" question below; supersedes the
WU38d checkmark display noted above):** the two-way-link mechanism is settled as a **two-tier
verification model**, both tiers confirmed by a moderator opening the URL in their own browser —
**no server-side outbound HTTP / scraping, ever** (SSRF surface + Cloudflare/FFN blocking risk;
permanently deferred, not a future phase of this feature).

- **Account tier (new `UserExternalIdentity` entity):** a user proves control of an external
  platform *account* once per (user, platform), ever. The system gives each user a single
  site-wide public code (`TCL-Verify-XXXXXX`, lazily generated, shown at the bottom of their own
  TCL profile — a public nonce, no security downside to publishing it; doubles as a "find me on
  TCL" discovery nudge). The author places it on the external profile (placement surface is
  platform-specific — a column on `ExternalPlatform`, not a code branch; e.g. FFN's profile bio,
  as plain text since FFN disallows profile hyperlinks — "two-way" means both sides show the same
  code, not a mutual hyperlink). A moderator opens the profile URL, confirms the code text is
  present, flips the identity's `VerificationStatus`.
- **Per-link tier (existing `StoryExternalLink.VerificationStatus`, reused, not retired):** even
  with a verified account, each linked story still needs its own authorship confirmation, because
  platform work URLs (`/works/12345`, `/s/1234567/…`) don't name the author — "account verified"
  ≠ "this specific story is theirs." Once the account tier is Verified for that platform, the
  author may request per-link review; a moderator opens the linked story, confirms its listed
  author matches the confirmed handle, flips that link. No per-link mod-queue item exists before
  the account tier is Verified.
- **Reader display — no checkmark, ever (settled, supersedes all "checkmark" language above):** a
  reviewed link shows the plain external link (new tab) plus an indented, muted sub-line —
  "reviewed · author's account: `<handle>` ↗" (linking to the confirmed profile) — only when the
  per-link tier is `Verified`. A checkmark asserts one-time-judgment-as-permanent-trust and invites
  complacency; the muted sub-line instead invites the reader to click through and compare, every
  time. Never-requested / pending / rejected links are **visually identical** (a plain link) —
  deliberate: reporting is driven by a reader's own outside knowledge that a story belongs to
  someone else, never by reading TCL's internal verification microstate, so an "unverified" flag
  would catch ~nobody and only cast suspicion on slow-but-legitimate authors. No dates anywhere in
  this UI (a "content changed since review" staleness signal was considered and explicitly
  rejected as overengineering against a weak-incentive edge case).
- **Verification stays display-only — unchanged from the reframe above:** it adds no new gate.
  Rejected links are **not** hidden (hiding reads as an accusation and invites misdirected
  reports) — the author gets private feedback (status + reason + notification) to fix and
  re-request; a moderator who suspects actual theft uses the *existing* Feature 46 report / 48
  takedown path by hand. No new automation for that case either.
- **Cluster decision:** the verification *entity + service* (`UserExternalIdentity`,
  `IExternalVerificationReadService`/`WriteService`) colocate in `Core/Stories`/`Server/Stories`
  alongside `StoryExternalLink`/`ExternalPlatform` (precedent: `ISpotlightSlotAllocator`, a
  mod-gated feature service living in its own cluster, not folded into `IModerationWriteService`).
  Only the mod **review tab** (`/mod/submissions` → Imports) is Moderation-cluster UI, injecting
  the Stories-cluster service — mirrors `ModStatsPage`→`ISiteDailyStatReadService`,
  `ModSpotlightPage`→`ISpotlightSlotAllocator`. See `folder_clusters.md`.

**WU-InertFeatures (2026-09-30), no flip — L2 stays Stage 5:** the four verification outcomes (76–79)
are null-sourced (owner ruling D5 — the reviewing moderator is no longer named to the author; the
account tier still records `ReviewedByModeratorUserId` internally), 76/77 are exempt from
cross-existing dedup (D4), and 76–79 gained their own actor-free presenter arms (previously the
catch-all "You have a new notification"). Covered by Integration `ExternalVerificationTests` (all four
assert `SourceUserId == null`) and Unit `NotificationPresenterTests`. **Review fixes (2026-09-30), no
flip:** a moderator reviewing their own account or their own story's link gets no 76–79 (the link tier
reads the moderator id again for this check only). Integration
`AModeratorReviewingTheirOwnAccountAndLink_GetsNoOutcomeNotification`.

**WU-ModerationIntegrity browser verification Stage note (2026-09-30) — F53 L4.5 1→5.** AuthorAlpha,
`psql` after every write:
- **Setup:** AuthorAlpha submitted an AO3 account in Settings, and ModUser approved it from
  `/mod/submissions` → Imports (circuit). An AO3 link was then saved on story 2.
- **The refusal, on both phases.** With the editor open and its "Request verification" enabled, the
  account was re-submitted from another tab, which resets it to Unverified. For the second run it was
  first re-marked Verified through `psql`. Clicking Request then read "Verify your Archive of Our Own
  account first." inline and wrote nothing:
  - WASM: `POST …/links/1/request` 400, and no session-expired redirect;
  - circuit: no `/api` call, and no generic error.
- **Positive path:** with the account Verified again, the request landed ("Pending moderator review",
  `date_verification_requested` set) and appeared in the moderator's link queue.
- **Settings (WASM):** a profile URL of `ftp://…` read "Profile URL must be an absolute http or https
  URL." in the page's feedback alert (400).
Detail: F47's browser-verification note.

**WU-ModerationIntegrity review-fixes Stage note (2026-09-30) — F53 L4.5 5→1.** The build changed what
an author sees when a verification request breaks a business rule ("Verify your X account first", a
profile URL that is not absolute http(s), a blank handle, a platform without verification): it was `InvalidOperationException`, which the endpoint maps to 401, so WASM
showed the session-expired path and the circuit the generic error; it is now
`ExternalVerificationValidationException`, shown inline on both phases. Nobody drove that in a browser,
so L4.5 drops to 1 until tracker **H19**'s EV step (both render phases). No code changed in this note.

**WU-ModerationIntegrity Stage note (2026-09-30) — no flip at the build; L4.5 lowered by the review
fixes (note above).** Owner ruling D9 and its sub-edge: the two
queue reads (`GetPendingAccountVerificationsAsync`, `GetPendingLinkVerificationsAsync`) gate in the
service with the shared `RequireModerator()`, and their handlers wrap in `ExecuteAsync`. Business rules
throw the new `ExternalVerificationValidationException` (400 — they were `InvalidOperationException` →
401, so "Verify your X account first" read as an expired session), and unknown identity/link/platform
ids are `KeyNotFoundException` (404). `ClientExternalVerificationWriteService` reconstructs the new type
from a 400. The private `RequireModerator` copy is gone. Verified by Integration `ExternalVerificationTests`
(the two business rules now expect the new type; unknown ids; a non-moderator refused on both queues)
and Unit `ClientExternalVerificationServiceTests` (400 → the new type). Left open: the author/moderator
interface split (tracker F15) and status guards on the approve/reject actions (tracker D10).

**Stages (updated 2026-09-30, WU-ModerationIntegrity browser verification — L4.5 back to 5; the build's
service gates and exception types sit beneath L2; WU39 2026-07-25 before that):** L1 — Stage 5.
L2/L3-Logic/L3.5-Structure — Stage 5
(WU39 shipped the mod-verification half; both tiers built, tested, browser-verified end to end).
L4-Style — Stage 1 (pending visual/token sign-off, per the WU8/WU13/WU23/WU28/WU37/WU41
precedent — functional browser verification is not the same as visual polish). L4.5-Browser —
Stage 5 (WU39 drove it to 5; the review fixes lowered it to 1 because the business-rule refusals
changed undriven; the browser pass the same day drove them inline on both render phases — tracker H19
closed, browser-verification Stage note above). L5/L6/L8 — N/A.

**WU38d Stage note (2026-07-11) — author-facing half shipped:**
- **L1:** migration `WU38d_StoryExternalLinks` (drop `story_imports`, create
  `story_external_links` + seeded `external_platforms` ×7, unique `(story_id, url)`,
  Restrict FK to the lookup). Applied cleanly to the 3012-story dev workbench and to
  Testcontainers in every integration run. Global URL uniqueness (old rule) deliberately
  dropped — whether two stories claiming one URL is theft is a WU39/Feature-46 judgment, not a
  schema constraint a thief could use to squat a URL.
- **Built:** `Core/Stories/{StoryExternalLink, ExternalPlatform}` + `VerificationStatusEnum`;
  `StoryExternalLinkDto`/`EditDto`/`ExternalPlatformDto`; `GetExternalPlatformsAsync` on
  `IStoryReadService`; projection into `StoryDetailsDTO.ExternalLinks` and
  `GetStoryForEditAsync`; write-service sync (match on (platform, URL): unchanged rows keep
  status, missing rows delete, new rows start Unverified — so editing a verified link's URL
  resets it); URL validation (absolute http/https); `StoryPropertiesForm` "Also posted on"
  section (repeatable rows, paste-a-URL platform auto-detect via `DomainPattern`, original-dates
  inputs); `StoryExternalLinksRow` on the story page (after chapters, before recommendations —
  settled placement; checkmark + "Author verified" tooltip only when `Verified`; `rel=nofollow`).
- **Verified:** Integration (`StoryExternalLinkTests`, 7 tests — seeded lookup, Unverified start,
  dedupe/blank-drop, invalid-URL validation, **verified-kept vs URL-change-reset sync semantics**,
  row deletion, original-dates round-trip); RazorComponents (`StoryExternalLinksRowTests`, 4 —
  checkmark gating, absent-when-empty, and the settled after-chapters/before-recommendations
  placement asserted on `StoryDesktop`); browser (2026-07-11) — add-link with live AO3
  auto-detect, save, psql-confirmed Unverified row + original date, story-page row rendered in
  place, psql flip to Verified → checkmark + tooltip appeared.

**WU39 Stage note (2026-07-25) — mod-verification half shipped:**
- **L1:** migration `WU39ExternalLinkVerification` (new `user_external_identities` table — unique
  `(user_id, external_platform_id)`, FKs to `AspNetUsers` (owner Cascade, reviewing moderator
  SetNull) and `external_platforms` (Restrict); `story_external_links.date_verification_requested`
  + `.rejection_reason` columns; `external_platforms.placement_instructions` +
  `.supports_verification` columns + seed update (6 platforms `true` with per-platform placement
  text, "Other" `false`); `asp_net_users.verification_code` + filtered unique index; four
  `NotificationType` seed rows). Applied cleanly to the dev workbench and Testcontainers.
- **Built:** `Core/Stories/{UserExternalIdentity, PublicVerificationCode}`;
  `IExternalVerification{Read,Write}Service` + `Server{...}` impls (account-tier
  submit/approve/reject, per-link request/approve/reject, the account-gates-link-request rule,
  the pending-account/pending-link mod queues); `ServerStoryReadService` projection changes
  (`StoryExternalLinkDto` gains `IsReviewed`/`AuthorAccountHandle`/`AuthorAccountProfileUrl`,
  correlated on the story author's Verified identity for that platform;
  `StoryExternalLinkEditDto` gains `StoryExternalLinkId`/`VerificationStatus`/
  `VerificationRequested`/`RejectionReason`); `ExternalVerificationEndpoints` + Client read/write
  services; four `INotificationWriteService` methods (`NotifyExternalAccount{Verified,Rejected}`,
  `NotifyExternalLink{Verified,Rejected}`) + `KindFor` branches; `WriteActionKind.VerificationRequest`
  (5 burst / 1 per 2 min, mirrored in `security.md`). UI: `StoryExternalLinksRow` reworked to the
  two-line "reviewed · author's account: `<handle>`" model (no checkmark); `StoryPropertiesForm`
  per-link status label + gated "Request verification" button; new
  `ExternalAccountsSettingsForm` (code + per-platform submit) composed into `SettingsPage`;
  public verification-code line on `ProfilePage`'s Profile tab (`ProfileHeaderDto.VerificationCode`);
  `ModSubmissionsPage` Imports tab replaced with the two live queues (account + link), reusing the
  Stories-tab Approve/Reject/reject-reason idiom exactly.
- **Verified:** Integration (`ExternalVerificationTests`, 22 tests — account create/resubmit/
  approve/reject + notification rows, per-link request gating (throws without a Verified account,
  throws for non-owner) + approve/reject + notification, both mod queues' filter semantics
  (account queue = Unverified only; link queue = Unverified + requested + account Verified for
  that platform, with an explicit case proving a requested link on an unverified account is
  excluded), story-page projection both ways; `ExternalVerificationEndpointsTests`, 5 tests — mod
  routes 403 for a signed-in non-mod / 401 anonymous, author routes 401 anonymous; extended
  `StoryExternalLinkTests` with a test proving a URL edit clears `DateVerificationRequested`
  alongside the existing status-reset assertion). RazorComponents (`StoryExternalLinksRowTests`
  rewritten — reviewed sub-line + handle link vs. plain non-reviewed line, no checkmark/date
  anywhere, settled placement preserved; new `ExternalAccountsSettingsFormTests`, 7 tests; new
  `ModSubmissionsPageImportsTests`, 9 tests — both queues render, Approve/Reject fire with the
  right id, reject requires a reason; `StoryPropertiesFormTests` extended — request button
  enabled only when the platform is in `VerifiedPlatformIds`, fires the callback with the link id,
  status label per state). Unit (`PublicVerificationCodeTests`,
  `LinkVerificationStatusHelperTests`, `ClientExternalVerificationServiceTests` — route/verb
  shapes + status-code translation). Full suite green: 752 Unit + 544 RazorComponents + 783
  Integration. Browser (2026-07-25, server-only path) — full live walk as AuthorAlpha/AdminUser:
  Settings generated `TCL-Verify-MNQEPU`, submitted an AO3 account (→ "Pending moderator
  review"), added an AO3 link to a story (paste-URL auto-detect confirmed), per-link "Request
  verification" correctly disabled with the "verify your account first" hint pre-approval; mod
  Imports tab showed the account request with its code and the profile link, approved it; the
  per-link button then enabled, requested, appeared in the mod's link queue showing the confirmed
  handle `gengarlover` for comparison, approved; the public story page rendered the settled
  two-line "reviewed · author's account: gengarlover ↗" sub-line with no checkmark, both links
  `target="_blank"`/`rel=nofollow`. psql confirmed `user_external_identities` Verified +
  moderator-stamped, `story_external_links.verification_status` Verified, `AspNetUsers
  .verification_code` matching the UI, and both `ExternalAccountVerified`(76)/
  `ExternalLinkVerified`(78) notification rows with the correct `related_entity_id` (0 / storyId).

## Feature 62 — SiteDailyStat Worker

**WU-ModerationIntegrity Stage note (2026-09-30) — no flip.** D9's sweep: `GetLatestAsync` and
`GetSeriesAsync` are moderator-only, so `ServerSiteDailyStatReadService` now takes `IActiveUserContext`
and gates both with `RequireModerator()`; the two handlers wrap in `ExecuteAsync`. Verified by
Integration `ModerationIntegrityTests.EveryModeratorOnlyRead_RefusesASignedInNonModerator`;
`SiteDailyStatAggregatorTests` is unaffected (it drives the aggregator, not the read). **Browser-driven
2026-09-30** by the WU-ModerationIntegrity pass: `/mod/stats` renders for a moderator on both phases,
and `/api/site-daily-stats/latest` and `/series` answer 200 to a moderator, 403 to a member and 401
anonymous (F47's browser-verification note).

**Stages (updated 2026-09-30, WU-CounterSymmetry `total_words` predicate beneath L8, no flip;
WU-ModerationIntegrity read gate beneath L2, no flip; WU-StoryLifecycle review fixes before that):**
L1–L3.5 = 5, L4 = 3, L4.5 = 5,
L5/L6 = N/A, L8 = 5 — unchanged; `new_chapters`/`new_words` re-sourced to `Chapter.FirstPublishedDate`
(D2), with one known divergence from `new_stories` left for the owner (`roadmap.md` decision row 16) —
see the two WU-StoryLifecycle Stage notes at the end of this section.

**WU-CounterSymmetry Stage note (2026-09-30; service audit §2.4.6) — no flip.** `total_words` summed
`stories.word_count` over **every** story (drafts, pending, rejected, taken down, and stories first
published after the day), while `total_stories` beside it counted only visible stories published by
the day's end. It now uses that same predicate: `WHERE published_date < @range_end AND
{VisibleStoryPredicate}`. Combined with `Story.WordCount` now counting published chapters only
(`audit/Chapters.md` F6), the stock means "published words on visible stories". `layer8-data-marts.md`
§`site_daily_stats` states it. **Test tier: Integration.** `SiteDailyStatAggregatorTests` seeds three
excluded stories (a Draft at 1,000 words, a taken-down story at 2,000, one first published the day
after at 4,000), and `UpsertDayAsync_ComputesEveryCounter` now asserts `TotalWords == 800` and
`TotalStories == 2` exactly (was `>=`). Mutation-checked: the old subquery gives 7,800.

**Requirements settled 2026-07-10 (WU-SiteDailyStat plan)** — reconciling the Gemini design source
(`GeminiDiscussions/MyActivity September to November 2025_filtered.md:38146`, 2025-10-29) against
the live schema. Full counter-by-counter source audit, the `new_`/`total_` rule, and the privacy
reasoning for `active_users` live at `.claude/skills/canalave-conventions/layer8-data-marts.md`
§`site_daily_stats` — this note carries the settled-vs-open constraints for the build session, per
Doc-Touch Timing.

**Settled constraints (do not revisit without a Stage-4 diagnosis):**
- `site_daily_stats` is an **append-only time-series of ground truth**, upserted
  (`INSERT … ON CONFLICT (stat_date) DO UPDATE`) — **not** a swap-table rebuild like the three
  discovery marts. L1 was previously N/A ("Phase A removed the EF model, raw-SQL mart") — that is
  now reversed: `SiteDailyStat` gets a normal EF entity + `DbSet` + migration (the one documented
  L8 exception — low-volume ground truth with rich time-series reads, unlike the rebuildable marts
  or the SUM-only `daily_story_stats`). The worker still writes only via raw SQL, never through the
  EF change tracker. `DailyStoryStat` (a different, never-built table from the same Gemini
  discussion) stays dropped/never-modeled — do not confuse the two.
- Full column set, `new_`/`total_` split, exclusions (series/vouches/badges/messages/likes), and
  the `stories_approved`/`favorites_added` build-time source verifications: see the skill doc table.
  `total_stories` counts published/visible stories only.
- `active_users`/"last seen" requires new `User.CreatedUtc` + `User.LastActiveUtc` columns and a
  third Signal-Buffering signal (`LastActive*`, `Server/Identity/`) — **authenticated requests
  only**, no tracking cookie, gated for public display by the existing
  `PrivacySettings.ShowActivityStatus`. This is a build prerequisite, not part of the worker itself.
- A **user-facing dashboard is in scope** (`/mod/stats`, mod/admin-gated, per the user — beyond
  MVP "flourishes"), activating L2/L3-Logic/L3.5/L4/L4.5 for this row (previously N/A). L5 stays
  server-rendered (no WASM flip as part of this work).

**Resolved during build:** `favorites_added` is sourced from `UserStoryInteractionDate.FavoriteDate`/
`HiddenFavoriteDate`. `stories_approved` is **dropped** — confirmed no dated column exists anywhere
on the approval path (`ApproveStoryAsync` flips `StoryStatusId` with no timestamp write); adding one
is out of this build's scope. The moderation-health panel's approval signal is `reports_resolved`
only. *(Amended 2026-09-30, WU-StoryLifecycle: approve now stamps `PublishedDate` on a story's first
publication — still not an approval date, so `stories_approved` stays dropped.)*

**Resolved during build (chart set):** headline totals (users/stories/words); 3 small-multiple
growth line charts (one axis each — users/stories/words differ in scale, per the dataviz skill's
"never dual-axis" rule); a DAU line chart; a 2-series Reports-Filed-vs-Resolved line chart (shared
axis, both are "report counts"); a plain data table for the 12 flow counters — a table was chosen
over an 11-color bar chart per the dataviz skill's "sometimes the answer is not a chart" guidance
(too many disparate categorical counts for a legible fixed-hue-order palette).

**Stage note (WU-SiteDailyStat, 2026-07-11):** L1=5, L2=5, L3-Logic=5, L3.5-Structure=5, L4-Style=3
(functional Tailwind, passes `check-design-tokens.ps1`; not design-reviewed — same convention as
sibling mod pages), L4.5-Browser=5, L5=N/A (server-rendered only, no WASM flip), L6=N/A (the
`stat_date` PK is already the covering index for time-series reads — no additional index needed),
L8=5.

- **Built:** `SiteDailyStat` EF entity/migration + `User.CreatedUtc`/`LastActiveUtc` columns
  (`AddSiteDailyStatAndUserActivityColumns` migration); `UserActivityBuffer`/`Flusher`/
  `FlushWorker` + `ServerUserActivityWriteService` (`Server/Identity/`) — a third Signal-Buffering
  instance; `UserActivityTracker` (non-visual, mounted once in `Routes.razor`, stamps activity on
  circuit start + every navigation for authenticated users only); `SiteDailyStatAggregator` +
  `SiteDailyStatWorker` (`Server/Moderation/`) — one raw `INSERT … ON CONFLICT` per completed UTC
  day, day-boundary comparisons via explicit UTC range parameters (never a `::date` cast, which
  would be session-timezone-dependent); `ISiteDailyStatReadService`/`ServerSiteDailyStatReadService`
  (plain LINQ, since this is the one L8 table with an EF model); `/mod/stats` dashboard
  (`ModStatsPage.razor` + `DailyStatLineChart`/`StatTile`/`ActivityRow`); `ProfileHeaderDto`/
  `ServerUserProfileReadService`/`ProfileBanner` extended with `LastSeenUtc` (gated by
  `PrivacySettings.ShowActivityStatus`, same shape as the existing `Stats` gate); a `/dev/marts/
  site-daily-stat` diagnostic probe.
- **Test tiers:** Unit — `UserActivityBufferTests` (latest-timestamp coalescing/restore, mirrors
  `ViewCountBufferTests`), `SiteDailyStatWorkerTests` (`PreviousCompletedUtcDay`/`MissingDays` pure
  boundary logic, made `public` test seams per the repo's no-`InternalsVisibleTo` convention).
  Integration — `SiteDailyStatAggregatorTests` (one dated event of every counted kind seeded on a
  target day plus one deliberately outside the day's range to prove boundary filtering; every
  column asserted; a second pass proves the upsert **recomputes** rather than accumulates, unlike
  the view-count flusher's `+=`), `UserActivityFlushTests` (buffer→flush→`GREATEST` no-regression,
  mirrors `ViewCountFlushTests`). RazorComponents — **no dedicated test for `ModStatsPage`**: its
  `@code` is a thin init-load (service calls assigned to fields) with no `EventCallback`
  invocations or non-trivial computed state, which the repo's own testing convention says to skip
  ("cover what cannot be verified by reading the file"); sibling mod pages (`ModReportsPage`,
  `ModUsersPage`) — whose `@code` is more complex — carry no RazorComponents test either. The real
  logic (aggregation math) is covered by the Integration tier above.
- **Live browser verification (2026-07-11, server-only path, standing dev DB — not wiped):** the
  migration applied cleanly to 3012 existing stories / 2007 real users (all backfilled to the
  migration's deploy instant, confirming the documented one-time `new_users` deploy-day-spike
  limitation); the worker's bounded startup gap-fill backfilled 30 days unprompted, with real
  varying `new_comments` per day (121–283) proving day-boundary aggregation across genuine history;
  `/mod/stats` loaded and rendered live as AdminUser (mod-gate passed); the
  activity-buffer→flush→`active_users`/"Last seen Jul 11, 2026" loop was confirmed end-to-end on a
  real profile page, for both the owner (AdminUser) and a non-owner viewer (TestUser).
- `dotnet test`: 1421/1421 (524 Unit + 479 RazorComponents + 418 Integration).

**Stage note (WU-StoryLifecycle — 2026-09-30) — no cell flips; F62 stays L1–L3.5 = 5, L4 = 3,
L4.5 = 5, L5/L6 = N/A, L8 = 5.** Owner ruling D2 re-sourced two flows and fixed a third by side
effect: `new_chapters`/`new_words` now count on `chapters.first_published_date` (the chapter-level
publish anchor) instead of the primary version's `chapter_contents.publish_date`, so promoting an
alternate version never re-counts an old chapter; `new_stories` needed no SQL change, but drafts,
pending and rejected stories stopped counting because their `published_date` is now NULL (it used to
be stamped at creation). `stories_approved` stays dropped — approve now stamps `PublishedDate` on
first publication, but that is not an approval date (`layer8-data-marts.md` amended). **Verified:**
Integration (`SiteDailyStatAggregatorTests` — the fixture chapter now carries `FirstPublishedDate`;
new case: an old chapter whose newer version is promoted today does not count, so `NewChapters`
stays 1 and `NewWords` 500 — it fails against the old `publish_date` source).

**Stage note (WU-StoryLifecycle review fixes — 2026-09-30) — no cell flips; a recorded divergence,
not a fix.** The note above overstated the side effect: drafts, pending and rejected *stories* stopped
counting in `new_stories`, but their *chapters* did not stop counting in `new_chapters`/`new_words`.
`Chapter.FirstPublishedDate` is stamped on the chapter's own first publish whatever its story's status
(D2: "stamped once on the chapter's first publish", invariant tied to `IsPublished`), and the two
chapter flows have no story predicate — so chapters an author publishes inside a still-Draft story
(the normal way to prepare a first submission), and those of a story later rejected as spam, count
as new chapters on their own publish day, while the story never counts. (The pre-WU source,
`chapter_contents.publish_date`, had no story predicate either — this is not a regression.) D2 also
defines the anchor as "went live **on this site**", which such a chapter has not, so the stamp rule
itself is an owner question — promoted to `roadmap.md` **decision row 16** (tracker F9 item 5) rather
than guessed: adding the story predicate to L8 alone would instead drop pre-launch chapters from every
day's count. `layer8-data-marts.md` §`site_daily_stats` records the divergence.

### WU-AuditFixPass note (2026-07-18)

MA-123/MA-701 closed: `RequireModerator()`'s role branch throws `UnauthorizedAccessException`
(→ 403) for a signed-in non-mod, matching Spotlight/SiteSettings/Poll; the unauthenticated branch
stays 401. `ModerationServiceTests` pins the 403 contract. Error channels normalized: ModReportsPage/
ModUsersPage/ModSubmissionsPage route catches through `ExceptionPresenter` + `InlineAlert` (raw
`ex.Message` gone); `ReportDialog` logs unexpected failures at Error instead of silently swallowing
(MA-704). MA-702's Tier-3 edge role gate (RequireAuthorization(ModeratorOnly) on the mod-write
group) remains open. Full detail: `workplan.md` WU-AuditFixPass.

### WU-AuditFixPass-2 note (2026-07-18)

MA-702 closed (the edge role gate the pass-1 note left open), F46/F47/F62 (cells stay Stage 5 —
defense-in-depth added, no behavior change for legitimate mods): the named
`AuthorizationPolicies.RequireModerator` policy is now registered (Program.cs /
`Server/Identity/AuthorizationPolicies.cs`) and applied as the edge role gate on the mod-write group +
queue reads + SiteDailyStat (plus SpotlightSlotAllocator + SiteSettings write) — sits on top of the
service-layer `RequireModerator`, replacing 4 duplicated inline `AuthorizeAttribute` copies. Covered:
`ModerationEndpointsTests` (Integration) + browser both directions (non-mod → 403 on every mod route,
AdminUser → 200/204). Full detail: `workplan.md` WU-AuditFixPass-2.

---

**WU-ParentVisibility slice (2026-07-26) — F46.** `SubmitReportAsync` had **no existence check at
all**, so the queue could accumulate reports against ids that never existed while
`AdjustActiveReportCountAsync` bumped `ActiveReportCount` on drafts. Settled rule (2026-07-26): the
target must **exist always** — checked on the unfiltered write context, so a taken-down row still
counts as existing — and must be **visible to the reporter, except when the parent is hidden solely
by takedown**. That exemption is deliberate and tested: a good-faith report filed in the moments after
a moderator removes content must still land, rather than failing with a confusing not-found for an
action that was valid when the user began it. Everything else (unpublished, non-public story status,
rating without consent, Private profile, M-audience group) is refused. Moderator work surfaces remain
exempt from the invariant entirely, per `content-safety.md`.

Invariant, guards, and the two root causes: `identity-and-authorization.md` §"Parent-visibility guards" (conditionality kind (g)). Enforcement: `Tests.Integration/ParentVisibilityContractTests.cs`. Full narrative: `workplan.md` WU-ParentVisibility. **No Stage number changed — every affected cell was already Stage 5 and remains 5.**
