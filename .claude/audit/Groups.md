# Audit — Groups/

**Features:** 38 (management), 39 (content & folders), 40 (display). Routes `/group/{GroupId:int}/{*Slug}`,
`/groups` (§5.29).

## Shared Context
**Entities (Core/Models/):** `Group` (unique `GroupName`, `Rating`/`MaxContentRating`→short, three
audience types), `GroupMember` (composite `(UserId,GroupId)`, Role enum, `DateJoined`), `GroupStory`
(first-class, `AddedByUserId` SetNull), `GroupFolder` (nesting via `ParentFolderId`, unique
`(GroupId,ParentFolderId,Name)`, `MaxRating`→short), `GroupComment`, `GroupBlogPost`. **No services or
components built.** Composes `StoryDeck` for story listings.

## Feature 38 — Group Management
- **L1 — Stage 5.** `Group` + `GroupMember` with role/audience model. **L2 — Stage 2.** **L3/L3.5 —
  Stage 2. L4 — Stage 1. L5 — Stage 2. L6 — Stage 2.**

## Feature 39 — Group Content & Folders
- **L1 — Stage 5.** `GroupFolder` self-nesting + `GroupStory` first-class entity established. **L2 —
  Stage 2** (content-rating enforcement **at write time**, §5.11). **L3-Logic — Stage 2. L3.5-Structure —
  Stage 2** (folder-tree UI; composes `StoryDeck`). **L4 — Stage 1. L5 — Stage 2.**

## Feature 40 — Group Display
- **L1 — Stage 5.** **L2 — Stage 2** (member list, story listing, folder browsing). **L3/L3.5 — Stage 2**
  (group page). **L4 — Stage 1. L5 — Stage 2.**
