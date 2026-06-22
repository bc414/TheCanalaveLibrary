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
- **L2 — Stage 2** (follow/unfollow write + "Followed Users" read; not author-specific).
- **L3-Logic — Stage 2** (bell toggles `ReceiveAlerts`; self-contained-write injection is legitimate).
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
  them, same as WU4's `TagChip`. **L5 — Stage 2.**
  **2026-06-22 (WU12.5 backfill):** verification migrated into asserted tests — `UserCardTests` in
  `TheCanalaveLibrary.Tests.RazorComponents` (tier: **RazorComponents**). Covers: username renders;
  profile link `href` correct (`/user/{UserId}`); tagline shown/hidden; avatar URL vs. default
  `/img/default-avatar.svg`; menu closed by default; caret click opens menu (`div.absolute` appears);
  no optional callbacks → no buttons in menu; `OnReport` delegate → Report button appears and invokes
  callback. CSS visual rendering (avatar `rounded-full`, caret shape, spacing) remains human sign-off
  for Stage 6. `dotnet test` green.
- **L6 — Stage 2** (filtered index `(followed_user_id)` for reverse lookups).

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
- **L2 — Stage 2** (5-vouch limit enforced in C#; display asymmetry: outgoing public, incoming private).
  **Settled constraints for the opusplan/build pass — do not revisit:** dedicated `Vouch` table;
  `VouchText` max length 1000; display asymmetry (outgoing public / incoming private, §5.8, decided
  independently of the schema-shape question); 5-per-user limit, C#-enforced; FK delete behavior as
  above.
- **L3/L3.5 — Stage 2** (vouch button visible only if already followed). **L4 — Stage 1. L5 — Stage 2.**
- **L6 — Stage 5 (reconciled Phase B).** The migration-verified indexes for the new shape: the
  composite PK `(vouching_user_id, vouched_user_id)` covers outgoing-vouch lookups, and
  `ix_vouches_vouched_user_id` covers incoming-vouch lookups. The spec's old filtered-index pair on
  `followed_users` (`WHERE is_vouched = true`) no longer applies — see
  `skills/canalave-conventions/layer6-indexes.md` "Vouch Indexes" (updated to match).
