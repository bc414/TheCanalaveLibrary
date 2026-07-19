/*
================================================================================
 FanFiction Website Database Schema (MODIFIED FOR ASP.NET CORE IDENTITY)
 (Version 3 - INT/BIGINT PK Optimization)

-- This script applies the following PK optimizations:
-- 1. AspNetUsers.Id is assumed to be INT.
-- 2. Stories.StoryID and other entity tables (Tags, Groups, etc.) remain INT
--    for fast foreign key joins (e.g., in UserStoryInteractions).
-- 3. Conversations.ConversationID remains INT as 2.1B conversations is a high
--    limit and the performance gain on PrivateMessages FK is high.
-- 4. Chapters.ChapterID and related character/relationship tables remain INT.
-- 5. High-volume "event" tables are moved to BIGINT for safety:
--    - BaseComments.CommentID (and all FKs pointing to it)
--    - Notifications.NotificationID
--    - PrivateMessages.MessageID
--    - Reports.ReportID
--    - ChapterContents.ChapterContentID (and its FK)
================================================================================
*/

-- Create the database (optional, run this part only if needed)
-- CREATE DATABASE FanFictionDB;
-- GO
-- USE FanFictionDB;
-- GO

/*
================================================================================
 Phase 1: Create Core Tables (UGC AuthorID columns set to NULLABLE)
================================================================================
*/

/* ================================================================================
-- 1. STORY STATUS LOOKUP TABLE
-- This table is created first. It replaces the 'magic string' / CHECK constraint.
-- It's the "source of truth" for what statuses exist.
================================================================================
*/
DROP TABLE IF EXISTS StoryStatuses;
GO

CREATE TABLE StoryStatuses (
                               StoryStatusID TINYINT IDENTITY(1,1) PRIMARY KEY,
                               StatusName NVARCHAR(50) NOT NULL,
                               Description NVARCHAR(255) NULL
);
GO

/* -- Populate the lookup table with your 9 defined statuses.
-- We set IDENTITY_INSERT ON to ensure the IDs are exactly as we define them,
-- which makes the default 'Draft' = 1 predictable.
*/
SET IDENTITY_INSERT StoryStatuses ON;
GO

INSERT INTO StoryStatuses (StoryStatusID, StatusName, Description)
VALUES
    (1, 'Draft', 'Story is a work in progress and not visible to the public.'),
    (2, 'PendingApproval', 'Story has been submitted and is awaiting moderator approval.'),
    (3, 'In Progress', 'Story is approved, public, and actively being updated.'),
    (4, 'Completed', 'The story is finished.'),
    (5, 'On Hiatus', 'The author is taking a break from updating.'),
    (6, 'Cancelled', 'The story will not be continued.'),
    (7, 'Rewriting', 'The story is undergoing major revisions.'),
    (8, 'Open Beta', 'Story is visible to beta readers for feedback.'),
    (9, 'Rejected', 'Story was submitted but did not pass moderation.');
GO

SET IDENTITY_INSERT StoryStatuses OFF;
GO

/* ================================================================================
-- 2. STORIES TABLE (INT PK)
-- This is your original table, now updated with the Slug and StoryStatusID
================================================================================
*/
DROP TABLE IF EXISTS Stories;
GO

CREATE TABLE Stories (
                         StoryID INT IDENTITY(1,1) PRIMARY KEY, -- OPTIMIZATION: INT is 4 bytes, faster FKs
                         AuthorID INT NULL, -- Assumes AspNetUsers.Id is INT
                         StoryTitle NVARCHAR(255) NOT NULL,

    /* -- NEW: Slug for human-readable URLs. Nullable to allow drafts to not have one. -- */
                         Slug NVARCHAR(255) NULL,

                         ShortDescription NVARCHAR(500) NULL,
                         LongDescription NVARCHAR(MAX) NULL,
                         Rating TINYINT NOT NULL DEFAULT 1,

    /* -- REPLACED: The old 'Status' string column is gone. -- */
    /* -- NEW: This TINYINT is a foreign key to the StoryStatuses table. -- */
    /* -- The default of 1 corresponds to 'Draft' from the insert script above. -- */
                         StoryStatusID TINYINT NOT NULL DEFAULT 1,

                         PostApprovalStatus NVARCHAR(30) NULL,
                         WordCount INT NOT NULL DEFAULT 0,
                         ViewCount INT NOT NULL DEFAULT 0,
                         CoverArtURL NVARCHAR(500) NULL,

                         PublishedDate DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                         LastUpdatedDate DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                         OriginalPublishedDate DATE NULL,
                         OriginalLastUpdatedDate DATE NULL,
                         ActiveReportCount INT NOT NULL DEFAULT 0,

    /* -- REMOVED: The old CHECK constraint for Status is no longer needed. -- */

    /* -- NEW: Foreign key constraint to ensure data integrity for the status. -- */
                         CONSTRAINT FK_Stories_StoryStatus FOREIGN KEY (StoryStatusID) REFERENCES StoryStatuses(StoryStatusID)
);
GO

/* ================================================================================
-- 3. SLUG UNIQUE INDEX
-- This is created *after* the table.
-- It provides the unique constraint AND makes slug lookups extremely fast.
-- The "WHERE Slug IS NOT NULL" clause is a filter that allows
-- multiple rows to have a NULL slug (e.g., drafts) without violating the constraint.
================================================================================
*/
CREATE UNIQUE NONCLUSTERED INDEX IX_Stories_Slug
    ON Stories(Slug)
    WHERE Slug IS NOT NULL;
GO


-- Table for chapter metadata (container)
DROP TABLE IF EXISTS Chapters;
GO

CREATE TABLE Chapters (
                          ChapterID INT IDENTITY(1,1) PRIMARY KEY, -- OPTIMIZATION: INT provides FK gains on interaction tables
                          StoryID INT NOT NULL,
                          ChapterNumber INT NOT NULL,
                          Title NVARCHAR(255) NULL,
                          PrimaryContentID BIGINT NOT NULL, -- CHANGED: Must be BIGINT to match ChapterContents
                          IsPublished BIT NOT NULL DEFAULT 0,
                          CONSTRAINT UQ_Story_ChapterNumber UNIQUE(StoryID, ChapterNumber)
);
GO

-- Table for chapter content versions - use for T/M variants and rewrites where original is desired to be preserved
DROP TABLE IF EXISTS ChapterContents;
GO

CREATE TABLE ChapterContents (
                                 ChapterContentID BIGINT IDENTITY(1,1) PRIMARY KEY, -- CHANGED: BIGINT for safety (high volume)
                                 ChapterID INT NOT NULL,
                                 AuthorID INT NULL, -- Assumes AspNetUsers.Id is INT
                                 VersionName NVARCHAR(100) NULL, -- special version names will allow custom formatting on the webpage
                                 TopAuthorsNote NVARCHAR(MAX) NULL,
                                 ChapterText NVARCHAR(MAX) NOT NULL,
                                 BottomAuthorsNote NVARCHAR(MAX) NULL,
                                 WordCount INT NOT NULL DEFAULT 0,
                                 ViewCount INT NOT NULL DEFAULT 0,
                                 Rating TINYINT NULL,
                                 PublishDate DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                                 OriginalPublishDate DATETIME2(7) NULL
);
GO

/*
================================================================================
-- STORY ARCS (Volumes / Books)
-- This table allows authors to group chapters into "arcs" or "volumes".
================================================================================
*/

CREATE TABLE StoryArcs (
                           StoryArcID INT IDENTITY(1,1) PRIMARY KEY,

                           StoryID INT NOT NULL,

                           Title NVARCHAR(255) NOT NULL,

    -- The display order for this arc (e.g., 1, 2, 3...)
                           SortOrder INT NOT NULL,

    -- The number of the first chapter in this arc (inclusive)
                           StartChapterNumber INT NOT NULL,

    -- The number of the last chapter in this arc (inclusive)
                           EndChapterNumber INT NOT NULL,

    -- Foreign key to the story. If the story is deleted, its arcs are deleted too.
                           CONSTRAINT FK_StoryArcs_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE
);
GO

/* ================================================================================
-- 1. TAG TYPES LOOKUP TABLE
-- This table replaces the 'TagType' CHECK constraint.
-- It's the "source of truth" for what tag types exist.
================================================================================
*/
DROP TABLE IF EXISTS TagTypes;
GO

CREATE TABLE TagTypes (
                          TagTypeID TINYINT IDENTITY(1,1) PRIMARY KEY,
                          TypeName NVARCHAR(50) NOT NULL UNIQUE
);
GO

/* -- Populate the lookup table with your 6 defined types. */
INSERT INTO TagTypes (TypeName)
VALUES
    ('Genre'),
    ('Character'),
    ('Relationship'),
    ('Warning'),
    ('CrossoverFandom'),
    ('Universe');
GO

