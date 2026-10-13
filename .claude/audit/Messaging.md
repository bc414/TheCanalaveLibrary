# Audit — Messaging/

**Feature:** 49 (private messaging). Route `/messages/{ConversationId:int?}`. Uses the full
`EditorView` for rich-text composition (§5.19). **Stateless, permanently — no SignalR** (hardened
2026-07-07 from an earlier "deferred post-MVP" framing; see WU35 note #2 below and
`cross-cutting.md` "Private Messaging Architecture").

## Shared Context

> **2026-07-18 — Desktop/Mobile fork removed (WU-ResponsiveMerge).** `MessagesDesktop`/`MessagesMobile`
> merged into `MessagesPage` (page renders its own markup; mobile variant deleted — conversation
> list and thread stack at narrow via CSS; a deliberate narrow master/detail model is a future
> mobile-phase decision). Narrow rendering is provisional. Desktop/mobile assertions elsewhere in
> this file are historical. Rules: `canalave-conventions/render-and-layout.md` §"Responsive Layout
> Architecture"; spec §3.9/§3.10 superseded on this axis.
> Verified 2026-07-18: full suite green post-merge (Unit 702 / Integration 727 / RazorComponents
> 510); browser smoke at desktop width clean (loads, no error banner, zero console errors);
> narrow rendering deliberately unpolished, no visual pass yet.

**Entities (now `Core/Messaging/` — to be migrated out of legacy `Core/Models/` during WU35):**
`Conversation` (`ConversationId`, `Subject` max 2048, `DateCreated` default),
`ConversationParticipant` (composite `(ConversationId, UserId)`, `LastReadTimestamp`, `IsArchived`,
Cascade from both User and Conversation), `PrivateMessage` (`MessageId` long, `SenderUserId?`
SetNull, `MessageText`, `DateSent` default, Cascade from Conversation). Three-table model.
DbSets + EF configs (`MessagingConfigurations.cs`) + FK edges all exist.
**Migration status: all three tables are already in `InitialSchema` (2026-06-20).** The audit's
prior "Awaiting migration" note was stale. No migration is needed for WU35.

**Spec vs code discrepancy (code authoritative):** spec names the PK `PrivateMessageId`; code uses
`MessageId` (long). Code is correct; spec is a read-only snapshot.

**`Conversation.Subject` (max 2048):** a settled field already on the entity; multiple threads per
pair are allowed and meaningful (each has a distinct subject). The spec prose didn't explicitly
mention Subject; the code is authoritative.

**`AllowPrivateMessages` gate:** `User.PrivacySettings.AllowPrivateMessages` is a
`SocialInteractionPermission` enum (not a bool) with four tiers:
- `Public` / `UsersOnly` — allow any authenticated sender.
- `Following` — recipient must follow the sender (write-side existence check against
  `writeDb.FollowedUsers.AnyAsync(f => f.FollowedUserId == senderId && f.UserId == recipientId)`).
- `Nobody` — throw `MessagingPermissionException`.

The gate is enforced **in the write service, on `StartConversationAsync` only** — not re-checked
on replies to an existing thread.

## WU35 — Settled Decisions (2026-06-24)

Four decisions settled before WU35 build; see `forward_plan.md` Resolved + `cross-cutting.md`
"Private Messaging Architecture" for rationale:

1. **1-on-1 only.** The N-participant data model is kept, but the compose flow always targets a
   single recipient. Group conversations happen off-site (Discord) or in public on-site. No
   group-conversation UI is built; a conversation always has exactly two participants.

2. **Stateless, permanently — SignalR ruled out entirely (hardened 2026-07-07).** This reverses the
   spec's "SignalR real-time" framing. Messaging is request/response like every other feature:
   recipient sees new messages on navigate/refresh; global unread badge refreshes on navigation.
   Originally framed as "SignalR push deferred post-MVP"; settled 2026-07-07 that it's never
   built at all — Discord already covers real-time chat, and this site's messaging is deliberately
   async/long-form. `MessagesHub` was the only app-level SignalR Hub ever proposed in this project;
   with it gone the app has none, permanently (see `canalave-conventions/horizontal-scaling.md` §2
   for why that also means no SignalR backplane is ever needed). Feature 49 L5 was N/A under this
   framing until 2026-07-13, when the global flip reclassified it — see the L5 note below
   (statelessness / SignalR-never stands; only the client-impl question changed).

3. **Global unread-messages badge in the layout chrome.** Placed beside `LoginDisplay` in
   `DesktopLayout.razor` and `MobileLayout.razor`. Derived from the `LastReadTimestamp` watermark
   via `IMessagingReadService.GetUnreadConversationCountAsync()`, refreshed per layout render.

4. **No PM Notification rows.** Messaging's own `LastReadTimestamp` watermark is its only
   bookkeeping; the Notification cluster is never touched. Rationale: event-rows (notifications)
   and conversation-watermark (messaging) are differently shaped read-state; unifying creates two
   unread truths to sync. The substantive/infrequent use case offers none of the value the
   notification dedup/batch machinery provides. See `cross-cutting.md` "Private Messaging
   Architecture" for full analysis. **Ratified by owner ruling D18 (2026-08-07), which supplies the
   reason:** a notification is an event, pruned at 60 days; a private message is durable
   correspondence with no expiry. PM email exists since WU-PrivateMessageEmail (2026-10-13), but it
   is Messaging-owned and touches nothing in the Notification cluster (settled note below).

## D18 — Private-message email (owner ruling answered 2026-08-07; built WU-PrivateMessageEmail 2026-10-13)

**D18's own text (settled, do not revisit):**
- PMs never create `Notification` rows and never take a `NotificationTypeEnum` value. No category,
  seed row, `RelatedEntityKind` arm, presenter arm or Messaging→Notifications edge.
- Messaging owns PM email end to end: the preference (`EmailOnNewMessage`), the unread-run
  suppression (`ConversationParticipant.LastNotifiedTimestamp`, nullable), the body, and its own
  unsubscribe token (purpose `TheCanalaveLibrary.MessagingUnsubscribe.v1`, payload = user id, one
  action, never enables).
- Shared with Notifications: only `IMailTransport` and the buffer/worker, generalized to a
  `(kind, id)` payload. The unsubscribe pattern and the RFC 8058 header pair are a shared
  *convention*, not shared code (`security.md` §"One-click unsubscribe tokens").
- One email per unread run, never a re-nag: a burst sends one, reading then a new message sends
  another, a standing unread state is never re-poked. No nudge sweeper.
- The notification settings page renders a second section for messaging email, sourced from
  Messaging's own service. Standing rider: every outbound non-transactional email appears on that
  page, enforced by review, not by a registry.
- General rule: sharing a transport is not coupling two features; sharing an identity is.
- Drain interval: left to the build ("decide when the composer is written").

**Derived by WU-PrivateMessageEmail (built defaults, owner confirmation in `roadmap.md` decision
row 28 / tracker F25):**
- (a) `EmailOnNewMessage` defaults to **on**.
- (b) The mail carries **no sender-authored text** (neither the message nor the conversation subject).
- (c) **Archived conversations never mail** (from the settled "archiving mutes" rule).
- (d) The suppression predicate is the **run reading**: mail when the run is not yet notified
  (`LastNotifiedTimestamp` NULL, or at or before `LastReadTimestamp`) and an unread other-party
  message exists. D18's literal "newer than both watermarks" would re-mail every drain cycle during
  one unread run, contradicting its own outcomes.
