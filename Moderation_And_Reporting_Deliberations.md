# Moderation & Reporting Deliberations
### Canalave Library — Design Session Notes

This document records all deliberations regarding the moderation and reporting system for the Canalave Library fanfiction site, including superseded designs and the reasoning behind each decision. Entries are drawn from the design session transcript (September–November 2025).

---

## Table of Contents

1. [High-Level Feature Specification](#1-high-level-feature-specification)
2. [Reports Table Design](#2-reports-table-design)
3. [Polymorphic vs. TPT for Reports](#3-polymorphic-vs-tpt-for-reports)
4. [Orphaned Reports Problem](#4-orphaned-reports-problem)
5. [Report Threshold & Auto-Hiding Policy](#5-report-threshold--auto-hiding-policy)
6. [Story Approval Workflow](#6-story-approval-workflow)
7. [Moderator Role System (ASP.NET Identity)](#7-moderator-role-system-aspnet-identity)
8. [Moderation Dashboard Design](#8-moderation-dashboard-design)
9. [Banned User Cascade Behavior](#9-banned-user-cascade-behavior)
10. [EF Core Delete Policies for Reports](#10-ef-core-delete-policies-for-reports)
11. [DefaultCommentModeration User Setting](#11-defaultcommentmoderation-user-setting)
12. [Notification System for Moderation Events](#12-notification-system-for-moderation-events)
13. [Moderation Transparency Philosophy](#13-moderation-transparency-philosophy)

---

## 1. High-Level Feature Specification

**Source:** Entry #1578 / #1577

The top-level feature specification (Section 8 — Moderation & User Settings, and Section X — Moderation & Safety) captures the agreed scope of the moderation system:

**Story Approval Queue**
- Stories require moderator approval before publication.
- Moderators see a queue of `PendingApproval` stories and can Approve or Reject each one.

**Reporting System**
- A polymorphic `Reports` table handles all user-submitted reports, regardless of the type of entity being reported.
- Content tables carry an `ActiveReportCount` denormalized column so threshold-based auto-hiding can be evaluated without a subquery.
- Report threshold policy: content is NOT auto-hidden on the first report. Auto-hide (to `UnderReview` status) triggers at **3+ reports from different users within 24 hours**.

**Moderator Interface**
- `ModerationQueuePage.razor` — unified queue showing all open reports and pending stories.
- `TagWranglingPage.razor` — tag management tools for moderators.
- `UserManagementPage.razor` — ban, warn, shadowban, and role tools.

**Service Layer**
- All moderation operations are exposed through an `IModerationService` interface.

**Content Filtering**
- SFW / M (Mature) content filtering is a site-wide concern enforced at the query level, not at the moderation queue level.

---

## 2. Reports Table Design

### 2a. Field Naming

**Source:** Entry #1470

The initial scaffolded schema used verbose field names. After deliberation, the following names were standardized:

| Field | Decision |
|---|---|
| `ReportedEntityType` | Keep — necessary for polymorphic routing |
| `ReportedEntityID` | Keep — the target entity's PK |
| `ReporterUserID` | Keep — explicit and unambiguous |
| `DateReported` | Keep |
| `ReportReasonLongDescription` | Shorten to `Reason` |
| `ModeratorNotes` | Shorten to `Notes` |
| `ReportStatusID` | Shorten to `Status` (or keep as `ReportStatusID` per Hybrid pattern — see §2b) |

The rationale: shorter field names reduce query verbosity without sacrificing clarity.

### 2b. Design Pattern for Each Reports Field

**Source:** Entry #1128

Three patterns were established for lookup/enum-like fields across the schema. For the `Reports` table specifically:

| Field | Pattern | Rationale |
|---|---|---|
| `ReportedEntityTypeID` | **Pure Magic Byte / Enum** | C# `switch` *must* know: 1=User, 2=Story, 3=Comment, etc. to route to the correct entity. No lookup table needed — logic is entirely in code. |
| `ReportReasonID` | **Content-Only Lookup Table** | The C# application only saves the user-selected ID from a dropdown. The reasons themselves are admin-editable content. No C# logic branches on which reason was selected. |
| `ReportStatusID` | **Hybrid (Lookup Table + C# Enum)** | Needs a lookup table for flexibility (e.g., to add a new `Escalated` status without a migration) AND needs a C# enum for rigid `WHERE` clause logic (e.g., `WHERE status = 'Open'` must be hardcoded). |

---

## 3. Polymorphic vs. TPT for Reports

### 3a. First Attempt: TPT Rejected

**Source:** Entry #1461 / #1460

The initial question was whether to use Table-Per-Type (TPT) inheritance for the `Reports` table, creating separate `StoryReport`, `CommentReport`, and `UserReport` child tables that inherit from a `BaseReport` parent — mirroring the approach taken for `BaseComments`.

**Decision: TPT rejected for Reports.**

The primary use case for the Reports table is the **moderator queue**: retrieving ALL open reports regardless of what entity type is being reported. With TPT, this primary query becomes:

```csharp
// COMPLEX — what TPT would require:
var storyReports = _context.StoryReports.Select(r => ...);
var commentReports = _context.CommentReports.Select(r => ...);
var userReports = _context.UserReports.Select(r => ...);
var allReports = storyReports.Concat(commentReports).Concat(userReports)
    .OrderByDescending(r => r.DateReported).ToList();
```

With a single polymorphic table, the same query is:

```csharp
// SIMPLE — polymorphic model:
var reports = await _context.Reports.Where(r => r.Status == "Open").ToListAsync();
```

TPT is optimized for the inverse use case — when you primarily query a single derived type (e.g., "show me only chapter comments"). Because the moderator queue must show everything together, polymorphic wins.

### 3b. Final Decision: Keep Reports Polymorphic; Split Comments into TPT

**Source:** Entry #1467 / #1466

The structural question was generalized: which of the three major "mixed entity" tables (Reports, Notifications, Comments) should use polymorphic single-table design vs. TPT?

**The deciding criterion: what is the primary query for each table?**

| Table | Primary Query | Decision |
|---|---|---|
| `Reports` | All reports together (moderator queue) | **Polymorphic** |
| `Notifications` | All notifications together (user notification feed) | **Polymorphic** |
| `Comments` | Comments on a specific chapter, profile, or group (not mixed) | **TPT** |

For `Comments`, the primary query is always scoped to a single entity type (e.g., "get comments for Chapter 12"). TPT allows `ChapterComment`, `UserProfileComment`, and `GroupComment` to have NOT NULL foreign keys (e.g., `ChapterId NOT NULL`), which is the primary benefit of TPT: **data integrity**. A single polymorphic comments table would require all entity FKs to be nullable with no DB-level enforcement.

For `Reports` and `Notifications`, the unified view is the primary use case and the entity-type column is sufficient routing information.

---

## 4. Orphaned Reports Problem

### 4a. No DB-Level FK Enforcement

**Source:** Entry #1469

Because the `Reports` table is polymorphic, the `ReportedEntityID` column cannot have a database-level foreign key constraint. The database cannot enforce referential integrity on a column that could reference any of several tables.

**Consequence:** The application is 100% responsible for orphan cleanup. If a story, comment, or user is deleted and the reports referencing it are not cleaned up first, those report rows become orphans pointing at non-existent entities.

### 4b. Orphan Links Are a UX Problem

**Source:** Entry #1459

Even if moderators manually clean up reports before deleting content, orphaned reports in the queue would produce 404 errors when a moderator clicks through to review the reported content. The app must handle this at the service layer.

**Resolution:** Delete reports targeting an entity before (or as part of) deleting the entity itself. Example:

```csharp
public async Task DeleteStoryAsync(int storyId)
{
    var reports = await _context.Reports
        .Where(r => r.ReportedEntityType == "Story" && r.ReportedEntityID == storyId)
        .ToListAsync();
    if (reports.Any()) _context.Reports.RemoveRange(reports);
    _context.Stories.Remove(story);
    await _context.SaveChangesAsync();
}
```

This pattern must be applied for every entity type that can be reported (stories, chapters, comments, users).

---

## 5. Report Threshold & Auto-Hiding Policy

**Source:** Entry #1578 / #1577

**Policy (as specified):**
- Content is **not** auto-hidden on the first report (avoids spam/abuse by malicious reporters).
- Content is automatically placed into `UnderReview` status when it receives **3 or more reports from different users within a 24-hour window**.
- `ActiveReportCount` is a denormalized integer column on each content table (Stories, Comments, etc.) that is incremented when a report is filed and decremented when a report is resolved or retracted.
- The threshold check reads `ActiveReportCount` to avoid a subquery — a single row read determines whether to trigger auto-hiding.

**`UnderReview` status:** This is a value on the `ReportStatusEnum` that indicates the content is hidden from public view pending moderator review.

---

## 6. Story Approval Workflow

### 6a. The Problem: Post-Approval Status

**Source:** Entry #1133 / #1132

Stories flow through a status lifecycle: `Draft` → `PendingApproval` → `Approved` (or `Rejected`).

The problem identified: when a moderator clicks "Approve," the system needs to know which status to assign the story. The author may intend different outcomes — they might want the story to be immediately `Published`, or to remain `Draft` (in case they want to do more editing), or to become `UnlistedPublished`. Without storing this intent at submission time, the moderator has no way to know what to set.

**Superseded approach:** An earlier `PostApprovalStatus` field was considered but removed at some point.

**Final decision:** Reinstate the intent-storage field as `RequestedStatusId` — a **nullable** integer on the `Stories` table that stores the author's intended post-approval status at the time of submission.

### 6b. Approval Workflow with `RequestedStatusId`

**Source:** Entry #1132

The complete approval workflow:

1. Author submits story: `StoryStatusId = PendingApproval`, `RequestedStatusId = <author's desired final status>`.
2. Moderator views queue. Story shows as pending.
3. **On Approve:** Set `StoryStatusId = RequestedStatusId.Value`, clear `RequestedStatusId = null`, set `PublishedDate = DateTime.UtcNow`.
4. **On Reject:** Set `StoryStatusId = Rejected`, optionally set `RequestedStatusId = null`.

```csharp
public async Task ApproveStory(int storyId)
{
    var story = await _context.Stories.FindAsync(storyId);
    if (story == null || story.RequestedStatusId == null)
        throw new InvalidOperationException("This story has no requested status and cannot be approved.");
    story.StoryStatusId = story.RequestedStatusId.Value;
    story.RequestedStatusId = null;
    story.PublishedDate = DateTime.UtcNow;
    await _context.SaveChangesAsync();
}
```

The null guard prevents approving a story that was submitted without specifying an intended status (a data integrity safeguard).

### 6c. `StoryStatus` as a Hybrid Enum

**Source:** Entry #1133

`StoryStatus` uses the Hybrid pattern: a lookup table for flexibility plus a C# enum for type-safe branching. The approval/rejection logic must branch on status values, so a pure lookup table would not be sufficient.

---

## 7. Moderator Role System (ASP.NET Identity)

**Source:** Entry #1276

Moderator assignment uses ASP.NET Core Identity's built-in role system (`RoleManager<ApplicationRole>`, `UserManager<User>`).

### 7a. Role Seeding at Startup

Roles are seeded in `Program.cs`:

```csharp
string[] roleNames = { "Admin", "Moderator", "Author" };
foreach (var roleName in roleNames)
{
    if (!await roleManager.RoleExistsAsync(roleName))
        await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
}
```

### 7b. Assigning Moderator Role

```csharp
public async Task<bool> MakeUserAModerator(int userIdToPromote)
{
    var user = await _userManager.FindByIdAsync(userIdToPromote.ToString());
    if (user == null) return false;
    var result = await _userManager.AddToRoleAsync(user, "Moderator");
    return result.Succeeded;
}
```

### 7c. UI Gating

Blazor UI elements that should only be visible to moderators and admins use `<AuthorizeView>`:

```razor
<AuthorizeView Roles="Moderator, Admin">
    <button class="btn btn-danger">Delete This Comment</button>
</AuthorizeView>
```

### 7d. Roles Used

| Role | Purpose |
|---|---|
| `Admin` | Full site access, including role management |
| `Moderator` | Access to moderation queue, ban tools, story approval |
| `Author` | Elevated trust for story submission (if needed) |

Roles are stored in ASP.NET Identity's standard `AspNetRoles` and `AspNetUserRoles` tables.

---

## 8. Moderation Dashboard Design

**Source:** Entry #1162

The moderator dashboard consolidates all moderation tools:

### 8a. Queue Sections

- **Pending Reports** — Open reports grouped by entity type, showing reporter, entity, reason, and date.
- **Pending Stories** — Stories in `PendingApproval` status, with Approve and Reject actions.

### 8b. User Management Tools

User account actions are driven by an `AccountStatus` column on `AspNetUsers`, typed as `TINYINT`:

| Value | Status | Effect |
|---|---|---|
| 0 | Active | Normal access |
| 1 | Warned | Visible warning shown to user; no access restriction |
| 2 | Suspended | Temporary login block |
| 3 | Banned | Permanent login block; content treatment TBD |
| 4 | Shadowbanned | User can log in and post but content is hidden from others |

The dashboard exposes tools to:
- **Warn** a user (sets `AccountStatus = 1`)
- **Suspend** a user (sets `AccountStatus = 2`, with duration)
- **Ban** a user (sets `AccountStatus = 3`)
- **Shadowban** a user (sets `AccountStatus = 4`)

---

## 9. Banned User Cascade Behavior

**Source:** Entry #1422

When a user is banned (or deleted), their `UserLists` are cascade-deleted via `ON DELETE CASCADE` on the `UserLists` table.

**Problem:** Each `UserList` entry that gets deleted should decrement the `FavoriteCount` on the associated story. `ON DELETE CASCADE` runs at the database level, bypassing the EF Core change tracker, so no C# code is automatically invoked.

**Solution:** A database trigger on the `UserLists` table fires on `DELETE` and decrements the corresponding `FavoriteCount`. This is one of the few places where a SQL trigger is appropriate — it's the only way to maintain the denormalized counter when deletes bypass the application layer.

---

## 10. EF Core Delete Policies for Reports

### 10a. Scaffolded FK Behavior

**Source:** Entry #1058

The scaffolded `OnModelCreating` for `Report` uses a mix of `SetNull` and `ClientSetNull`:

```csharp
entity.HasOne(d => d.ReporterUser).WithMany(p => p.ReportReporterUsers)
    .HasForeignKey(d => d.ReporterUserId)
    .OnDelete(DeleteBehavior.SetNull);  // ReporterUserId is nullable

entity.HasOne(d => d.ReportReason).WithMany(p => p.Reports)
    .HasForeignKey(d => d.ReportReasonId)
    .OnDelete(DeleteBehavior.ClientSetNull);  // ReportReasonId is non-nullable

entity.HasOne(d => d.ReportStatus).WithMany(p => p.Reports)
    .HasForeignKey(d => d.ReportStatusId)
    .OnDelete(DeleteBehavior.ClientSetNull);  // ReportStatusId is non-nullable
```

The distinction: `SetNull` creates a true `ON DELETE SET NULL` database constraint (database does the work). `ClientSetNull` creates no database constraint — EF Core handles it in-memory only, which fails if the related entities aren't loaded.

### 10b. Intended Policy (First Principles)

**Source:** Entry #948

The deliberate intent for all foreign keys on `Reports`:

| FK | Policy | Reason |
|---|---|---|
| `ReporterUserId` | **SetNull** | Keep the report for records even if the reporter account is deleted. |
| `ModeratorUserId` | **SetNull** | Keep the report for records even if the moderator account is deleted. |
| `ReportReasonId` | **ClientSetNull** / Restrict | Lookup value — should not be deletable while reports reference it. |
| `ReportStatusId` | **ClientSetNull** / Restrict | Lookup value — same protection. |

The design principle: reports are **site moderation records** and should survive the deletion of any involved user. A report whose reporter no longer exists is still a valid record of what happened and when.

### 10c. Report as a Multiple-Cascade-Path Diamond

**Source:** Entry #939

The `Reports` table creates a "direct A to B" diamond: a single `User` deletion can reach `Report` via two paths simultaneously (`Report.ReporterUserId` and `Report.ModeratorUserId`). Setting both to `SetNull` (rather than `Cascade`) resolves the ambiguity — neither path attempts to delete the `Report` row, they just null out the FKs.

---

## 11. DefaultCommentModeration User Setting

**Source:** Entry #870

Authors can set a default comment moderation policy for their own stories. This is stored as part of the `AuthorSettings` JSON blob on the `User` table (using `OwnsOne(...).ToJson()`).

```csharp
public enum DefaultCommentModeration : short
{
    None = 0,     // All comments appear immediately
    HoldAll = 1   // All comments held for author approval before appearing
}
```

This is stored as a short (smallint) inside the JSON:

```csharp
entity.OwnsOne(u => u.AuthorSettings, settings =>
{
    settings.ToJson();
    settings.Property(s => s.DefaultCommentModeration).HasConversion<short>();
    settings.Property(s => s.DefaultStoryRating).HasConversion<short>();
});
```

The full `AuthorSettings` class also contains `AllowGuestComments` (bool) and `AllowStoryRecommendations` (bool). Keeping these settings in a JSON blob avoids extra columns and JOINs while the `User` table remains fast for the hot read path.

---

## 12. Notification System for Moderation Events

### 12a. Moderation-Related Notification Types

**Source:** Entry #1115

The `NotificationTypeEnum` (which mirrors the `NotificationTypes` lookup table) was extended to include moderation-specific events:

```csharp
// Moderation & Reports
StoryStatusChanged,    // "Your story was approved/rejected by a moderator"
ReportStatusChanged,   // "Your report on [X] has been resolved"
ModeratorWarning,      // "You have received an official warning from a moderator"
AccountBanned,         // System-level: account permanently banned
AccountSuspended,      // System-level: account temporarily suspended
```

`StoryStatusChanged` covers both approval and rejection outcomes (one enum value, two possible messages), so the display text is data-driven from the notification payload rather than hardcoded per-case.

### 12b. Notification Category for Moderation Events

**Source:** Entry #1112, #1111

Notification categories are stored in a `NotificationCategories` lookup table. The UI settings page groups notifications by category using a data-driven loop, not a hardcoded list.

**Naming deliberation for the moderation category:**

| Candidate Name | Decision |
|---|---|
| "Account & Moderation" | Considered — too clunky |
| "Moderation & Safety" | **Recommended as top option** |
| "Site & Account Alerts" | Also acceptable |

**Rejected idea: split into positive and negative categories.** The suggestion to separate "positive" moderation outcomes (report approved) from "negative" ones (warnings, bans) was explicitly rejected. Best practice (used by banks, Google, etc.) is a single unified category for all administrative/safety alerts. This trains users that messages in this category are important and must be read. Splitting creates UI clutter without benefit.

**`StoryStatusChanged` placement:** Moved OUT of the moderation category and INTO the "Your Stories" category. From the author's perspective, a story being approved is a **content milestone**, not a "moderation event." It belongs alongside new favorites and new comments.

### 12c. Notification Category Names (Final Set)

**Source:** Entry #1112, #1110, #1111

| Category Name | Contains |
|---|---|
| Site News | `SiteAnnouncement` |
| Your Follows | `NewChapterOnFollowedStory`, `NewStoryByFollowedUser`, `NewBlogPostByFollowedUser`, etc. |
| Your Stories | `NewStoryFavorite`, `NewStoryComment`, `NewReview`, `StoryStatusChanged` (approval/rejection) |
| Your Profile | `NewFollowerOnYou`, `NewCommentOnYourProfile`, `NewVouchOnYou` |
| Your Contributions | `RecommendationApproved`, `RecommendationSpotlighted`, `TagUpdateSuggestion` |
| Collaboration | `StoryRelationshipRequested`, `StoryRelationshipApproved`, `NewStoryAcknowledgement` |
| Group Updates | `NewGroupStory`, `NewGroupBlogPost`, `GroupInvite` |
| **Moderation & Safety** | `ReportStatusChanged`, `ModeratorWarning`, `AccountBanned`, `AccountSuspended` |

Note on "Your Follows" name: "Subscriptions" was explicitly rejected because it carries a monetary connotation. "Your Follows" was chosen to match the site's follow-based social model.

Note on "Your Profile" name: "Interactions" was rejected as ambiguous (unclear whether it refers to content interactions or user interactions). "Social" was rejected as too vague. "Your Profile" was chosen as the clearest parallel to "Your Stories."

---

## 13. Moderation Transparency Philosophy

**Source:** Entry #1111

This is the explicit design philosophy driving the notification decisions above.

**The problem with FFN (Fanfiction.Net):** The "report to a black hole" model — where users submit reports and never hear back — destroys community trust. Users don't know if their report was seen, if the moderator reviewed it, or if the system is even functioning.

**The principle:** All reports, regardless of outcome, should produce a closing notification to the reporter.

| Outcome | Notification? | Reason |
|---|---|---|
| Report → Action Taken | **Yes** | User feels validated; system is working |
| Report → No Action Taken | **Yes** | User gets closure; moderator *did* review it; prevents re-reporting and forum complaints |

**Quote from deliberation:** "This single feature — closing the loop on all reports, regardless of outcome — is the 'best practice' that builds a healthy, trusting community."

**`Resolved-NoActionTaken` notification:** This was explicitly deliberated and confirmed: YES, users should be notified even when no action is taken. The user is not expected to agree with the outcome, but they are entitled to know they were heard and that the report was genuinely reviewed. This prevents the site from feeling like moderation is broken or unresponsive.

**Notification for `StoryRelationshipRejected` (related principle):** The same closure principle applies to story relationship requests. When User A requests to link their story to User B's story and User B rejects it, User A should receive a notification. Without it, the request status is ambiguous from User A's perspective — they cannot tell if User B saw it, ignored it, or if the system failed. Closure, even from a negative outcome, is a better UX than silence.

---

*Source transcript: `MyActivity September to November 2025_filtered.md`*
*Companion document: `Production_Setup_Deliberations.md`*