-- Table for series metadata
DROP TABLE IF EXISTS Series;
GO

CREATE TABLE Series (
                        SeriesID INT IDENTITY(1,1) PRIMARY KEY, -- OPTIMIZATION: INT is safe (not 2.1B series)
                        AuthorID INT NULL, -- <-- CHANGED: Made NULLABLE for ON DELETE SET NULL
                        Name NVARCHAR(255) NOT NULL,
                        Description NVARCHAR(1000) NULL,
                        DateCreated DATETIME2(7) NOT NULL DEFAULT GETUTCDATE()
);
GO

/*
================================================================================
 Phase 2: Create Junction & Feature Tables (UGC UserID columns set to NULLABLE)
================================================================================
*/

-- Table to link stories to a series
DROP TABLE IF EXISTS SeriesEntries;
GO

CREATE TABLE SeriesEntries (
                               SeriesID INT NOT NULL,
                               StoryID INT NOT NULL,
                               OrderIndex INT NOT NULL,
                               CONSTRAINT PK_SeriesEntries PRIMARY KEY (SeriesID, StoryID)
);
GO

-- NEW TABLE for co-author authorization
DROP TABLE IF EXISTS CoAuthors;
GO

CREATE TABLE CoAuthors (
                           StoryID INT NOT NULL,
                           CoAuthorUserID INT NOT NULL,
                           DateAdded DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                           CONSTRAINT PK_StoryCoAuthors PRIMARY KEY (StoryID, CoAuthorUserID)
);
GO

-- Table for beta reader authorization
DROP TABLE IF EXISTS BetaReaders;
GO

CREATE TABLE BetaReaders (
                             StoryID INT NOT NULL,
                             BetaReaderUserID INT NOT NULL,
                             DateAdded DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                             CONSTRAINT PK_StoryBetaReaders PRIMARY KEY (StoryID, BetaReaderUserID)
);
GO

/* ================================================================================
-- 1. ACKNOWLEDGMENT ROLES LOOKUP TABLE
-- This table replaces the 'Role' string in StoryAcknowledgments.
-- It's the "source of truth" for what roles exist.
================================================================================
*/
DROP TABLE IF EXISTS AcknowledgmentRoles;
GO

CREATE TABLE AcknowledgmentRoles (
                                     AcknowledgmentRoleID TINYINT IDENTITY(1,1) PRIMARY KEY,
                                     RoleName NVARCHAR(100) NOT NULL UNIQUE
);
GO

/* -- Populate the lookup table with the starting values and other common examples. -- */
INSERT INTO AcknowledgmentRoles (RoleName)
VALUES
    ('Beta Reader'),
    ('Planner'),
    ('Cover Artist'),
    ('Editor'),
    ('Inspiration');
GO

/* ================================================================================
-- 2. STORY ACKNOWLEDGMENTS TABLE (MODIFIED)
-- This table links stories to the users they are acknowledging,
-- and the role they served.
================================================================================
*/
DROP TABLE IF EXISTS StoryAcknowledgments;
GO

CREATE TABLE StoryAcknowledgments (
                                      StoryID INT NOT NULL,
                                      AcknowledgedUserID INT NOT NULL,

    /* -- NEW: Foreign key to the AcknowledgmentRoles table. -- */
                                      AcknowledgmentRoleID TINYINT NOT NULL,

                                      DateAcknowledged DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),

    /* -- NEW: The primary key now uses the ID instead of the string. -- */
    /* -- This still allows a user to be acknowledged for multiple roles on the same story. -- */
                                      CONSTRAINT PK_StoryAcknowledgments PRIMARY KEY (StoryID, AcknowledgedUserID, AcknowledgmentRoleID),

    /* -- NEW: Foreign key constraint to ensure data integrity. -- */
                                      CONSTRAINT FK_StoryAcknowledgments_Role FOREIGN KEY (AcknowledgmentRoleID) REFERENCES AcknowledgmentRoles(AcknowledgmentRoleID)

);
GO

/* ================================================================================
-- 1. REPORT REASONS LOOKUP TABLE
-- Replaces the 'Reason' CHECK constraint.
================================================================================
*/
DROP TABLE IF EXISTS ReportReasons;
GO

CREATE TABLE ReportReasons (
                               ReportReasonID TINYINT IDENTITY(1,1) PRIMARY KEY,
                               ReasonName NVARCHAR(100) NOT NULL UNIQUE,
                               Description NVARCHAR(255) NULL
);
GO

INSERT INTO ReportReasons (ReasonName, Description)
VALUES
    ('Other', 'A reason not covered by other categories.'),
    ('Spam', 'Unsolicited advertising or repeated, low-effort content.'),
    ('Hate Speech', 'Content that attacks a person or group based on race, ethnicity, religion, etc.'),
    ('Harassment', 'Targeted abuse, bullying, or intimidation of a user.'),
    ('Illegal Content', 'Content violating laws, such as child pornography or piracy.'),
    ('Plagiarism', 'Posting content that is not your own without attribution.');
GO

/* ================================================================================
-- 2. REPORT STATUSES LOOKUP TABLE
-- Replaces the 'Status' CHECK constraint.
================================================================================
*/
DROP TABLE IF EXISTS ReportStatuses;
GO

CREATE TABLE ReportStatuses (
                                ReportStatusID TINYINT IDENTITY(1,1) PRIMARY KEY,
                                StatusName NVARCHAR(50) NOT NULL UNIQUE
);
GO

INSERT INTO ReportStatuses (StatusName)
VALUES
    ('Open'),
    ('UnderReview'),
    ('Resolved-NoAction'),
    ('Resolved-ActionTaken');
GO


/* ================================================================================
-- 3. REPORTS TABLE (MODIFIED)
-- This is your original table, now refactored.
================================================================================
*/
DROP TABLE IF EXISTS Reports;
GO

CREATE TABLE Reports (
                         ReportID BIGINT IDENTITY(1,1) PRIMARY KEY, -- CHANGED: BIGINT for safety (high volume)
                         ReporterUserID INT NULL, -- <-- CHANGED: Made NULLABLE for ON DELETE SET NULL

    /* -- NEW: Replaced string with a 'magic byte' (enum in C#). -- */
    /* -- 1=User, 2=Story, 3=Recommendation, 4=Comment, 5=BlogPost -- */
                         ReportedEntityTypeID TINYINT NOT NULL,

                         ReportedEntityID INT NOT NULL,

    /* -- NEW: Replaced string with a foreign key to the lookup table. -- */
                         ReportReasonID TINYINT NOT NULL,

                         Notes NVARCHAR(1000) NULL,

    /* -- NEW: Replaced string with a foreign key to the lookup table. -- */
    /* -- Default 1 = 'Open' from the insert script above. -- */
                         ReportStatusID TINYINT NOT NULL DEFAULT 1,

                         ModeratorUserID INT NULL,
                         ActionTaken NVARCHAR(255) NULL,
                         DateReported DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                         DateResolved DATETIME2(7) NULL,

    /* -- NEW: Foreign key constraints for the new lookup tables. -- */
                         CONSTRAINT FK_Reports_Reason FOREIGN KEY (ReportReasonID) REFERENCES ReportReasons(ReportReasonID),
                         CONSTRAINT FK_Reports_Status FOREIGN KEY (ReportStatusID) REFERENCES ReportStatuses(ReportStatusID)

    /* -- REMOVED: Old CHECK constraints are no longer needed. -- */
);
GO


-- Handles system lists and is the dataset for user-specific filtering criteria on past interactions
DROP TABLE IF EXISTS UserStoryInteractions;
GO

CREATE TABLE UserStoryInteractions (
                                       UserID INT NOT NULL, -- OPTIMIZATION: INT (4 bytes)
                                       StoryID INT NOT NULL, -- OPTIMIZATION: INT (4 bytes)

                                       ReadStatus TINYINT NOT NULL DEFAULT 0,
                                       IsActivelyReading BIT NOT NULL DEFAULT 0,
                                       FavoriteStatus TINYINT NOT NULL DEFAULT 0,
                                       IsFollowed BIT NOT NULL DEFAULT 0,
                                       IsReadItLater BIT NOT NULL DEFAULT 0,
                                       IsIgnored BIT NOT NULL DEFAULT 0,

    -- Date tracking for all interactions --
                                       FavoriteDate DATETIME2(7) NULL,
                                       FollowedDate DATETIME2(7) NULL,
                                       ReadItLaterDate DATETIME2(7) NULL,
                                       IgnoredDate DATETIME2(7) NULL, -- <-- The new column
                                       SourceRecommendationID INT NULL,

                                       CONSTRAINT PK_UserStoryInteractions PRIMARY KEY (UserID, StoryID) -- OPTIMIZATION: Small 8-byte key
);
GO

