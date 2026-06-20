# Audit — Following/

**Features:** 18 (user following), 19 (vouches). User-to-user relationship; distinct from story
interactions.

## Shared Context
**Entity:** `FollowedUser` (Core/Models/) — composite PK `(UserId, FollowedUserId)`, `ReceiveAlerts`,
`DateFollowed` (DB default), `IsVouched`. Fluent config: the follower edge is Cascade, the followed edge
is `Restrict` ("CONFLICT: resolved in C#"). Self-referential M:N through the explicit entity. No services,
no components built.

## Feature 18 — User Following
- **L1 — Stage 5.** `FollowedUser` matches §5.8 (follow/unfollow, bell `ReceiveAlerts`, date). The
  asymmetric delete behavior is deliberate. Awaiting migration.
- **L2 — Stage 2** (follow/unfollow write + "Followed Users" read; not author-specific).
- **L3-Logic — Stage 2** (bell toggles `ReceiveAlerts`; self-contained-write injection is legitimate).
- **L3.5-Structure — Stage 2** (`UserCard` leaf, §5.30.7, unbuilt). **L4 — Stage 1. L5 — Stage 2.**
- **L6 — Stage 2** (filtered index `(followed_user_id)` for reverse lookups).

## Feature 19 — Vouches
- **L1 — Stage 1 (conceptual, §8.13).** `IsVouched` bool exists on `FollowedUser`, but the spec leaves an
  **open Layer-1 decision**: keep the bool, or promote Vouch to its own junction table with optional
  `VouchText`. This decision gates the feature's downstream layers and the filtered indexes
  (`WHERE is_vouched = true`). Resolve in chat before building.
- **L2 — Stage 2** (5-vouch limit enforced in C#; display asymmetry: outgoing public, incoming private).
- **L3/L3.5 — Stage 2** (vouch button visible only if already followed). **L4 — Stage 1. L5 — Stage 2.**
- **L6 — Stage 2** (the two filtered vouch indexes — pending the L1 shape decision).
