# Status Grid — Feature × Layer → Stage

Dashboard only. Stage values per CLAUDE.md (1–6 or N/A). Rows are the dependency-ordered
features from `grid_axes.md`, grouped by folder cluster. Columns:

`L1 | L2 | L3-Logic | L3.5-Structure | L4-Style | L5 | L6 | L7 | L8`

Global conditions affecting many cells (see `audit-summary.md` for detail):
- **Spec supersedes stale code** — the spec is the recent consolidation; the ~7-month-old code is mostly non-working. Where they disagree and the code isn't working, the spec wins. Most **Stage 4** cells here are therefore *stale-code traps* (resolution direction known: build to spec; existing code is salvage), not two-way adjudications. The only genuine reconciliations are mechanical: Identity post-move references and Story L5 endpoint wiring (audit-summary §3a).
- **L4-Style is Stage 1 everywhere it applies** — Tailwind is not installed (no `package.json`/`tailwind.config.js`); existing UI is Bootstrap. Blocked on design tokens.
- **RESOLVED (2026-06-20) — Migration generated, build verified.** L1 fluent config was extracted from
  the single inline `OnModelCreating` into `IEntityTypeConfiguration<T>` classes under
  `TheCanalaveLibrary.Server/Data/Configurations/` (see `layer1-data-model.md` §"Fluent API
  Organization"). `InitialSchema`, the project's first migration, was generated from the resulting model;
  `dotnet build` and `dotnet ef migrations has-pending-model-changes` both pass clean. Every L1 marked
  Stage 5 below is therefore now migration-verified, not just "sound, awaiting verification."

| # | Feature | Folder | L1 | L2 | L3-Logic | L3.5-Struct | L4-Style | L5 | L6 | L7 | L8 |
|---|---------|--------|----|----|----------|-------------|----------|----|----|----|----|
| 1 | Identity & Auth | Identity | 5 | 4 | 4 | 4 | 1 | N/A | N/A | N/A | N/A |
| 2 | Lookup Tables & Seed Data | Lookups | 4 | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| 3 | Sprite & Theme System | Sprites | 5 | 4 | 2 | 2 | 1 | 4 | N/A | N/A | N/A |
| 4 | Story Creation & Editing | Stories | 5 | 5 | 4 | 4 | 1 | 4 | 2 | N/A | N/A |
| 5 | Story Browsing & Display | Stories | 5 | 2 | 4 | 4 | 1 | 4 | 2 | N/A | N/A |
| 6 | Chapter Writing & Versioning | Chapters | 5 | 2 | 2 | 2 | 1 | 2 | 2 | N/A | N/A |
| 7 | Chapter Reading | Chapters | 5 | 2 | 2 | 2 | 1 | 2 | 2 | N/A | N/A |
| 8 | Story Arcs | Stories | 5 | 2 | 1 | 1 | 1 | 2 | N/A | N/A | N/A |
| 9 | Series & Ordering | Stories | 5 | 2 | 2 | 2 | 1 | 2 | N/A | N/A | N/A |
| 10 | Story Relationships | Stories | 5 | 2 | 2 | 2 | 1 | 2 | N/A | N/A | N/A |
| 11 | Tag Administration | Tags | 5 | 2 | 2 | 2 | 1 | 2 | 2 | N/A | N/A |
| 12 | Story Tagging | Tags | 5 | 2 | 4 | 4 | 1 | 2 | 2 | N/A | N/A |
| 13 | Tag Display & Sprites | Tags | 5 | 4 | 4 | 4 | 1 | 2 | N/A | N/A | N/A |
| 14 | Tag Filtering & Selection UI | Tags | N/A | 4 | 4 | 4 | 1 | 2 | N/A | N/A | N/A |
| 15 | Saved Tag Selections | Tags | 5 | 2 | 2 | 2 | 1 | 2 | N/A | N/A | N/A |
| 16 | Story Interaction State Writes | UserStoryInteractions | 4 | 2 | 2 | 2 | 1 | 2 | 4 | 2 | N/A |
| 17 | Interaction Lists & Bookshelves | UserStoryInteractions | 4 | 2 | 2 | 2 | 1 | 2 | 4 | N/A | N/A |
| 18 | User Following | Following | 5 | 2 | 2 | 2 | 1 | 2 | 2 | N/A | N/A |
| 19 | Vouches | Following | 1 | 2 | 2 | 2 | 1 | 2 | 2 | N/A | N/A |
| 20 | User Profile Editing | Profiles | 5 | 2 | 2 | 2 | 1 | 2 | N/A | N/A | N/A |
| 21 | User Profile Display | Profiles | 5 | 2 | 2 | 2 | 1 | 2 | N/A | N/A | N/A |
| 22 | User Stats | Profiles | 5 | 2 | 2 | 2 | 1 | 2 | N/A | N/A | N/A |
| 23 | Comment Posting | Comments | 5 | 2 | 2 | 2 | 1 | 2 | 2 | N/A | N/A |
| 24 | Comment Display & Pagination | Comments | 5 | 2 | 2 | 2 | 1 | 2 | 2 | N/A | N/A |
| 25 | Comment Likes | Comments | 4 | 2 | 2 | 2 | 1 | 2 | N/A | N/A | N/A |
| 26 | Spoiler Comments | Comments | 5 | 2 | 2 | 2 | 1 | 2 | N/A | N/A | N/A |
| 27 | Recommendation Submission | Recommendations | 5 | 2 | 2 | 2 | 1 | 2 | 2 | N/A | N/A |
| 28 | Recommendation Display | Recommendations | 5 | 2 | 2 | 2 | 1 | 2 | N/A | N/A | N/A |
| 29 | Hidden Gem Management | Recommendations | 5 | 2 | 1 | 2 | 1 | 2 | N/A | N/A | N/A |
| 30 | Recommendation Attribution | Recommendations | 5 | 2 | 2 | 2 | 1 | 2 | N/A | N/A | N/A |
| 31 | Search Page | Discovery | N/A | 2 | 2 | 2 | 1 | 2 | 2 | N/A | N/A |
| 32 | Full-Text Search | Discovery | 5 | 2 | 2 | 2 | 1 | 2 | 5 | N/A | N/A |
| 33 | Manual Tree Search | Discovery | N/A | 2 | 2 | 2 | 1 | 2 | 2 | N/A | N/A |
| 34 | Tag Directory | Discovery | N/A | 2 | 2 | 2 | 1 | 2 | N/A | N/A | N/A |
| 35 | Blog Post Writing | BlogPosts | 5 | 2 | 2 | 2 | 1 | 2 | 2 | N/A | N/A |
| 36 | Blog Post Display | BlogPosts | 5 | 2 | 2 | 2 | 1 | 2 | N/A | N/A | N/A |
| 37 | Polls | BlogPosts | 5 | 2 | 1 | 1 | 1 | 2 | N/A | N/A | N/A |
| 38 | Group Management | Groups | 5 | 2 | 2 | 2 | 1 | 2 | 2 | N/A | N/A |
| 39 | Group Content & Folders | Groups | 5 | 2 | 2 | 2 | 1 | 2 | N/A | N/A | N/A |
| 40 | Group Display | Groups | 5 | 2 | 2 | 2 | 1 | 2 | N/A | N/A | N/A |
| 41 | Notification Generation | Notifications | 5 | 2 | N/A | N/A | N/A | N/A | 2 | N/A | N/A |
| 42 | Notification Display | Notifications | 5 | 2 | 2 | 2 | 1 | 2 | 2 | N/A | N/A |
| 43 | Notification Settings | Notifications | 5 | 2 | 2 | 2 | 1 | 2 | N/A | N/A | N/A |
| 44 | Reading Progress Tracking | Chapters | 5 | 2 | 2 | 2 | N/A | N/A | N/A | 2 | N/A |
| 45 | View Count Tracking | Stories | 5 | 2 | 2 | N/A | N/A | 2 | N/A | 2 | N/A |
| 46 | Content Reporting | Moderation | 5 | 2 | 2 | 2 | 1 | 2 | 2 | N/A | N/A |
| 47 | Moderation Queue & Actions | Moderation | 5 | 2 | 2 | 2 | 1 | N/A | 2 | N/A | N/A |
| 48 | Story Approval Workflow | Moderation | 5 | 2 | 2 | 2 | 1 | N/A | N/A | N/A | N/A |
| 49 | Private Messaging | Messaging | 5 | 2 | 2 | 2 | 1 | N/A | 2 | N/A | N/A |
| 50 | Badge System | Badges | 5 | 2 | 2 | 2 | 1 | 2 | N/A | N/A | N/A |
| 51 | Custom Lists | CustomLists | 5 | 2 | 1 | 1 | 1 | 2 | N/A | N/A | N/A |
| 52 | User Account Deletion | Identity | 5 | 4 | 4 | 4 | 1 | N/A | N/A | N/A | N/A |
| 53 | Story Import & Verification | Moderation | 5 | 2 | 2 | 2 | 1 | N/A | N/A | N/A | N/A |
| 54 | Content Download/Export | Export | N/A | 2 | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| 55 | Community Spotlight | Spotlight | 1 | 1 | 1 | 1 | 1 | N/A | N/A | N/A | N/A |
| 56 | Feature Contributions | BlogPosts | 5 | 2 | 2 | 2 | 1 | N/A | N/A | N/A | N/A |
| 57 | Notification Cleanup Worker | Notifications | N/A | 2 | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| 58 | UserStat Recalculation Worker | Profiles | N/A | 2 | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| 59 | Automatic Tree Search | Discovery | 4 | 2 | 2 | 2 | 1 | 2 | 4 | N/A | 2 |
| 60 | Tree Search Data Mart Worker | Discovery | 4 | N/A | N/A | N/A | N/A | N/A | 4 | N/A | 2 |
| 61 | Also Favorited / Also Recommended | Discovery | 4 | 2 | 2 | 2 | 1 | 2 | 2 | 2 | 2 |
| 62 | SiteDailyStat Worker | Moderation | 4 | N/A | N/A | N/A | N/A | N/A | N/A | N/A | 2 |