/*
================================================================================
-- SEARCH MODES & FILTERS
-- Uses logical string keys (SearchModeKey, InteractionFilterKey).
================================================================================
*/
DROP TABLE IF EXISTS UserSearchSettings;
DROP TABLE IF EXISTS DefaultSearchSettings;
DROP TABLE IF EXISTS UserInteractionFilters;
DROP TABLE IF EXISTS SearchModes;
GO

-- 1. Search Modes (e.g., 'TreeSearch', 'RandomSearch')
CREATE TABLE SearchModes (
                             SearchModeKey NVARCHAR(50) PRIMARY KEY,
                             Name NVARCHAR(100) NOT NULL
);
GO

-- 2. User Interaction Filters (e.g., 'Favorited', 'ReadItLater', 'IsComplete')
CREATE TABLE UserInteractionFilters (
                                        InteractionFilterKey NVARCHAR(50) PRIMARY KEY,
                                        Name NVARCHAR(100) NOT NULL,
                                        Description NVARCHAR(255) NULL
);
GO

-- 3. Site-wide defaults for which filters are enabled for which modes.
CREATE TABLE DefaultSearchSettings (
                                       SearchModeKey NVARCHAR(50) NOT NULL,
                                       InteractionFilterKey NVARCHAR(50) NOT NULL,
                                       IsEnabled BIT NOT NULL DEFAULT 0,
                                       DefaultValue NVARCHAR(100) NULL,

                                       CONSTRAINT PK_DefaultSearchSettings PRIMARY KEY (SearchModeKey, InteractionFilterKey),
                                       CONSTRAINT FK_DefaultSearchSettings_Mode FOREIGN KEY (SearchModeKey) REFERENCES SearchModes(SearchModeKey) ON DELETE CASCADE,
                                       CONSTRAINT FK_DefaultSearchSettings_Filter FOREIGN KEY (InteractionFilterKey) REFERENCES UserInteractionFilters(InteractionFilterKey) ON DELETE CASCADE
);
GO

-- 4. User's personal overrides for search settings (Sparse table)
CREATE TABLE UserSearchSettings (
                                    UserSearchSettingID INT IDENTITY(1,1) PRIMARY KEY,
                                    UserID INT NOT NULL,
                                    SearchModeKey NVARCHAR(50) NOT NULL,
                                    InteractionFilterKey NVARCHAR(50) NOT NULL,
                                    IsEnabled BIT NOT NULL,
                                    Value NVARCHAR(100) NULL,

                                    CONSTRAINT FK_UserSearchSettings_User FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
                                    CONSTRAINT FK_UserSearchSettings_Mode FOREIGN KEY (SearchModeKey) REFERENCES SearchModes(SearchModeKey) ON DELETE CASCADE,
                                    CONSTRAINT FK_UserSearchSettings_Filter FOREIGN KEY (InteractionFilterKey) REFERENCES UserInteractionFilters(InteractionFilterKey) ON DELETE CASCADE,
                                    CONSTRAINT UK_UserSearchSettings UNIQUE (UserID, SearchModeKey, InteractionFilterKey)
);
GO


/*
================================================================================
-- USER CUSTOM FILTERS (Formerly UserSearchEntityFilters)
-- Stores saved filters that reference UserLists or Groups.
-- Uses a Magic Byte (FilterTypeID) for logic.
================================================================================
*/
DROP TABLE IF EXISTS UserSearchEntityFilters;
DROP TABLE IF EXISTS UserCustomFilters;
GO

CREATE TABLE UserCustomFilters (
                                   UserCustomFilterID INT IDENTITY(1,1) PRIMARY KEY, -- Renamed
                                   UserID INT NOT NULL,
                                   SearchModeKey NVARCHAR(50) NOT NULL,

    /* Magic Byte: 1 = UserList, 2 = Group */
                                   FilterTypeID TINYINT NOT NULL,

                                   EntityID INT NOT NULL,
                                   Include BIT NOT NULL DEFAULT 1,

                                   CONSTRAINT FK_UserCustomFilters_User FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE, -- Renamed
                                   CONSTRAINT FK_UserCustomFilters_SearchMode FOREIGN KEY (SearchModeKey) REFERENCES SearchModes(SearchModeKey) ON DELETE CASCADE, -- Renamed
                                   CONSTRAINT CK_UserCustomFilters_FilterType CHECK (FilterTypeID IN (1, 2)) -- Renamed
);
GO
/*
================================================================================
-- NOTIFICATIONS
-- This section refactors the notification system to use a lookup table
-- with a TINYINT key for performance, as the Notifications table will be
-- one of the largest in the database.
================================================================================
*/

-- We must drop tables in the correct order due to foreign keys.
DROP TABLE IF EXISTS Notifications;
DROP TABLE IF EXISTS UserNotificationSettings;
DROP TABLE IF EXISTS NotificationTypeDefaults; -- Old table name
DROP TABLE IF EXISTS NotificationTypes; -- New table name
GO

/* 1. The Lookup Table (Replaces NotificationTypeDefaults) */
CREATE TABLE NotificationTypes (
                                   NotificationTypeID TINYINT IDENTITY(1,1) PRIMARY KEY,

    /* This is the logical key your C# code will use (e.g., "NewComment") */
                                   NotificationKey NVARCHAR(50) NOT NULL UNIQUE,
                                   DisplayName NVARCHAR(50) NOT NULL,
                                   Description NVARCHAR(255) NOT NULL,
                                   DefaultEmailEnabled BIT NOT NULL DEFAULT 0
);
GO

/* Populate the lookup table with site-defined notification types */
INSERT INTO NotificationTypes (NotificationKey, DisplayName, Description, DefaultEmailEnabled)
VALUES
    ('NewComment', 'New Comment', 'Someone commented on one of your stories', 1),
    ('NewFavorite', 'New Favorite', 'Someone favorited one of your stories', 1),
    ('NewFollower', 'New Follower', 'A new user followed you', 1),
    ('StoryUpdated', 'Story Updated', 'A story you follow has been updated', 1),
    ('SiteAnnouncement', 'Site Announcement', 'A new announcement from site staff', 0);
GO

/* 2. User-Specific Overrides */
CREATE TABLE UserNotificationSettings (
                                          UserID INT NOT NULL,

    /* NEW: Uses the tinyint foreign key */
                                          NotificationTypeID TINYINT NOT NULL,

    /* User's override value */
                                          EmailEnabled BIT NOT NULL,

                                          CONSTRAINT PK_UserNotificationSettings PRIMARY KEY (UserID, NotificationTypeID),
                                          CONSTRAINT FK_UserNotificationSettings_User FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,

    /* NEW: Foreign key points to the new lookup table */
                                          CONSTRAINT FK_UserNotificationSettings_Type FOREIGN KEY (NotificationTypeID) REFERENCES NotificationTypes(NotificationTypeID) ON DELETE CASCADE
);
GO

/* 3. The Main Notifications Table (The one that gets huge) */
CREATE TABLE Notifications (
                               NotificationID BIGINT IDENTITY(1,1) PRIMARY KEY, -- CHANGED: BIGINT for safety (high volume)
                               RecipientUserID INT NOT NULL,

    /* NEW: Replaces 'EventType' string with the fast tinyint key */
                               NotificationTypeID TINYINT NOT NULL,

                               SourceUserID INT NULL, -- The user who triggered the event (e.g., left the comment)

    /* The ID of the story, comment, user, etc. that is related to this event */
                               RelatedEntityID INT NOT NULL,

                               IsRead BIT NOT NULL DEFAULT 0,
                               DateCreated DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),

                               CONSTRAINT FK_Notifications_RecipientUser FOREIGN KEY (RecipientUserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
                               CONSTRAINT FK_Notifications_SourceUser FOREIGN KEY (SourceUserID) REFERENCES AspNetUsers(Id) ON DELETE NO ACTION,

    /* NEW: Foreign key points to the new lookup table */
                               CONSTRAINT FK_Notifications_NotificationType FOREIGN KEY (NotificationTypeID) REFERENCES NotificationTypes(NotificationTypeID) ON DELETE CASCADE
);
GO

/*
================================================================================
-- BADGES
-- Renamed BadgeID to BadgeKey for clarity.
================================================================================
*/
DROP TABLE IF EXISTS UserBadges;
DROP TABLE IF EXISTS Badges;
GO

CREATE TABLE Badges (
                        BadgeKey NVARCHAR(50) PRIMARY KEY, -- Renamed from BadgeID
                        Name NVARCHAR(100) NOT NULL,
                        Description NVARCHAR(500) NULL,
                        IconURL NVARCHAR(500) NOT NULL,
                        SortOrder INT NOT NULL DEFAULT 0
);
GO

