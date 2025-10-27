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

-- Table for story metadata
CREATE TABLE Stories (
    StoryID INT IDENTITY(1,1) PRIMARY KEY,
    AuthorID NVARCHAR(450) NULL, -- <-- CHANGED: Made NULLABLE for ON DELETE SET NULL
    StoryTitle NVARCHAR(255) NOT NULL,
    ShortDescription NVARCHAR(500) NULL,
    LongDescription NVARCHAR(MAX) NULL,
    Rating NVARCHAR(10) NOT NULL DEFAULT 'T',
    Status NVARCHAR(30) NOT NULL DEFAULT 'Draft',
    PostApprovalStatus NVARCHAR(30) NULL,
    WordCount INT NOT NULL DEFAULT 0,
    ViewCount INT NOT NULL DEFAULT 0,
    CoverArtURL NVARCHAR(500) NULL,
  
    PublishedDate DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    LastUpdatedDate DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    OriginalPublishedDate DATE NULL,
    OriginalLastUpdatedDate DATE NULL, --If the story is marked as Complete, the UI will display "Completion Date" instead of "Last Updated Date"
    ActiveReportCount INT NOT NULL DEFAULT 0,
    CONSTRAINT CK_Stories_Rating CHECK (Rating IN ('E', 'T', 'M')),
    CONSTRAINT CK_Stories_Status CHECK (Status IN ('Draft', 'PendingApproval', 'In Progress', 'Completed', 'On Hiatus', 'Cancelled', 'Rewriting', 'Open Beta', 'Rejected'))
);
GO

-- Table for chapter metadata (container)
CREATE TABLE Chapters (
    ChapterID INT IDENTITY(1,1) PRIMARY KEY,
    StoryID INT NOT NULL,
    ChapterNumber INT NOT NULL,
    Title NVARCHAR(255) NULL,
    PrimaryContentID INT NOT NULL,
    IsPublished BIT NOT NULL DEFAULT 0,
    IsRewritten BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_Story_ChapterNumber UNIQUE(StoryID, ChapterNumber)
);
GO

-- Table for chapter content versions
CREATE TABLE ChapterContents (
    ChapterContentID INT IDENTITY(1,1) PRIMARY KEY,
    ChapterID INT NOT NULL,
    AuthorID NVARCHAR(450) NULL, -- <-- CHANGED: Made NULLABLE for ON DELETE SET NULL
    VersionName NVARCHAR(100) NULL,
    TopAuthorsNote NVARCHAR(MAX) NULL,
    ChapterText NVARCHAR(MAX) NOT NULL,
    BottomAuthorsNote NVARCHAR(MAX) NULL,
    WordCount INT NOT NULL DEFAULT 0,
    ViewCount INT NOT NULL DEFAULT 0,
    Rating NVARCHAR(10) NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Approved',
    PublishDate DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    OriginalPublishDate DATETIME2(7) NULL,
    CONSTRAINT CK_ChapterVersions_Rating CHECK (Rating IN ('E', 'T', 'M')),
    CONSTRAINT CK_ChapterVersions_Status CHECK (Status IN ('PendingApproval', 'Approved', 'Rejected'))
);
GO


-- Master list of all official tags
CREATE TABLE Tags (
    TagID INT IDENTITY(1,1) PRIMARY KEY,
    TagName NVARCHAR(100) NOT NULL UNIQUE,
    TagType NVARCHAR(50) NOT NULL,
    TagOrigin NVARCHAR(20) NOT NULL,
    Description NVARCHAR(1000) NULL,
    ParentTagID INT NULL,
    SpriteURL NVARCHAR(500) NULL,
    AnimatedSpriteURL NVARCHAR(500) NULL,
    CONSTRAINT CK_Tags_TagType CHECK (TagType IN ('Genre', 'Character', 'Relationship', 'Warning', 'CrossoverFandom', 'Universe')),
    CONSTRAINT CK_Tags_TagOrigin CHECK (TagOrigin IN ('Canon', 'Fanon'))
);
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
CREATE TABLE StoryCoAuthors (
    StoryID INT NOT NULL,
    CoAuthorUserID NVARCHAR(450) NOT NULL, 
    DateAdded DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_StoryCoAuthors PRIMARY KEY (StoryID, CoAuthorUserID)
);
GO

