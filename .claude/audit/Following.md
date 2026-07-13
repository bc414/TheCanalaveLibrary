# Audit — Following/

**Features:** 18 (user following), 19 (vouches). User-to-user relationship; distinct from story
interactions.

## Shared Context
**Entity:** `FollowedUser` (Core/Models/) — composite PK `(UserId, FollowedUserId)`, `ReceiveAlerts`,
`DateFollowed` (DB default). Fluent config: the follower edge is Cascade, the followed edge is `Restrict`
("CONFLICT: resolved in C#"). Self-referential M:N through the explicit entity. Vouching does **not**
live on this entity — see Feature 19 below; it was promoted to its own `Vouch` table during Phase A. No
services, no components built.

## Feature 18 — User Following
- **L1 — Stage 5.** `FollowedUser` matches §5.8 (follow/unfollow, bell `ReceiveAlerts`, date). The
  asymmetric delete behavior is deliberate. Migration-verified (`InitialSchema`).
- **L2 — Stage 5 (WU21, 2026-06-22).** `IFollowingReadService` / `IFollowingWriteService` (inherits
  read) built in `Core/Following/`. `ServerFollowingReadService` (`ReadOnlyApplicationDbContext` +
  `IActiveUserContext`) and `ServerFollowingWriteService` (adds `ApplicationDbContext` +
  `IHtmlSanitizationService`) in `Server/Following/`; both DI-registered in `Program.cs`. Idempotent
  follow/unfollow with self-follow guard; bell toggle via `SetReceiveAlertsAsync`; `VouchAsync`
  sanitizes via `IHtmlSanitizationService.Sanitize` before persist; 5-limit enforced in C# (count
  on `writeDb`) throwing `VouchLimitException`. Notification seams deferred: `// TODO(WU22)`.
  **Verified:** tier **Integration** — `FollowingWriteServiceTests` + `FollowingReadServiceTests`
  (78 integration tests total, including: follow/unfollow idempotency, self-follow guard, bell toggle,
  vouch with/without text, 5-limit, 6th call throws `VouchLimitException`, remove-vouch frees slot,
  outgoing/incoming asymmetry, `GetIncomingVouchesAsync` scoped to active user, avatar default fallback,
  `VouchText` sanitization strips XSS payload while preserving allowed HTML, long text exceeds old
  1000-char cap). `dotnet test` green (78/78).
- **L3-Logic — Stage 5 (WU21, 2026-06-22).** Bell toggles `ReceiveAlerts` via `SetReceiveAlertsAsync`;
  `FollowButton` and `VouchButton` are self-contained-write composites injecting `IFollowingWriteService`
  (legitimate per `layer3-logic.md`). Optimistic toggle on follow/unfollow click. `VouchButton` opens
  `ConfirmDialog` hosting `EditorView` for optional rich note; disabled+tooltip at 5-limit. `VouchList`
  is pass-through (no service injection); owner-conditional edit affordances via `IsEditable` parameter.
  **Verified:** tier **RazorComponents** — `FollowButtonTests`, `VouchButtonTests`, `VouchListTests`
  (see Feature 19 L3/L3.5 note for shared coverage). `dotnet test` green (64/64).