CREATE TABLE UserBadges (
                            UserID INT NOT NULL,
                            BadgeKey NVARCHAR(50) NOT NULL, -- Renamed from BadgeID
                            DateEarned DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                            DisplayOrder INT NOT NULL DEFAULT 0,
                            CONSTRAINT PK_UserBadges PRIMARY KEY (UserID, BadgeKey),
                            CONSTRAINT FK_UserBadges_User FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
                            CONSTRAINT FK_UserBadges_Badge FOREIGN KEY (BadgeKey) REFERENCES Badges(BadgeKey) ON DELETE CASCADE
);
GO

DROP TABLE IF EXISTS UserStats;
GO

CREATE TABLE UserStats (
                           UserID INT PRIMARY KEY,

    -- Reading Stats
                           StoriesRead INT NOT NULL DEFAULT 0,
                           StoriesInProgress INT NOT NULL DEFAULT 0,
                           StoriesIgnored INT NOT NULL DEFAULT 0,
                           ChaptersRead INT NOT NULL DEFAULT 0,
                           WordsRead INT NOT NULL DEFAULT 0,
                           RecommendationsFoundUseful INT NOT NULL DEFAULT 0,

    -- Content Creation Stats
                           StoriesWritten INT NOT NULL DEFAULT 0,
                           WordsWritten BIGINT NOT NULL DEFAULT 0, -- Use BIGINT in case of prolific authors
                           CommentsWritten INT NOT NULL DEFAULT 0,
                           RecommendationsWritten INT NOT NULL DEFAULT 0,
                           BlogPostsWritten INT NOT NULL DEFAULT 0,

                           AcknowledgedAsBetaReaderCount INT NOT NULL DEFAULT 0,
                           AcknowledgedAsInspirationCount INT NOT NULL DEFAULT 0,

    -- Community Interaction Stats
                           FollowerCount INT NOT NULL DEFAULT 0,
                           AuthorsFollowed INT NOT NULL DEFAULT 0,
                           FavoritesOnStories INT NOT NULL DEFAULT 0, -- includes private favorites
                           ViewsOnStories BIGINT NOT NULL DEFAULT 0,

                           GroupsJoined INT NOT NULL DEFAULT 0,
                           RecommendationsReceived INT NOT NULL DEFAULT 0,

                           SpotlightCount INT NOT NULL DEFAULT 0,
                           ActiveReportCount INT NOT NULL DEFAULT 0,

    -- Foreign Key to AspNetUsers
                           CONSTRAINT FK_UserStats_User FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
);
GO

/* ================================================================================
-- FANFIC SCHEMA - TAGS & RELATIONSHIPS (v7 - Refactored)
-- TAGS TABLE (MODIFIED)
-- Fixed the UNIQUE constraint on TagName.
-- Added 'AllowOCDetails' to validate 'StoryCharacters' logic.
================================================================================
*/
DROP TABLE IF EXISTS StoryTags;
DROP TABLE IF EXISTS Tags;
GO
-- Note: This assumes 'TagTypes' table from the previous script already exists.
CREATE TABLE Tags (
                      TagID INT IDENTITY(1,1) PRIMARY KEY, -- OPTIMIZATION: INT is safe (not 2.1B tags)
                      TagName NVARCHAR(100) NOT NULL,
                      TagTypeID TINYINT NOT NULL,
                      IsFanon BIT NOT NULL DEFAULT 0, -- 0 = Canon, 1 = Fanon
                      Description NVARCHAR(1000) NULL,
                      ParentTagID INT NULL,
                      SpriteURL NVARCHAR(500) NULL,
                      AnimatedSpriteURL NVARCHAR(500) NULL,

    /* -- NEW: This bit controls if a tag can be used as a base for an OC -- */
                      AllowOCDetails BIT NOT NULL DEFAULT 0,

                      CONSTRAINT FK_Tags_TagType FOREIGN KEY (TagTypeID) REFERENCES TagTypes(TagTypeID),
                      CONSTRAINT FK_Tags_ParentTag FOREIGN KEY (ParentTagID) REFERENCES Tags(TagID),

    /* -- FIXED: TagName only needs to be unique *within* its type. -- */
                      CONSTRAINT UK_Tags_Name_Type UNIQUE (TagName, TagTypeID)
);
GO

/*
================================================================================
-- STORYTAGS (Junction Table)
-- Links stories to non-character/relationship tags.
-- REFACTORED: 'Priority' string to 'Priority' tinyint (Magic Byte)
================================================================================
*/
DROP TABLE IF EXISTS StoryTags;
GO
CREATE TABLE StoryTags (
                           StoryID INT NOT NULL,
                           TagID INT NOT NULL,
    /* -- REFACTORED: 'Priority' string to 'Priority' tinyint (Magic Byte) -- */
    /* -- 0 = None, 1 = Primary, 2 = Supporting -- */
                           Priority TINYINT NOT NULL DEFAULT 0,

                           CONSTRAINT PK_StoryTags PRIMARY KEY (StoryID, TagID),
                           CONSTRAINT FK_StoryTags_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE,
                           CONSTRAINT FK_StoryTags_Tag FOREIGN KEY (TagID) REFERENCES Tags(TagID) ON DELETE CASCADE,
                           CONSTRAINT CK_StoryTags_Priority CHECK (Priority IN (0, 1, 2))
);
GO

/*
================================================================================
-- STORYCHARACTERS (NEW UNIFIED TABLE)
-- REPLACES the old 'OCs' table.
-- This table holds ALL characters in a story (Canon and OC).
-- REMOVED: OC_SpriteURL
================================================================================
*/
DROP TABLE IF EXISTS StoryCharacterRelationshipMembers;
DROP TABLE IF EXISTS StoryCharacterRelationships;
DROP TABLE IF EXISTS OCs; -- Drop old table
DROP TABLE IF EXISTS StoryCharacters; -- Drop new table before creating
GO

CREATE TABLE StoryCharacters (
                                 StoryCharacterID INT IDENTITY(1,1) PRIMARY KEY, -- OPTIMIZATION: INT is a good tradeoff
                                 StoryID INT NOT NULL,
                                 CharacterTagID INT NOT NULL, -- Foreign Key to the 'Tags' table

    /* -- REFACTORED: 'Priority' string to 'Priority' tinyint (Magic Byte) -- */
    /* -- 0 = None, 1 = Primary, 2 = Supporting -- */
                                 Priority TINYINT NOT NULL DEFAULT 0,

    -- OC-Specific Fields
                                 IsOC BIT NOT NULL DEFAULT 0,
                                 OC_Name NVARCHAR(100) NULL, -- Custom name if different from tag
                                 OC_Bio NVARCHAR(1000) NULL,
    /* -- REMOVED: OC_SpriteURL per user request -- */

                                 CONSTRAINT FK_StoryCharacters_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE,
                                 CONSTRAINT FK_StoryCharacters_Tag FOREIGN KEY (CharacterTagID) REFERENCES Tags(TagID) ON DELETE CASCADE,
                                 CONSTRAINT CK_StoryCharacters_Priority CHECK (Priority IN (0, 1, 2)),
                                 CONSTRAINT UQ_StoryCharacters_StoryTag UNIQUE (StoryID, CharacterTagID)
);
GO


/*
================================================================================
-- TRIGGER (NEW)
-- Enforces that 'IsOC = 1' can only be set on tags where 'AllowOCDetails = 1'.
================================================================================
*/
DROP TRIGGER IF EXISTS TR_StoryCharacters_EnforceOCLogic;
GO

CREATE TRIGGER TR_StoryCharacters_EnforceOCLogic
    ON StoryCharacters
    AFTER INSERT, UPDATE
    AS
BEGIN
    IF EXISTS (
        SELECT 1
        FROM inserted i
                 JOIN Tags t ON i.CharacterTagID = t.TagID
        WHERE i.IsOC = 1 -- The user is trying to mark this as an OC
          AND t.AllowOCDetails = 0 -- But the base tag does not allow it
    )
        BEGIN
            RAISERROR ('The selected character tag cannot be used as a base for an OC. Please select a generic tag (e.g., "Bulbasaur") or an "OC" tag.', 16, 1);

            RETURN;
        END
END;
GO


/*
================================================================================
-- STORY CHARACTER RELATIONSHIPS
-- Defines relationships between characters *within* a story.
-- REFACTORED: 'RelationshipType' and 'Priority' to TINYINT
================================================================================
*/
CREATE TABLE StoryCharacterRelationships (
                                             StoryCharacterRelationshipID INT IDENTITY(1,1) PRIMARY KEY, -- OPTIMIZATION: INT is a good tradeoff
                                             StoryID INT NOT NULL,

    /* -- REFACTORED: 'RelationshipType' string to tinyint (Magic Byte) -- */
    /* -- 1 = Romantic (/), 2 = Platonic (&) -- */
                                             RelationshipType TINYINT NOT NULL,

    /* -- REFACTORED: 'Priority' string to 'Priority' tinyint (Magic Byte) -- */
    /* -- 0 = None, 1 = Primary, 2 = Supporting -- */
                                             Priority TINYINT NOT NULL DEFAULT 0,

                                             CONSTRAINT FK_StoryCharacterRelationships_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE,
                                             CONSTRAINT CK_StoryCharacterRelationships_Type CHECK (RelationshipType IN (1, 2)),
                                             CONSTRAINT CK_StoryCharacterRelationships_Priority CHECK (Priority IN (0, 1, 2))
);
GO