-- Table for beta reader authorization
CREATE TABLE StoryBetaReaders (
    StoryID INT NOT NULL,
    BetaReaderUserID NVARCHAR(450) NOT NULL,
    DateAdded DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_StoryBetaReaders PRIMARY KEY (StoryID, BetaReaderUserID)
);
GO

-- Table for public author acknowledgments
CREATE TABLE StoryAcknowledgments (
    StoryID INT NOT NULL,
    AcknowledgedUserID NVARCHAR(450) NOT NULL,
    Role NVARCHAR(100) NOT NULL,
    DateAcknowledged DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_StoryAcknowledgments PRIMARY KEY (StoryID, AcknowledgedUserID, Role)
);
GO

-- Table to track all user-submitted reports
CREATE TABLE Reports (
    ReportID INT IDENTITY(1,1) PRIMARY KEY,
    ReporterUserID NVARCHAR(450) NULL, -- <-- CHANGED: Made NULLABLE for ON DELETE SET NULL
    ReportedEntityType NVARCHAR(50) NOT NULL,
    ReportedEntityID INT NOT NULL,
    Reason NVARCHAR(100) NOT NULL,
    Notes NVARCHAR(1000) NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Open',
    ModeratorUserID NVARCHAR(450) NULL,
    ActionTaken NVARCHAR(255) NULL,
    DateReported DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    DateResolved DATETIME2(7) NULL,
    CONSTRAINT CK_ReportedEntityType CHECK (ReportedEntityType IN ('User', 'Story', 'Recommendation', 'Comment', 
    'BlogPost')),
    CONSTRAINT CK_ReportReason CHECK (Reason IN ('Spam', 'Hate Speech', 'Harassment', 'Illegal Content', 'Plagiarism', 'Other')),
    CONSTRAINT CK_ReportStatus CHECK (Status IN ('Open', 'UnderReview', 'Resolved-NoAction', 'Resolved-ActionTaken'))
);
GO

-- 1. Master list of all search modes (Admin-defined)
CREATE TABLE SearchModes (
                             SearchModeID NVARCHAR(50) PRIMARY KEY,
                             Name NVARCHAR(100) NOT NULL,
                             Description NVARCHAR(500) NULL,
                             SortOrder INT NOT NULL DEFAULT 0
);
GO

-- 2. Master list of all boolean filter criteria (Admin-defined)
CREATE TABLE FilterCriteria (
                                CriterionID NVARCHAR(50) PRIMARY KEY,
                                Name NVARCHAR(100) NOT NULL,
                                Description NVARCHAR(500) NULL,
                                SortOrder INT NOT NULL DEFAULT 0
);
GO

-- 3. Site-wide default settings (Admin-defined)
-- Stores the default checked state for every combination of mode and criteria.
CREATE TABLE DefaultSearchSettings (
                                       SearchModeID NVARCHAR(50) NOT NULL,
                                       CriterionID NVARCHAR(50) NOT NULL,
                                       DefaultValue BIT NOT NULL DEFAULT 0,
                                       CONSTRAINT PK_DefaultSearchSettings PRIMARY KEY (SearchModeID, CriterionID),
                                       CONSTRAINT FK_DefaultSearchSettings_SearchMode FOREIGN KEY (SearchModeID) REFERENCES SearchModes(SearchModeID) ON DELETE CASCADE,
                                       CONSTRAINT FK_DefaultSearchSettings_FilterCriteria FOREIGN KEY (CriterionID) REFERENCES FilterCriteria(CriterionID) ON DELETE CASCADE
);
GO

-- 4. User's saved overrides (Sparse table)
-- Only stores rows where a user's preference *differs* from the site-wide default.
CREATE TABLE UserSearchSettings (
                                    UserID NVARCHAR(450) NOT NULL,
                                    SearchModeID NVARCHAR(50) NOT NULL,
                                    CriterionID NVARCHAR(50) NOT NULL,
                                    UserValue BIT NOT NULL,
                                    CONSTRAINT PK_UserSearchSettings PRIMARY KEY (UserID, SearchModeID, CriterionID),
                                    CONSTRAINT FK_UserSearchSettings_User FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
                                    CONSTRAINT FK_UserSearchSettings_SearchMode FOREIGN KEY (SearchModeID) REFERENCES SearchModes(SearchModeID) ON DELETE CASCADE,
                                    CONSTRAINT FK_UserSearchSettings_FilterCriteria FOREIGN KEY (CriterionID) REFERENCES FilterCriteria(CriterionID) ON DELETE CASCADE
);
GO