- (e) **Account status is not a gate** (added by the WU-PrivateMessageEmail review fixes, 2026-10-13).
  D18 is silent. A Banned or currently Suspended recipient cannot sign in to read, so their runs
  never reopen: at most one mail per conversation until they read again.

**Decided by the build, as D18 directed:** the drain interval is the shared 30 s worker (a PM waits
0–30 s for the next tick); no second knob.

**Decided by the build (a technical constraint and a reading of D18's two placement clauses, not a
product choice):** the preference is a scalar column on `User`, not a field in the `PrivacySettings`
JSON beside `AllowPrivateMessages`, because the privacy form replaces that JSON wholesale. Its one
toggle is the "Private messages" section of `/notifications/settings` (D18: "users will look in one
place"; the page "renders a second section for messaging email"). D18's "beside
`AllowPrivateMessages` — where a user looks for it" is met by a one-line pointer under the privacy
form's "Private messages" control (WU-PrivateMessageEmail review fixes, 2026-10-13; `audit/Profiles.md`
F20).

Rules: `layer2-services.md` §"Private-Message Email" and §"Outbound email pipeline";
`cross-cutting.md` §"Two unread systems"; `security.md` §"One-click unsubscribe tokens".

## Feature 49 — Private Messaging

**Settled — a deleted participant (owner ruling D13 edge (a), answered 2026-08-06; settled for
WU-UserDeletion 2026-10-13). Do not revisit:**
- **One participant left: the conversation survives intact.** The remaining participant keeps her
  participant row, the conversation and every message. The deleted sender's messages keep their text
  with a NULL `sender_user_id` and read **"[deleted user]"**, the site-wide placeholder (it replaces
  messaging's own "[deleted]" and "[unknown]"). Deleting the conversation would destroy the other
  person's data; purging only the leaver's messages would leave her replies answering holes.
- **No participant left: deleted at the source.** `UserDeletionService` deletes the conversations the
  deletion leaves with no participant, in its own transaction, after the save (messages cascade), and
  closes open reports on their messages first (`ReportLedger`). No sweeper, no worker. Both
  participants deleting at overlapping times are serialized on the conversation row (`FOR NO KEY
  UPDATE`, taken before the delete), so the second deletion's emptiness check sees the first one's
  committed delete (WU-UserDeletion review fixes, 2026-10-13; `audit/Identity.md` F52).
- **Not decided here:** whether a sender can delete their own sent messages before deleting the
  account (GDPR rider (i) read onto messages; tracker **F18**). No sender-side delete exists; the
  deletion page says so.
Rule: `cross-cutting.md` §"Account deletion".

**WU-ModerationIntegrity Stage note (2026-09-30) — no flip:** the shared `ActiveUser.RequireUserId()`
guard (`Core/Identity/ActiveUserContextExtensions.cs`) replaces this service's private
`RequireAuthenticatedUser` copy — owner ruling D9's "one shared guard", WU-ModerationIntegrity
2026-09-30. Same `InvalidOperationException` → 401, so no behavior change; the existing Integration
tests for the anonymous refusal stay green. Messaging keeps its `RequireAuthenticatedUser()` name as a
one-line wrapper over the shared guard (the Lineage/Acknowledgment shape); its message text is now the
shared one.

- **L1 — Stage 5 (two columns added by WU-PrivateMessageEmail, 2026-10-13 — `User.EmailOnNewMessage`, `ConversationParticipant.LastNotifiedTimestamp`, owner ruling D18; Integration-tested, stage unchanged, see its note below).** Three-table schema with `LastReadTimestamp` watermark, `IsArchived`, `Subject`
  field, and `SenderUserId` SetNull. Fully migrated (`InitialSchema`). FK edges wired in
  `IdentityConfigurations.cs`.
- **L2 — Stage 5 (WU35, 2026-06-24; new-message email — both sends enqueue, `PrivateMessageEmailComposer` mails one email per unread run, the preference methods and the one-click unsubscribe — WU-PrivateMessageEmail, 2026-10-13, owner ruling D18, Integration- and Unit-tested, stage unchanged, see its note below; its review fixes recorded the no-account-status-gate default as decision row 28 (e) and pinned it, Integration-tested, stage unchanged; a deleted participant reads "[deleted user]" and account deletion removes the conversations it leaves empty — WU-UserDeletion, 2026-10-13, Integration-tested, stage unchanged, see its note below; gate code unchanged, but the settings form's "Off" now really writes `Nobody` — WU-AccessGateSweep2, 2026-09-30, see its note below; shared auth guard WU-ModerationIntegrity, 2026-09-30; `StartConversationAsync` one unit of work and the subject cap enforced, WU-NotificationCorrectness 2026-10-12 — stage unchanged, see its note below).** CQRS-lite cluster in `Core/Messaging/` +
  `Server/Messaging/`. Interfaces: `IMessagingReadService` (5 methods: `GetConversationsAsync`,
  `GetConversationThreadAsync`, `GetUnreadConversationCountAsync`, `FindUserByUsernameAsync`,
  `GetMyNewMessageEmailEnabledAsync` — the last added WU-PrivateMessageEmail) and
  `IMessagingWriteService : IMessagingReadService` (5 mutations: `StartConversationAsync` [gated],
  `SendMessageAsync`, `MarkConversationReadAsync`, `SetArchivedAsync`, `SetMyNewMessageEmailEnabledAsync`). Server impls:
  `ServerMessagingReadService` (partial — `[GeneratedRegex]` requires it), `ServerMessagingWriteService`.
  DTOs: `ConversationSummaryDto`, `MessageDto`, `ConversationThreadDto`, `MessagingParticipantDto`,
  `StartConversationDto`. Validations: `MessagingValidations` (static ext methods) +
  `MessagingValidationException` + `MessagingPermissionException`. `AllowPrivateMessages` 4-tier
  gate on `StartConversationAsync` only. DI: paired `AddScoped` in `Program.cs`.
  **Verified:** `dotnet build` green; integration tests all pass (see Tests below). Three legacy
  entity files moved from `Core/Models/` to `Core/Messaging/`.
- **L3-Logic — Stage 5 (WU35, 2026-06-24; after a full load, archiving no longer leaves the conversation in the Inbox — the restored list is field-backed — WU-PrivateMessageEmail browser-pass fix, 2026-10-13, RazorComponents-tested, stage unchanged, see that WU's note below; the reply box empties after a sent reply — WU-UserDeletion browser-pass fix, 2026-10-13, RazorComponents-tested, stage unchanged, see that WU's note below).** `MessagesPage.razor` (`@page "/messages/{ConversationId:int?}"`,
  `[Authorize]`). Injects `IMessagingWriteService` and `IDeviceDetectionService`. Manages: conversation
  list, thread state (`_allMessages` in newest-first order for `flex-col-reverse` rendering), compose
  modal, busy/error flags. Handles: `HandleSendReplyAsync` (appends to `_allMessages`; refreshes
  conversations), `HandleLoadOlderAsync` (accumulates older pages without discarding earlier),
  `HandleSubmitComposeAsync` (resolves recipient by username; navigates to new thread), `HandleNewConversation`,
  `HandleCancelCompose`. `OnParametersSetAsync` detects `ConversationId` changes for client-side
  navigation to different conversations. `MarkConversationReadAsync` called after each thread load.
  `ComposeForUsername` query param pre-fills the compose modal when navigating from a UserCard "Send PM".
  **Verified:** RazorComponent and integration tests cover key paths; see Tests below.
- **L3.5-Structure — Stage 5 (WU35, 2026-06-24).** Seven components in `SharedUI/Messaging/`:
  `MessagesPage` (dispatcher), `MessagesDesktop` (two-pane, injection-free), `MessagesMobile`
  (single-pane + back button, injection-free), `MessageThread` (composite: header + load-older +
  messages + reply composer; newest-first list + `flex-col-reverse` = newest at bottom),
  `ComposeConversationModal` (composite: recipient + subject + `MessageComposer`; preset from
  `RecipientPreset` or picked via `UserPicker` — retrofitted off a free-text username input,
  WU-StatBadgeProducers 2026-07-31, see below), `ConversationListItem` (leaf: link, avatar, subject,
  preview, unread badge, ~~archived label~~ — chip removed 2026-07-26, see WU-MsgArchive below),
  `MessageItem` (leaf: own=right `flex-row-reverse`,
  other=left; `RichTextView` for HTML), `MessageComposer` (leaf: `EditorView` pull-on-submit via
  `@ref` + `GetHtmlAsync`; `aria-label` on Send for bUnit collision-free selector),
  `MessagesNavLink` (layout chrome leaf: `LocationChanged` subscription for navigation refresh;
  inside `<AuthorizeView>`; envelope SVG + unread badge). `MessagesNavLink` added to
  `DesktopLayout.razor` and `MobileLayout.razor` beside `LoginDisplay`.
  **Verified:** see Tests below.
- **L4-Style — Stage 5 (WU35, 2026-06-24).** Tailwind v4 design-token classes throughout all
  components. Visual sign-off (human) pending — Stage 6 gate still open, consistent with WU8/WU13/
  WU24 precedent. Flip to Stage 6 after a visual run on `/messages`.
- **L4.5 — Stage 5 (5→1 at WU-PrivateMessageEmail, 1→5 at its browser pass, both 2026-10-13: the
  new-message email read in Mailpit, its run rule, archive mute and shutdown drain driven with `psql`,
  and its two unsubscribe pages, on the Aspire path; tracker **H35** closed; the browser verification
  in the WU-PrivateMessageEmail note below).** Before that: Stage 5 (5→1 at WU-UserDeletion, 1→5 at its browser pass, both 2026-10-13). A deleted
  participant reads "[deleted user]" in the list, the thread header and on their messages, seen on both
  render phases (tracker **H29** closed; the WU-UserDeletion note below). Before that it went 5→1 at the WU-NotificationCorrectness
  review fixes (2026-10-13): the compose modal now gets a 400 ("Subject must be 2048 characters or
  fewer.") where it got a 500. It went 1→5 at that WU's browser pass the same day, which drove the
  refusal on both render phases (tracker **H26** step 4; the WU-NotificationCorrectness note below).
  The browser verification of 2026-07-02 below covers everything else.
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13 — reclassified from N/A; `GET`/`PUT /api/messaging/email-preference` and their client calls added by WU-PrivateMessageEmail, 2026-10-13 — Integration- and Unit-tested, stage unchanged, see its note below).** The flip makes every
  injected service need a client impl, so the old "no REST endpoints to wasm-enable" N/A no longer
  holds: endpoints + client impl live (WU-L5Sweep), and messaging read+send verified in a real WASM
  runtime during the flip's browser wave (thread render + reply persisted). Statelessness stands —
  SignalR remains ruled out entirely (2026-07-07), not deferred, never built. Full wave narrative +
  the 7 bugs found/fixed: `workplan.md` WU-GlobalFlip.
- **L6 — Stage 5 (WU-L6, 2026-07-07).** `ix_private_messages_conversation_id_date_sent` built in
  `L6_IndexBatch` for the thread-page query (supersedes the convention FK index; equality-bound
  leading column, so DESC rides a backward scan). Not volume-measured — SeedTool generates no
  messages — but the identical comment-paging shape measured −98.8%, and the indexed columns are
  INSERT-only (no HOT impact). The once-floated participant index is NOT built: the
  conversation-list query narrows via the existing convention `ix_conversation_participants_user_id`
  and per-user conversation counts are small (R4). Detail: `layer6-indexes.md`.

- **Circuit-concurrency fix (2026-07-01) — L2 remains Stage 5; found via browser debugging:**
  `MessagesNavLink` + `NotificationBell` initializing concurrently on one circuit scope crashed
  every authenticated page load (`InvalidOperationException: A second operation…` on the shared
  scoped `ReadOnlyApplicationDbContext`; the nav link's `LocationChanged` refresh also collides
  with every page dispatcher's loads). Cross-cutting root fix: `ServerMessagingReadService` (and
  all read services) now creates a per-method context from a scoped
  `IDbContextFactory<ReadOnlyApplicationDbContext>` via a protected `ReadDbFactory`. Convention:
  `layer2-services.md` §"Read-Context Concurrency: Factory Per Method"; chrome note:
  `render-and-layout.md` §"Layout-chrome concurrency". **Verified:** browser (authenticated home
  renders with both chrome components, no 500) + Integration `ConcurrentReadAccessTests`.

- **L4.5-Browser verification (2026-07-02) — Feature 49 → L4.5=5.** Full messaging loop driven in a
  real browser as TestUser against the seeded dev DB:
  - Conversation list rendered subject/preview/unread badge; opening the thread advanced only the
    viewer's `LastReadTimestamp` (psql-verified) and cleared the badge.
  - Reply via `MessageComposer` persisted (`private_messages` row) and appeared at the bottom
    (newest-first + `flex-col-reverse`).
  - Compose modal: recipient resolved by username, `StartConversationAsync` created the full
    three-table graph (conversation + 2 participants + message, recipient unread), navigated to the
    new thread.
  - **Two bugs found & fixed same-session:**
    1. *Literal string-parameter bindings* — `ReplyError="_replyError"`/`ComposeError="_composeError"`
       (MessagesPage, both layout branches) and `ErrorMessage="ReplyError"`/`"ComposeError"`
       (MessagesDesktop/MessagesMobile) passed literal text instead of the field/parameter (string
       params take attribute text verbatim without `@`), so the thread pane permanently displayed a
       phantom "ReplyError" alert. All six corrected to `@`-prefixed. Same bug class as the
       TagDirectoryDesktop `ServerError` fix (see `audit/Tags.md`, 2026-07-01); a project-wide
       `="_field"` sweep found no further instances.
    2. *Compose modal never closed after success* — `HandleSubmitComposeAsync` navigated to
       `/messages/{newId}` without resetting `_composeOpen`; same-route navigation reuses the page
       instance so the modal survived into the new thread. Now closes the modal before navigating.
  - ~~**Observation (not a defect):** no UI affordance calls `SetArchivedAsync`…~~ **Closed by
    WU-MsgArchive (2026-07-26)** — the archive/unarchive control now exists. See the WU-MsgArchive
    slice below.

### WU-MsgArchive — archive/unarchive UI (2026-07-26)

Closes hidden-deferrals-tracker item **B5**. No grid cells changed: F49 L2/L3-Logic/L3.5/L4/L4.5
were all already Stage 5 and remain so — this is work *under* Stage-5 cells, not a stage transition.

**Provenance (why this was never spec'd).** `IsArchived` has **no design deliberation anywhere in
the record**. It first appears in `GeminiDiscussions/MyActivity September to November 2025_filtered.md`
Entry #1539 (2025-10-25) already present in a SQL script the owner pasted in for Identity
conversion, and the one first-principles PM design turn (Entry #1409, "is there an industry standard
way to implement private messaging in .NET?") never mentions archiving. Spec §5.19's *"Archive
support via `IsArchived` on participant"* describes a column, not a user story. WU35 built
`SetArchivedAsync` because the column existed and stopped where the written scope stopped. The
capability was **ratified deliberately in this WU** rather than inherited by default.

**Settled semantics — archive is sticky, it mutes rather than files.** Rules and rationale live in
`canalave-conventions/layer2-services.md` §"Conversation Archiving Is Sticky"; the short form:
a new inbound message never clears `IsArchived` (Gmail-style raise-on-reply was considered and
**rejected** — with no block available for an established thread, it would let a persistent unwanted
correspondent drag a thread back indefinitely, leaving archive with no relief value), the global
nav badge excludes archived conversations, and the per-conversation `UnreadCount` deliberately
stays populated so the Archived tab surfaces a reply rather than swallowing it. This needed **zero
service change** — it is what `GetConversationsAsync` already did.

**Built:**
- `MessageThread` — Archive/Unarchive button in the thread header; new `IsArchived` /
  `OnToggleArchive` / `BusyArchiving` params, gated on `HasDelegate` so the control stays dark for
  owners that don't wire it. This is the **sole** archive affordance; `ConversationListItem` stays a
  single `<a>` (a `<button>` cannot legally nest inside it, so a per-row control would have meant
  restructuring the card into a stretched-link container — deliberately not done).
- `MessagesPage` — Inbox|Archived segmented toggle (recipe mirrors `NotificationsPage`'s view
  toggle), placed on its own row because the `md:w-72` sidebar can't carry segments beside the
  heading + New button. Tab state is ephemeral component state, no querystring (same choice
  `NotificationsPage` documents). Archiving navigates to `/messages` and resets to Inbox — the
  conversation leaves the list, so clearing the pane is what makes the outcome unambiguous.
- **Per-tab fetch, not fetch-once-and-partition.** `[PersistentState]` stays on the Inbox list only;
  the archived list is ephemeral and fetched on demand. Archived conversations are the set that
  grows without bound — archiving is the gesture that keeps the active set small — so pulling them
  into every `/messages` load (and every prerender payload) would make a diligent user's inbox
  monotonically heavier. Note the service exposes `includeArchived` (both) rather than an
  archived-only mode, so the archived subset is filtered client-side in `LoadArchivedAsync`; the
  goal still holds because the wider read only happens when the tab is opened.
- `ConversationThreadDto.IsArchived` added — sourced from the viewer's own participant row, which
  the thread header query was already reading, so it costs nothing. Deliberately **not** read off
  the conversation list: a thread opened by direct URL may not be in the sidebar list at all.
- `ConversationListItem` "Archived" chip **removed** — every row in the Archived tab is archived, so
  it carried no information. Its ratified `design/surface-registry.md` row is struck; the token
  mismatch recorded against it is closed by deletion, not by fix.

**L2 — sort pushed into SQL.** `GetConversationsAsync` used to materialize every row then sort in
C#. Ordering now happens in SQL. **The two-key idiom is load-bearing:** Postgres defaults to
`NULLS FIRST` for `ORDER BY … DESC`, so a single-key sort silently promotes message-less
conversations to the top of every inbox; `.OrderByDescending(x => x.LastMessage!.DateSent != null)
.ThenByDescending(…)` preserves the message-less-sorts-LAST contract. The projection widens
`DateSent` to `DateTime?` so this expresses without a CS8073 warning. No paging added — conversation
counts are bounded by human effort (a person must start one), unlike notifications.

**Verified:**
- `dotnet test` full suite green — Unit 764 / RazorComponents 590 / Integration 859.
- **Integration** (`MessagingWriteServiceTests`, 3 new): inbox ordering newest-first with a
  message-less conversation last (the guard on the C#→SQL sort move — it is what would catch the
  NULLS regression); `includeArchived` filtering both directions; and the sticky invariant, driven
  through the real service — recipient archives, sender replies, recipient's global badge stays 0
  while the per-conversation `UnreadCount` is > 0.
- **RazorComponents** (13 new + 1 rewritten): `MessagesPageTests` (8 — first page-level messaging
  coverage, via the new `FakeMessagingWriteService`, which honours `includeArchived` so the tab
  split is genuinely exercised); `MessageThreadTests` (5 — label flip, callback, absent-without-
  delegate, busy-disabled); `ConversationListItemTests` — the archived-chip test was replaced by its
  inverse (chip absent) plus one pinning that the unread badge still renders for archived rows.
- **L4.5-Browser (2026-07-26)**, server-only path, TestUser against the seeded dev DB: archive from
  the thread header → navigated to `/messages`, pane cleared, row gone from Inbox, nav badge went
  quiet; `psql`-confirmed `is_archived` flipped for the viewer's row **only** (the other
  participant's row untouched); Archived tab showed the conversation with no chip; header read
  "Unarchive". Sticky proven end-to-end by signing in as the other participant and sending a **real
  reply** while the thread was archived — it did not return to the Inbox and did not light the nav
  badge, but the Archived tab showed its unread count and new preview. Unarchive returned it to the
  Inbox. Ordering checked live with seeded fixtures: newest → older → **message-less last**. Zero
  console messages on a full page load; zero server-log errors. Fixtures removed afterwards, dev
  workbench restored to seeded state.
- `scripts/check-design-tokens.ps1`: no findings in any Messaging file. (The script exits 1 on two
  **pre-existing** violations elsewhere — `Import/ImportReviewPanel.razor` UGC-outside-ContentSurface
  and an undeclared `--color-link` in `Profiles/ProfilePage.razor` — both untouched by this WU.)

**Same-session review addendum (2026-07-26).** A post-completion review pass found and fixed three
things (fix-same-session discipline):

1. **Archived-tab sidebar staleness (defect, fixed).** `LoadThreadAsync` and `HandleSendReplyAsync`
   refreshed only the Inbox `Conversations` list, but the sidebar renders `_archivedConversations`
   while the Archived tab is active — so opening an archived thread left its unread badge sitting
   stale in the archived list until a tab toggle (and a reply left the preview stale). Both handlers
   now also refresh the archived list when `_tab == Archived`. Missed by the original browser pass
   (which zoomed to the Unarchive button without re-checking the sidebar) and by the original bUnit
   set (none opened a thread *while* the Archived tab was active). Covered now:
   `MessagesPageTests.OpeningArchivedThread_FromArchivedTab_ClearsItsSidebarUnreadBadge`, with
   `FakeMessagingWriteService.MarkConversationReadAsync` taught to zero the store's unread count
   (mirroring the real service) so the flow is genuinely observable.
2. **Generated-SQL inspection (a plan step originally skipped, now discharged).** `ToQueryString()`
   on the inbox query: the projection is good (EF emits ROW_NUMBER window LEFT JOINs for
   OtherParticipant and LastMessage — one scan each), but the ORDER BY keys do **not** reuse the
   LastMessage join — EF re-emits correlated subqueries per key. Changed the first key from
   `x.LastMessage!.DateSent != null` to `x.LastMessage != null`, which translates to a cheaper
   `EXISTS(...)` probe (the second key remains a correlated top-1 fetch). Both keys are single seeks
   on `ix_private_messages_conversation_id_date_sent` — negligible at human-bounded conversation
   counts; full reuse would require the hand-written/ID-first read-path rework, which stays
   deferred. Shape + reasoning recorded in a comment at the ORDER BY site.
3. **Polish:** Inbox empty-state copy is now "Your inbox is empty." (tab-scoped — archived
   conversations may exist that the view deliberately hasn't fetched, so "No conversations yet"
   could be false); `SetTabAsync`'s archived fetch now catches into an inline `InlineAlert` instead
   of propagating to the error boundary, matching the page's other user-clicked handlers.

Post-addendum suite: full `dotnet test` green — 764 Unit / 591 RazorComponents / 859 Integration
(**2214/2214**; +1 = the staleness pin).

### WU-MsgReadPath — ID-first conversation listing + scoped reads (2026-07-26)

Builds the read-path rework that WU-MsgArchive's review had deferred (owner chose to take it now
as foundation). **L2 stays Stage 5** — behavior-preserving restructure plus a contract tightening.

- **`ConversationScope` (Core enum, `Active`/`Archived`) replaces `includeArchived`.** Scopes are
  disjoint; there is deliberately no "all" member (no UI merges the lists; a merged mode would
  reinvite fetch-and-filter-client-side — the enum's doc comment carries the rationale).
  `MessagesPage.LoadArchivedAsync` collapsed to a direct `GetConversationsAsync(Archived)` call;
  its client-side `.Where(IsArchived)` filter is gone.
- **`ConversationSummaryDto.IsArchived` removed** — under scoped listings every row's archived
  state is implied by the scope it was requested under; keeping the field would have been exactly
  the inert-plumbing shape the deferrals tracker exists to catch. The per-thread flag stays on
  `ConversationThreadDto.IsArchived` (direct-URL navigation needs it). The fake's store now
  carries the flag beside the DTO; `ConversationListItemTests`' two WU-MsgArchive chip tests were
  retired because the pin is now structural — a per-row archived marker is *uncompilable*.
- **`GetConversationsAsync` is two-step ID-first** (mirrors the thread query's own idiom):
  metadata step (ids + `MAX(date_sent)`, ordered NULLS-last via the two-key idiom, ~4 bytes/row —
  the future `Skip/Take` site) → hydration step (participant, unread count, and
  `SUBSTRING(message_text, 1, 2048)` instead of the whole body — the preview is ≤100 plain-text
  chars, so unbounded `MessageText` transfer was the dominant waste). Rows reassembled in step-1
  order in C#. `MakePreview` now drops a SQL-bisected trailing tag fragment.
- **SQL shape verified** (`ToQueryString`, scratch harness deleted): step 1 emits two correlated
  `MAX()` ordering subqueries (single min/max seeks on
  `ix_private_messages_conversation_id_date_sent`); step 2 emits ROW_NUMBER window joins with the
  substring inside the join and **no ORDER BY**. Convention text updated:
  `layer2-services.md` §"Conversation listing is scoped, ID-first, and unpaged".
- **Endpoint/client:** `GET /api/messaging/conversations?scope=Archived` (enum TryParse binding,
  absent → Active); `ClientMessagingReadService` updated. One additional
  `IMessagingReadService` implementer updated (`ModSpotlightPageTests.FakeUserLookup`).
- **Verified:** full suite green (753 Unit / 591 RazorComponents / 916 Integration — counts also
  absorb the concurrent WU-TagFanon session's tests). Integration: scope-disjointness test
  (Archived returns archived rows *only*) and a new **bounded-preview pin** — a ~9 KB body yields
  a ≤101-char preview, which also proves the `Substring` SQL translation against real Postgres
  (an untranslatable expression would throw there). Browser smoke (server-only path, TestUser):
  Inbox renders; Archived tab empty-state; archive → Archived tab lists it (via the new
  scope=Archived read) with its preview rendered; unarchive → back in Inbox;
  `psql`-confirmed both flags false afterwards (workbench restored); zero console errors.
  **Scope of that browser pass, stated precisely:** the seed conversation's messages are far under
  `PreviewFetchPrefixChars`, so the browser exercised the *scoped read and the UI*, not the
  truncation — for short bodies the SUBSTRING is a no-op. Bounded-prefix behavior is covered at the
  Integration tier only. An earlier revision of this note claimed the browser verified "preview
  rendered from the bounded prefix"; it did not, and could not have.
  Note: the L6 story is unchanged — `(user_id, is_archived)` remains served by the plain
  `ix_conversation_participants_user_id` (C4's messaging half stays open; all index work deferred
  to a later pass per the owner's standing instruction).

#### Measurement — and the regression it caught (2026-07-26, post-WU review)

The WU-MsgReadPath entry above originally claimed a payload improvement **without measuring** —
a violation of the project's standing "always measure" rule, flagged in review. Measuring it
reversed the conclusion twice, so the numbers matter more than the narrative:

| shape | p50 | verdict |
|---|---|---|
| pre-rework single query (full `message_text` per row) | **5.72 ms** | baseline |
| WU-MsgReadPath **as first shipped** (substring inside the window) | **11.31 ms** (0.61 + 10.55) | **88 % regression** |
| corrected shape (substring in the outer projection) | **3.14 ms** (0.61 + 2.53) | **45 % improvement** |

Measured on 400 conversations / 8 460 messages / 28 MB of body text via
`TheCanalaveLibrary.PerfBaseline` (`msgreadpath` and `msgreadpath_fixed` labels, EXPLAIN plans
alongside); volume seeded by `PerfBaseline/seed-messaging-volume.sql` (SeedTool generates none —
the same gap that left F49's L6 unmeasured).

**Root cause of the regression.** Writing the `Substring` inside the `FirstOrDefault` projection put
it inside EF's `ROW_NUMBER()` window over the *entire* `private_messages` table, so Postgres
evaluated it — and detoasted `message_text` — for all 8 460 rows before eliminating them to 401
(`WindowAgg` 0.88 ms → 5.96 ms). Moving it to the outer projection, over a scalar subquery result,
made EF emit a correlated `ORDER BY … LIMIT 1` instead: an **index seek** on
`ix_private_messages_conversation_id_date_sent` with `Memoize`, and **no `Seq Scan` on
`private_messages` at all**. That is also why the corrected shape beats the pre-rework baseline
rather than merely matching it — the old single query seq-scanned every message too.

The constraint is recorded as a do-not-simplify rule in `layer2-services.md`. Two lessons worth
carrying: a payload optimization can cost more in per-row function evaluation than it saves in
transfer, and `dotnet test` green plus a clean browser pass said nothing about either — only the
measurement did.

### WU-StatBadgeProducers — ComposeConversationModal retrofitted onto UserPicker (2026-07-31)

No grid cells changed: F49 was already Stage 5 and remains so — inert-plumbing-under-Stage-5 shape,
same as WU-MsgArchive above. `ComposeConversationModal`'s free-text `<input type="text">` recipient
field (the manual-compose path only — the `RecipientPreset` chip path from a UserCard "Send PM" is
untouched) is replaced with `UserPicker`, the same retrofit `StoryTitlePicker` performed for
stories. `IMessagingReadService.FindUserByUsernameAsync` (exact-match) stays live for the
`ComposeForUsername` deep-link resolution — it is not this WU's job to relocate a messaging method,
and that path still needs an exact-username resolver. The public contract
(`OnSubmit`'s `(RecipientUsername, Subject, MessageHtml)` tuple) is unchanged — the modal derives
`RecipientUsername` from the picked `UserCardDto.Username` internally. Full context (why the picker
now exists, the `SearchUsersByNameAsync` service behind it, the visibility decision):
`audit/Badges.md` §"WU-StatBadgeProducers". Verified: `dotnet test` full suite green (2,414 total);
browser-verified — `/messages` → New → picker renders in place of the old text input, typeahead
returns live results.

### WU-AccessGateSweep2 note (2026-09-30) — "Off" now actually blocks private messages

**No cell flips — F49 stays Stage 5; no messaging code changed.** The `AllowPrivateMessages` gate in
`StartConversationAsync` was always correct, but the settings form's "Off — no private messages"
option posted `2` (`Following`), so a user who chose "Off" got the "only people I follow" tier, not
`Nobody`. The form now writes `Nobody` (`audit/Profiles.md` F20 Stage note). Covering tier:
RazorComponents (`PrivacySettingsFormTests`); the gate's own tiers stay covered by
`MessagingWriteServiceTests`. Workbench users who saved "Off" earlier still hold `Following` until they
re-save (or `reset-dev-db.ps1`).

**Browser-verified 2026-09-30 (WU-AccessGateSweep2 browser pass):** with "Off" saved through the form,
starting a conversation was refused with "This user does not accept private messages." on WASM (a
403 on `POST /api/messaging/conversations`) and on the circuit. No `conversations` row was written.

### WU-UserDeletion note (2026-10-13) — F49 L2 stays 5; L4.5 5→1 (owner ruling D13 edge (a))

- **The survive case is current behavior, ratified** (the Settled note at this feature's top). Only
  the placeholder changed: `ServerMessagingReadService.DeletedUserLabel = "[deleted user]"` replaces
  "[deleted]" (a sender, and a participant with no username) and "[unknown]" (a vanished other
  participant) at all seven sites, and the write service's send echo uses the same constant. The
  `MessageDto` doc says so. Account deletion is the only way a participant vanishes.
- **The orphan case is new:** `UserDeletionService` deletes a conversation the deletion leaves with no
  participant, after its save and in its transaction, and first closes open reports on its messages
  (`audit/Identity.md` F52's Stage note). The other producer of an empty conversation, a crash between
  `StartConversationAsync`'s two saves, closed with WU-NotificationCorrectness's one-save rewrite.
- **Tier: Integration** — `UserDeletionServiceTests`: one participant deleted keeps the participant
  row, the conversation, every message (sender NULL) and the open message report, and
  `GetConversationThreadAsync`/`GetConversationsAsync` as the survivor read "[deleted user]" for the
  other participant and the deleted sender's messages; both deleted removes the conversation and its
  messages and closes the message report. No messaging test asserted the old strings.
- **L4.5 5→1:** the new label is undriven in a browser (tracker **H29**). It went back to 5 at the
  browser pass the same day (the browser bullets below).
- **Review fixes (same day), no flip:** two participants deleting at overlapping times could both miss
  the emptied conversation (each saw the other's uncommitted participant row). `UserDeletionService`
  now locks the user's conversations before the delete; the race and its Integration test are in
  `audit/Identity.md` F52's review-fix note.
- **Browser-pass fix (2026-10-13), F49 L3-Logic stays 5: the reply box empties after a send.** Driving
  H29's messaging steps showed the reply composer kept the sent text, so a second click sent it again.
  It never cleared: a WU-GlobalFlip browser wave noted it in July as pre-existing, and no ledger
  carried it. Quill stays mounted and ignores later changes to `EditorView.Html`, so only
  `EditorView.SetHtmlAsync` can empty it (the `CommentEditor.ClearAsync` precedent).
  `MessageComposer.ClearAsync`, `MessageThread.ClearComposerAsync` and an `@ref` on the page now do it.
  `HandleSendReplyAsync` clears the box right after `SendMessageAsync` succeeds, before the sidebar
  refresh, which can fail on its own. A refused send keeps the text. **Tier: RazorComponents.**
  `MessagesPageTests` +2: a sent reply loads "" into Quill once, and a refused one does not. The first
  fails with the clear removed. Seen in the browser on both render phases: the circuit makes no
  `/api` call, and WASM makes `POST /api/messaging/conversations/2/messages`. Each time the box went
  back to its placeholder, and a second reply after a clear sent normally (`private_messages` 9–11).
- **Browser verification (2026-10-13) — F49 L4.5 1→5.** Server-only path; the method, accounts and
  logs are in `audit/Identity.md` F52's browser verification Stage note.
  - **One participant deleted.** E2EDelX had conversation 2 with TestUser and conversation 3 with
    E2EDelY, with a message each way in both. After X was deleted, TestUser's `/messages` listed
    conversation 2 under "[deleted user]". The thread header and X's message read "[deleted user]" on
    both render phases: the circuit made no `/api` call, and WASM loaded the thread through
    `GET /api/messaging/conversations/2` and the list through `?scope=Active`. Y's view of
    conversation 3 read the same.
  - **Replying still works.** TestUser replied into conversation 2 after X was gone (WASM, 200). The
    conversation survives for its remaining participant, as D13 edge (a) rules.
  - **Both participants deleted.** After Y was deleted, `psql` showed conversation 3 and its messages
    gone. Y's open report on X's message became Resolved – No Action with no moderator and the
    "both participants" note.
  - No console error and no `fail:` line. The one bug found is the reply-box fix above.

### WU-PrivateMessageEmail note (2026-10-13) — owner ruling D18 built; F49 L1/L2/L5 stay 5, L4.5 5→1 and 1→5 at its browser pass

What changed (rules: `layer2-services.md` §"Private-Message Email" and §"Outbound email pipeline";
`security.md` §"One-click unsubscribe tokens"; the settled note "D18" above):
- **L1:** migration `20261013183147_WU_PrivateMessageEmail` adds `AspNetUsers.email_on_new_message`
  (NOT NULL DEFAULT true — the bool-true shape, which also backfilled the dev DB's 7 users on) and
  `conversation_participants.last_notified_timestamp` (nullable, no backfill). Applied, rolled back
  and re-applied on the server-only dev DB. No index: the composer's query rides the participant PK
  and `ix_private_messages_conversation_id_date_sent`. SeedTool's COPY omits the new column like
  `can_auto_approve` and gets the DB default (a deviation from the WU spec, which asked for an explicit
  `true`; the default keeps one source).
- **L2:** `StartConversationAsync` and `SendMessageAsync` enqueue `(PrivateMessage, messageId)` on the
  shared `OutboundEmailBuffer` after their save (a full buffer logs Error, never fails the send).
  `PrivateMessageEmailComposer` (write context, untracked) mails a participant row when its run is not
  yet notified, an unread other-party message exists, it is not archived, and the user has the
  preference on and a confirmed address; the after-send hook stamps `LastNotifiedTimestamp` to the
  newest covered `DateSent`. `PrivateMessageEmailBodies` carries no sender-authored text.
  `MessagingUnsubscribeTokenService` (purpose `…MessagingUnsubscribe.v1`, payload = user id) and
  `MessagingEmailEndpoints` (`/unsubscribe/messages/{token}`: read-only GET, one-action POST; a
  foreign-purpose, tampered or deleted-account token answers the same 400 page, the notification
  routes' rule rather than the spec's "success page") are new.
  `GetMyNewMessageEmailEnabledAsync` / `SetMyNewMessageEmailEnabledAsync` are new; the write is
  set-based and detaches the tracked `User`.
- **L5:** `GET`/`PUT /api/messaging/email-preference` (`ExecuteAsync`, `RequireAuthorization`) and the
  matching `ClientMessagingReadService`/`ClientMessagingWriteService` calls.
- **L4.5 5→1:** no browser or Mailpit was available. Tracker **H35** holds the pass (Mailpit loop,
  unsubscribe pages, the settings section on both render phases).
- **Derived, not ruled:** default on, no sender text, archive mutes, the run reading of D18's formula —
  `roadmap.md` decision row 28 / tracker **F25**. Inherited: the shared 30 s drain (D18 left it to the
  build).

**Verified:** `dotnet build` green (no new warnings); `dotnet test` green — Unit 1,304, RazorComponents
859, Integration 1,610. Tiers: **Integration** `PrivateMessageEmailTests` (20: one mail per run, burst,
re-fire after read, standing unread re-enqueued sends nothing, read before drain, the stamp value, a
NULL sender counts as the other party, preference off, unconfirmed address, archive and unarchive, a
connection failure restores unstamped, a mixed notification+PM flush, the preference service and
endpoints with 401s, the tracked-copy detach through one scope, the unsubscribe GET/POST/re-POST and
the four refusals); **Unit** `PrivateMessageEmailBodiesTests`, `MessagingUnsubscribeTokenServiceTests`
(both cross-purpose directions), `ClientMessagingEmailPreferenceTests`, `OutboundEmailFlusherTests`,
`OutboundEmailBufferTests`; **RazorComponents** `NotificationSettingsPageTests` (F43). Mutation-checked:
dropping the run predicate, the archive gate, the after-send stamp or the detach each fails its test.

**Live smoke, no browser (2026-10-13).** The server-only host ran on the `Smtp` branch against a
throwaway local SMTP sink (no Mailpit); requests went through `curl` with `/dev/wu12/login-as`
cookies. The real worker, flusher, `SmtpMailTransport` and composer behaved as the rule says:
TestUser → AuthorAlpha produced one mail ("New private message from TestUser", both RFC 8058
headers, a `/messages/{id}` link, neither the subject nor the body text in it) and stamped
`last_notified_timestamp` = the message's `date_sent`; two more sends produced no second mail after
the next tick; a read then a send produced the second. The mail's unsubscribe GET rendered the
confirmation and changed nothing, its POST set `email_on_new_message` false, and `PUT
/api/messaging/email-preference?enabled=true` set it back. The settings page's prerender carried the
Private messages section. The smoke conversation was deleted from the dev DB afterwards. Not a browser
pass: interactivity on both render phases and real-client rendering stay with **H35**.

**Review fixes (2026-10-13) — no further flip on F49.** Three reviews raised seven findings; all were
real and none needed an owner ruling (one became a derived default for confirmation):
- **No account-status gate, recorded as a choice.** The composer and `layer2-services.md` justified it
  as "mirroring the notification composer", whose reason (account-action notices) does not carry over.
  It is now decision row 28 **(e)** / tracker F25 (e) with its own reason (the run rule bounds a
  restricted recipient to one mail per conversation until they read again), pinned by
  `PrivateMessageEmailTests.ABannedRecipient_IsMailedOncePerConversation_AndNeverAgain`.
- **D18's "where a user looks for it".** The privacy form's "Private messages" control now points to
  the toggle (`audit/Profiles.md` F20, which takes the L4.5 flip). The settled note's "as D18
  directed" no longer covers the scalar-column choice.
- **An unsubscribe undone by a moderator.** A moderator circuit that had tracked the member wrote
  `email_on_new_message` back to true on a later Suspend/Ban (Identity's full-row stamp bump). Fixed in
  Moderation (`audit/Moderation.md` F47 review-fixes note); `User.cs`'s "written only by" comment and
  the rule bullet now name the full-row writer.
- **Tests and docs.** The landing page's settings link is pinned (Unit `UnsubscribeLandingPageTests`;
  both Integration unsubscribe GETs); the flusher's per-group after-send indices are asserted for a
  group that is not first in the batch (Unit `OutboundEmailFlusherTests`); a dead section pointer in
  `layer2-services.md` fixed; tracker H36 folded into **H17**, which already held it.

`dotnet test` green — Unit 1,308, RazorComponents 860, Integration 1,614; build 0 errors, no new
warnings; the four gates pass. Mutation-checked: the moderation detach, the flusher's per-group index
and the landing page's link each fail their tests when reverted.

**Browser verification (2026-10-13) — F49 L4.5 1→5; tracker H35 closed.** Aspire path (Mailpit;
`psql` on 5433), on the persistent Aspire workbench, not reset; the WU's migration applied at that
start. Actors: A = AuthorBeta, B = ReaderGamma, AdminUser as moderator. The circuit was proven by
`_blazor/negotiate` and no `/api` call, with the Auto hash removed before the full load. WASM was
proven by no negotiate and the action's `/api` request.
- **The email (step 2).** A started conversation 3 with B in the compose modal (the GIF). One mail
  reached Mailpit within a tick, to B only: "New private message from AuthorBeta". Its HTML and text
  held neither the subject nor the body; both carried marker words, so their absence was checked by
  search. It had absolute links to `/messages/3`, the unsubscribe page and `/notifications/settings`,
  and both `List-Unsubscribe` headers (Mailpit shows its own Unsubscribe from them). In `psql`, B's
  `last_notified_timestamp` equalled the message's `date_sent` to the microsecond; A's stayed NULL.
- **The run rule (steps 3–4).** A sent three more without a read: the next drain composed them and
  mailed nothing. B opened the thread at the mail's link (circuit), which moved `last_read_timestamp`.
  A sent again: a second mail, stamped with the new `date_sent`.
- **Archive (step 5).** B read the thread and archived it (WASM), so the run was open and the row
  archived. A's next message was composed and not mailed. Unarchiving mailed nothing over the next
  interval. A's next message after that was mailed and stamped.
- **Unsubscribe (step 6).** The footer link's page changed nothing in `psql`. Its button turned
  `email_on_new_message` off, and so did a cookieless one-click POST, the way a mail client sends it. A
  second submit showed the same "You're unsubscribed" page (200). "Manage your email settings" opened
  `/notifications/settings` with the box unticked.
  - **Refusals:** a tampered token, a notification token on `/unsubscribe/messages/…` and a message
    token on `/unsubscribe/…` each answered the 400 "Link no longer valid" page, GET and POST.
  - **Ground truth:** nothing changed; B's `user_notification_settings` stayed empty.
- **Shutdown drain (step 8).** A sent a message, and a second later the `web` resource was stopped from
  the Aspire dashboard. The log shows "Application is shutting down…", then "Sent PrivateMessage email
  to user 6": the drain mailed it, and the stamp was written.
  - **Not through the script:** `stop-aspire.ps1` cannot show this. Killing the AppHost takes DCP's
    port proxies with it, so the drain runs but cannot reach Postgres or Mailpit. A first try that way
    logged two `fail:` connection errors and lost the mail, leaving the run open for the next message,
    as the worker's crash-loss posture says.
  - The technique is now in `run-server/SKILL.md`.
- **The moderator stale-copy fix** (the review fixes; `audit/Moderation.md` F47). All on one circuit as
  AdminUser at `/mod/users/6`: Warn B, then B's one-click unsubscribe, then Suspend B, then Reinstate B.
  `email_on_new_message` stayed false through both. The stamp bump did write: `security_stamp`
  changed.
- **Runtime bug found and fixed.** After a full load of `/messages/3`, archiving left the conversation
  in the Inbox until a reload, on both render phases; the API answered an empty Inbox.
  - **Cause:** .NET 10 hands a `[PersistentState]` property's restored snapshot back on the
    component's next parameter set, here the archive's return to `/messages`.
  - **Fix:** `Conversations` is field-backed, and its setter takes the framework's value only during
    initialization (`layer3-logic.md` §"State Persistence").
  - **Tier: RazorComponents.** `MessagesPageTests` +2, through the new
    `PersistentStateTestSupport.RestoreDeclarativeStateAsync`. One test guards that the restore is
    real; the archive test fails without the fix.
  - **Re-driven on both phases:** the Inbox read "Your inbox is empty.", and unarchive still brought
    the conversation back.
  - The other `[PersistentState]` components are not swept: tracker **D20**.
- No console error. No `fail:` or `crit:` line beyond the two at the `stop-aspire.ps1` stop. The dev-DB
  state left is in `workplan.md` WU-PrivateMessageEmail.

### WU-NotificationCorrectness note (2026-10-12) — F49 L2 stays 5: one unit of work, the subject cap enforced; L4.5 5→1 at its review fixes and 1→5 at its browser pass (both 2026-10-13)

**No cell flips at the build** (the review fixes flipped L4.5; last bullet). The WU35 settled decisions are untouched; the `AllowPrivateMessages` gate stays on
`StartConversationAsync`.
- **One save (service audit §2.8).** `StartConversationAsync` used to save the `Conversation` alone
  and then save its two participants and first message, on the false comment "EF needs the
  Conversation row … before adding participants". A failure between the two saves left an orphaned,
  participant-less conversation. The children now ride `Conversation.ConversationParticipants` /
  `PrivateMessages`, EF's fixup assigns the FK, and one `SaveChangesAsync` (one transaction) writes
  the graph. One `DateTime now` stamps `DateCreated`, the sender's `LastReadTimestamp` and `DateSent`,
  so the sender's watermark is never behind their own first message.
- **Subject cap (orchestrator amendment U8).** `conversations.subject` is `varchar(2048)`; an
  over-long subject reached the INSERT and came back as a 500. `MessagingValidations.Validate` now
  refuses a trimmed subject over `MessagingConstants.MaxSubjectLength` (2048 — the column's existing
  cap, which `Conversation.Subject`'s `[MaxLength]` now reads, so the two cannot drift), so it is a
  `MessagingValidationException` → 400. No new number, no migration (the snapshot did not move).
- **Tests.** Integration `MessagingWriteServiceTests` +3: one save counted through `SavedChanges`
  (with the shared-timestamp assertion), a failure injected on the participants insert leaves no
  conversation (`InterleavingCommandInterceptor` `failWith`), and the over-cap subject is a validation
  error with nothing written. Unit `MessagingValidationsTests` +2 (over the cap; at the cap after
  trimming). Each fails with its fix reverted.
- **Browser: not run.** The compose modal's refusal for a 2,049-character subject is a step in
  tracker H26. The review fixes (2026-10-13) flipped **F49 L4.5 5→1** until that step runs. The
  campaign flips L4.5 when a visible refusal ships without a browser pass (WU-ModerationIntegrity's
  F46 duplicate-report refusal); this note had first left it at 5.
- **Browser verification (2026-10-13): F49 L4.5 1→5.** Aspire path, as TestUser, on
  `/messages?ComposeForUsername=AuthorAlpha`.
  - A 2,049-character subject was set by script, because the input's `maxlength` stops typing past
    2,048. On both render phases the modal showed "Subject must be 2048 characters or fewer."
  - On WASM, `POST /api/messaging/conversations` answered 400. The circuit made no `/api` call.
    `conversations` still had one row.
  - A 2,048-character subject with two trailing spaces went through on the circuit (conversation 2).
    It is stored trimmed, at 2,048 characters. `date_created` equals the first message's `date_sent`,
    which is the one-save timestamp.
  - No bug was found. The full pass is in `audit/Notifications.md`, in the
    WU-NotificationCorrectness browser verification Stage note.

### Tests (WU35, 2026-06-24)

- **Unit** (`Tests.Unit/MessagingValidationsTests.cs`, 11 tests): `MessagingValidations.Validate`
  (empty subject, whitespace subject, empty body, whitespace body, self-message, all valid,
  two-errors-simultaneously) and `ValidateMessageBody` (empty, null, whitespace, valid). All pass.
- **Integration** (`Tests.Integration/MessagingWriteServiceTests.cs`, 14 tests): StartConversation
  creates Conversation + 2 ConversationParticipants + 1 PrivateMessage; returns new ConversationId;
  self-message throws `MessagingValidationException`; empty subject throws; empty body throws;
  `UsersOnly` gate allows; `Nobody` gate throws `MessagingPermissionException`; `Following` gate
  throws when no follow edge; `Following` gate allows when recipient follows sender; `SendMessageAsync`
  appends + returns `MessageDto`; non-participant throws `KeyNotFoundException`; `<script>` stripped
  on save; own messages excluded from unread count; recipient sees unread=1; `MarkConversationReadAsync`
  advances watermark (unread→0); `SetArchivedAsync` sets `IsArchived` on sender row; non-participant
  `SetArchivedAsync` throws. All pass.
- **RazorComponents** (`Tests.RazorComponents/MessageComposerTests.cs`, 9 tests; `ConversationListItemTests.cs`,
  7 tests; `MessageItemTests.cs`, 6 tests — 22 tests total): MessageComposer Send/Cancel labels,
  `aria-label` selector, Busy disables, OnSend/OnCancel fire; ConversationListItem unread badge
  renders/hides, ~~archived label~~ (superseded 2026-07-26 — now asserts the chip is *absent* and
  that the unread badge survives archiving), IsSelected border class, link href; MessageItem own-message uses
  `flex-row-reverse` + no username label, other-message no reverse + shows username, `RichTextView`
  renders HTML. All pass.

### WU-AuditFixPass note (2026-07-18)

`MessageThread` + `ComposeConversationModal` error markup normalized to `InlineAlert` (MA-504).
`MessagesNavLink` no longer bare-swallows the unread-count read (MA-503): anonymous viewers skip
the query via the AuthState cascade; genuine failures log Warning and the badge shows 0. Full
detail: `workplan.md` WU-AuditFixPass.
