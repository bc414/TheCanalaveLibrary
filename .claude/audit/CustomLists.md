# Audit — CustomLists/

**Feature:** 51 (custom lists). User-created collections beyond system lists. Public/private. Distinct
from `Discovery/`'s search-result narrowing — this is **personal organization** (§7.4). Mostly Stage 1.

## Shared Context
**Entities (Core/Models/):** `CustomList` (`UserId` Cascade, unique `(UserId,ListName)`, `IsPublic`,
`DateCreated` default), `CustomListEntry` (composite `(ListId,StoryId)`, Cascade from both list and story,
`DateAdded` default). **No services or components built.**

## Feature 51 — Custom Lists
- **L1 — Stage 5.** `CustomList`/`CustomListEntry` with unique-name constraint and cascade entries. Sound.
- **L2 — Stage 2.** Copy-on-write on share (application logic) unbuilt.
- **L3-Logic — Stage 1 (conceptual, §8.7).** Creation flow + filter composition are mostly TBD in the
  spec. Resolve in chat with skill files.
- **L3.5-Structure — Stage 1 (conceptual, §8.7).** **L4 — Stage 1** (blocked). **L5 — Stage 2.**
- Custom Lists also feed `UserCustomFilter` (a discovery exclusion source) — but that linkage lives in
  Discovery; here the concern is personal list CRUD.
