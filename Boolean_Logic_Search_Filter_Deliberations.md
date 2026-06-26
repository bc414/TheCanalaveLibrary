# Boolean Logic, Search Modes & Filter Deliberations
## Canalave Library — Fanfiction Site Design Notes

---

## 1. The Original Concept: Random Search & UserStoryInteractions (~Entry #2035)

The first search-related design question established the core interaction state table. The goal was a "random search" that could filter out stories a user had already seen, and let users mark stories as undesirable.

**Decision:** A many-to-many junction table `UserStoryInteractions` between users and stories, with an `InteractionType` column.

**Naming deliberation:**
- "Read" was rejected due to ambiguity with the verb form.
- **`Viewed`** was chosen for the "already seen" state.
- **`Ignored`** was chosen for "not interested" (over alternatives like `Hidden`).

Initial schema (later refined):

```sql
CREATE TABLE UserStoryInteractions (
    UserID INT NOT NULL,
    StoryID INT NOT NULL,
    InteractionType NVARCHAR(50) NOT NULL,
    InteractionDate DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_UserStoryInteractions PRIMARY KEY (UserID, StoryID),
    CONSTRAINT CK_InteractionType CHECK (InteractionType IN ('Viewed', 'Ignored'))
);
```

**Boolean logic at query time:** The random search used a `NOT IN` subquery to exclude all stories where any `UserStoryInteractions` row existed for the active user — meaning Viewed and Ignored were both excluded together under the same condition. The user had no per-type control at this early stage.

---

## 2. Tree Search Design: Graph Traversal & Relationship Types (Entry #2031)

### The concept

The tree search starts at a root story or root user, follows connections up to a configurable `MaxDegrees`, gathers stories matching the user's tag filters that haven't been Viewed or Ignored, and returns them in random order for discoverability.

**Terminology decisions:**
- A single connection step was called a **Degree** (from "Degree of Connection").
- The type of connection to follow was called a **Relationship Type** (from "hop criteria").

### Relationship types (hop criteria)

The user selects which connection types the algorithm is permitted to follow:

| Relationship Type | Breadth | Description |
|---|---|---|
| **Favorited** | Very broad | Story → users who favorited it → their other favorites |
| **Recommended** | Narrower | Story → users who wrote a recommendation for it → their other recommendations |
| **Authored** | Most narrow | Story → its author → author's other stories |

These are checked by the user as independent boolean options. The SQL rCTE is built dynamically to include only the JOIN clauses for the selected relationship types.

### Algorithm: Recursive CTE (BFS)

The traversal is implemented as a Recursive Common Table Expression in PostgreSQL. The graph alternates between story nodes and user nodes each degree:

```sql
;WITH GraphTraversal AS (
    -- Anchor: start node (Degree 0)
    SELECT StoryID, NULL AS UserID, 0 AS Degree FROM Stories WHERE StoryID = @RootStoryID
    UNION ALL
    -- Recursive: story → connected users
    SELECT NULL, u.UserID, gt.Degree + 1
    FROM GraphTraversal gt
    INNER JOIN Stories s ON gt.StoryID = s.StoryID
    LEFT JOIN Users u ON s.AuthorID = u.UserID  -- 'Author' relationship
    WHERE gt.Degree < @MaxDegrees
    UNION ALL
    -- Recursive: user → connected stories
    SELECT s.StoryID, NULL, gt.Degree + 1
    FROM GraphTraversal gt
    INNER JOIN Users u ON gt.UserID = u.UserID
    LEFT JOIN Stories s ON u.UserID = s.AuthorID  -- 'Authored' relationship
    WHERE gt.Degree < @MaxDegrees
)
SELECT DISTINCT s.StoryID, s.Title
FROM GraphTraversal gt
INNER JOIN Stories s ON gt.StoryID = s.StoryID
WHERE s.StoryID NOT IN (
    SELECT StoryID FROM UserStoryInteractions WHERE UserID = @ActiveUserID
)
ORDER BY NEWID();
```