/*
================================================================================
-- STORYCHARACTERRELATIONSHIPMEMBERS (Junction Table)
-- Links characters (from StoryCharacters) to a relationship.
-- FIXED: Now correctly links to 'StoryCharacters' table.
================================================================================
*/
CREATE TABLE StoryCharacterRelationshipMembers (
                                                   StoryCharacterRelationshipID INT NOT NULL,
                                                   StoryCharacterID INT NOT NULL, -- This is the FK to the new StoryCharacters table

                                                   CONSTRAINT PK_StoryCharacterRelationshipMembers PRIMARY KEY (StoryCharacterRelationshipID, StoryCharacterID),
                                                   CONSTRAINT FK_StoryCharRelationshipMembers_Rel FOREIGN KEY (StoryCharacterRelationshipID) REFERENCES StoryCharacterRelationships(StoryCharacterRelationshipID) ON DELETE CASCADE,
                                                   CONSTRAINT FK_StoryCharRelationshipMembers_Char FOREIGN KEY (StoryCharacterID) REFERENCES StoryCharacters(StoryCharacterID) -- NO CASCADE: Deleting a char should not delete the whole relationship
);
GO


/*
================================================================================
-- STORYUNIVERSES (REMOVED)
-- This table is no longer needed. Base settings are now just tags in 'StoryTags'.
-- Custom details for settings are now handled by 'SettingDetails'.
================================================================================
*/
DROP TABLE IF EXISTS StoryUniverses;
GO


/*
================================================================================
-- SETTINGDETAILS (RENAMED from AUs)
-- Table for story-specific *optional* details for a 'Setting' or 'AU Trope' tag.
-- This follows the same logic as 'StoryCharacters' (e.g., OC details).
================================================================================
*/
DROP TABLE IF EXISTS AUs; -- Drop old name
DROP TABLE IF EXISTS SettingDetails; -- Drop new name
GO

CREATE TABLE SettingDetails (
                                SettingDetailID INT IDENTITY(1,1) PRIMARY KEY,
                                StoryID INT NOT NULL,
                                BaseTagID INT NOT NULL, -- The 'Setting' or 'AU Trope' tag (e.g., "PMD" or "Coffee Shop AU")
                                Name NVARCHAR(100) NULL, -- Story-specific override name (e.g., "My PMD Variant")
                                Description NVARCHAR(2000) NULL,

                                CONSTRAINT FK_SettingDetails_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE,
                                CONSTRAINT FK_SettingDetails_Tag FOREIGN KEY (BaseTagID) REFERENCES Tags(TagID) ON DELETE CASCADE,

    /* -- A story can have details for *multiple* settings/AUs. -- */
                                CONSTRAINT UQ_SettingDetails_Story_Tag UNIQUE (StoryID, BaseTagID)
);
GO


/*
================================================================================
-- TRIGGER (FIXED)
-- Enforces Priority is only used for certain tag types in StoryTags.
-- FIXED: Trigger logic now correctly joins TagTypes to check the name.
================================GETUTCDATE()
*/
DROP TRIGGER IF EXISTS TR_StoryTags_EnforcePriorityLogic;
GO

CREATE TRIGGER TR_StoryTags_EnforcePriorityLogic
    ON StoryTags
    AFTER INSERT, UPDATE
    AS
BEGIN
    IF EXISTS (
        SELECT 1
        FROM inserted i
                 JOIN Tags t ON i.TagID = t.TagID
                 JOIN TagTypes tt ON t.TagTypeID = tt.TagTypeID -- <-- FIXED: Added join to TagTypes
        WHERE i.Priority > 0 -- 0 = None, so > 0 means Primary or Supporting
          AND tt.TypeName NOT IN ('Genre') -- <-- FIXED: Checking tt.Name
        -- (Note: 'Character', 'Relationship' 'Setting', 'AU Trope' etc.
        -- are now all correctly handled by this logic and will be
        -- prevented from having a priority, which is correct.)
    )
        BEGIN
            RAISERROR ('A Priority (e.g., Primary, Supporting) can only be assigned to tags of type Genre.', 16, 1);

            RETURN;
        END
END;
GO

-- Table for daily story statistics
DROP TABLE IF EXISTS DailyStoryStats;
GO

CREATE TABLE DailyStoryStats (
                                 StoryID INT NOT NULL,
                                 StatDate DATE NOT NULL,
                                 Views INT NOT NULL DEFAULT 0,
                                 Favorites INT NOT NULL DEFAULT 0,
                                 CONSTRAINT PK_DailyStoryStats PRIMARY KEY (StoryID, StatDate)
);
GO

-- Table for the Community Spotlight feature
DROP TABLE IF EXISTS CommunitySpotlight;
GO

CREATE TABLE CommunitySpotlight (
                                    SpotlightID INT IDENTITY(1,1) PRIMARY KEY,
                                    StoryID INT NOT NULL,
                                    SponsoringUserID INT NULL, -- <-- CHANGED: Made NULLABLE for ON DELETE SET NULL
                                    SponsorComment NVARCHAR(280) NULL,
                                    StartDate DATETIME2(7) NOT NULL,
                                    EndDate DATETIME2(7) NOT NULL,
                                    PaymentID NVARCHAR(255) NULL,
                                    DateCreated DATETIME2(7) NOT NULL DEFAULT GETUTCDATE()
);
GO

-- Table for user interactions with chapters (read status, progress)
DROP TABLE IF EXISTS UserChapterInteractions;
GO

CREATE TABLE UserChapterInteractions (
                                         UserID INT NOT NULL,
                                         ChapterID INT NOT NULL,
                                         ReadProgress FLOAT NOT NULL DEFAULT 0,
                                         LastInteractionDate DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                                         CONSTRAINT PK_UserChapterInteractions PRIMARY KEY (UserID, ChapterID)
);
GO

-- Table for groups
DROP TABLE IF EXISTS Groups;
GO

CREATE TABLE Groups (
                        GroupID INT IDENTITY(1,1) PRIMARY KEY,
                        CreatorID INT NULL, -- <-- CHANGED: Made NULLABLE for ON DELETE SET NULL
                        GroupName NVARCHAR(100) NOT NULL UNIQUE,
                        Description NVARCHAR(1000) NULL,
                        DateCreated DATETIME2(7) NOT NULL DEFAULT GETUTCDATE()
);
GO

-- Junction table for group membership
DROP TABLE IF EXISTS GroupMembers;
GO

CREATE TABLE GroupMembers (
                              UserID INT NOT NULL,
                              GroupID INT NOT NULL,
                              Role TINYINT NOT NULL DEFAULT 0, -- 0 = Member, 1 = Admin, 2 = Moderator
                              DateJoined DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                              CONSTRAINT PK_GroupMembers PRIMARY KEY (UserID, GroupID)
);
GO

-- Junction table for stories in a group
DROP TABLE IF EXISTS GroupStories;
GO

CREATE TABLE GroupStories (
                              GroupID INT NOT NULL,
                              StoryID INT NOT NULL,
                              AddedByUserID INT NULL, -- <-- CHANGED: Made NULLABLE for ON DELETE SET NULL
                              DateAdded DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                              CONSTRAINT PK_GroupStories PRIMARY KEY (GroupID, StoryID)
);
GO

/*
================================================================================
 Phase 2: Comment Tables (Replaced Polymorphic Model)
================================================================================
*/

-- 1. BaseComments: Holds all common data for all comment types
DROP TABLE IF EXISTS BaseComments;
GO

CREATE TABLE BaseComments (
                              CommentID BIGINT IDENTITY(1,1) PRIMARY KEY, -- CHANGED: BIGINT for safety (high volume)
                              UserID INT NULL, -- Nullable for ON DELETE SET NULL
                              ParentCommentID BIGINT NULL, -- CHANGED: Must be BIGINT to match PK
                              CommentText NVARCHAR(MAX) NOT NULL,
                              LikeCount INT NOT NULL DEFAULT 0,
                              DatePosted DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                              ActiveReportCount INT NOT NULL DEFAULT 0,
                              CommentType NVARCHAR(50) NOT NULL, -- Discriminator for TPH/TPT in EF Core

                              CONSTRAINT CK_BaseComments_CommentType CHECK (CommentType IN ('Chapter', 'UserProfile', 'Group'))
);
GO

-- 2. ChapterComments: Links a BaseComment to a Chapter
DROP TABLE IF EXISTS ChapterComments;
GO

