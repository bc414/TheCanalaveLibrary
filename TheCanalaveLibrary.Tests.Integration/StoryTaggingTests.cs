using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for Feature 12 structured story tagging (WU37 Phase 5; reshaped by
/// WU-TagFanon). Covers character routing (StoryCharacter vs StoryTag), the custom-name gate
/// (IsOc/CustomName require Tag.AllowCustomName; Nuance never gated), multi-OC-per-species
/// uniqueness, ContentWarning priority coercion, index-based pairing persistence,
/// GetStoryForEditAsync round-trip, and the discovery character-filter branch in ApplyFilters.
/// Tier: Integration (Testcontainers Postgres).
/// </summary>
[Collection("Postgres")]
public class StoryTaggingTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;
    private int _settingTagId;
    private int _settingTagWithCustomNameId;
    private int _genreTagId;
    private int _charTagId;
    private int _charTagAllowCustomNameId;
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

    // ── Custom-name gate (WU-TagFanon) ─────────────────────────────────────────

    [Fact]
    public async Task CreateStory_WithOcFlagOnUngatedTag_ThrowsStoryValidationException()
    {
        SetActiveUser(_authorId);
        Func<Task> act = () => CreateStoryViaServiceAsync(characters:
        [
            new StoryCharacterDto { CharacterTagId = _charTagId, IsOc = true, CustomName = "My OC" }
        ]);

        await act.Should().ThrowAsync<StoryValidationException>("tag does not allow custom-named characters");
    }

    [Fact]
    public async Task CreateStory_WithCustomNameButNoOcFlag_ThrowsStoryValidationException()
    {
        SetActiveUser(_authorId);
        Func<Task> act = () => CreateStoryViaServiceAsync(characters:
        [
            new StoryCharacterDto { CharacterTagId = _charTagAllowCustomNameId, IsOc = false, CustomName = "Rogue Name" }
        ]);

        await act.Should().ThrowAsync<StoryValidationException>("a character custom name requires the OC flag");
    }

    [Fact]
    public async Task CreateStory_WithOcOverlayOnGatedTag_Succeeds()
    {
        SetActiveUser(_authorId);
        Func<Task> act = () => CreateStoryViaServiceAsync(characters:
        [
            new StoryCharacterDto { CharacterTagId = _charTagAllowCustomNameId, IsOc = true, CustomName = "Kira", Nuance = "Friendly Pikachu OC" }
        ]);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CreateStory_NuanceOnUngatedCharacter_Succeeds()
    {
        // Nuance is NEVER gated — a portrayal note is legal on canon/fanon characters
        // (the WU-TagFanon split: the gate covers custom naming only).
        SetActiveUser(_authorId);
        Func<Task> act = () => CreateStoryViaServiceAsync(characters:
        [
            new StoryCharacterDto { CharacterTagId = _charTagId, Nuance = "competent portrayal" }
        ]);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CreateStory_TwoOcsOfOneSpecies_WithDistinctNames_Succeeds()
    {
        SetActiveUser(_authorId);
        int storyId = await CreateStoryViaServiceAsync(characters:
        [
            new StoryCharacterDto { CharacterTagId = _charTagAllowCustomNameId, IsOc = true, CustomName = "Saura" },
            new StoryCharacterDto { CharacterTagId = _charTagAllowCustomNameId, IsOc = true, CustomName = "Spiky" }
        ]);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        int rows = await db.Set<StoryCharacter>().CountAsync(sc => sc.StoryId == storyId && sc.CharacterTagId == _charTagAllowCustomNameId);
        rows.Should().Be(2, "a story may hold several custom-named characters of one species");
    }

    [Fact]
    public async Task CreateStory_TwoCharactersSameTagSameName_ThrowsStoryValidationException()
    {
        SetActiveUser(_authorId);
        Func<Task> act = () => CreateStoryViaServiceAsync(characters:
        [
            new StoryCharacterDto { CharacterTagId = _charTagAllowCustomNameId, IsOc = true, CustomName = "Saura" },
            new StoryCharacterDto { CharacterTagId = _charTagAllowCustomNameId, IsOc = true, CustomName = "Saura" }
        ]);

        await act.Should().ThrowAsync<StoryValidationException>("duplicate (tag, custom name) pairs are rejected before the unique index fires");
    }

    [Fact]
    public async Task CreateStory_TwoUnnamedRowsOfOneSpecies_ThrowsStoryValidationException()
    {
        // Nulls are NOT distinct — at most one unnamed row per (story, tag).
        SetActiveUser(_authorId);
        Func<Task> act = () => CreateStoryViaServiceAsync(characters:
        [
            new StoryCharacterDto { CharacterTagId = _charTagId },
            new StoryCharacterDto { CharacterTagId = _charTagId }
        ]);

        await act.Should().ThrowAsync<StoryValidationException>();
    }

    // ── Flat-tag overlay (WU-TagFanon: SettingDetail folded onto StoryTag) ─────

    [Fact]
    public async Task CreateStory_FlatCustomNameOnUngatedTag_ThrowsStoryValidationException()
    {
        SetActiveUser(_authorId);
        Func<Task> act = () => CreateStoryViaServiceAsync(extraFlatTags:
        [
            new StoryTagDTO { TagId = _settingTagId, TagTypeEnum = TagTypeEnum.Setting, CustomName = "Custom Pallet" }
        ]);

        await act.Should().ThrowAsync<StoryValidationException>("tag does not allow custom names");
    }

    [Fact]
    public async Task CreateStory_FlatOverlayOnGatedTag_PersistsOnStoryTagRow()
    {
        SetActiveUser(_authorId);
        int storyId = await CreateStoryViaServiceAsync(extraFlatTags:
        [
            new StoryTagDTO
            {
                TagId = _settingTagWithCustomNameId, TagTypeEnum = TagTypeEnum.Setting,
                CustomName = "Custom Viridian", Nuance = "Alternate timeline"
            }
        ]);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        StoryTag? row = await db.Set<StoryTag>().FirstOrDefaultAsync(st => st.StoryId == storyId && st.TagId == _settingTagWithCustomNameId);
        row.Should().NotBeNull();
        row!.CustomName.Should().Be("Custom Viridian");
        row.Nuance.Should().Be("Alternate timeline");
    }

    [Fact]
    public async Task CreateStory_NuanceOnUngatedGenre_Succeeds()
    {
        // The universal portrayal note: "slow burn, no love triangle" on a plain Genre tag.
        SetActiveUser(_authorId);
        int storyId = await CreateStoryViaServiceAsync(extraFlatTags:
        [
            new StoryTagDTO { TagId = _cwTagId, TagTypeEnum = TagTypeEnum.ContentWarning, Nuance = "chapter 3 only" }
        ]);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        StoryTag? row = await db.Set<StoryTag>().FirstOrDefaultAsync(st => st.StoryId == storyId && st.TagId == _cwTagId);
        row!.Nuance.Should().Be("chapter 3 only", "Nuance is never gated, any tag type");
    }

    // ── Pairing constraints (index-based members — WU-TagFanon) ────────────────

    [Fact]
    public async Task CreateStory_PairingWithFewerThanTwoDistinctMembers_ThrowsStoryValidationException()
    {
        SetActiveUser(_authorId);
        Func<Task> act = () => CreateStoryViaServiceAsync(
            characters: [new StoryCharacterDto { CharacterTagId = _charTagId }],
            pairings:
            [
                new StoryCharacterPairingDto
                {
                    PairingType = CharacterPairingType.Romantic,
                    MemberIndexes = [0]   // only 1 member
                }
            ]);

        await act.Should().ThrowAsync<StoryValidationException>("pairing needs ≥2 distinct members");
    }

    [Fact]
    public async Task CreateStory_PairingMemberIndexOutOfRange_ThrowsStoryValidationException()
    {
        SetActiveUser(_authorId);
        Func<Task> act = () => CreateStoryViaServiceAsync(
            characters: [new StoryCharacterDto { CharacterTagId = _charTagId }],
            pairings:
            [
                new StoryCharacterPairingDto
                {
                    PairingType = CharacterPairingType.Romantic,
                    MemberIndexes = [0, 5]  // index 5 has no character row
                }
            ]);

        await act.Should().ThrowAsync<StoryValidationException>("pairing member index out of range");
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
                new StoryCharacterDto { CharacterTagId = _charTagAllowCustomNameId }
            ],
            pairings:
            [
                new StoryCharacterPairingDto
                {
                    PairingType = CharacterPairingType.Romantic,
                    Priority = TagPriority.Primary,
                    MemberIndexes = [0, 1]
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

    [Fact]
    public async Task CreateStory_PairingBetweenTwoOcsOfOneSpecies_ResolvesTheRightRows()
    {
        // The whole reason members are index-based: tag ids can't tell Saura from Spiky.
        SetActiveUser(_authorId);
        int storyId = await CreateStoryViaServiceAsync(
            characters:
            [
                new StoryCharacterDto { CharacterTagId = _charTagAllowCustomNameId, IsOc = true, CustomName = "Saura" },
                new StoryCharacterDto { CharacterTagId = _charTagAllowCustomNameId, IsOc = true, CustomName = "Spiky" }
            ],
            pairings:
            [
                new StoryCharacterPairingDto { PairingType = CharacterPairingType.Platonic, MemberIndexes = [0, 1] }
            ]);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        StoryCharacterPairing? pairing = await db.Set<StoryCharacterPairing>()
            .Include(p => p.Members).ThenInclude(m => m.StoryCharacter)
            .FirstOrDefaultAsync(p => p.StoryId == storyId);

        pairing.Should().NotBeNull();
        pairing!.Members.Select(m => m.StoryCharacter.CustomName)
            .Should().BeEquivalentTo(["Saura", "Spiky"], "the pairing links the two distinct same-species rows");
    }

    // ── Round-trip ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStoryForEditAsync_ReturnsAllStructuredData_AfterCreate()
    {
        SetActiveUser(_authorId);
        int storyId = await CreateStoryViaServiceAsync(
            characters:
            [
                new StoryCharacterDto { CharacterTagId = _charTagAllowCustomNameId, Priority = TagPriority.Supporting, IsOc = true, CustomName = "Pixel" },
                new StoryCharacterDto { CharacterTagId = _charTagId }
            ],
            extraFlatTags:
            [
                new StoryTagDTO
                {
                    TagId = _settingTagWithCustomNameId, TagTypeEnum = TagTypeEnum.Setting,
                    CustomName = "Custom Region"
                }
            ],
            pairings:
            [
                new StoryCharacterPairingDto
                {
                    PairingType = CharacterPairingType.Platonic,
                    MemberIndexes = [0, 1]
                }
            ]);

        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryReadService readService = scope.ServiceProvider.GetRequiredService<IStoryReadService>();

        StoryUpdateDTO? editDto = await readService.GetStoryForEditAsync(storyId);

        editDto.Should().NotBeNull();
        editDto!.StoryCharacters.Should().HaveCount(2);
        editDto.StoryCharacters.Should().ContainSingle(c => c.CharacterTagId == _charTagAllowCustomNameId && c.IsOc && c.CustomName == "Pixel");
        editDto.StoryTags.Should().ContainSingle(t => t.TagId == _settingTagWithCustomNameId && t.CustomName == "Custom Region");
        editDto.StoryCharacterPairings.Should().HaveCount(1);
        editDto.StoryCharacterPairings[0].PairingType.Should().Be(CharacterPairingType.Platonic);
        // Round-trip contract: member indexes point into the hydrated StoryCharacters list.
        editDto.StoryCharacterPairings[0].MemberIndexes.Should().BeEquivalentTo([0, 1]);
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
        IReadOnlyList<StoryCharacterPairingDto>? pairings = null,
        IReadOnlyList<StoryTagDTO>? extraFlatTags = null)
    {
        List<IStoryTag> flatTags =
        [
            new StoryTagDTO { TagId = _settingTagId, TagTypeEnum = TagTypeEnum.Setting, Priority = TagPriority.Primary },
            new StoryTagDTO { TagId = _genreTagId, TagTypeEnum = TagTypeEnum.Genre, Priority = TagPriority.Primary },
            ..( extraFlatTags ?? [])
        ];

        CreateStoryDTO dto = new()
        {
            Title = $"Tagging Test Story {Guid.NewGuid():N}",
            ShortDescription = "Integration test",
            Rating = Rating.T,
            StoryStatusId = StoryStatusEnum.InProgress,
            LongDescription = "Integration test long description",
            PostApprovalStatus = StoryStatusEnum.InProgress,
            StoryTags = flatTags,
            StoryCharacters = (characters ?? []).ToList(),
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
        Tag setting            = new() { TagName = $"Setting-{s}",     TagTypeId = TagTypeEnum.Setting };
        Tag settingCustomName  = new() { TagName = $"SettingDet-{s}",  TagTypeId = TagTypeEnum.Setting, AllowCustomName = true };
        Tag genre              = new() { TagName = $"Genre-{s}",       TagTypeId = TagTypeEnum.Genre };
        Tag character          = new() { TagName = $"Char-{s}",        TagTypeId = TagTypeEnum.Character };
        Tag characterCustomName = new() { TagName = $"CharOC-{s}",     TagTypeId = TagTypeEnum.Character, AllowCustomName = true };
        Tag cw                 = new() { TagName = $"CW-{s}",          TagTypeId = TagTypeEnum.ContentWarning };

        db.Tags.AddRange(setting, settingCustomName, genre, character, characterCustomName, cw);
        await db.SaveChangesAsync();

        _settingTagId               = setting.TagId;
        _settingTagWithCustomNameId = settingCustomName.TagId;
        _genreTagId                 = genre.TagId;
        _charTagId                  = character.TagId;
        _charTagAllowCustomNameId   = characterCustomName.TagId;
        _cwTagId                    = cw.TagId;
    }
}