- **L3.5-Structure — Stage 5 (WU10, 2026-06-21). L4-Style — Stage 5 (WU10, rode inline with L3.5).**
  Built the `UserCard` leaf per §5.30.7: `Core/Users/UserCardDto.cs` (+ `UserCardBadgeDto.cs`) and
  `SharedUI/Users/UserCard.razor`, in a new cross-cutting `Users/` cluster (no single feature owns
  the atom — see `SKILL.md` "Code Organization"); cell number stays 18. Pure leaf, no service
  injection, one `[Parameter, EditorRequired] UserCardDto User`. View Profile is a plain always-on
  `<a>`; the other caret actions (Discover from this User, Copy link, Report, Send PM) are optional
  `EventCallback`s gated by `HasDelegate` — Report (WU34)/Send PM (WU35) stay dark until those
  features land. Badge collection field minted on the DTO now, rendered conditionally (empty until
  WU36 populates it). Avatar is `User.ProfilePictureRelativeUrl` copied verbatim by the producing
  read service — not resolved via `ISpriteReadService.GetSpriteUrl` (settled + doc-touched into
  `layer4-style.md` "Avatars Are Stored URLs, Not Sprite Keys" and `layer2-services.md`, ahead of
  the build); added a static `wwwroot/img/default-avatar.svg` fallback asset for the null case.
  **Verified:** `dotnet build` green (4 projects, 0 warnings); live server run, homepage `200`;
  throwaway harness on `HomeDesktop.razor` (full avatar+tagline+caret, minimal/no-avatar-no-caret,
  badges+partial-caret variants) user-confirmed visually correct (avatar `rounded-full` +
  default-fallback, linked bold username, conditional tagline/badges, caret open/close, only wired
  menu items render, no doubled spacing); harness removed after confirmation; no real consumer
  exists yet (lands in WU21/WU30/…), so those stay Stage 2 — the DTO contract alone doesn't flip
  them, same as WU4's `TagChip`.
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; relationship data rendered on the profile page verified in a
  real WASM runtime during the flip's browser wave (follow writes not driven this wave). Full wave
  narrative + the 7 bugs found/fixed: `workplan.md` WU-GlobalFlip.
  **2026-06-22 (WU12.5 backfill):** verification migrated into asserted tests — `UserCardTests` in
  `TheCanalaveLibrary.Tests.RazorComponents` (tier: **RazorComponents**). Covers: username renders;
  profile link `href` correct (`/user/{UserId}`); tagline shown/hidden; avatar URL vs. default
  `/img/default-avatar.svg`; menu closed by default; caret click opens menu (`div.absolute` appears);
  no optional callbacks → no buttons in menu; `OnReport` delegate → Report button appears and invokes
  callback. CSS visual rendering (avatar `rounded-full`, caret shape, spacing) remains human sign-off
  for Stage 6. `dotnet test` green.
- **L6 — Stage 5 (WU-L6, 2026-07-07 — resolved as already-covered, no DDL).** The reverse-lookup
  index exists by EF FK convention (`ix_followed_users_followed_user_id` — verified in
  `pg_indexes`); forward lookups ride the PK. A `(user_id, date_followed)` sort index was
  REJECTED under R4: per-user follow counts are small (same rationale as the Bookshelves recency
  ruling). Detail: `layer6-indexes.md` §"Rejected".

## Feature 19 — Vouches
- **L1 — Stage 5 (reconciled Phase B, 2026-06-20; was mis-marked Stage 1).** `Vouch` is a dedicated
  table (`Core/Models/Vouch.cs`): composite PK `(VouchingUserId, VouchedUserId)`, optional `VouchText`
  (`[MaxLength(1000)]`), `DateVouched` (DB default). `FollowedUser.IsVouched` was dropped. FK: voucher
  edge Cascade, vouched edge Restrict (incoming vouches cleared in C# `DeleteUserService`). Migration
  `20260620145246_InitialSchema` ships this shape; `dotnet build` and `has-pending-model-changes` are
  clean. **Superseded-spec note:** spec §5.8 and §8 Open Question #13 still present this as an open
  bool-vs-table choice with `VouchText MaxLength(280)` — that's the historical snapshot (spec is
  read-only). The user resolved the decision Phase B session 2026-06-20: ratify the dedicated table,
  and keep `VouchText` at **1000** (not 280) — code is authoritative, spec is not edited.
- **L1 — Stage 5 (re-verified WU21, 2026-06-22).** `MakeVouchTextUnlimited` migration removes
  `[MaxLength(1000)]` from `VouchText` — column is now unbounded `text` (widening, safe). Supersedes
  Phase B's 1000-char ruling and spec §5.8's original 280; code is authoritative. Applied via
  Testcontainers `Database.MigrateAsync` in the integration suite. `has-pending-model-changes` clean.