-- 5. Admin-created search templates (Header)
CREATE TABLE SearchTemplates (
                                 TemplateID INT IDENTITY(1,1) PRIMARY KEY,
                                 SearchModeID NVARCHAR(50) NOT NULL,
                                 Name NVARCHAR(100) NOT NULL,
                                 Description NVARCHAR(500) NOT NULL,
                                 SortOrder INT NOT NULL DEFAULT 0,
                                 CONSTRAINT FK_SearchTemplates_SearchMode FOREIGN KEY (SearchModeID) REFERENCES SearchModes(SearchModeID) ON DELETE CASCADE
);
GO

-- 6. Settings for each template (Details)
CREATE TABLE SearchTemplateSettings (
                                        TemplateID INT NOT NULL,
                                        CriterionID NVARCHAR(50) NOT NULL,
                                        Value BIT NOT NULL,
                                        CONSTRAINT PK_SearchTemplateSettings PRIMARY KEY (TemplateID, CriterionID),
                                        CONSTRAINT FK_SearchTemplateSettings_Template FOREIGN KEY (TemplateID) REFERENCES SearchTemplates(TemplateID) ON DELETE CASCADE,
                                        CONSTRAINT FK_SearchTemplateSettings_FilterCriteria FOREIGN KEY (CriterionID) REFERENCES FilterCriteria(CriterionID) ON DELETE CASCADE
);
GO

-- 7. User's saved entity filters (e.g., lists, groups) (Sparse table)
CREATE TABLE UserSearchEntityFilters (
                                         UserSearchEntityFilterID INT IDENTITY(1,1) PRIMARY KEY,
                                         UserID NVARCHAR(450) NOT NULL,
                                         SearchModeID NVARCHAR(50) NOT NULL,
                                         FilterType NVARCHAR(50) NOT NULL,
                                         EntityID INT NOT NULL,
                                         Include BIT NOT NULL DEFAULT 1,
                                         CONSTRAINT FK_UserSearchEntityFilters_User FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
                                         CONSTRAINT FK_UserSearchEntityFilters_SearchMode FOREIGN KEY (SearchModeID) REFERENCES SearchModes(SearchModeID) ON DELETE CASCADE,
                                         CONSTRAINT CK_UserSearchEntityFilters_FilterType CHECK (FilterType IN ('PersonalList', 'PublicList', 'Group'))
);
GO

CREATE TABLE NotificationTypeDefaults (
                                          NotificationType NVARCHAR(50) PRIMARY KEY,
                                          Description NVARCHAR(255) NOT NULL,
                                          DefaultEmailEnabled BIT NOT NULL DEFAULT 0
);
GO

CREATE TABLE UserNotificationSettings (
                                          UserID NVARCHAR(450) NOT NULL,
                                          NotificationTypeID NVARCHAR(50) NOT NULL,
                                          EmailEnabled BIT NOT NULL,
                                          CONSTRAINT PK_UserNotificationSettings PRIMARY KEY (UserID, NotificationTypeID),
                                          CONSTRAINT FK_UserNotificationSettings_User FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
                                          CONSTRAINT FK_UserNotificationSettings_Type FOREIGN KEY (NotificationTypeID) REFERENCES NotificationTypeDefaults(NotificationType) ON DELETE CASCADE
);
GO

-- Table for notifications and user engagement feed
CREATE TABLE Notifications (
                               NotificationID INT IDENTITY(1,1) PRIMARY KEY,
                               RecipientUserID NVARCHAR(450) NOT NULL,
                               EventType NVARCHAR(50) NOT NULL,
                               SourceUserID NVARCHAR(450) NULL,
                               RelatedEntityID INT NOT NULL,
                               IsRead BIT NOT NULL DEFAULT 0,
                               DateCreated DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),

    -- Foreign keys from your original script
                               CONSTRAINT FK_Notifications_RecipientUser FOREIGN KEY (RecipientUserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
                               CONSTRAINT FK_Notifications_SourceUser FOREIGN KEY (SourceUserID) REFERENCES AspNetUsers(Id) ON DELETE SET NULL,

    -- The NEW foreign key, replacing the old CHECK constraint
                               CONSTRAINT FK_Notifications_NotificationType FOREIGN KEY (EventType) REFERENCES NotificationTypeDefaults(NotificationType) ON DELETE CASCADE
);
GO