CREATE TABLE ChapterComments (
                                 CommentID BIGINT PRIMARY KEY, -- CHANGED: Must be BIGINT to match BaseComments
                                 ChapterID INT NOT NULL,
                                 CONSTRAINT FK_ChapterComments_BaseComment FOREIGN KEY (CommentID) REFERENCES BaseComments(CommentID) ON DELETE CASCADE,
                                 CONSTRAINT FK_ChapterComments_Chapter FOREIGN KEY (ChapterID) REFERENCES Chapters(ChapterID) ON DELETE CASCADE
);
GO

-- 3. UserProfileComments: Links a BaseComment to a User's Profile
DROP TABLE IF EXISTS UserProfileComments;
GO

CREATE TABLE UserProfileComments (
                                     CommentID BIGINT PRIMARY KEY, -- CHANGED: Must be BIGINT to match BaseComments
                                     ProfileUserID INT NOT NULL,
                                     CONSTRAINT FK_UserProfileComments_BaseComment FOREIGN KEY (CommentID) REFERENCES BaseComments(CommentID) ON DELETE CASCADE,
                                     CONSTRAINT FK_UserProfileComments_User FOREIGN KEY (ProfileUserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
);
GO

-- 4. GroupComments: Links a BaseComment to a Group
DROP TABLE IF EXISTS GroupComments;
GO

CREATE TABLE GroupComments (
                               CommentID BIGINT PRIMARY KEY, -- CHANGED: Must be BIGINT to match BaseComments
                               GroupID INT NOT NULL,
                               CONSTRAINT FK_GroupComments_BaseComment FOREIGN KEY (CommentID) REFERENCES BaseComments(CommentID) ON DELETE CASCADE,
                               CONSTRAINT FK_GroupComments_Group FOREIGN KEY (GroupID) REFERENCES Groups(GroupID) ON DELETE CASCADE
);
GO

-- 5. BlogPostComments: Links a BaseComment to a BlogPost
DROP TABLE IF EXISTS BlogPostComments;
GO

CREATE TABLE BlogPostComments (
                                  CommentID BIGINT PRIMARY KEY, -- CHANGED: Must be BIGINT to match BaseComments
                                  BlogPostID INT NOT NULL,
                                  CONSTRAINT FK_BlogPostComments_BaseComment FOREIGN KEY (CommentID) REFERENCES BaseComments(CommentID) ON DELETE CASCADE
    -- FK to BlogPosts added in Phase 3
);
GO

-- 6. CommentLikes: Now cleanly points to the BaseComments table
DROP TABLE IF EXISTS CommentLikes;
GO

CREATE TABLE CommentLikes (
                              UserID INT NOT NULL,
                              CommentID BIGINT NOT NULL, -- CHANGED: Must be BIGINT to match BaseComments
                              DateLiked DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                              CONSTRAINT PK_CommentLikes PRIMARY KEY (UserID, CommentID),
                              CONSTRAINT FK_CommentLikes_User FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
                              CONSTRAINT FK_CommentLikes_Comment FOREIGN KEY (CommentID) REFERENCES BaseComments(CommentID) ON DELETE CASCADE
);
GO

/*
================================================================================
-- 1. NEW LOOKUP TABLE for Recommendation Statuses
-- This replaces the 'Status' string column.
================================================================================
*/
DROP TABLE IF EXISTS RecommendationStatuses;
GO

CREATE TABLE RecommendationStatuses (
                                        StatusID TINYINT IDENTITY(1,1) PRIMARY KEY,
                                        StatusName NVARCHAR(50) NOT NULL UNIQUE,
                                        Description NVARCHAR(255) NULL
);
GO

/* -- Populate with your existing values -- */
INSERT INTO RecommendationStatuses (StatusName, Description)
VALUES
    ('PendingApproval', 'Submitted by user, awaiting author review.'),
    ('Approved', 'Publicly visible.'),
    ('Rejected', 'Rejected by author, not visible.'),
    ('UnderReview', 'An approved recommendation that was reported and is under review.');
GO


/*
================================================================================
-- 2. REFACTORED 'Recommendations' Table
-- Replaced 'Status' with 'StatusID'.
-- Added correct, non-conflicting foreign key policies.
================================================================================
*/
DROP TABLE IF EXISTS Recommendations;
GO

CREATE TABLE Recommendations (
                                 RecommendationID INT IDENTITY(1,1) PRIMARY KEY,
                                 StoryID INT NOT NULL,
                                 RecommenderID INT NULL, -- Renamed from UserID

                                 Text NVARCHAR(MAX) NOT NULL,

    /* -- REFACTORED: Replaced string with a TINYINT foreign key -- */
    /* -- Default 1 = 'PendingApproval' from the insert script above. -- */
                                 StatusID TINYINT NOT NULL DEFAULT 1,
/* -- NEW COLUMN: For the "Hidden Gem" feature -- */
                                 IsHiddenGem BIT NOT NULL DEFAULT 0,
                                 IsHighlightedByAuthor BIT NOT NULL DEFAULT 9,
                                 LikeCount INT NOT NULL DEFAULT 0,
                                 DatePosted DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                                 ActiveReportCount INT NOT NULL DEFAULT 0,

    /* -- FIXED: Unique constraint now uses the correct RecommenderID -- */
                                 CONSTRAINT UQ_User_Story_Recommendation UNIQUE(RecommenderID, StoryID),

    /* -- REMOVED: Old CHECK constraint is no longer needed -- */

    /*
    -- NEW: Foreign Key constraints with correct, non-conflicting policies.
    */
    -- Policy 1: If the Story is deleted, delete the recommendation.
                                 CONSTRAINT FK_Recommendations_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE,

    -- Policy 2: If the Recommender's account is deleted, set their ID to NULL (anonymize).
                                 CONSTRAINT FK_Recommendations_User FOREIGN KEY (RecommenderID) REFERENCES AspNetUsers(Id) ON DELETE SET NULL,

    -- Policy 3: New FK for the status lookup table.
                                 CONSTRAINT FK_Recommendations_Status FOREIGN KEY (StatusID) REFERENCES RecommendationStatuses(StatusID)
);
GO


-- Table for recommendation likes; use "Successful Recs" on the web page
DROP TABLE IF EXISTS RecommendationSuccesses;
GO

CREATE TABLE RecommendationSuccesses (
                                         UserID INT NOT NULL,
                                         RecommendationID INT NOT NULL,
                                         DateRecorded DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                                         CONSTRAINT PK_RecommendationLikes PRIMARY KEY (UserID, RecommendationID)
);
GO

-- Table for user-created lists (Favorites, Read Later, etc.)
DROP TABLE IF EXISTS CustomLists;
GO

CREATE TABLE CustomLists (
                             ListID INT IDENTITY(1,1) PRIMARY KEY,
                             UserID INT NOT NULL,
                             ListName NVARCHAR(100) NOT NULL,
                             IsPublic BIT NOT NULL DEFAULT 0,
                             DateCreated DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                             CONSTRAINT UQ_User_ListName UNIQUE(UserID, ListName)
);
GO

-- Junction table for stories in a user list
DROP TABLE IF EXISTS CustomListEntries;
GO

CREATE TABLE CustomListEntries (
                                   ListID INT NOT NULL,
                                   StoryID INT NOT NULL,
                                   DateAdded DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                                   CONSTRAINT PK_UserListEntries PRIMARY KEY (ListID, StoryID)
);
GO

-- Junction table for user-to-user follows

CREATE TABLE FollowedUsers (
                               UserID INT NOT NULL, -- The one doing the following
                               FollowedUserID INT NOT NULL, -- The one being followed
                               DateFollowed DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),

    -- This is the solution!
    -- A "bell" icon in the UI toggles this.
                               ReceiveAlerts BIT NOT NULL DEFAULT 1, -- 1=Get all notifications, 0=Just follow (bookmark)

                               CONSTRAINT PK_FollowedUsers PRIMARY KEY (UserID, FollowedUserID),
                               CONSTRAINT FK_FollowedUsers_Follower FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
                               CONSTRAINT FK_FollowedUsers_Following FOREIGN KEY (FollowedUserID) REFERENCES AspNetUsers(Id) ON DELETE NO ACTION
);

/*
================================================================================
-- 1. NEW LOOKUP TABLE for Story Relationship Types
-- This table is flexible, for display, and can be added to later.
================================================================================
*/
DROP TABLE IF EXISTS StoryRelationshipTypes;
GO

CREATE TABLE StoryRelationshipTypes (
                                        RelationshipTypeID TINYINT IDENTITY(1,1) PRIMARY KEY,
                                        TypeName NVARCHAR(50) NOT NULL UNIQUE
);
GO

INSERT INTO StoryRelationshipTypes (TypeName)
VALUES
    ('InspiredBy'),
    ('Prequel'),
    ('Sequel'),
    ('CompanionPiece');