**Boolean logic for exclusion:** The final WHERE clause excludes any story that has *any* interaction row for the active user — both Viewed and Ignored are excluded simultaneously, regardless of relationship type.

**Duplicate prevention:** The BFS handles the dense/cyclical graph (two recommenders recommending the same story is common) through `SELECT DISTINCT` at the final step and by using a hard `MaxDegrees` ceiling as the stop condition. A graph database (e.g., Neo4j) was considered but rejected as architectural overkill for the expected scale.

### Stateless approach (Entry #2028)

When a user clicks a new root story to "pivot" from within results, the question arose whether to pass the existing result set back to the database as seed data. The decision was to use a **stateless fresh search** each time:

- Each click triggers a brand-new rCTE query with the new root.
- Already-seen stories are filtered out in application code by comparing against the previous result set.
- **Reason:** The database traversal cost is dominated by the rCTE itself, not by excluding a few hundred already-seen IDs. The stateful approach would add complexity with negligible performance benefit.

Saving tree search results to a user list is supported by creating a new private `UserList` row and inserting result `StoryID`s into `UserListEntries` — reusing existing schema with no changes.

---

## 3. Hidden Gem Recommendations: Deep Tree Search (Entry #2024)

### Problem

The broad tree search (using Favorited) expands extremely quickly because "power recommenders" may have hundreds of ordinary recommendations, flooding the graph with low-signal paths.

### Decision

A special default list — **"Hidden Gem Recommendations"** — is introduced with a strict entry limit (5 stories). The **deep tree search mode** uses only this list as its hop type:

> A story's Hidden Gem holders → their other Hidden Gem list entries → those entries' Hidden Gem holders → ...

This forms a **"chain of trust"**: every traversal step follows only the highest-signal, most curated endorsements on the site. Because the list is limited to 5 entries, the graph stays narrow even at high degree counts.

**Schema:** No new tables needed. This is implemented as a default, non-deletable `UserList` where `ListName = 'Hidden Gem Recommendations'`. The 5-entry limit is enforced in application logic. The business rule requires the user to have written an approved recommendation for the story before it can be added.

**Author curation is separate:** When an author picks "featured reviews" from their story's recommendations, that selection has no effect on the tree search algorithm. It appears as pinned reviews in the story's UI only.

---

## 4. Search Modes: The Filter Matrix (~Entry #1500)

### Five distinct search modes

The site has five search modes, each with different use cases that warrant independent default filter states:

1. **Standard Search** — broad discovery browsing
2. **Random Search** — randomized discoverability
3. **Tree Search** — graph-based discovery (broad and deep variants)
4. **Hidden Gem Search** — deep tree search via Hidden Gem lists only
5. **Also Favorited** — collaborative filtering (finds stories favorited by users who share favorites with the active user)

A common Razor component renders the filter checkboxes for any mode, with a "Save as Default" button to persist the user's preferred state per mode.

### Filter criteria (interaction states)

Six boolean filter criteria apply across search modes:

| Criterion | Description |
|---|---|
| `Viewed` | Has the user started/read this story? |
| `Ignored` | Has the user explicitly marked "not interested"? |
| `ReadItLater` | Is the story on the user's Read It Later list? |
| `Completed` | Has the user finished the story? |
| `Favorited` | Is the story on the user's Favorites list? |
| `InProgress` | Is the user currently mid-read? (later refined with `CaughtUp` and `IsActivelyReading`) |

---

## 5. Search Settings Storage: The Sparse Override Model (Entry #1500, #1499)

### The problem with dense models

Options considered:
- **Option 1:** Wide columns directly on `AspNetUsers` — too rigid, becomes 30+ columns.
- **Option 2/3:** Pre-populate all mode×criteria rows per user on registration — 30+ `INSERT`s per new user, storage bloat, all defaults redundantly stored.

### Decision: Sparse Override Model (Option 4)

Four tables:

```sql
-- Admin-defined lookup: all search modes
CREATE TABLE SearchModes (
    SearchModeID NVARCHAR(50) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NULL,
    SortOrder INT NOT NULL DEFAULT 0
);

-- Admin-defined lookup: all filter criteria
CREATE TABLE FilterCriteria (
    CriterionID NVARCHAR(50) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NULL,
    SortOrder INT NOT NULL DEFAULT 0
);

-- Site-wide defaults: one row per mode×criterion combination
CREATE TABLE DefaultSearchSettings (
    SearchModeID NVARCHAR(50) NOT NULL,
    CriterionID NVARCHAR(50) NOT NULL,
    DefaultValue BIT NOT NULL DEFAULT 0,
    CONSTRAINT PK_DefaultSearchSettings PRIMARY KEY (SearchModeID, CriterionID),
    CONSTRAINT FK_DefaultSearchSettings_SearchMode FOREIGN KEY (SearchModeID)
        REFERENCES SearchModes(SearchModeID) ON DELETE CASCADE,
    CONSTRAINT FK_DefaultSearchSettings_FilterCriteria FOREIGN KEY (CriterionID)
        REFERENCES FilterCriteria(CriterionID) ON DELETE CASCADE
);

-- User overrides: ONLY rows where a user deviates from the site default
CREATE TABLE UserSearchSettings (
    UserID NVARCHAR(450) NOT NULL,
    SearchModeID NVARCHAR(50) NOT NULL,
    CriterionID NVARCHAR(50) NOT NULL,
    UserValue BIT NOT NULL,
    CONSTRAINT PK_UserSearchSettings PRIMARY KEY (UserID, SearchModeID, CriterionID),
    CONSTRAINT FK_UserSearchSettings_User FOREIGN KEY (UserID)
        REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserSearchSettings_SearchMode FOREIGN KEY (SearchModeID)
        REFERENCES SearchModes(SearchModeID) ON DELETE CASCADE,
    CONSTRAINT FK_UserSearchSettings_FilterCriteria FOREIGN KEY (CriterionID)
        REFERENCES FilterCriteria(CriterionID) ON DELETE CASCADE
);

CREATE NONCLUSTERED INDEX IX_UserSearchSettings_UserMode
    ON UserSearchSettings(UserID, SearchModeID);
```

**On user registration:** Zero writes to these tables.

**On search page load:** Two queries, merged in C#:

```csharp
// 1. Get all defaults for this mode (tiny, cacheable site-wide)
var siteDefaults = _context.DefaultSearchSettings
    .Where(s => s.SearchModeID == currentMode)
    .ToDictionary(s => s.CriterionID, s => s.DefaultValue);

// 2. Get the user's overrides (0–6 rows)
var userOverrides = _context.UserSearchSettings
    .Where(s => s.UserID == userId && s.SearchModeID == currentMode)
    .ToDictionary(s => s.CriterionID, s => s.UserValue);

// 3. Merge (user value wins)
foreach (var o in userOverrides)
    siteDefaults[o.Key] = o.Value;
// siteDefaults now has the correct state
```

**On "Save as Default":** Compare each checkbox to the *site default*. If different → upsert a row in `UserSearchSettings`. If same → delete the row (keeps the table sparse). 

**Why not put defaults in the lookup tables (Option 4.5)?** The default value is a property of the *relationship* between a mode and a criterion, not of either dimension alone. `FilterCriteria` can't store a per-mode default; `SearchModes` would need a column per criterion, recreating the wide-table problem. The junction table `DefaultSearchSettings` is the only design that allows both dimensions to expand independently.

### `SearchModeID` / `CriterionID` as `NVARCHAR(50)`, not `TINYINT`