CREATE TABLE Badges (
                        BadgeID NVARCHAR(50) PRIMARY KEY,
                        Name NVARCHAR(100) NOT NULL,
                        Description NVARCHAR(500) NULL,
                        IconURL NVARCHAR(500) NOT NULL,
                        SortOrder INT NOT NULL DEFAULT 0
);
GO

CREATE TABLE UserBadges (
                            UserID NVARCHAR(450) NOT NULL,
                            BadgeID NVARCHAR(50) NOT NULL,
                            DateEarned DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
                            DisplayOrder INT NOT NULL DEFAULT 0,
                            CONSTRAINT PK_UserBadges PRIMARY KEY (UserID, BadgeID),
                            CONSTRAINT FK_UserBadges_User FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
                            CONSTRAINT FK_UserBadges_Badge FOREIGN KEY (BadgeID) REFERENCES Badges(BadgeID) ON DELETE CASCADE
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

-- Junction table for Story <=> Official Tag relationship
CREATE TABLE StoryTags (
    StoryID INT NOT NULL,
    TagID INT NOT NULL,
    Priority NVARCHAR(50) NULL,
    CONSTRAINT PK_StoryTags PRIMARY KEY (StoryID, TagID),
    CONSTRAINT CK_StoryTags_TagRole CHECK (Priority IS NULL OR Priority IN ('Primary', 'Supporting'))
);
GO

-- Table for story-specific OC character details
CREATE TABLE OCs (
    OCID INT IDENTITY(1,1) PRIMARY KEY,
    StoryID INT NOT NULL,
    BaseTagID INT NOT NULL,
    Name NVARCHAR(100) NULL,
    Bio NVARCHAR(1000) NULL,
    SpriteURL NVARCHAR(500) NULL,
    Priority NVARCHAR(50) NULL,
    CONSTRAINT CK_StoryCharacters_TagRole CHECK (Priority IN ('Primary', 'Supporting')),
    CONSTRAINT UQ_StoryCharacters_StoryTag UNIQUE (StoryID, BaseTagID, Name)
);
GO

-- Table for defining relationships between characters within a story
CREATE TABLE StoryCharacterRelationships (
    StoryCharacterRelationshipID INT IDENTITY(1,1) PRIMARY KEY,
    StoryID INT NOT NULL,
    RelationshipType NVARCHAR(10) NOT NULL,
    Priority NVARCHAR(50) NULL,
    CONSTRAINT CK_StoryCharacterRelationships_Type CHECK (RelationshipType IN ('/', '&')),
    CONSTRAINT CK_StoryCharacterRelationships_Role CHECK (Priority IN ('Primary', 'Supporting'))
);
GO

-- Junction table to link characters to a relationship
CREATE TABLE StoryCharacterRelationshipMembers (
    StoryCharacterRelationshipID INT NOT NULL,
    StoryCharacterID INT NOT NULL,
    CONSTRAINT PK_StoryCharacterRelationshipMembers PRIMARY KEY (StoryCharacterRelationshipID, StoryCharacterID)
);
GO


-- Table for story-specific AU details
CREATE TABLE AUs (
    AUID INT IDENTITY(1,1) PRIMARY KEY,
    StoryID INT NOT NULL,
    BaseTagID INT NOT NULL,
    Name NVARCHAR(100) NULL,
    Description NVARCHAR(2000) NULL,
    CONSTRAINT UQ_StoryUniverses_StoryID UNIQUE (StoryID)
);
GO


-- Trigger to enforce Emphasis is only used for certain tag types
CREATE TRIGGER TR_StoryTags_EnforceEmphasisLogic -- Renamed to match your column
    ON StoryTags
    AFTER INSERT, UPDATE
    AS
BEGIN
    IF EXISTS (
        SELECT 1
        FROM inserted i
                 JOIN Tags t ON i.TagID = t.TagID
        WHERE i.Priority IS NOT NULL -- Assuming you renamed the column to Emphasis
          AND t.TagType NOT IN ('Character', 'Relationship', 'Genre')
    )
        BEGIN

            RAISERROR ('An Emphasis (e.g., Primary, Supporting) can only be assigned to tags of type Character, Relationship, or Genre.', 16, 1);
            -- No ROLLBACK needed. RAISERROR handles it.
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

-- Table for user interactions with stories (e.g., hiding from search).
CREATE TABLE UserStoryInteractions (
    UserID NVARCHAR(450) NOT NULL,
    StoryID INT NOT NULL,
    InteractionType NVARCHAR(50) NOT NULL,
    InteractionDate DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_UserStoryInteractions PRIMARY KEY (UserID, StoryID),
    CONSTRAINT CK_InteractionType CHECK (InteractionType IN ('InProgress', 'Completed', 'Ignored'))
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

-- Table for recommendation likes
CREATE TABLE RecommendationLikes (
    UserID NVARCHAR(450) NOT NULL,
    RecommendationID INT NOT NULL,
    DateLiked DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_RecommendationLikes PRIMARY KEY (UserID, RecommendationID)
);
GO

-- Table for user-created lists (Favorites, Read Later, etc.)
CREATE TABLE UserLists (
    ListID INT IDENTITY(1,1) PRIMARY KEY,
    UserID NVARCHAR(450) NOT NULL,
    ListName NVARCHAR(100) NOT NULL,
    IsPublic BIT NOT NULL DEFAULT 0,
    DateCreated DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT UQ_User_ListName UNIQUE(UserID, ListName)
);
GO

-- Junction table for stories in a user list
CREATE TABLE UserListEntries (
    ListID INT NOT NULL,
    StoryID INT NOT NULL,
    DateAdded DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_UserListEntries PRIMARY KEY (ListID, StoryID)
);
GO

-- Table to manage email alerts for specific user lists
CREATE TABLE ListAlerts (
    ListID INT NOT NULL PRIMARY KEY,
    IsEnabled BIT NOT NULL DEFAULT 1
);
GO

-- Junction table for user-to-user follows
CREATE TABLE UserFollows (
    FollowerUserID NVARCHAR(450) NOT NULL,
    FollowingUserID NVARCHAR(450) NOT NULL,
    FollowType NVARCHAR(50) NOT NULL,
    DateFollowed DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_UserFollows PRIMARY KEY (FollowerUserID, FollowingUserID, FollowType),
    CONSTRAINT CK_UserFollows_FollowType CHECK (FollowType IN ('Favorite', 'Track'))
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
ALTER TABLE StoryCoAuthors ADD CONSTRAINT FK_StoryCoAuthors_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
ALTER TABLE StoryCoAuthors ADD CONSTRAINT FK_StoryCoAuthors_User FOREIGN KEY (CoAuthorUserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
-- StoryCharacters
ALTER TABLE OCs ADD CONSTRAINT FK_StoryCharacters_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
ALTER TABLE OCs ADD CONSTRAINT FK_StoryCharacters_Tag FOREIGN KEY (BaseTagID) REFERENCES Tags(TagID) ON DELETE CASCADE;
GO
-- StoryCharacterRelationships
ALTER TABLE StoryCharacterRelationships ADD CONSTRAINT FK_StoryCharacterRelationships_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
-- StoryCharacterRelationshipMembers
ALTER TABLE StoryCharacterRelationshipMembers ADD CONSTRAINT FK_StoryCharacterRelationshipMembers_Relationship FOREIGN KEY (StoryCharacterRelationshipID) REFERENCES StoryCharacterRelationships(StoryCharacterRelationshipID) ON DELETE CASCADE;
ALTER TABLE StoryCharacterRelationshipMembers ADD CONSTRAINT FK_StoryCharacterRelationshipMembers_Character FOREIGN KEY (StoryCharacterID) REFERENCES OCs(OCID) ON DELETE CASCADE;
-- StoryUniverses
ALTER TABLE AUs ADD CONSTRAINT FK_StoryUniverses_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
ALTER TABLE AUs ADD CONSTRAINT FK_StoryUniverses_Tag FOREIGN KEY (BaseTagID) REFERENCES Tags(TagID) ON DELETE CASCADE;
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
ALTER TABLE RecommendationLikes ADD CONSTRAINT FK_RecommendationLikes_Users FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
ALTER TABLE RecommendationLikes ADD CONSTRAINT FK_RecommendationLikes_Recommendations FOREIGN KEY (RecommendationID) REFERENCES Recommendations(RecommendationID) ON DELETE CASCADE;
-- UserLists
ALTER TABLE UserLists ADD CONSTRAINT FK_UserLists_Users FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
-- UserListEntries
ALTER TABLE UserListEntries ADD CONSTRAINT FK_UserListEntries_UserLists FOREIGN KEY (ListID) REFERENCES UserLists(ListID) ON DELETE CASCADE;
ALTER TABLE UserListEntries ADD CONSTRAINT FK_UserListEntries_Stories FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
-- ListAlerts
ALTER TABLE ListAlerts ADD CONSTRAINT FK_ListAlerts_UserLists FOREIGN KEY (ListID) REFERENCES UserLists(ListID) ON DELETE CASCADE; -- <-- Fixed constraint name
-- UserFollows
ALTER TABLE UserFollows ADD CONSTRAINT FK_UserFollows_Follower FOREIGN KEY (FollowerUserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
ALTER TABLE UserFollows ADD CONSTRAINT FK_UserFollows_Following FOREIGN KEY (FollowingUserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
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
ALTER TABLE Notifications ADD CONSTRAINT FK_Notifications_NotificationTypes FOREIGN KEY (EventType) REFERENCES NotificationTypeDefaults(NotificationType) ON DELETE CASCADE;
-- UserNotificationSettings
ALTER TABLE UserNotificationSettings ADD CONSTRAINT FK_UserNotificationSettings_Users FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
ALTER TABLE UserNotificationSettings ADD CONSTRAINT FK_UserNotificationSettings_NotificationTypes FOREIGN KEY (NotificationTypeID) REFERENCES NotificationTypeDefaults(NotificationType) ON DELETE CASCADE;
-- StoryBetaReaders
ALTER TABLE StoryBetaReaders ADD CONSTRAINT FK_StoryBetaReaders_Story FOREIGN KEY (StoryID) REFERENCES Stories(StoryID) ON DELETE CASCADE;
ALTER TABLE StoryBetaReaders ADD CONSTRAINT FK_StoryBetaReaders_User FOREIGN KEY (BetaReaderUserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE; 
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
CREATE NONCLUSTERED INDEX IX_Comments_ParentEntity ON BaseComments(ParentEntityType, ParentEntityID);
CREATE NONCLUSTERED INDEX IX_Comments_UserID ON BaseComments(UserID);
CREATE NONCLUSTERED INDEX IX_Recommendations_StoryID ON Recommendations(StoryID);

CREATE NONCLUSTERED INDEX IX_BlogPosts_AuthorID ON BlogPosts(AuthorID);
CREATE NONCLUSTERED INDEX IX_BlogPosts_StoryID ON BlogPosts(StoryID) WHERE StoryID IS NOT NULL;
CREATE NONCLUSTERED INDEX IX_UserChapterInteractions_ChapterID ON UserChapterInteractions(ChapterID);
CREATE NONCLUSTERED INDEX IX_CommunitySpotlight_Dates ON CommunitySpotlight(StartDate, EndDate);
CREATE NONCLUSTERED INDEX IX_Reports_Status ON Reports(Status) WHERE Status = 'Open';
CREATE NONCLUSTERED INDEX IX_Notifications_Recipient ON Notifications(RecipientUserID, IsRead, DateCreated DESC);
CREATE NONCLUSTERED INDEX IX_SeriesEntries_StoryID ON SeriesEntries(StoryID);
CREATE NONCLUSTERED INDEX IX_Tags_ParentTagID ON Tags(ParentTagID) WHERE ParentTagID IS NOT NULL;
CREATE NONCLUSTERED INDEX IX_PrivateMessages_ConversationID ON PrivateMessages(ConversationID, DateSent DESC);
CREATE NONCLUSTERED INDEX IX_UserSearchSettings_UserMode ON UserSearchSettings(UserID, SearchModeID);
CREATE NONCLUSTERED INDEX IX_UserSearchEntityFilters_UserMode ON UserSearchEntityFilters(UserID, SearchModeID);
GO
GO
