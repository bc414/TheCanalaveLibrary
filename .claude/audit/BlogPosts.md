# Audit — BlogPosts/

**Features:** 35 (writing), 36 (display), 37 (polls), 56 (feature contributions). Universal `EditorView`
for composition (§5.19).

## Shared Context
**Entities (Core/Models/):** `BaseBlogPost` (TPT root, `ToTable("base_blog_posts")`, `Rating`→short,
M:N `LikedByUsers`) → `ProfileBlogPost` (optional `StoryId`, `SetNull`), `GroupBlogPost` (`GroupId`).
`BasePoll` (TPT) → `SitePoll` / `BlogPostPoll`; `PollOption` (unique `(PollId,Text)` and
`(PollId,SortOrder)`). `FeatureContribution` (`SetNull` diamond-breaking to blog/comment).
**No services or components built.** All blog posts support comments (see Comments cluster).

## Feature 35 — Blog Post Writing
- **L1 — Stage 5.** TPT split sound. **L2 — Stage 2.** **L3/L3.5 — Stage 2** (depends on the universal
  `EditorView` atom owned by Chapters/). **L4 — Stage 1. L5 — Stage 2. L6 — Stage 2.**

## Feature 36 — Blog Post Display
- **L1 — Stage 5.** **L2 — Stage 2** (view in profile/story/group context). **L3/L3.5 — Stage 2.
  L4 — Stage 1. L5 — Stage 2.**

## Feature 37 — Polls
- **L1 — Stage 5** (`BasePoll`/`SitePoll`/`BlogPostPoll`, `PollOption` with `Voters` M:N + unique
  constraints). **L2 — Stage 2.** **L3 / L3.5 — Stage 1 (conceptual, §8.6):** detailed poll UI was never
  specified — resolve in chat. **L4 — Stage 1. L5 — Stage 2.**

## Feature 56 — Feature Contributions
- **L1 — Stage 5** (`FeatureContribution`; SetNull diamond-breaking FKs to `BaseBlogPost`/`BaseComment`).
  **L2 — Stage 2** (admin attribution of accepted suggestions; tied to "Site Development" group).
  **L3/L3.5 — Stage 2. L4 — Stage 1. L5 — N/A** (admin-only server surface).
