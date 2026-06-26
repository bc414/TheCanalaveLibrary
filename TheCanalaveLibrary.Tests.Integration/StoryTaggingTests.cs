using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for Feature 12 structured story tagging (WU37 Phase 5).
/// Covers character routing (StoryCharacter vs StoryTag), OC + SettingDetail gates, ContentWarning
/// priority coercion, pairing persistence, GetStoryForEditAsync round-trip, and the discovery
/// character-filter branch in ApplyFilters.
/// Tier: Integration (Testcontainers Postgres).
/// </summary>
[Collection("Postgres")]
public class StoryTaggingTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;
    private int _settingTagId;
    private int _settingTagWithDetailsId;
    private int _genreTagId;
    private int _charTagId;
    private int _charTagAllowOcId;
    private int _cwTagId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId = await SeedUserAsync();
        await SeedBaseTagsAsync();
    }

    // ── Character routing ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateStory_WithCharacter_RoutesToStoryCharacters_NotStoryTags()
    {
        SetActiveUser(_authorId);
        int storyId = await CreateStoryViaServiceAsync(characters:
        [
            new StoryCharacterDto { CharacterTagId = _charTagId, Priority = TagPriority.Primary }
        ]);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        bool inCharacters = await db.Set<StoryCharacter>().AnyAsync(sc => sc.StoryId == storyId && sc.CharacterTagId == _charTagId);
        bool inFlatTags = await db.Set<StoryTag>().AnyAsync(st => st.StoryId == storyId && st.TagId == _charTagId);

        inCharacters.Should().BeTrue("character must be stored in StoryCharacters");
        inFlatTags.Should().BeFalse("character must NOT appear in flat StoryTags");
    }

    // ── ContentWarning coercion ────────────────────────────────────────────────

    [Fact]
    public async Task CreateStory_WithContentWarning_CoercesToPrimaryRegardlessOfSubmittedPriority()
    {
        SetActiveUser(_authorId);
        int storyId = await CreateStoryViaServiceAsync(extraFlatTags:
        [
            new StoryTagDTO { TagId = _cwTagId, TagTypeEnum = TagTypeEnum.ContentWarning, Priority = TagPriority.Supporting }
        ]);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        StoryTag? cwRow = await db.Set<StoryTag>().FirstOrDefaultAsync(st => st.StoryId == storyId && st.TagId == _cwTagId);
        cwRow.Should().NotBeNull();
        cwRow!.Priority.Should().Be(TagPriority.Primary, "server always coerces ContentWarning to Primary");
    }

    // ── OC gate ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateStory_WithOcDataOnNonOcTag_ThrowsStoryValidationException()
    {
        SetActiveUser(_authorId);
        Func<Task> act = () => CreateStoryViaServiceAsync(characters:
        [
            new StoryCharacterDto { CharacterTagId = _charTagId, IsOc = true, OcName = "My OC" }
        ]);

        await act.Should().ThrowAsync<StoryValidationException>("tag does not allow OC details");
    }

    [Fact]
    public async Task CreateStory_WithOcDataOnOcAllowedTag_Succeeds()
    {
        SetActiveUser(_authorId);
        Func<Task> act = () => CreateStoryViaServiceAsync(characters:
        [
            new StoryCharacterDto { CharacterTagId = _charTagAllowOcId, IsOc = true, OcName = "Kira", OcBio = "Friendly Pikachu OC" }
        ]);

        await act.Should().NotThrowAsync();
    }

    // ── SettingDetail gate ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateStory_WithSettingDetailOnNonDetailTag_ThrowsStoryValidationException()
    {
        SetActiveUser(_authorId);
        Func<Task> act = () => CreateStoryViaServiceAsync(settingDetails:
        [
            new SettingDetailDto { BaseTagId = _settingTagId, Name = "Custom Pallet" }
        ]);

        await act.Should().ThrowAsync<StoryValidationException>("tag does not allow setting details");
    }

    [Fact]
    public async Task CreateStory_WithSettingDetailOnAllowedTag_Succeeds()
    {
        SetActiveUser(_authorId);
        Func<Task> act = () => CreateStoryViaServiceAsync(
            flatSettingTagIds: [_settingTagWithDetailsId],
            settingDetails:
            [
                new SettingDetailDto { BaseTagId = _settingTagWithDetailsId, Name = "Custom Viridian", Description = "Alternate timeline" }
            ]);

        await act.Should().NotThrowAsync();
    }

    // ── Pairing constraints ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateStory_PairingWithFewerThanTwoMembers_ThrowsStoryValidationException()
    {
        SetActiveUser(_authorId);
        Func<Task> act = () => CreateStoryViaServiceAsync(
            characters: [new StoryCharacterDto { CharacterTagId = _charTagId }],
            pairings:
            [
                new StoryCharacterPairingDto
                {
                    PairingType = CharacterPairingType.Romantic,
                    MemberCharacterTagIds = [_charTagId]   // only 1 member
                }
            ]);

        await act.Should().ThrowAsync<StoryValidationException>("pairing needs ≥2 members");
    }

    [Fact]
    public async Task CreateStory_PairingMemberNotInStoryCharacters_ThrowsStoryValidationException()
    {
        SetActiveUser(_authorId);
        Func<Task> act = () => CreateStoryViaServiceAsync(
            characters: [new StoryCharacterDto { CharacterTagId = _charTagId }],
            pairings:
            [
                new StoryCharacterPairingDto
                {
                    PairingType = CharacterPairingType.Romantic,
                    MemberCharacterTagIds = [_charTagId, _charTagAllowOcId]  // _charTagAllowOcId not in StoryCharacters
                }
            ]);

        await act.Should().ThrowAsync<StoryValidationException>("pairing member not in story's character list");
    }

    // ── Pairing persistence ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateStory_WithPairing_PersistsCharacterPairingAndMembers()
    {
        SetActiveUser(_authorId);
        int storyId = await CreateStoryViaServiceAsync(
            characters:
            [
                new StoryCharacterDto { CharacterTagId = _charTagId },
                new StoryCharacterDto { CharacterTagId = _charTagAllowOcId }
            ],
            pairings:
            [
                new StoryCharacterPairingDto
                {
                    PairingType = CharacterPairingType.Romantic,
                    Priority = TagPriority.Primary,
                    MemberCharacterTagIds = [_charTagId, _charTagAllowOcId]
                }
            ]);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        StoryCharacterPairing? pairing = await db.Set<StoryCharacterPairing>()
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.StoryId == storyId);

        pairing.Should().NotBeNull();
        pairing!.PairingType.Should().Be(CharacterPairingType.Romantic);
        pairing.Members.Should().HaveCount(2, "both character members must be persisted");
    }

    // ── Round-trip ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStoryForEditAsync_ReturnsAllStructuredData_AfterCreate()
    {
        SetActiveUser(_authorId);
        int storyId = await CreateStoryViaServiceAsync(
            flatSettingTagIds: [_settingTagWithDetailsId],
            characters:
            [
                new StoryCharacterDto { CharacterTagId = _charTagAllowOcId, Priority = TagPriority.Supporting, IsOc = true, OcName = "Pixel" }
            ],
            settingDetails:
            [
                new SettingDetailDto { BaseTagId = _settingTagWithDetailsId, Name = "Custom Region" }
            ],
            pairings:
            [
                new StoryCharacterPairingDto
                {
                    PairingType = CharacterPairingType.Platonic,
                    MemberCharacterTagIds = [_charTagAllowOcId, _charTagId]   // need 2 chars
                }
            ],
            extraCharacters: [new StoryCharacterDto { CharacterTagId = _charTagId }]);

        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryReadService readService = scope.ServiceProvider.GetRequiredService<IStoryReadService>();

        StoryUpdateDTO? editDto = await readService.GetStoryForEditAsync(storyId);

        editDto.Should().NotBeNull();
        editDto!.StoryCharacters.Should().HaveCount(2);
        editDto.StoryCharacters.Should().ContainSingle(c => c.CharacterTagId == _charTagAllowOcId && c.IsOc && c.OcName == "Pixel");
        editDto.SettingDetails.Should().ContainSingle(d => d.BaseTagId == _settingTagWithDetailsId && d.Name == "Custom Region");
        editDto.StoryCharacterPairings.Should().HaveCount(1);
        editDto.StoryCharacterPairings[0].PairingType.Should().Be(CharacterPairingType.Platonic);
        editDto.StoryCharacterPairings[0].MemberCharacterTagIds.Should().HaveCount(2);
    }

    // ── Discovery character filter ─────────────────────────────────────────────

    [Fact]
    public async Task GetListingsAsync_IncludeByCharacterTagId_MatchesViaStoryCharacters()
    {
        SetActiveUser(_authorId);
        int storyWithChar = await CreateStoryViaServiceAsync(
            characters: [new StoryCharacterDto { CharacterTagId = _charTagId }]);
        int storyWithoutChar = await CreateStoryViaServiceAsync();

        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryReadService readService = scope.ServiceProvider.GetRequiredService<IStoryReadService>();

        var (items, _) = await readService.GetListingsAsync(new StoryFilterDto
        {
            IncludedTagIds = [_charTagId],
            PageSize = 10_000,
            Page = 1
        });

        int[] ids = items.Select(i => i.StoryId).ToArray();
        ids.Should().Contain(storyWithChar, "story with character via StoryCharacters must appear");
        ids.Should().NotContain(storyWithoutChar, "story without that character must not appear");
    }

    [Fact]
    public async Task SanityCheck_CharacterFilter_CharacterIsNotInStoryTags()
    {
        // Confirms the filter test above is meaningful: the character tag lives only in StoryCharacters,
        // not in StoryTags, so without the OR branch in ApplyFilters the filter would miss it.
        SetActiveUser(_authorId);
        int storyId = await CreateStoryViaServiceAsync(
            characters: [new StoryCharacterDto { CharacterTagId = _charTagId }]);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        bool inFlatTags = await db.Set<StoryTag>()
            .AnyAsync(st => st.StoryId == storyId && st.TagId == _charTagId);

        inFlatTags.Should().BeFalse("character tag must not appear in StoryTags — confirms the filter test exercises the OR branch");
    }

    // ── Seeding / service helpers ──────────────────────────────────────────────

    private async Task<int> CreateStoryViaServiceAsync(
        IReadOnlyList<StoryCharacterDto>? characters = null,
        IReadOnlyList<SettingDetailDto>? settingDetails = null,
        IReadOnlyList<StoryCharacterPairingDto>? pairings = null,
        IReadOnlyList<StoryTagDTO>? extraFlatTags = null,
        IReadOnlyList<int>? flatSettingTagIds = null,
        IReadOnlyList<StoryCharacterDto>? extraCharacters = null)
    {
        List<IStoryTag> flatTags =
        [
            new StoryTagDTO { TagId = _settingTagId, TagTypeEnum = TagTypeEnum.Setting, Priority = TagPriority.Primary },
            new StoryTagDTO { TagId = _genreTagId, TagTypeEnum = TagTypeEnum.Genre, Priority = TagPriority.Primary },
            ..( flatSettingTagIds ?? []).Select(id => (IStoryTag)new StoryTagDTO { TagId = id, TagTypeEnum = TagTypeEnum.Setting, Priority = TagPriority.Primary }),
            ..( extraFlatTags ?? [])
        ];

        List<StoryCharacterDto> allChars = [..(characters ?? []), ..(extraCharacters ?? [])];

        CreateStoryDTO dto = new()
        {
            Title = $"Tagging Test Story {Guid.NewGuid():N}",
            ShortDescription = "Integration test",
            Rating = Rating.T,
            StoryStatusId = StoryStatusEnum.InProgress,
            LongDescription = "Integration test long description",
            PostApprovalStatus = StoryStatusEnum.InProgress,
            StoryTags = flatTags,
            StoryCharacters = allChars,
            SettingDetails = (settingDetails ?? []).ToList(),
            StoryCharacterPairings = (pairings ?? []).ToList()
        };

        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryWriteService writeService = scope.ServiceProvider.GetRequiredService<IStoryWriteService>();
        return await writeService.CreateStoryAsync(dto);
    }

    private async Task SeedBaseTagsAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string s = Guid.NewGuid().ToString("N")[..8];
        Tag setting          = new() { TagName = $"Setting-{s}",     TagTypeId = TagTypeEnum.Setting };
        Tag settingWithDet   = new() { TagName = $"SettingDet-{s}",  TagTypeId = TagTypeEnum.Setting, AllowSettingDetails = true };
        Tag genre            = new() { TagName = $"Genre-{s}",       TagTypeId = TagTypeEnum.Genre };
        Tag character        = new() { TagName = $"Char-{s}",        TagTypeId = TagTypeEnum.Character };
        Tag characterAllowOc = new() { TagName = $"CharOC-{s}",      TagTypeId = TagTypeEnum.Character, AllowOCDetails = true };
        Tag cw               = new() { TagName = $"CW-{s}",          TagTypeId = TagTypeEnum.ContentWarning };

        db.Tags.AddRange(setting, settingWithDet, genre, character, characterAllowOc, cw);
        await db.SaveChangesAsync();

        _settingTagId           = setting.TagId;
        _settingTagWithDetailsId = settingWithDet.TagId;
        _genreTagId             = genre.TagId;
        _charTagId              = character.TagId;
        _charTagAllowOcId       = characterAllowOc.TagId;
        _cwTagId                = cw.TagId;
    }
}