- **L2 — Stage 5 (WU21, 2026-06-22).** Covered by shared `IFollowingWriteService` /
  `IFollowingReadService` cluster above (Feature 18 L2 note). Vouch-specific: `VouchAsync` sanitizes
  `VouchText` before persist; 5-limit C#-enforced; `VouchLimitException` thrown on 6th. Integration
  tests include long-text (exceeds old 1000-char cap), XSS sanitization, limit enforcement.
  **Settled constraints — do not revisit:** dedicated `Vouch` table; outgoing public / incoming private
  asymmetry (§5.8); 5-per-user cap (anti-snowball scarcity lever); FK delete behavior (see Shared
  Context); `VouchText` is rich HTML sanitized-once-on-save (EditorView/RichTextView/sanitize path).
- **L3-Logic — Stage 5. L3.5-Structure — Stage 5. L4-Style — Stage 5 (all WU21, 2026-06-22).**
  `SharedUI/Following/` cluster: `FollowButton.razor` (follow/unfollow + bell; self-contained-write
  inject `IFollowingWriteService`), `VouchButton.razor` (follow-gated; `ConfirmDialog` + `EditorView`
  for rich note; disabled+tooltip at 5-limit; `JSInterop.Loose` in tests), `VouchList.razor`
  (pass-through; `UserCard` + `RichTextView` per row; owner-conditional remove controls via
  `IsEditable` — see `layer3.5-structure.md` "Owner-Conditional Edit Affordances"). Tailwind tokens
  only; `hover:underline`, `text-danger`, `text-muted` etc. Applied `@namespace TheCanalaveLibrary.SharedUI`
  on all three files (flat-namespace rule). **Verified:** tier **RazorComponents** —
  `FollowButtonTests` (7 tests: follow/unfollow labels, bell visibility, aria-label states, click
  callbacks), `VouchButtonTests` (6 tests: visibility gate, enabled/disabled states, dialog opens;
  `JSInterop.Loose`), `VouchListTests` (8 tests: empty message, per-row count, usernames, VouchText
  render, null VouchText omits RichTextView div, IsEditable gate, OnRemoveVouch callback).
  CSS visual rendering (avatar layout, spacing, disabled tooltip appearance) is human sign-off
  for Stage 6. `dotnet test` green (64/64).
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; vouch data rendered on the profile page verified in a real
  WASM runtime during the flip's browser wave (vouch writes not driven this wave). Full wave
  narrative + the 7 bugs found/fixed: `workplan.md` WU-GlobalFlip.
- **L6 — Stage 5** (see above; existing composite PK + `ix_vouches_vouched_user_id`).
- **L6 — Stage 5 (reconciled Phase B).** The migration-verified indexes for the new shape: the
  composite PK `(vouching_user_id, vouched_user_id)` covers outgoing-vouch lookups, and
  `ix_vouches_vouched_user_id` covers incoming-vouch lookups. The spec's old filtered-index pair on
  `followed_users` (`WHERE is_vouched = true`) no longer applies — see
  `skills/canalave-conventions/layer6-indexes.md` "Vouch Indexes" (updated to match).

## L4.5-Browser verification (2026-07-01) — F18 + F19 → Stage 5

As TestUser on ModUser's profile: Follow button → active "Following" + alerts-bell toggle appears;
`followed_users (1,3, receive_alerts=t)` via psql; recipient got a NewFollowerOnYou (type 30)
notification through the real WU22 seam (Feature 41 generation evidence). Vouch: button gated on
IsFollowing per WU21; dialog shows remaining-slots count (respected the seeded vouch), optional
rich-text note; Submit persisted the sanitized note (`<p>…</p>` via psql), button flipped to
"✓ Vouched", recipient got a NewVouchOnYou (type 32) notification. **Minor staleness (not
unsound):** VouchButton's IsFollowing comes from page-load RelationshipState, so it appears only
after a reload when the user follows and vouches in one visit — polish candidate for a later WU.
