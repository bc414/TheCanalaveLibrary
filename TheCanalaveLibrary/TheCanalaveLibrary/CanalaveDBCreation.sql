/*
================================================================================
 FanFiction Website Database Schema (MODIFIED FOR ASP.NET CORE IDENTITY)
 (Version 2 - Best Practice ON DELETE Policies)
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
-- 2. STORIES TABLE (MODIFIED)
-- This is your original table, now updated with the Slug and StoryStatusID
================================================================================
*/
CREATE TABLE Stories (
                         StoryID INT IDENTITY(1,1) PRIMARY KEY,
                         AuthorID NVARCHAR(450) NULL, -- <-- CHANGED: Made NULLABLE for ON DELETE SET NULL
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
CREATE TABLE Chapters (
    ChapterID INT IDENTITY(1,1) PRIMARY KEY,
    StoryID INT NOT NULL,
    ChapterNumber INT NOT NULL,
    Title NVARCHAR(255) NULL,
    PrimaryContentID INT NOT NULL,
    IsPublished BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_Story_ChapterNumber UNIQUE(StoryID, ChapterNumber)
);
GO

-- Table for chapter content versions - use for T/M variants and rewrites where original is desired to be preserved
CREATE TABLE ChapterContents (
    ChapterContentID INT IDENTITY(1,1) PRIMARY KEY,
    ChapterID INT NOT NULL,
    AuthorID NVARCHAR(450) NULL,
    VersionName NVARCHAR(100) NULL, -- special version names will allow custom formatting on the webpage
    TopAuthorsNote NVARCHAR(MAX) NULL,
    ChapterText NVARCHAR(MAX) NOT NULL,
    BottomAuthorsNote NVARCHAR(MAX) NULL,
    WordCount INT NOT NULL DEFAULT 0,
    ViewCount INT NOT NULL DEFAULT 0,
    Rating TINYINT NULL,
    PublishDate DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    OriginalPublishDate DATETIME2(7) NULL,
);
GO


/* ================================================================================
-- 1. TAG TYPES LOOKUP TABLE
-- This table replaces the 'TagType' CHECK constraint.
-- It's the "source of truth" for what tag types exist.
================================================================================
*/
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
CREATE TABLE Series (
    SeriesID INT IDENTITY(1,1) PRIMARY KEY,
    AuthorID NVARCHAR(450) NULL, -- <-- CHANGED: Made NULLABLE for ON DELETE SET NULL
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
CREATE TABLE SeriesEntries (
    SeriesID INT NOT NULL,
    StoryID INT NOT NULL,
    OrderIndex INT NOT NULL,
    CONSTRAINT PK_SeriesEntries PRIMARY KEY (SeriesID, StoryID)
);
GO

-- NEW TABLE for co-author authorization
CREATE TABLE CoAuthors (
    StoryID INT NOT NULL,
    CoAuthorUserID NVARCHAR(450) NOT NULL, 
    DateAdded DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_StoryCoAuthors PRIMARY KEY (StoryID, CoAuthorUserID)
);
GO

-- Table for beta reader authorization
CREATE TABLE BetaReaders (
    StoryID INT NOT NULL,
    BetaReaderUserID NVARCHAR(450) NOT NULL,
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
CREATE TABLE StoryAcknowledgments (
                                      StoryID INT NOT NULL,
                                      AcknowledgedUserID NVARCHAR(450) NOT NULL,

    /* -- NEW: Foreign key to the AcknowledgmentRoles table. -- */
                                      AcknowledgmentRoleID TINYINT NOT NULL,

                                      DateAcknowledged DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),

    /* -- NEW: The primary key now uses the ID instead of the string. -- */
    /* -- This still allows a user to be acknowledged for multiple roles on the same story. -- */
                                      CONSTRAINT PK_StoryAcknowledgments PRIMARY KEY (StoryID, AcknowledgedUserID, AcknowledgmentRoleID),

    /* -- NEW: Foreign key constraint to ensure data integrity. -- */
                                      CONSTRAINT FK_StoryAcknowledgments_Role FOREIGN KEY (AcknowledgmentRoleID) REFERENCES AcknowledgmentRoles(AcknowledgmentRoleID),

    
    CONSTRAINT FK_StoryAcknowledgments_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE,
    CONSTRAINT FK_StoryAcknowledgments_User FOREIGN KEY (AcknowledgedUserID) REFERENCES AspNetUsers(Id)
    
);
GO

/* ================================================================================
-- 1. REPORT REASONS LOOKUP TABLE
-- Replaces the 'Reason' CHECK constraint.
================================================================================
*/
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
CREATE TABLE Reports (
                         ReportID INT IDENTITY(1,1) PRIMARY KEY,
                         ReporterUserID NVARCHAR(450) NULL, -- <-- CHANGED: Made NULLABLE for ON DELETE SET NULL

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

                         ModeratorUserID NVARCHAR(450) NULL,
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
CREATE TABLE UserStoryInteractions (
                                       UserID NVARCHAR(450) NOT NULL,
                                       StoryID INT NOT NULL,

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

                                       CONSTRAINT PK_UserStoryInteractions PRIMARY KEY (UserID, StoryID),
                                       CONSTRAINT FK_UserStoryInteractions_User FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
                                       CONSTRAINT FK_UserStoryInteractions_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE,
    CONSTRAINT FK_UserStoryInteractions_SourceRecommendation FOREIGN KEY (SourceRecommendationID) REFERENCES Recommendations(RecommendationID) ON DELETE SET NULL
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
                                    UserID NVARCHAR(450) NOT NULL,
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
                                   UserID NVARCHAR(450) NOT NULL,
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
                                          UserID NVARCHAR(450) NOT NULL,

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
                               NotificationID INT IDENTITY(1,1) PRIMARY KEY,
                               RecipientUserID NVARCHAR(450) NOT NULL,

    /* NEW: Replaces 'EventType' string with the fast tinyint key */
                               NotificationTypeID TINYINT NOT NULL,

                               SourceUserID NVARCHAR(450) NULL, -- The user who triggered the event (e.g., left the comment)

    /* The ID of the story, comment, user, etc. that is related to this event */
                               RelatedEntityID INT NOT NULL,

                               IsRead BIT NOT NULL DEFAULT 0,
                               DateCreated DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),

                               CONSTRAINT FK_Notifications_RecipientUser FOREIGN KEY (RecipientUserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
                               CONSTRAINT FK_Notifications_SourceUser FOREIGN KEY (SourceUserID) REFERENCES AspNetUsers(Id) ON DELETE SET NULL,

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
                            UserID NVARCHAR(450) NOT NULL,
                            BadgeKey NVARCHAR(50) NOT NULL, -- Renamed from BadgeID
                            DateEarned DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                            DisplayOrder INT NOT NULL DEFAULT 0,
                            CONSTRAINT PK_UserBadges PRIMARY KEY (UserID, BadgeKey),
                            CONSTRAINT FK_UserBadges_User FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
                            CONSTRAINT FK_UserBadges_Badge FOREIGN KEY (BadgeKey) REFERENCES Badges(BadgeKey) ON DELETE CASCADE
);
GO

CREATE TABLE UserStats (
                           UserID NVARCHAR(450) PRIMARY KEY,

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
    FeatureContributions INT NOT NULL DEFAULT 0,

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
                      TagID INT IDENTITY(1,1) PRIMARY KEY,
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
                                 StoryCharacterID INT IDENTITY(1,1) PRIMARY KEY,
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
-- Add an index for validation: OC fields can only be used if IsOC = 1
-- UPDATED: Removed OC_SpriteURL from this check
CREATE NONCLUSTERED INDEX IX_StoryCharacters_OCCheck
    ON StoryCharacters(IsOC)
    WHERE IsOC = 0 AND (OC_Name IS NOT NULL OR OC_Bio IS NOT NULL);
GO -- (Your C# logic should enforce this, but this helps queries)


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
                                             StoryCharacterRelationshipID INT IDENTITY(1,1) PRIMARY KEY,
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
CREATE TABLE DailyStoryStats (
    StoryID INT NOT NULL,
    StatDate DATE NOT NULL,
    Views INT NOT NULL DEFAULT 0,
    Favorites INT NOT NULL DEFAULT 0,
    CONSTRAINT PK_DailyStoryStats PRIMARY KEY (StoryID, StatDate)
);
GO

-- Table for the Community Spotlight feature
CREATE TABLE CommunitySpotlight (
    SpotlightID INT IDENTITY(1,1) PRIMARY KEY,
    StoryID INT NOT NULL,
    SponsoringUserID NVARCHAR(450) NULL, -- <-- CHANGED: Made NULLABLE for ON DELETE SET NULL
    SponsorComment NVARCHAR(280) NULL,
    StartDate DATETIME2(7) NOT NULL,
    EndDate DATETIME2(7) NOT NULL,
    PaymentID NVARCHAR(255) NULL,
    DateCreated DATETIME2(7) NOT NULL DEFAULT GETUTCDATE()
);
GO

-- Table for user interactions with chapters (read status, progress)
CREATE TABLE UserChapterInteractions (
    UserID NVARCHAR(450) NOT NULL,
    ChapterID INT NOT NULL,
    ReadProgress FLOAT NOT NULL DEFAULT 0,
    LastInteractionDate DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_UserChapterInteractions PRIMARY KEY (UserID, ChapterID)
);
GO

/*
================================================================================
 Phase 2: Comment Tables (Replaced Polymorphic Model)
================================================================================
*/

-- 1. BaseComments: Holds all common data for all comment types
CREATE TABLE BaseComments (
                              CommentID INT IDENTITY(1,1) PRIMARY KEY,
                              UserID NVARCHAR(450) NULL, -- Nullable for ON DELETE SET NULL
                              ParentCommentID INT NULL,
                              CommentText NVARCHAR(MAX) NOT NULL,
                              LikeCount INT NOT NULL DEFAULT 0,
                              DatePosted DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                              ActiveReportCount INT NOT NULL DEFAULT 0,
                              CommentType NVARCHAR(50) NOT NULL, -- Discriminator for TPH/TPT in EF Core

                              CONSTRAINT CK_BaseComments_CommentType CHECK (CommentType IN ('Chapter', 'UserProfile', 'Group'))
);
GO

-- 2. ChapterComments: Links a BaseComment to a Chapter
CREATE TABLE ChapterComments (
                                 CommentID INT PRIMARY KEY, -- This is BOTH a PK and a FK
                                 ChapterID INT NOT NULL,
                                 CONSTRAINT FK_ChapterComments_BaseComment FOREIGN KEY (CommentID) REFERENCES BaseComments(CommentID) ON DELETE CASCADE,
                                 CONSTRAINT FK_ChapterComments_Chapter FOREIGN KEY (ChapterID) REFERENCES Chapters(ChapterID) ON DELETE CASCADE
);
GO

-- 3. UserProfileComments: Links a BaseComment to a User's Profile
CREATE TABLE UserProfileComments (
                                     CommentID INT PRIMARY KEY, -- This is BOTH a PK and a FK
                                     ProfileUserID NVARCHAR(450) NOT NULL,
                                     CONSTRAINT FK_UserProfileComments_BaseComment FOREIGN KEY (CommentID) REFERENCES BaseComments(CommentID) ON DELETE CASCADE,
                                     CONSTRAINT FK_UserProfileComments_User FOREIGN KEY (ProfileUserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
);
GO

-- 4. GroupComments: Links a BaseComment to a Group
CREATE TABLE GroupComments (
                               CommentID INT PRIMARY KEY, -- This is BOTH a PK and a FK
                               GroupID INT NOT NULL,
                               CONSTRAINT FK_GroupComments_BaseComment FOREIGN KEY (CommentID) REFERENCES BaseComments(CommentID) ON DELETE CASCADE,
                               CONSTRAINT FK_GroupComments_Group FOREIGN KEY (GroupID) REFERENCES Groups(GroupID) ON DELETE CASCADE
);
GO

-- 5. BlogPostComments: Links a BaseComment to a BlogPost
CREATE TABLE BlogPostComments (
                               CommentID INT PRIMARY KEY, -- This is BOTH a PK and a FK
                               BlogPostID INT NOT NULL,
                               CONSTRAINT FK_GroupComments_BaseComment FOREIGN KEY (CommentID) REFERENCES BaseComments(CommentID) ON DELETE CASCADE,
                               CONSTRAINT FK_GroupComments_BlogPost FOREIGN KEY (BlogPostID) REFERENCES BlogPosts(BlogPostID) ON DELETE CASCADE
);
GO

-- 6. CommentLikes: Now cleanly points to the BaseComments table
CREATE TABLE CommentLikes (
                              UserID NVARCHAR(450) NOT NULL,
                              CommentID INT NOT NULL,
                              DateLiked DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                              CONSTRAINT PK_CommentLikes PRIMARY KEY (UserID, CommentID),
                              CONSTRAINT FK_CommentLikes_User FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
                              CONSTRAINT FK_CommentLikes_Comment FOREIGN KEY (CommentID) REFERENCES BaseComments(CommentID) ON DELETE CASCADE
);
GO

-- Table for story recommendations/reviews
CREATE TABLE Recommendations (
    RecommendationID INT IDENTITY(1,1) PRIMARY KEY,
    StoryID INT NOT NULL,
    UserID NVARCHAR(450) NULL, -- <-- CHANGED: Made NULLABLE for ON DELETE SET NULL
    Text NVARCHAR(MAX) NOT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'PendingApproval',
    LikeCount INT NOT NULL DEFAULT 0,
    DatePosted DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    ActiveReportCount INT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_User_Story_Recommendation UNIQUE(UserID, StoryID),
    CONSTRAINT CK_Recommendations_Status CHECK (Status IN ('PendingApproval', 'Approved', 'Rejected', 'UnderReview'))
);
GO

-- Table for recommendation likes; use "Successful Recs" on the web page
CREATE TABLE RecommendationSuccesses (
    UserID NVARCHAR(450) NOT NULL,
    RecommendationID INT NOT NULL,
    DateRecorded DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_RecommendationLikes PRIMARY KEY (UserID, RecommendationID)
);
GO

-- Table for user-created lists (Favorites, Read Later, etc.)
CREATE TABLE CustomLists (
    ListID INT IDENTITY(1,1) PRIMARY KEY,
    UserID NVARCHAR(450) NOT NULL,
    ListName NVARCHAR(100) NOT NULL,
    IsPublic BIT NOT NULL DEFAULT 0,
    DateCreated DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT UQ_User_ListName UNIQUE(UserID, ListName)
);
GO

-- Junction table for stories in a user list
CREATE TABLE CustomListEntries (
    ListID INT NOT NULL,
    StoryID INT NOT NULL,
    DateAdded DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_UserListEntries PRIMARY KEY (ListID, StoryID)
);
GO

-- Junction table for user-to-user follows
CREATE TABLE FavoriteAuthors (
    UserID NVARCHAR(450) NOT NULL,
    AuthorID NVARCHAR(450) NOT NULL,
    DateFavorited DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_UserFollows PRIMARY KEY (UserID, AuthorID),
);
GO

-- Junction table for user-to-user follows
CREATE TABLE FollowedAuthors (
                             UserID NVARCHAR(450) NOT NULL,
                             AuthorID NVARCHAR(450) NOT NULL,
                             DateFollowed DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                             CONSTRAINT PK_UserFollows PRIMARY KEY (UserID, AuthorID)
);
GO

-- Junction table for story-to-story relationships
CREATE TABLE StoryRelationships (
    ParentStoryID INT NOT NULL,
    ChildStoryID INT NOT NULL,
    RelationshipType NVARCHAR(50) NOT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Approved',
    DateCreated DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_StoryRelationships PRIMARY KEY (ParentStoryID, ChildStoryID, RelationshipType),
    CONSTRAINT CK_StoryRelationships_Type CHECK (RelationshipType IN ('InspiredBy', 'TranslationOf', 'RemasterOf', 'CompanionPiece', 'Prequel', 'Sequel')),
    CONSTRAINT CK_StoryRelationships_Status CHECK (Status IN ('Pending', 'Approved', 'Rejected'))
);
GO

-- Table for author-curated recommendations
CREATE TABLE AuthorCuratedRecommendations (
    StoryID INT NOT NULL,
    RecommendationID INT NOT NULL,
    DateCurated DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_AuthorCuratedRecommendations PRIMARY KEY (StoryID, RecommendationID)
);
GO

-- Table for groups
CREATE TABLE Groups (
    GroupID INT IDENTITY(1,1) PRIMARY KEY,
    CreatorID NVARCHAR(450) NULL, -- <-- CHANGED: Made NULLABLE for ON DELETE SET NULL
    GroupName NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(1000) NULL,
    DateCreated DATETIME2(7) NOT NULL DEFAULT GETUTCDATE()
);
GO

-- Junction table for group membership
CREATE TABLE GroupMembers (
    UserID NVARCHAR(450) NOT NULL,
    GroupID INT NOT NULL,
    Role NVARCHAR(50) NOT NULL DEFAULT 'Member',
    DateJoined DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_GroupMembers PRIMARY KEY (UserID, GroupID),
    CONSTRAINT CK_GroupMembers_Role CHECK (Role IN ('Admin', 'Moderator', 'Member'))
);
GO

-- Junction table for stories in a group
CREATE TABLE GroupStories (
    GroupID INT NOT NULL,
    StoryID INT NOT NULL,
    AddedByUserID NVARCHAR(450) NULL, -- <-- CHANGED: Made NULLABLE for ON DELETE SET NULL
    DateAdded DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_GroupStories PRIMARY KEY (GroupID, StoryID)
);
GO

-- Table for blog posts
CREATE TABLE BlogPosts (
    BlogPostID INT IDENTITY(1,1) PRIMARY KEY,
    AuthorID NVARCHAR(450) NULL, -- <-- CHANGED: Made NULLABLE for ON DELETE SET NULL
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
CREATE TABLE BlogPostLikes (
    UserID NVARCHAR(450) NOT NULL,
    BlogPostID INT NOT NULL,
    DateLiked DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_BlogPostLikes PRIMARY KEY (UserID, BlogPostID)
);
GO

-- Table to track official admin attributions for feature suggestions
CREATE TABLE FeatureContributions (
    ContributionID INT IDENTITY(1,1) PRIMARY KEY,
    UserID NVARCHAR(450) NULL, -- <-- CHANGED: Made NULLABLE for ON DELETE SET NULL
    CommentID INT NULL,
    BlogPostID INT NULL,
    FeatureName NVARCHAR(255) NOT NULL,
    DateAwarded DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT CK_FeatureContribution_Source CHECK (
        (CASE WHEN CommentID IS NOT NULL THEN 1 ELSE 0 END +
         CASE WHEN BlogPostID IS NOT NULL THEN 1 ELSE 0 
    END) = 1
    )
);
GO

-- Table for story import verification
CREATE TABLE StoryImports (
    ImportID INT IDENTITY(1,1) PRIMARY KEY,
    StoryID INT NOT NULL,
    SourcePlatform NVARCHAR(50) NOT NULL,
    SourceURL NVARCHAR(500) NOT NULL,
    VerificationStatus NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    DateImported DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT UQ_StoryImports_StoryID UNIQUE (StoryID),
    CONSTRAINT CK_StoryImports_Status CHECK (VerificationStatus IN ('Pending', 'Verified', 'Rejected'))
);
GO

-- Private Messaging
CREATE TABLE Conversations (
    ConversationID INT IDENTITY(1,1) PRIMARY KEY,
    Subject NVARCHAR(255) NULL,
    DateCreated DATETIME2(7) NOT NULL DEFAULT GETUTCDATE()
);
GO

CREATE TABLE ConversationParticipants (
    ConversationID INT NOT NULL,
    UserID NVARCHAR(450) NOT NULL,
    LastReadTimestamp DATETIME2(7) NULL,
    IsArchived BIT NOT NULL DEFAULT 0,
    CONSTRAINT PK_ConversationParticipants PRIMARY KEY (ConversationID, UserID)
);
GO

CREATE TABLE PrivateMessages (
    MessageID INT IDENTITY(1,1) PRIMARY KEY,
    ConversationID INT NOT NULL,
    SenderUserID NVARCHAR(450) NULL, -- <-- CHANGED: Made NULLABLE for ON DELETE SET NULL
    MessageText NVARCHAR(MAX) NOT NULL,
    DateSent DATETIME2(7) NOT NULL DEFAULT GETUTCDATE()
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
ALTER TABLE Tags ADD CONSTRAINT FK_Tags_ParentTag FOREIGN KEY (ParentTagID) REFERENCES Tags(TagID) ON DELETE SET NULL;
-- StoryTags
ALTER TABLE StoryTags ADD CONSTRAINT FK_StoryTags_Stories FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
ALTER TABLE StoryTags ADD CONSTRAINT FK_StoryTags_Tags FOREIGN KEY (TagID) REFERENCES Tags(TagID) ON DELETE CASCADE;
-- StoryCoAuthors
ALTER TABLE CoAuthors ADD CONSTRAINT FK_StoryCoAuthors_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
ALTER TABLE CoAuthors ADD CONSTRAINT FK_StoryCoAuthors_User FOREIGN KEY (CoAuthorUserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
-- StoryCharacters
ALTER TABLE SettingDetails ADD CONSTRAINT FK_StoryCharacters_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
ALTER TABLE SettingDetails ADD CONSTRAINT FK_StoryCharacters_Tag FOREIGN KEY (BaseTagID) REFERENCES Tags(TagID) ON DELETE CASCADE;
GO
-- StoryCharacterRelationships
ALTER TABLE StoryCharacterRelationships ADD CONSTRAINT FK_StoryCharacterRelationships_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
-- DailyStoryStats
ALTER TABLE DailyStoryStats ADD CONSTRAINT FK_DailyStoryStats_Stories FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
-- CommunitySpotlight
ALTER TABLE CommunitySpotlight ADD CONSTRAINT FK_CommunitySpotlight_Stories FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
ALTER TABLE CommunitySpotlight ADD CONSTRAINT FK_CommunitySpotlight_Users FOREIGN KEY (SponsoringUserID) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
-- UserStoryInteractions
ALTER TABLE UserStoryInteractions ADD CONSTRAINT FK_UserStoryInteractions_Users FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
ALTER TABLE UserStoryInteractions ADD CONSTRAINT FK_UserStoryInteractions_Stories FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
-- UserChapterInteractions
ALTER TABLE UserChapterInteractions ADD CONSTRAINT FK_UserChapterInteractions_Users FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
ALTER TABLE UserChapterInteractions ADD CONSTRAINT FK_UserChapterInteractions_Chapters FOREIGN KEY (ChapterID) REFERENCES Chapters(ChapterID) ON DELETE CASCADE;
-- Comments
ALTER TABLE BaseComments ADD CONSTRAINT FK_Comments_Users FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
ALTER TABLE BaseComments ADD CONSTRAINT FK_Comments_ParentComment FOREIGN KEY (ParentCommentID) REFERENCES BaseComments(CommentID) ON DELETE SET NULL;
-- CommentLikes
ALTER TABLE CommentLikes ADD CONSTRAINT FK_CommentLikes_Users FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
ALTER TABLE CommentLikes ADD CONSTRAINT FK_CommentLikes_Comments FOREIGN KEY (CommentID) REFERENCES BaseComments(CommentID) ON DELETE CASCADE;
-- Recommendations
ALTER TABLE Recommendations ADD CONSTRAINT FK_Recommendations_Stories FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
ALTER TABLE Recommendations ADD CONSTRAINT FK_Recommendations_Users FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
-- RecommendationLikes
ALTER TABLE RecommendationSuccesses ADD CONSTRAINT FK_RecommendationLikes_Users FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
ALTER TABLE RecommendationSuccesses ADD CONSTRAINT FK_RecommendationLikes_Recommendations FOREIGN KEY (RecommendationID) REFERENCES Recommendations(RecommendationID) ON DELETE CASCADE;
-- UserLists
ALTER TABLE CustomLists ADD CONSTRAINT FK_UserLists_Users FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
-- UserListEntries
ALTER TABLE CustomListEntries ADD CONSTRAINT FK_UserListEntries_UserLists FOREIGN KEY (ListID) REFERENCES CustomLists(ListID) ON DELETE CASCADE;
ALTER TABLE CustomListEntries ADD CONSTRAINT FK_UserListEntries_Stories FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
-- FollowedAuthors
ALTER TABLE FollowedAuthors ADD CONSTRAINT FK_UserFollows_Follower FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
ALTER TABLE FollowedAuthors ADD CONSTRAINT FK_UserFollows_Following FOREIGN KEY (AuthorID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
-- FavoriteAuthors
ALTER TABLE FavoriteAuthors ADD CONSTRAINT FK_FavoriteAuthors_User FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
ALTER TABLE FavoriteAuthors ADD CONSTRAINT FK_FavoriteAuthors_Author FOREIGN KEY (AuthorID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
-- StoryRelationships
ALTER TABLE StoryRelationships ADD CONSTRAINT FK_StoryRelationships_ParentStory FOREIGN KEY (ParentStoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
ALTER TABLE StoryRelationships ADD CONSTRAINT FK_StoryRelationships_ChildStory FOREIGN KEY (ChildStoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE; -- <-- NOTE: Changed to NO ACTION (or CASCADE)
-- AuthorCuratedRecommendations
ALTER TABLE AuthorCuratedRecommendations ADD CONSTRAINT FK_AuthorCuratedRecommendations_Stories FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
ALTER TABLE AuthorCuratedRecommendations ADD CONSTRAINT FK_AuthorCuratedRecommendations_Recommendations FOREIGN KEY (RecommendationID) REFERENCES Recommendations(RecommendationID) ON DELETE CASCADE;
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
-- Reports
ALTER TABLE Reports ADD CONSTRAINT FK_Reports_ReporterUser FOREIGN KEY (ReporterUserID) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
ALTER TABLE Reports ADD CONSTRAINT FK_Reports_ModeratorUser FOREIGN KEY (ModeratorUserID) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
-- Notifications
ALTER TABLE Notifications ADD CONSTRAINT FK_Notifications_RecipientUser FOREIGN KEY (RecipientUserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
ALTER TABLE Notifications ADD CONSTRAINT FK_Notifications_SourceUser FOREIGN KEY (SourceUserID) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
ALTER TABLE Notifications ADD CONSTRAINT FK_Notifications_NotificationTypes FOREIGN KEY (NotificationTypeID) REFERENCES NotificationTypes(NotificationTypeID) ON DELETE CASCADE;
-- UserNotificationSettings
ALTER TABLE UserNotificationSettings ADD CONSTRAINT FK_UserNotificationSettings_Users FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
ALTER TABLE UserNotificationSettings ADD CONSTRAINT FK_UserNotificationSettings_NotificationTypes FOREIGN KEY (NotificationTypeID) REFERENCES NotificationTypes(NotificationTypeID) ON DELETE CASCADE;
-- StoryBetaReaders
ALTER TABLE BetaReaders ADD CONSTRAINT FK_StoryBetaReaders_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
ALTER TABLE BetaReaders ADD CONSTRAINT FK_StoryBetaReaders_User FOREIGN KEY (BetaReaderUserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE; 
-- StoryAcknowledgments
ALTER TABLE StoryAcknowledgments ADD CONSTRAINT FK_StoryAcknowledgments_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
ALTER TABLE StoryAcknowledgments ADD CONSTRAINT FK_StoryAcknowledgments_User FOREIGN KEY (AcknowledgedUserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
-- FeatureContributions
ALTER TABLE FeatureContributions ADD CONSTRAINT FK_FeatureContributions_User FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
ALTER TABLE FeatureContributions ADD CONSTRAINT FK_FeatureContributions_Comment FOREIGN KEY (CommentID) REFERENCES BaseComments(CommentID) ON DELETE SET NULL;
ALTER TABLE FeatureContributions ADD CONSTRAINT FK_FeatureContributions_BlogPost FOREIGN KEY (BlogPostID) REFERENCES BlogPosts(BlogPostID) ON DELETE SET NULL;
-- StoryImports
ALTER TABLE StoryImports ADD CONSTRAINT FK_StoryImports_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
-- Private Messaging
ALTER TABLE ConversationParticipants ADD CONSTRAINT FK_ConversationParticipants_Conversation FOREIGN KEY (ConversationID) REFERENCES Conversations(ConversationID) ON DELETE CASCADE;
ALTER TABLE ConversationParticipants ADD CONSTRAINT FK_ConversationParticipants_User FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
ALTER TABLE PrivateMessages ADD CONSTRAINT FK_PrivateMessages_Conversation FOREIGN KEY (ConversationID) REFERENCES Conversations(ConversationID) ON DELETE CASCADE;
ALTER TABLE PrivateMessages ADD CONSTRAINT FK_PrivateMessages_SenderUser FOREIGN KEY (SenderUserID) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
GO


/*
================================================================================
 Phase 4: Create Indexes for Performance
================================================================================
*/
CREATE NONCLUSTERED INDEX IX_Stories_AuthorID ON Stories(AuthorID);
CREATE NONCLUSTERED INDEX IX_Comments_UserID ON BaseComments(UserID);
CREATE NONCLUSTERED INDEX IX_Recommendations_StoryID ON Recommendations(StoryID);

CREATE NONCLUSTERED INDEX IX_BlogPosts_AuthorID ON BlogPosts(AuthorID);
CREATE NONCLUSTERED INDEX IX_BlogPosts_StoryID ON BlogPosts(StoryID) WHERE StoryID IS NOT NULL;
CREATE NONCLUSTERED INDEX IX_UserChapterInteractions_ChapterID ON UserChapterInteractions(ChapterID);
CREATE NONCLUSTERED INDEX IX_CommunitySpotlight_Dates ON CommunitySpotlight(StartDate, EndDate);
CREATE NONCLUSTERED INDEX IX_Notifications_Recipient ON Notifications(RecipientUserID, IsRead, DateCreated DESC);
CREATE NONCLUSTERED INDEX IX_SeriesEntries_StoryID ON SeriesEntries(StoryID);
CREATE NONCLUSTERED INDEX IX_Tags_ParentTagID ON Tags(ParentTagID) WHERE ParentTagID IS NOT NULL;
CREATE NONCLUSTERED INDEX IX_PrivateMessages_ConversationID ON PrivateMessages(ConversationID, DateSent DESC);
CREATE NONCLUSTERED INDEX IX_UserSearchSettings_UserMode ON UserSearchSettings(UserID, SearchModeKey);
CREATE NONCLUSTERED INDEX IX_UserSearchEntityFilters_UserMode ON UserSearchSettings(UserID, SearchModeKey);
GO
GO
