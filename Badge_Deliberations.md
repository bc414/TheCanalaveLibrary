# Badge System Deliberations
### Canalave Library — Design Session Notes

This document records all deliberations regarding the badge system for the Canalave Library fanfiction site, including superseded designs and the reasoning behind each decision. Entries are drawn from the design session transcript (September–November 2025).

---

## Table of Contents

1. [Design Philosophy & Badge Catalogue](#1-design-philosophy--badge-catalogue)
2. [Schema Architecture: Two-Table Design](#2-schema-architecture-two-table-design)
3. [Badge Award Mechanism: Event-Driven Background Worker](#3-badge-award-mechanism-event-driven-background-worker)
4. [UserStats Table](#4-userstats-table)
5. [BadgeKey Type: String vs. Int](#5-badgekey-type-string-vs-int)
6. [Naming Convention: BadgeID → BadgeKey](#6-naming-convention-badgeid--badgekey)
7. [Why Strings Instead of Enums](#7-why-strings-instead-of-enums)
8. [String Constants (SiteBadges Class)](#8-string-constants-sitebadges-class)
9. [Composite PK Order for UserBadges](#9-composite-pk-order-for-userbadges)
10. [SQL Schema (Final)](#10-sql-schema-final)
11. [EF Core Seed Data](#11-ef-core-seed-data)
12. [Delete Policies](#12-delete-policies)
13. [UI Components & Service Layer](#13-ui-components--service-layer)

---

## 1. Design Philosophy & Badge Catalogue

**Source:** Entry #1578 / #1577 (Section XI — Badge System Overview)

**Goal:** Reward meaningful, positive-sum interactions and non-predatory site support. Discourage "slop" / volume grinding. Badges are displayed subtly on profiles, comment headers, and user bars.

**Implementation:** Driven by counter columns on the `UserStats` table, updated via application logic. Award logic runs in a background worker, not inline.

### Full Badge Catalogue

| Badge Name | Metric / Source | Rewarded Action | Example Tiers | Category |
|---|---|---|---|---|
| **The Patron / Spotlighter** | `SpotlightCount` | Financially supporting the site & using Spotlight Credits | Bronze(1), Silver(5) | Community Support |
| **The Tastemaker** | `RecommendationLikesReceivedCount` | Writing recommendations the community finds genuinely helpful ("Found Helpful" click) | Bronze(10), Silver(50) | Community Curation |
| **The Librarian** | `PublicListStoryCount` (calculated) | Creating and sharing public curated reading lists that aid discovery | Bronze(25), Silver(100) | Community Curation |
| **The Valued Commenter** | `CommentLikesReceivedCount` | Writing comments that others find insightful or helpful (via Comment Likes) | Bronze(50), Silver(250) | Constructive Engagement |
| **The First Responder** | (Needs new tracking — complex) | Being the first to comment on a newly published chapter | Simple count | Constructive Engagement |
| **The Series Champion** | (Needs complex tracking) | Reading every chapter in a long, completed series | Per series length? | Constructive Engagement |
| **The Site Veteran** | `DateCreated` | Long-term membership and loyalty | 1 Year, 3 Years, 5 Years | Site Stewardship |
| **The Good Samaritan** | `HelpfulReportCount` (Needs tracking) | Submitting accurate, actionable reports that help moderators | Bronze(5), Silver(20) | Site Stewardship |
| **The Muse / Inspirer / Spark** | `AcknowledgedInspirationsCount` | Authoring a story that inspires another (requires approval of "InspiredBy" link) | Bronze(1), Silver(5) | Contribution |
| **The Beta Contributor** | `AcknowledgedBetaCount` | Providing helpful beta reading feedback, explicitly acknowledged by author | Bronze(3), Silver(10) | Contribution |
| **The Architect** | `FeatureContributorCount` | Providing valuable site feature feedback acknowledged by admin | Bronze(1), Silver(3) | Site Stewardship |

**Anti-farming safeguards built into badge criteria:**
- `AcknowledgedBetaCount` requires explicit author acknowledgment, not just being listed as a beta reader. This prevents farming by having a friend list you on all their stories.
- `HelpfulReportCount` requires mod validation ("helpful" flag), not just report submission volume.
- `AcknowledgedInspirationsCount` requires the "InspiredBy" story relationship to be approved by the target author.

**Notable omissions (deliberate):**
- No badge for raw comment count, story count, or word count (volume metrics). These are stored in `UserStats` but do not directly award badges — they are tracked as statistics, not gamified.
- No badge for receiving story favorites (passive, no effort from recipient).

---

## 2. Schema Architecture: Two-Table Design

**Source:** Entry #1490

### The Problem (Superseded Approach)

The initial design put badge statistics as dedicated columns on `AspNetUsers`:
- e.g., `CommentCount`, `SpotlightCount`, `RecommendationLikesReceivedCount` all as separate INT columns on the identity table.

**Decision: Rejected.** Adding more badges would require adding more columns indefinitely — a major design flaw and a violation of the separation between identity data and application data.

### The Solution: Badges + UserBadges

Two new tables were introduced:

**`Badges` (master list / lookup table)**

```sql
CREATE TABLE Badges (
    BadgeKey  NVARCHAR(50) NOT NULL,
    DisplayName    NVARCHAR(100) NOT NULL,
    Description    NVARCHAR(500) NULL,
    IconBaseUrl    NVARCHAR(500) NOT NULL,
    SortOrder      INT NOT NULL DEFAULT 0,
    CONSTRAINT PK_Badges PRIMARY KEY (BadgeKey)
);
```

- An admin-defined lookup table. To add a new badge, add one row here — no code change or migration needed for the badge definition itself.
- `BadgeKey` is a human-readable string (e.g., `"beta-reader"`, `"first-story"`). See §5 and §6 for the key type deliberation.

**`UserBadges` (junction table)**

```sql
CREATE TABLE UserBadges (
    UserID       INT NOT NULL,
    BadgeKey     NVARCHAR(50) NOT NULL,
    DateEarned   DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    DisplayOrder INT NOT NULL DEFAULT 0,
    CONSTRAINT PK_UserBadges PRIMARY KEY (UserID, BadgeKey),
    CONSTRAINT FK_UserBadges_AspNetUsers FOREIGN KEY (UserID)
        REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserBadges_Badges FOREIGN KEY (BadgeKey)
        REFERENCES Badges(BadgeKey) ON DELETE CASCADE
);
```

This table solves both problems at once:
- **Which badges has the user earned?** — Every row in `UserBadges` is a badge the user has.
- **Which badges does the user want to display?** — The `DisplayOrder` column.

### The DisplayOrder Pattern

`DisplayOrder` is an `INT` with default `0`:
- `0` = Not displayed (badge earned but hidden).
- `1` = Displayed in the first slot.
- `2` = Displayed in the second slot.
- ...and so on.

The user's visible badge bar query:
```sql
SELECT * FROM UserBadges WHERE UserID = @id AND DisplayOrder > 0 ORDER BY DisplayOrder
```

This elegantly supports both "show all earned badges" (no filter on DisplayOrder) and "show the user's chosen highlighted badges" (filter `DisplayOrder > 0`).

---

## 3. Badge Award Mechanism: Event-Driven Background Worker

**Source:** Entry #1489

### The Question

Can the application use a background worker to check badge thresholds and award badges, instead of doing it inline during the triggering action?

**Answer: Yes, and this is the recommended pattern.**

### Superseded Approach

Inline badge logic — checking `if (commentCount == 100) { AwardBadge(...); }` directly in the API controller or service after saving the comment. This would slow down the response and couple the `Comments` service to the `Badge` service.

### Event-Driven Worker Pattern

**Step 1 — Main application (fast path):**
- Save the comment.
- Increment the counter: `UPDATE UserStats SET CommentCount = CommentCount + 1 WHERE UserId = @id` (extremely fast).
- Fire an event: publish `NewCommentPosted(UserId = @id)` to an in-memory queue or message bus.
- API request finishes in milliseconds.

**Step 2 — Background worker (async path):**
```csharp
// Worker logic — no COUNT(*) needed, stats are already computed
var stats = _context.UserStats.Single(u => u.UserId == id);

if (stats.CommentCount == 100)
{
    _badgeService.AwardBadge(id, SiteBadges.HundredComments);
}
if (stats.CommentCount == 500)
{
    _badgeService.AwardBadge(id, SiteBadges.FiveHundredComments);
}
```

### Trade-offs

| Aspect | Result |
|---|---|
| **API response time** | Fast — badge logic does not block the user's request |
| **Decoupling** | `CommentsService` has no dependency on `BadgeService` |
| **Resilience** | If badge logic fails, the comment was already saved; worker retries |
| **Award latency** | Badge is awarded seconds after the action, not the same millisecond |

The slight latency (a few seconds) is an acceptable trade-off for all badge types on this site.

---

## 4. UserStats Table

**Source:** Entry #1487, #1486

### Why a Separate Table (Not Columns on AspNetUsers)

`AspNetUsers` should be responsible only for **authentication and identity** (username, email, password hash). Mixing application-specific analytics columns into it violates separation of concerns and bloats the identity table.

**`UserStats`** is a dedicated one-to-one vertical partition with `UserId` as both PK and FK.

```sql
CREATE TABLE UserStats (
    UserID                          INT PRIMARY KEY,
    
    -- Content Creation
    StoryCount                      INT NOT NULL DEFAULT 0,
    ChapterCount                    INT NOT NULL DEFAULT 0,
    TotalWordCount                  BIGINT NOT NULL DEFAULT 0,
    RecommendationCount             INT NOT NULL DEFAULT 0,
    CommentCount                    INT NOT NULL DEFAULT 0,
    
    -- Community Interaction
    CommentLikesReceivedCount       INT NOT NULL DEFAULT 0,
    RecommendationLikesReceivedCount INT NOT NULL DEFAULT 0,
    FollowerCount                   INT NOT NULL DEFAULT 0,
    StoriesFavoritedByOthers        INT NOT NULL DEFAULT 0,
    SpotlightCount                  INT NOT NULL DEFAULT 0,
    
    -- Badge-specific metrics
    AcknowledgedBetaCount           INT NOT NULL DEFAULT 0,
    AcknowledgedInspirationsCount   INT NOT NULL DEFAULT 0,
    FeatureContributorCount         INT NOT NULL DEFAULT 0,
    HelpfulReportCount              INT NOT NULL DEFAULT 0,

    CONSTRAINT FK_UserStats_AspNetUsers FOREIGN KEY (UserID)
        REFERENCES AspNetUsers(Id) ON DELETE CASCADE
);
```

### Why Fixed Columns, Not a Key-Value Table

The flexible alternative — a key-value `UserStatsFlexible` table with `(UserID, StatName, StatValue)` rows — was explicitly considered and rejected:

**Key-value table problems:**
- **Performance:** To get a user's stats, you can't `SELECT` one row — you must pivot many rows, which is slow and complex.
- **No type safety:** `StatValue` must be a `string` or `sql_variant`; requires parsing back to `int` everywhere.
- **Complex C# models:** The stats class becomes `List<UserStatEntry>` instead of a simple POCO.

**Fixed columns conclusion:** The minor cost of running a migration when adding a new stat is a tiny price for massive gains in performance, simplicity, and type safety. Schema changes for new stats are a normal developer task tracked via EF Core migrations.

---

## 5. BadgeKey Type: String vs. Int

**Source:** Entry #1328, #1325

### The Question

Should `BadgeKey` (the primary key of `Badges`) be an `INT`/`TINYINT` (like `NotificationTypeID`) or a human-readable `NVARCHAR(50)` string?

### Decision: Keep as NVARCHAR(50) string

**Contrast with `NotificationType`:** The `NotificationTypes` lookup table was refactored to use a `TINYINT` identity key because the `Notifications` table is one of the largest and most-written-to tables in the database — potentially growing to billions of rows. Storing a 50-byte `NVARCHAR` string billions of times wastes gigabytes of storage and slows every query and join.

**`Badges` and `UserBadges` are different:**
- `Badges` is tiny (50–100 rows maximum).
- `UserBadges` is much smaller and much less "hot" than `Notifications`.
- The performance cost of `NVARCHAR(50)` vs. `TINYINT` is negligible at this scale.

**The benefit of the string key:** Your C# code is tightly coupled to the logical name of the badge. The background worker will contain code like `AwardBadge(id, "beta-reader")`. A string key makes this instantly readable, auditable, and self-documenting.

**Summary:** NotificationType = optimize for storage/speed (use TINYINT). BadgeKey = optimize for C# readability/maintainability (use string). The tables are different orders of magnitude in size.

---

## 6. Naming Convention: BadgeID → BadgeKey

**Source:** Entry #1324

### The Decision

`BadgeID` was renamed to `BadgeKey` (and `SearchModeID` → `SearchModeKey`, `CriterionID` → `FilterKey` in the same pass).

### Rationale

The `...ID` suffix strongly implies an `INT` or `TINYINT` numeric identity. The `...Key` suffix (consistent with `NotificationKey` established earlier) strongly implies a logical `NVARCHAR` string.

This small naming change makes the schema **self-documenting**: a developer reading the schema immediately knows the data type and intent of the column without inspecting the DDL. It reduces the chance of mistakenly treating `BadgeKey` as a numeric identity in C# code.

**Convention established:**
- `...ID` → numeric integer primary key (auto-increment or TINYINT lookup)
- `...Key` → logical string identifier used in C# application code

---

## 7. Why Strings Instead of Enums

**Source:** Entry #807

### Two Categories of Lookup Tables

**Category 1 — Static, code-driven (enum)**

Tables like `StoryStatus`. The application logic *must* understand these values at compile time (`if (story.StoryStatusId == StoryStatusEnum.InProgress) { ... }`). Adding a new status requires a code change, recompile, and migration.

**Category 2 — Dynamic, data-driven (string key)**

Tables like `Badges`. The application code is "dumb" with respect to which specific badges exist — it just inserts rows into `UserBadges` using string keys. New badges can be added by inserting a row into the `Badges` table in production without redeploying the application. The Blazor UI's badge display automatically picks up new entries.

**Badges are Category 2:** The worker's award conditions (`if (stats.CommentCount == 100) AwardBadge("hundred-comments")`) do reference specific badge keys, but these are not `switch` statements branching on every possible badge — they're discrete milestone checks. The distinction is that the *set* of badges is admin-extensible without a code change.

**The string key (`BadgeKey`) is the bridge** between the data-driven lookup table and the application code. It is a stable, human-readable identifier that never changes, analogous to `NotificationKey` in the `NotificationTypes` table.

---

## 8. String Constants (SiteBadges Class)

**Source:** Entry #808

### The Problem: Magic Strings

Without constants, badge keys appear as literal strings scattered across the codebase:

```csharp
// BAD — typo "beta-readr" compiles fine but fails silently at runtime
_badgeService.AwardBadge(userId, "beta-readr");

// BAD — renaming requires find-and-replace across all files
modelBuilder.Entity<Badge>().HasData(
    new Badge { BadgeKey = "first-story", ... }
);
```

### The Solution: Static Constants Class

```csharp
// In Data/SiteConstants.cs
namespace TheCanalaveLibrary.Data;

public static class SiteBadges
{
    public const string FirstStory     = "first-story";
    public const string BetaReader     = "beta-reader";
    public const string LegendaryAuthor = "legendary-author";
    // Add more as new badges are defined...
}
```

**Usage:**
```csharp
// Good — compile-time safety, IntelliSense, refactor-safe
_badgeService.AwardBadge(userId, SiteBadges.BetaReader);

modelBuilder.Entity<Badge>().HasData(
    new Badge { BadgeKey = SiteBadges.FirstStory, ... }
);
```

**Benefits:**
- No typos — compiler catches errors.
- Renaming a key requires changing only one constant.
- Code is more readable without requiring knowledge of the database values.

**Important:** Constants must be `public const string`, **not** `public static readonly string`. If the value is computed at runtime (e.g., `Guid.NewGuid().ToString()`), EF Core's `HasData` comparison will detect the value changing on every build and throw a `PendingModelChangesWarning`. A `const` is baked in at compile time and is always stable.

---

## 9. Composite PK Order for UserBadges

**Source:** Entry #1220

**Decision:** `PRIMARY KEY (UserID, BadgeKey)` — `UserID` first.

**Rationale:** The composite PK creates a B-Tree index that can be seeked using the leading column. The most common query is "get all badges for this user" (`WHERE UserID = ?`), so `UserID` must be the leading column to use the index efficiently.

**Secondary index:** If the reverse query is ever needed ("find all users who have this badge," e.g., for an admin dashboard), a separate index on `(BadgeKey)` would be added at that time. This is noted as a future consideration, not part of the initial design.

---

## 10. SQL Schema (Final)

**Source:** Entry #1219, #1220, #1324

After all renaming and constraint cleanup passes, the final SQL definition:

```sql
-- BADGES (master list, no associated C# enum)
CREATE TABLE Badges (
    BadgeKey     NVARCHAR(50)  NOT NULL,
    DisplayName  NVARCHAR(100) NOT NULL,
    Description  NVARCHAR(500) NULL,
    IconBaseUrl  NVARCHAR(500) NOT NULL,
    SortOrder    INT           NOT NULL,
    CONSTRAINT PK_Badges       PRIMARY KEY (BadgeKey),
    CONSTRAINT DF_Badges_SortOrder DEFAULT 0
);

CREATE TABLE UserBadges (
    UserID       INT          NOT NULL,
    BadgeKey     NVARCHAR(50) NOT NULL,
    DateEarned   DATETIME2(7) NOT NULL,
    DisplayOrder INT          NOT NULL,
    CONSTRAINT PK_UserBadges            PRIMARY KEY (UserID, BadgeKey),
    CONSTRAINT FK_UserBadges_AspNetUsers FOREIGN KEY (UserID)
        REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserBadges_Badges     FOREIGN KEY (BadgeKey)
        REFERENCES Badges(BadgeKey) ON DELETE CASCADE,
    CONSTRAINT DF_UserBadges_DateEarned  DEFAULT GETUTCDATE(),
    CONSTRAINT DF_UserBadges_DisplayOrder DEFAULT 0
);
```

Constraint naming follows the site-wide convention: `PK_TableName`, `FK_SourceTable_TargetTable`, `DF_TableName_ColumnName`.

---

## 11. EF Core Seed Data

**Source:** Entry #810, #777

Badges live in **Section 2** of the `HasData` seed blocks — lookup tables with no associated C# enum (contrast with `StoryStatus`, `NotificationCategory`, etc. in Section 1 which all have enum mirrors).

Example seed:
```csharp
modelBuilder.Entity<Badge>().HasData(
    new Badge {
        BadgeKey    = SiteBadges.BetaReader,
        DisplayName = "Beta Reader",
        Description = "Acknowledged as a Beta Reader on a story.",
        IconBaseUrl = "icons/badges/beta.png",
        SortOrder   = 10
    },
    new Badge {
        BadgeKey    = SiteBadges.FirstStory,
        DisplayName = "First Story",
        Description = "Posted your first story.",
        IconBaseUrl = "icons/badges/first_story.png",
        SortOrder   = 1
    },
    new Badge {
        BadgeKey    = SiteBadges.WordCount100k,
        DisplayName = "Prolific Author",
        Description = "Wrote over 100,000 words.",
        IconBaseUrl = "icons/badges/100k.png",
        SortOrder   = 5
    }
    // ...
);
```

Only example/starter badges are seeded here. The full catalogue can be populated via admin tooling at any time without a migration.

---

## 12. Delete Policies

**Source:** Entry #948, #943

| Relationship | Policy | Reason |
|---|---|---|
| User → UserBadge | **Cascade** | When a user is deleted, their earned badges are meaningless and should be removed with them. |
| Badge → UserBadge | **Cascade** (in SQL), **Restrict** in EF Core spirit | Badges are not deleted in normal operation. If a Badge row were ever deleted, all user earnings of it would cascade-delete. In practice, badges are never deleted — they are retired by removing them from the UI rather than deleting the row. |
| Badge (directly) | **Restrict** | You cannot delete a `Badge` row while any `UserBadge` entry references it. This acts as a safeguard against accidentally removing a badge definition that users have earned. |

The practical policy: badge rows in the `Badges` table are **never deleted in production**. If a badge is retired, it is simply not displayed in the UI. Earned instances in `UserBadges` remain as historical records.

---

## 13. UI Components & Service Layer

**Source:** Entry #1574, #1577

**`BadgeDisplay.razor`** — A small Razor component that renders a user's badge bar. Injected wherever a user's identity appears: profile pages, comment headers, story author headers.

**`IUserService`** — Handles user profiles, settings, and badge counts (reading from `UserStats`). Badge display data (from `UserBadges`) is loaded separately.

**`BadgeService` (implied)** — The service called by the background worker to check earned thresholds and insert rows into `UserBadges`. Called as `_badgeService.AwardBadge(userId, SiteBadges.BetaReader)`.

---

*Source transcript: `MyActivity September to November 2025_filtered.md`*
*Companion documents: `Production_Setup_Deliberations.md`, `Moderation_And_Reporting_Deliberations.md`*
