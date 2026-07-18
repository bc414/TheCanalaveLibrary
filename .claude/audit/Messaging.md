# Audit — Messaging/

**Feature:** 49 (private messaging). Route `/messages/{ConversationId:int?}`. Uses the full
`EditorView` for rich-text composition (§5.19). **Stateless, permanently — no SignalR** (hardened
2026-07-07 from an earlier "deferred post-MVP" framing; see WU35 note #2 below and
`cross-cutting.md` "Private Messaging Architecture").

## Shared Context

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
   Architecture" for full analysis.

## Feature 49 — Private Messaging

- **L1 — Stage 5.** Three-table schema with `LastReadTimestamp` watermark, `IsArchived`, `Subject`
  field, and `SenderUserId` SetNull. Fully migrated (`InitialSchema`). FK edges wired in
  `IdentityConfigurations.cs`. No further L1 work needed.
- **L2 — Stage 5 (WU35, 2026-06-24).** CQRS-lite cluster in `Core/Messaging/` +
  `Server/Messaging/`. Interfaces: `IMessagingReadService` (4 methods: `GetConversationsAsync`,
  `GetConversationThreadAsync`, `GetUnreadConversationCountAsync`, `FindUserByUsernameAsync`) and
  `IMessagingWriteService : IMessagingReadService` (4 mutations: `StartConversationAsync` [gated],
  `SendMessageAsync`, `MarkConversationReadAsync`, `SetArchivedAsync`). Server impls:
  `ServerMessagingReadService` (partial — `[GeneratedRegex]` requires it), `ServerMessagingWriteService`.
  DTOs: `ConversationSummaryDto`, `MessageDto`, `ConversationThreadDto`, `MessagingParticipantDto`,
  `StartConversationDto`. Validations: `MessagingValidations` (static ext methods) +
  `MessagingValidationException` + `MessagingPermissionException`. `AllowPrivateMessages` 4-tier
  gate on `StartConversationAsync` only. DI: paired `AddScoped` in `Program.cs`.
  **Verified:** `dotnet build` green; integration tests all pass (see Tests below). Three legacy
  entity files moved from `Core/Models/` to `Core/Messaging/`.
- **L3-Logic — Stage 5 (WU35, 2026-06-24).** `MessagesPage.razor` (`@page "/messages/{ConversationId:int?}"`,
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
  `RecipientPreset` or manual username), `ConversationListItem` (leaf: link, avatar, subject,
  preview, unread badge, archived label), `MessageItem` (leaf: own=right `flex-row-reverse`,
  other=left; `RichTextView` for HTML), `MessageComposer` (leaf: `EditorView` pull-on-submit via
  `@ref` + `GetHtmlAsync`; `aria-label` on Send for bUnit collision-free selector),
  `MessagesNavLink` (layout chrome leaf: `LocationChanged` subscription for navigation refresh;
  inside `<AuthorizeView>`; envelope SVG + unread badge). `MessagesNavLink` added to
  `DesktopLayout.razor` and `MobileLayout.razor` beside `LoginDisplay`.
  **Verified:** see Tests below.
- **L4-Style — Stage 5 (WU35, 2026-06-24).** Tailwind v4 design-token classes throughout all
  components. Visual sign-off (human) pending — Stage 6 gate still open, consistent with WU8/WU13/
  WU24 precedent. Flip to Stage 6 after a visual run on `/messages`.
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13 — reclassified from N/A).** The flip makes every
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
  - **Observation (not a defect):** no UI affordance calls `SetArchivedAsync` — the service method
    and the `ConversationListItem` "Archived" label exist (Integration-covered), but no
    archive/unarchive control is surfaced. The audit scope above never promised one; noting it as a
    candidate future affordance.

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
  renders/hides, archived label, IsSelected border class, link href; MessageItem own-message uses
  `flex-row-reverse` + no username label, other-message no reverse + shows username, `RichTextView`
  renders HTML. All pass.

### WU-AuditFixPass note (2026-07-18)

`MessageThread` + `ComposeConversationModal` error markup normalized to `InlineAlert` (MA-504).
`MessagesNavLink` no longer bare-swallows the unread-count read (MA-503): anonymous viewers skip
the query via the AuthState cascade; genuine failures log Warning and the badge shows 0. Full
detail: `workplan.md` WU-AuditFixPass.
