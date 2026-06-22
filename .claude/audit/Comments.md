# Audit — Comments/

**Features:** 23 (posting), 24 (display & pagination), 25 (likes), 26 (spoiler comments).

## Shared Context
**Entities (Core/Models/):** `BaseComment` (TPT root, `ToTable("base_comments")`, `ParentCommentId`
self-ref `SetNull`, `LikeCount`, M:N `LikedByUsers`) + four children, each `.ToTable()` with `DatePosted`
denormalized: `ChapterComment` (+ `IsSpoiler`), `UserProfileComment`, `GroupComment`, `BlogPostComment`.
TPT is Settled Axiom #2. **No services or components built.**

## Feature 23 — Comment Posting
- **L1 — Stage 5.** TPT hierarchy + per-child `DatePosted` default; orphan handling via `SetNull`.
  Matches §5.9. **L2 — Stage 2** (write across all four contexts; server-side sanitization — shares the
  `HtmlSanitizer` gap with Chapters). **L3/L3.5 — Stage 2. L4 — Stage 1. L5 — Stage 2. L6 — Stage 2.**

## Feature 24 — Comment Display & Pagination
- **L1 — Stage 5.** **L2 — Stage 2** (threaded read; pagination on the golden index). **L3/L3.5 — Stage 2**
  (`CommentItem` leaf, `CommentSection` coordination composite; orphans shown under "[Deleted Comment]").
  **L4 — Stage 1. L5 — Stage 2. L6 — Stage 2** (golden index `(chapter_id, date_posted DESC)` — pending).

## Feature 25 — Comment Likes
- **L1 — Stage 4 (stale-code trap; spec wins).** Spec (§6.11, item 25) wants an explicit **`CommentLike`
  junction** (no `DateLiked` — anti-addictive) with denormalized `LikeCount`. The code instead uses an
  **implicit EF many-to-many** (`BaseComment.LikedByUsers` ⇄ `User.LikedComments`, `HasMany().WithMany()`),
  giving EF an auto-named join table with no entity to hang behavior on. `LikeCount` is present on
  `BaseComment` (good, reusable). Implicit M:N is a legitimate EF pattern *in general*, but the spec is the
  recent authority and explicitly calls for the junction — and this code is non-working — so the direction
  is fixed: **build the explicit `CommentLike` entity** (Stage 2 build-to-spec), don't preserve the
  implicit join.
- **L2/L3/L3.5 — Stage 2** (toggle like; no notification, no `DateLiked`). **L4 — Stage 1. L5 — Stage 2.**

## Feature 26 — Spoiler Comments
- **L1 — Stage 5.** `IsSpoiler` is on `ChapterComment` only (chapter-scoped, not `BaseComment`) — exactly
  §5.9.1. **L2 — Stage 2.** **L3-Logic — Stage 2** — completion-gated reveal (`IsCompleted=true` ⇒ single
  click; `false` ⇒ `ConfirmDialog`); `IsRevealed` ephemeral; dispatcher passes `UserHasCompletedStory`.
  *Depends on* the universal `ConfirmDialog` composite (§5.30.9). **L3.5 — Stage 5** (WU9, 2026-06-21 —
  see note below). **L4 — Stage 1** (spoiler blur/cover styling — `ConfirmDialog`'s own visuals are a
  separate, already-built concern; this stage covers the blur/cover markup around the comment itself,
  owned by WU20). **L5 — Stage 2.**
- **WU9 Stage-5 note (2026-06-21):** built the universal `ConfirmDialog` container composite at
  `SharedUI/Dialogs/ConfirmDialog.razor` (new cross-cutting cluster — no owning feature, mirrors
  `RichText/`/`Lookups/`). Contract: `@bind-IsOpen` (two-way `IsOpen`/`IsOpenChanged`), `Title`/
  `Message` for simple bodies, `ChildContent` for rich bodies (wins over `Message` when set),
  `ConfirmText`/`CancelText`, `IsDestructive` (red confirm button vs. green), `OnConfirm`/`OnCancel`
  EventCallbacks. Renders nothing when `!IsOpen`. Backdrop click cancels; panel uses
  `@onclick:stopPropagation`. Overlay shell (backdrop + panel) reuses the convention `EditorView`'s
  preview popup already established — recorded once in `layer3.5-structure.md`'s Container Composite
  section, not duplicated. This flips only `26 L3.5-Structure` — the spoiler-specific consumer wiring
  (blur/cover, completion-gated reveal calling this dialog) is WU20's job, not this work-unit's; flips
  `26 L3-Logic`/`L4` are unaffected. **Verified:** `dotnet build` green (4 projects, 0 new warnings);
  Tailwind JIT picked up `bg-danger` (theme token already existed, just unused) on `npm run css:build`;
  live server run, homepage `200`; user-confirmed visual check via a throwaway harness on
  `HomeDesktop.razor` (message-only dialog, `ChildContent` dialog, `IsDestructive` variant, backdrop
  click and Confirm/Cancel all round-tripping `@bind-IsOpen` correctly) — harness removed immediately
  after confirmation (self-contained, no missing-producer dependency, unlike WU4's TagChip harness).