GO

/*
================================================================================
-- 2. REFACTORED StoryRelationships Table
-- RelationshipType is now a TINYINT FK.
-- Status is now a TINYINT Magic Byte (1=Pending, 2=Approved, 3=Rejected).
================================================================================
*/
DROP TABLE IF EXISTS StoryRelationships;
GO

CREATE TABLE StoryRelationships (
                                    SourceStoryID INT NOT NULL,
                                    TargetStoryID INT NOT NULL,

    -- REFACTORED: Uses the lookup table FK
                                    RelationshipTypeID TINYINT NOT NULL,

    -- REFACTORED: Uses a Magic Byte/Enum
    -- 1 = Pending, 2 = Approved, 3 = Rejected
                                    StatusID TINYINT NOT NULL DEFAULT 1,

                                    DateCreated DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),

                                    CONSTRAINT PK_StoryRelationships PRIMARY KEY (SourceStoryID, TargetStoryID, RelationshipTypeID),

    -- New constraint for the lookup table
                                    CONSTRAINT FK_StoryRelationships_Type FOREIGN KEY (RelationshipTypeID) REFERENCES StoryRelationshipTypes(RelationshipTypeID),

    -- New constraint for the magic byte
                                    CONSTRAINT CK_StoryRelationships_Status CHECK (StatusID IN (1, 2, 3))
);
GO

-- Table for blog posts
DROP TABLE IF EXISTS BlogPosts;
GO

CREATE TABLE BlogPosts (
                           BlogPostID INT IDENTITY(1,1) PRIMARY KEY,
                           AuthorID INT NULL, -- <-- CHANGED: Made NULLABLE for ON DELETE SET NULL
                           StoryID INT NULL,
                           GroupID INT NULL,
                           Title NVARCHAR(255) NOT NULL,
                           Content NVARCHAR(MAX) NOT NULL,
                           ViewCount INT NOT NULL DEFAULT 0,
                           LikeCount INT NOT NULL DEFAULT 0,
                           IsPublished BIT NOT NULL DEFAULT 0,
                           DateCreated DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                           LastUpdatedDate DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),

                           ActiveReportCount INT NOT NULL DEFAULT 0,
                           CONSTRAINT CK_BlogPost_Parent CHECK (
                               (CASE WHEN StoryID IS NOT NULL THEN 1 ELSE 0 END +
                                CASE WHEN GroupID IS NOT NULL THEN 1 ELSE 0 END) <= 1
                               )
);
GO

-- Table for blog post likes
DROP TABLE IF EXISTS BlogPostLikes;
GO

CREATE TABLE BlogPostLikes (
                               UserID INT NOT NULL,
                               BlogPostID INT NOT NULL,
                               DateLiked DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                               CONSTRAINT PK_BlogPostLikes PRIMARY KEY (UserID, BlogPostID)
);
GO

-- (FeatureContributions table removed 2026-07-18 — Feature 56 was cut; see audit/BlogPosts.md.)

-- Table for story import verification
DROP TABLE IF EXISTS StoryImports;
GO

CREATE TABLE StoryImports (
                              ImportID INT IDENTITY(1,1) PRIMARY KEY,
                              StoryID INT NOT NULL,
                              SourcePlatform NVARCHAR(50) NOT NULL,
                              SourceURL NVARCHAR(500) NOT NULL,
                              VerificationStatus TINYINT NOT NULL DEFAULT 0, -- Pending, Verified, Rejected
                              DateImported DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                              CONSTRAINT UQ_StoryImports_StoryID UNIQUE (StoryID)
);
GO

-- Private Messaging
DROP TABLE IF EXISTS Conversations;
GO

CREATE TABLE Conversations (
                               ConversationID INT IDENTITY(1,1) PRIMARY KEY, -- OPTIMIZATION: INT is safe (not 2.1B conversations)
                               Subject NVARCHAR(255) NULL,
                               DateCreated DATETIME2(7) NOT NULL DEFAULT GETUTCDATE()
);
GO

DROP TABLE IF EXISTS ConversationParticipants;
GO

CREATE TABLE ConversationParticipants (
                                          ConversationID INT NOT NULL,
                                          UserID INT NOT NULL,
                                          LastReadTimestamp DATETIME2(7) NULL,
                                          IsArchived BIT NOT NULL DEFAULT 0,
                                          CONSTRAINT PK_ConversationParticipants PRIMARY KEY (ConversationID, UserID)
);
GO

DROP TABLE IF EXISTS PrivateMessages;
GO

CREATE TABLE PrivateMessages (
                                 MessageID BIGINT IDENTITY(1,1) PRIMARY KEY, -- CHANGED: BIGINT for safety (high volume)
                                 ConversationID INT NOT NULL,
                                 SenderUserID INT NULL, -- <-- CHANGED: Made NULLABLE for ON DELETE SET NULL
                                 MessageText NVARCHAR(MAX) NOT NULL,
                                 DateSent DATETIME2(7) NOT NULL DEFAULT GETUTCDATE()
);
GO

/*
================================================================================
-- SITE DAILY STATS (Data Warehouse Table)
-- This table is a "denormalized" summary of site activity for a given day.
-- It is intended to be populated by a background worker once per day
-- (e.g., at 23:59) to power a fast-loading admin dashboard.
--
-- The Primary Key is the 'StatDate' itself to ensure one row per day.
================================================================================
*/

CREATE TABLE SiteDailyStats (
                                StatDate DATE NOT NULL PRIMARY KEY,

    /* --- User Growth (Your Requests) --- */

    -- New users who registered this day.
    -- (Query: COUNT(*) FROM AspNetUsers WHERE JoinedDate = StatDate)
                                NewUsers INT NOT NULL DEFAULT 0,

    -- The TOTAL number of users on the site at the end of this day.
    -- (Query: COUNT(*) FROM AspNetUsers)
                                TotalUsers INT NOT NULL DEFAULT 0,

    /* --- Content Growth (Your Requests) --- */

    -- New stories (where PublishedDate = StatDate)
                                NewStories INT NOT NULL DEFAULT 0,

    -- The TOTAL number of (published) stories on the site.
    -- (Query: COUNT(*) FROM Stories WHERE StoryStatusID != 'Draft')
                                TotalStories INT NOT NULL DEFAULT 0,

    -- Total words from new chapters published this day.
    -- (Query: SUM(WordCount) FROM ChapterContents WHERE PublishDate = StatDate)
                                NewWords BIGINT NOT NULL DEFAULT 0,

                                TotalWords BIGINT NOT NULL DEFAULT 0,

    /* --- Engagement Metrics (Your Requests + Suggestions) --- */

    -- New "Successful Recs" logged this day.
    -- (Query: COUNT(*) FROM RecommendationSuccesses WHERE DateRecorded = StatDate)
                                NewRecommendationSuccesses INT NOT NULL DEFAULT 0,

    -- SUGGESTION: New comments posted this day.
    -- (Query: COUNT(*) FROM BaseComments WHERE DatePosted = StatDate)
                                NewComments INT NOT NULL DEFAULT 0,

    -- SUGGESTION: New user follows this day.
    -- (Query: COUNT(*) FROM FollowedUsers WHERE DateFollowed = StatDate)
                                NewFollows INT NOT NULL DEFAULT 0,

    -- SUGGESTION: New chapters published (a better metric than just NewStories).
    -- (Query: COUNT(*) FROM Chapters c JOIN ChapterContents cc ON c.PrimaryContentID = cc.ChapterContentID WHERE cc.PublishDate = StatDate)
                                NewChapters INT NOT NULL DEFAULT 0,

    -- SUGGESTION: New recommendations written this day.
    -- (Query: COUNT(*) FROM Recommendations WHERE DatePosted = StatDate)
                                NewRecommendationsWritten INT NOT NULL DEFAULT 0,

    /* --- Activity Metrics (Your Requests + Suggestions) --- */

    -- Total page views.
    -- NOTE: This CANNOT be calculated from the SQL database.
    -- It must be fed from your application's logging middleware or an
    -- external service like Google Analytics.
                                PageViews BIGINT NOT NULL DEFAULT 0,

    -- SUGGESTION: Daily Active Users (DAU).
    -- The most important metric. How many unique users logged in or
    -- performed a meaningful action (like a page view) this day.
    -- This also must be fed from an application-level tracking system.
                                ActiveUsers INT NOT NULL DEFAULT 0
);
GO


/*
================================================================================
 Phase 3: Define All Foreign Keys (All policies added)
================================================================================
*/