This was explicitly deliberated (Entry #1329). The argument for `TINYINT` was storage efficiency. The counter-argument:

- These tables are tiny (dozens of rows each) and will be fully cached in memory.
- `UserSearchSettings` is sparse — very few rows exist even across many users.
- C# code reads these values *directly* as logical identifiers: `if (criterion.CriterionID == "Favorited")`.
- Using `TINYINT` would require maintaining a parallel C# `enum` and casting every comparison, adding complexity for negligible gain.

Verdict: **Keep as `NVARCHAR(50)`.** Contrast with `NotificationTypeID` which was changed to `TINYINT` because the `Notifications` table is one of the largest, most-written-to tables in the system.

---

## 6. Search Templates (Entry #1498)

Templates are admin-created presets that describe filter combinations in plain English. Examples:
- "Discover stories I have never seen before" → all boxes unchecked
- "Give me all the stories (old school search)" → all boxes checked
- "I'm looking for new stories, but also remind me of my Read It Later" → only ReadItLater checked

**Key design clarification:** Templates and the sparse override model serve distinct purposes:
- **Sparse override model** answers "what should the checkboxes look like when the page loads?"
- **Templates** answer "what does the user want the checkboxes to look like *right now*?"

Selecting a template updates the checkbox state client-side without touching `UserSearchSettings`. Clicking "Save as Default" afterward writes the template's state to `UserSearchSettings`.

**Database schema:**

```sql
-- Template header
CREATE TABLE SearchTemplates (
    TemplateID INT IDENTITY(1,1) PRIMARY KEY,
    SearchModeID NVARCHAR(50) NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NOT NULL,
    SortOrder INT NOT NULL DEFAULT 0,
    CONSTRAINT FK_SearchTemplates_SearchMode
        FOREIGN KEY (SearchModeID) REFERENCES SearchModes(SearchModeID) ON DELETE CASCADE
);

-- Template details: one row per criterion
CREATE TABLE SearchTemplateSettings (
    TemplateID INT NOT NULL,
    CriterionID NVARCHAR(50) NOT NULL,
    Value BIT NOT NULL,
    CONSTRAINT PK_SearchTemplateSettings PRIMARY KEY (TemplateID, CriterionID),
    CONSTRAINT FK_SearchTemplateSettings_Template
        FOREIGN KEY (TemplateID) REFERENCES SearchTemplates(TemplateID) ON DELETE CASCADE,
    CONSTRAINT FK_SearchTemplateSettings_FilterCriteria
        FOREIGN KEY (CriterionID) REFERENCES FilterCriteria(CriterionID) ON DELETE CASCADE
);
```

Templates are not user-created. Admins create them and take community suggestions.

---

## 7. Dynamic Entity Criteria: Filtering by Lists & Groups (Entry #1497)

### Problem

The static boolean criteria (`Ignored`, `Completed`, etc.) can't reference another entity (a list ID, a group ID). A user should be able to include or exclude stories based on membership in a personal list, a public list, or a group.

### Decision

Add a second sparse table specifically for entity-based filters:

```sql
CREATE TABLE UserSearchEntityFilters (
    UserSearchEntityFilterID INT IDENTITY(1,1) PRIMARY KEY,
    UserID NVARCHAR(450) NOT NULL,
    SearchModeID NVARCHAR(50) NOT NULL,
    FilterType NVARCHAR(50) NOT NULL,  -- Later refined to TINYINT; values: 'UserList' or 'Group'
    EntityID INT NOT NULL,             -- ListID or GroupID
    Include BIT NOT NULL DEFAULT 1,    -- 1 = whitelist, 0 = blacklist
    CONSTRAINT FK_UserSearchEntityFilters_User
        FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserSearchEntityFilters_SearchMode
        FOREIGN KEY (SearchModeID) REFERENCES SearchModes(SearchModeID) ON DELETE CASCADE,
    CONSTRAINT CK_UserSearchEntityFilters_FilterType
        CHECK (FilterType IN ('UserList', 'Group'))
);

CREATE NONCLUSTERED INDEX IX_UserSearchEntityFilters_UserMode
    ON UserSearchEntityFilters(UserID, SearchModeID);
```

On page load, both the boolean settings and the entity filter list are fetched separately and provided to the UI.

### FilterType values: PersonalList vs. PublicList vs. UserList

Initially, three filter types were proposed: `PersonalList`, `PublicList`, and `Group`. This was later corrected: all user lists (whether private or public) live in the same `UserLists` table. Therefore `FilterType` only needs two values:

- **`UserList`**: `EntityID` refers to a `UserListID`. Application code checks `List.UserID == currentUser` OR `List.IsPublic == true` to verify access.
- **`Group`**: `EntityID` refers to a `GroupID`.

---

## 8. Include vs. Exclude Logic: Discovery Model vs. Library Model (Entry #1416)

### The question

Should users be allowed to toggle per-criterion whether it acts as an include or an exclude filter?

**Decision: No.** That is too complicated for users and the UX would be confusing. Instead, **the search mode itself determines whether all criteria act as includes or excludes.** Users only toggle checkboxes; the semantics are fixed by the mode.

### Discovery Model (Exclude / Blacklist logic)

Used by: Standard Search, Random Search, Hidden Gem Search, Tree Search.

**Logic:** "Show me everything, *except*..."

- All checkboxes default to **unchecked** (show everything by default).
- Checking a box **excludes** that category.
- Each checked box adds an `AND NOT` condition to the query.

Example SQL shape:
```sql
WHERE
    (@ExcludeIgnored = 0 OR StoryID NOT IN (SELECT StoryID FROM ... WHERE IsIgnored = 1))
    AND (@ExcludeFavorited = 0 OR StoryID NOT IN (SELECT StoryID FROM ... WHERE IsFavorited = 1))
    -- etc.
```

### Library Model (Include / Whitelist logic)

Used by: A dedicated "My Library" or "My Engagements" search page.

**Logic:** "Show me *only*..."

- All checkboxes default to **unchecked** (results start empty).
- Checking a box **includes** that category.
- Multiple checked boxes are combined with `OR` so users can see multiple engagement categories at once.

Example SQL shape:
```sql
WHERE
    ((@ShowFavorites = 1 AND IsFavorited = 1)
     OR (@ShowReadItLater = 1 AND IsReadItLater = 1)
     OR (@ShowCompleted = 1 AND ReadStatus = 'Completed'))
```

**Templates on Discovery page:** e.g., "Max Discovery" unchecks all boxes.
**Templates on Library page:** e.g., "Everything I've Touched" checks all boxes.

---

## 9. Whitelist / Blacklist Logic for Entity Filters (Entries #1333, #1332)

### Include = 0 (Blacklist)

"Show me all stories, *except* for those on my 'Overdone Tropes' list."

### Include = 1 (Whitelist)

"Show me *only* the stories that are on my 'Must Read' list."

### Combining multiple entity filters

The true power is combining multiple filters simultaneously. Example saved query:

| FilterType | EntityID | Include |
|---|---|---|
| `UserList` | 123 (Epic Sagas) | 1 (whitelist) |
| `UserList` | 456 (Site Must-Reads) | 1 (whitelist) |
| `UserList` | 789 (Overdone Tropes) | 0 (blacklist) |

**C# query logic:**

1. Fetch all stories from whitelisted lists → union them into a base set (OR logic between whitelists).
2. Fetch all stories from blacklisted lists.
3. Final query: `WHERE (StoryID IN whitelist_set) AND (StoryID NOT IN blacklist_set) AND (other filters like Status = 'Completed')`.

This combination cannot be achieved by browsing a single list page — it requires the `UserSearchEntityFilters` table to persist the multi-list configuration as a reusable saved search.

Initial conclusion was that `Include = 1` was redundant (users can just navigate to the list). The deliberation resolved that the whitelist is only essential when *combining multiple lists* into a single base set, which is the primary purpose of the feature.

---

## 10. ReadStatus Interaction States: Boolean Complexity (Entries #1414–#1412)

The `ReadStatus` and related boolean columns on `UserStoryEngagement` (the successor to `UserStoryInteractions`) directly affect how discovery and library search filters behave.

### Final state model

| State | Meaning | Filter behavior |
|---|---|---|
| `Unread` | No row in `UserStoryEngagement` (implicit) | Default for all stories |
| `InProgress` | User has read ≥1 chapter | Checked by "Exclude In Progress" on Discovery |
| `Completed` | User has finished a story the author marked Completed | Checked by "Show Completed" on Library |

### "Actively Reading" vs. "Abandoned InProgress"

The `InProgress` filter on Discovery search was designed primarily to hide stories the user *sampled and abandoned* (read a chapter, got bored, stopped). This distinction matters because:

- A user who is actively mid-read on several stories would not want those to be hidden from their Library search.
- A story a user partly read years ago is functionally "explored but abandoned" — the Discovery exclusion is appropriate.

A separate boolean flag `IsActivelyReading` was proposed and then reconsidered. The final decision was to keep `ReadStatus` simple and use `IsActivelyReading` as a manually-controlled flag (not automatically set), so:

- **Discovery page:** Uses `ReadStatus != 'InProgress'` to exclude all sampled stories.
- **Library page:** Uses `IsActivelyReading == true` to show only stories the user is currently following.

A `CaughtUp` state (user has read all available chapters of a still-in-progress story) was introduced and then abandoned as unnecessary complexity. The conclusion was that if a user got caught up and didn't add to `Tracking`, the story should stay `InProgress` indefinitely — the absence of tracking signals lack of interest in updates.

---

## 11. Tag Filtering and Indexing (Entry #1256)

### Why a separate index is needed

The `StoryTags` table has primary key `(StoryID, TagID)`. The most common tag query is:

```sql
SELECT StoryID FROM StoryTags WHERE TagID = 123;
```

This query cannot use the primary key because `TagID` is not the *leading* column. The database would perform a full table scan — O(n) — across potentially millions of rows.

**Decision:** A non-clustered index on `TagID` alone is required:

```sql
CREATE NONCLUSTERED INDEX IX_StoryTags_TagID ON StoryTags(TagID);
```

This reduces tag-based lookups from O(n) to O(log n) via B-Tree seeks.

### Multi-tag filtering

When a user selects multiple tags, the query pattern is:

```sql
-- Stories that have ALL selected tags (AND logic)
WHERE StoryID IN (SELECT StoryID FROM StoryTags WHERE TagID = 1)
  AND StoryID IN (SELECT StoryID FROM StoryTags WHERE TagID = 2)
  AND StoryID IN (SELECT StoryID FROM StoryTags WHERE TagID = 3)
```

Each subquery benefits from `IX_StoryTags_TagID`. Multi-tag intersection is AND logic by default — there was no deliberation about supporting OR across tags (i.e., "any of these tags"), suggesting AND (match-all) is the intended behavior for tag combination.

### Key index distinction

Two classes of indexes serve different purposes:

- **Background worker indexes** (filtered): Used to compute denormalized `COUNT` values like `FavoriteCount`, `FollowCount`, etc. These make `COUNT(*) WHERE StoryID = X` fast.
- **Real-time browsing indexes**: Used by live queries for sorting, joining, and filtering. `IX_StoryTags_TagID` is in this category.

---

## 12. Summary: Boolean Logic Decision Matrix

| Context | Logic used | Who controls it | Notes |
|---|---|---|---|
| Random Search exclusion (Viewed, Ignored) | Both excluded together (no per-type control) | User indirectly via interactions | Single `NOT IN` subquery |
| Tree Search exclusion (Viewed, Ignored) | Same exclusion as random search | User indirectly | Applied in final WHERE clause |
| Discovery Search filter checkboxes | AND NOT (exclude logic per checkbox) | User toggles | Mode defines semantics; user picks state |
| Library Search filter checkboxes | OR (include logic per checkbox) | User toggles | Accumulate "show me" categories |
| Per-criterion include/exclude toggle | **Not allowed** | N/A | Rejected as too confusing |
| Entity filter whitelist (Include=1) | OR across included lists | User's saved configuration | Union of lists forms base set |
| Entity filter blacklist (Include=0) | AND NOT excluded from result set | User's saved configuration | Applied after whitelist union |
| Final entity filter query shape | (IN whitelist) AND (NOT IN blacklist) AND (other criteria) | Combination | Standard multi-filter SQL |
| Tag combination (multi-tag) | AND (must match all selected tags) | User's tag selection | Each tag is a separate IN subquery |
| Search mode defaults per user | Sparse override (merge site default + user overrides) | User "Save as Default" | 0 writes on account creation |
