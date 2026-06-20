# Audit — Messaging/

**Feature:** 49 (private messaging). SignalR real-time. Route `/messages/{ConversationId:int?}`. Uses the
full `EditorView` for rich-text composition (§5.19).

## Shared Context
**Entities (Core/Models/):** `Conversation` (`DateCreated` default), `ConversationParticipant`
(composite `(ConversationId,UserId)`, `LastReadTimestamp`, `IsArchived`, Cascade from both User and
Conversation), `PrivateMessage` (`SenderUserId` SetNull, `DateSent` default, Cascade from Conversation).
Three-table model. **No hub, services, or components built.** Gated by `User.PrivacySettings.
AllowPrivateMessages`.

## Feature 49 — Private Messaging
- **L1 — Stage 5.** Three-table conversation/participant/message model with unread-tracking timestamp and
  archive flag. Sound. Awaiting migration.
- **L2 — Stage 2.** SignalR hub design + send UX unbuilt (no SignalR-specific packages beyond the
  framework). `LastReadTimestamp` comparison for unread counts.
- **L3-Logic — Stage 2** (SignalR delivery; respect `AllowPrivateMessages`). **L3.5-Structure — Stage 2**
  (conversation list + thread; composes `EditorView` atom owned by Chapters/). **L4 — Stage 1.**
- **L5 — N/A.** Real-time delivery is SignalR, not REST endpoints + `HttpClient` — the Layer-5 WASM
  enablement pattern doesn't apply.
- **L6 — Stage 2** (`(conversation_id, date_sent)` for thread paging).