-- Series
ALTER TABLE Series ADD CONSTRAINT FK_Series_AspNetUsers FOREIGN KEY (AuthorID) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
-- SeriesEntries
ALTER TABLE SeriesEntries ADD CONSTRAINT FK_SeriesEntries_Series FOREIGN KEY (SeriesID) REFERENCES Series(SeriesID) ON DELETE CASCADE;
ALTER TABLE SeriesEntries ADD CONSTRAINT FK_SeriesEntries_Stories FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
-- Stories
ALTER TABLE Stories ADD CONSTRAINT FK_Stories_AspNetUsers FOREIGN KEY (AuthorID) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
-- Chapters
ALTER TABLE Chapters ADD CONSTRAINT FK_Chapters_Stories FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
ALTER TABLE Chapters ADD CONSTRAINT FK_Chapters_PrimaryVersion FOREIGN KEY (PrimaryContentID) REFERENCES ChapterContents(ChapterContentID) ON DELETE NO ACTION;
-- ChapterVersions
ALTER TABLE ChapterContents ADD CONSTRAINT FK_ChapterVersions_Chapter FOREIGN KEY (ChapterID) REFERENCES Chapters(ChapterID) ON DELETE CASCADE;
ALTER TABLE ChapterContents ADD CONSTRAINT FK_ChapterVersions_User FOREIGN KEY (AuthorID) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
-- Tags
-- FK_Tags_TagType is in table def
-- FK_Tags_ParentTag is in table def
-- StoryTags
-- FK_StoryTags_Stories is in table def
-- FK_StoryTags_Tags is in table def
-- CoAuthors
ALTER TABLE CoAuthors ADD CONSTRAINT FK_StoryCoAuthors_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
ALTER TABLE CoAuthors ADD CONSTRAINT FK_StoryCoAuthors_User FOREIGN KEY (CoAuthorUserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
-- SettingDetails (formerly AUs / StoryCharacters)
-- FK_SettingDetails_Story is in table def
-- FK_SettingDetails_Tag is in table def
GO
-- StoryCharacterRelationships
-- FK_StoryCharacterRelationships_Story is in table def
-- DailyStoryStats
ALTER TABLE DailyStoryStats ADD CONSTRAINT FK_DailyStoryStats_Stories FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
-- CommunitySpotlight
ALTER TABLE CommunitySpotlight ADD CONSTRAINT FK_CommunitySpotlight_Stories FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
ALTER TABLE CommunitySpotlight ADD CONSTRAINT FK_CommunitySpotlight_Users FOREIGN KEY (SponsoringUserID) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
-- UserStoryInteractions
ALTER TABLE UserStoryInteractions ADD CONSTRAINT FK_UserStoryInteractions_Users FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
ALTER TABLE UserStoryInteractions ADD CONSTRAINT FK_UserStoryInteractions_Stories FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
ALTER TABLE UserStoryInteractions ADD CONSTRAINT FK_UserStoryInteractions_SourceRecommendation FOREIGN KEY (SourceRecommendationID) REFERENCES Recommendations(RecommendationID) ON DELETE NO ACTION;
-- UserChapterInteractions
ALTER TABLE UserChapterInteractions ADD CONSTRAINT FK_UserChapterInteractions_Users FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
ALTER TABLE UserChapterInteractions ADD CONSTRAINT FK_UserChapterInteractions_Chapters FOREIGN KEY (ChapterID) REFERENCES Chapters(ChapterID) ON DELETE CASCADE;
-- Comments
ALTER TABLE BaseComments ADD CONSTRAINT FK_Comments_Users FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
ALTER TABLE BaseComments ADD CONSTRAINT FK_Comments_ParentComment FOREIGN KEY (ParentCommentID) REFERENCES BaseComments(CommentID) ON DELETE NO ACTION; -- NO ACTION (Self-referencing)
-- CommentLikes
-- FK_CommentLikes_Users is in table def
-- FK_CommentLikes_Comments is in table def
-- RecommendationLikes
ALTER TABLE RecommendationSuccesses ADD CONSTRAINT FK_RecommendationLikes_Users FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
ALTER TABLE RecommendationSuccesses ADD CONSTRAINT FK_RecommendationLikes_Recommendations FOREIGN KEY (RecommendationID) REFERENCES Recommendations(RecommendationID) ON DELETE CASCADE;
-- UserLists
ALTER TABLE CustomLists ADD CONSTRAINT FK_UserLists_Users FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
-- UserListEntries
ALTER TABLE CustomListEntries ADD CONSTRAINT FK_UserListEntries_UserLists FOREIGN KEY (ListID) REFERENCES CustomLists(ListID) ON DELETE CASCADE;
ALTER TABLE CustomListEntries ADD CONSTRAINT FK_UserListEntries_Stories FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
-- StoryRelationships
ALTER TABLE StoryRelationships ADD CONSTRAINT FK_StoryRelationships_ParentStory FOREIGN KEY (SourceStoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
ALTER TABLE StoryRelationships ADD CONSTRAINT FK_StoryRelationships_ChildStory FOREIGN KEY (TargetStoryID) REFERENCES Stories(StoryID) ON DELETE NO ACTION; -- Cannot be CASCADE
-- Groups
ALTER TABLE Groups ADD CONSTRAINT FK_Groups_Users_Creator FOREIGN KEY (CreatorID) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
-- GroupMembers
ALTER TABLE GroupMembers ADD CONSTRAINT FK_GroupMembers_Users FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
ALTER TABLE GroupMembers ADD CONSTRAINT FK_GroupMembers_Groups FOREIGN KEY (GroupID) REFERENCES Groups(GroupID) ON DELETE CASCADE;
-- GroupStories
ALTER TABLE GroupStories ADD CONSTRAINT FK_GroupStories_Groups FOREIGN KEY (GroupID) REFERENCES Groups(GroupID) ON DELETE CASCADE;
ALTER TABLE GroupStories ADD CONSTRAINT FK_GroupStories_Stories FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
ALTER TABLE GroupStories ADD CONSTRAINT FK_GroupStories_Users_AddedBy FOREIGN KEY (AddedByUserID) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
-- BlogPosts
ALTER TABLE BlogPosts ADD CONSTRAINT FK_BlogPosts_Users FOREIGN KEY (AuthorID) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
ALTER TABLE BlogPosts ADD CONSTRAINT FK_BlogPosts_Stories FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE SET NULL;
ALTER TABLE BlogPosts ADD CONSTRAINT FK_BlogPosts_Groups FOREIGN KEY (GroupID) REFERENCES Groups(GroupID) ON DELETE SET NULL;
-- BlogPostLikes
ALTER TABLE BlogPostLikes ADD CONSTRAINT FK_BlogPostLikes_Users FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
ALTER TABLE BlogPostLikes ADD CONSTRAINT FK_BlogPostLikes_BlogPosts FOREIGN KEY (BlogPostID) REFERENCES BlogPosts(BlogPostID) ON DELETE CASCADE;
-- BlogPostComments
ALTER TABLE BlogPostComments ADD CONSTRAINT FK_BlogPostComments_BlogPost FOREIGN KEY (BlogPostID) REFERENCES BlogPosts(BlogPostID) ON DELETE CASCADE;
-- Reports
ALTER TABLE Reports ADD CONSTRAINT FK_Reports_ReporterUser FOREIGN KEY (ReporterUserID) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
ALTER TABLE Reports ADD CONSTRAINT FK_Reports_ModeratorUser FOREIGN KEY (ModeratorUserID) REFERENCES AspNetUsers(Id) ON DELETE NO ACTION;
-- Notifications
-- FKs defined in table
-- UserNotificationSettings
-- FKs defined in table
-- StoryBetaReaders
ALTER TABLE BetaReaders ADD CONSTRAINT FK_StoryBetaReaders_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
ALTER TABLE BetaReaders ADD CONSTRAINT FK_StoryBetaReaders_User FOREIGN KEY (BetaReaderUserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
-- StoryAcknowledgments
ALTER TABLE StoryAcknowledgments ADD CONSTRAINT FK_StoryAcknowledgments_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
ALTER TABLE StoryAcknowledgments ADD CONSTRAINT FK_StoryAcknowledgments_User FOREIGN KEY (AcknowledgedUserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
-- (FeatureContributions FKs removed 2026-07-18 — Feature 56 was cut.)
-- StoryImports
ALTER TABLE StoryImports ADD CONSTRAINT FK_StoryImports_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
-- Private Messaging
ALTER TABLE ConversationParticipants ADD CONSTRAINT FK_ConversationParticipants_Conversation FOREIGN KEY (ConversationID) REFERENCES Conversations(ConversationID) ON DELETE CASCADE;
ALTER TABLE ConversationParticipants ADD CONSTRAINT FK_ConversationParticipants_User FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
ALTER TABLE PrivateMessages ADD CONSTRAINT FK_PrivateMessages_Conversation FOREIGN KEY (ConversationID) REFERENCES Conversations(ConversationID) ON DELETE CASCADE;
ALTER TABLE PrivateMessages ADD CONSTRAINT FK_PrivateMessages_SenderUser FOREIGN KEY (SenderUserID) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
GO