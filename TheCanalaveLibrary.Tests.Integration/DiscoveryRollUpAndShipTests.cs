using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for WU-TagFanon Group 5: hierarchy roll-up in ApplyFilters (symmetric
/// include/exclude, independent AND terms) and the ship-filter axis (single-pairing coverage,
/// include/exclude, roll-up inheritance on members).
/// Tier: Integration (Testcontainers Postgres).
/// </summary>
[Collection("Postgres")]
public class DiscoveryRollUpAndShipTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;
    private int _parentTagId;    // Character species (parent)
    private int _childTagId;     // fanon child of the parent
    private int _otherTagId;     // unrelated Character tag
    private int _genreParentId;  // flat parent (Genre)
    private int _genreChildId;   // flat child

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId = await SeedUserAsync();

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string s = Guid.NewGuid().ToString("N")[..8];
        Tag parent = new() { TagName = $"Parent-{s}", TagTypeId = TagTypeEnum.Character, AllowCustomName = true };
        Tag child = new() { TagName = $"Child-{s}", TagTypeId = TagTypeEnum.Character, IsFanon = true, ParentTag = parent };
        Tag other = new() { TagName = $"Other-{s}", TagTypeId = TagTypeEnum.Character };
        Tag genreParent = new() { TagName = $"GenreP-{s}", TagTypeId = TagTypeEnum.Genre };
        Tag genreChild = new() { TagName = $"GenreC-{s}", TagTypeId = TagTypeEnum.Genre, ParentTag = genreParent };
        db.Tags.AddRange(parent, child, other, genreParent, genreChild);
        await db.SaveChangesAsync();
        _parentTagId = parent.TagId;
        _childTagId = child.TagId;
        _otherTagId = other.TagId;
        _genreParentId = genreParent.TagId;
        _genreChildId = genreChild.TagId;
    }

    private async Task<int> SeedStoryWithCharacterAsync(int characterTagId)
    {
        int storyId = await SeedStoryAsync(_authorId);
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.StoryCharacters.Add(new StoryCharacter { StoryId = storyId, CharacterTagId = characterTagId });
        await db.SaveChangesAsync();
        return storyId;
    }

    private async Task<int[]> QueryIdsAsync(StoryFilterDto filter)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryReadService read = scope.ServiceProvider.GetRequiredService<IStoryReadService>();
        var (items, _) = await read.GetListingsAsync(filter with { PageSize = 10_000, Page = 1 });
        return items.Select(i => i.StoryId).ToArray();
    }

    // ── Roll-up: include ───────────────────────────────────────────────────────

    [Fact]
    public async Task Include_ParentTag_MatchesChildOnlyStories()
    {
        SetActiveUser(_authorId);
        int childStory = await SeedStoryWithCharacterAsync(_childTagId);
        int otherStory = await SeedStoryWithCharacterAsync(_otherTagId);

        int[] ids = await QueryIdsAsync(new StoryFilterDto { IncludedTagIds = [_parentTagId] });

        ids.Should().Contain(childStory, "a parent filter matches its children (roll-up)");
        ids.Should().NotContain(otherStory);
    }

    [Fact]
    public async Task Include_ParentAndChild_OneChildRowSatisfiesBothAndTerms()
    {
        SetActiveUser(_authorId);
        int childStory = await SeedStoryWithCharacterAsync(_childTagId);

        int[] ids = await QueryIdsAsync(new StoryFilterDto
        {
            IncludedTagIds = [_parentTagId, _childTagId],
            IncludeMode = TagIncludeMode.And
        });

        ids.Should().Contain(childStory, "AND terms are independent — one child row satisfies both");
    }

    [Fact]
    public async Task Include_FlatParentGenre_MatchesChildGenreStories()
    {
        SetActiveUser(_authorId);
        int storyId = await SeedStoryAsync(_authorId);
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.StoryTags.Add(new StoryTag { StoryId = storyId, TagId = _genreChildId, Priority = TagPriority.Primary });
            await db.SaveChangesAsync();
        }

        int[] ids = await QueryIdsAsync(new StoryFilterDto { IncludedTagIds = [_genreParentId] });
        ids.Should().Contain(storyId, "roll-up covers the flat StoryTag side too");
    }

    // ── Roll-up: exclude (symmetric) ───────────────────────────────────────────

    [Fact]
    public async Task Exclude_ParentTag_ExcludesChildOnlyStories()
    {
        SetActiveUser(_authorId);
        int childStory = await SeedStoryWithCharacterAsync(_childTagId);
        int otherStory = await SeedStoryWithCharacterAsync(_otherTagId);

        int[] ids = await QueryIdsAsync(new StoryFilterDto { ExcludedTagIds = [_parentTagId] });

        ids.Should().NotContain(childStory, "excluding a parent excludes its children — symmetric roll-up");
        ids.Should().Contain(otherStory);
    }

    // ── Ship filter ────────────────────────────────────────────────────────────

    private async Task<int> SeedStoryWithPairingAsync(int tagA, int tagB, CharacterPairingType type)
    {
        int storyId = await SeedStoryAsync(_authorId);
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        StoryCharacter a = new() { StoryId = storyId, CharacterTagId = tagA };
        StoryCharacter b = new() { StoryId = storyId, CharacterTagId = tagB };
        db.StoryCharacters.AddRange(a, b);
        await db.SaveChangesAsync();
        db.StoryCharacterPairings.Add(new StoryCharacterPairing
        {
            StoryId = storyId, PairingType = type, Priority = TagPriority.Primary,
            Members =
            {
                new StoryCharacterPairingMember { StoryCharacterId = a.StoryCharacterId },
                new StoryCharacterPairingMember { StoryCharacterId = b.StoryCharacterId },
            }
        });
        await db.SaveChangesAsync();
        return storyId;
    }

    [Fact]
    public async Task ShipInclude_RequiresBothMembersInOnePairing()
    {
        SetActiveUser(_authorId);
        int shipped = await SeedStoryWithPairingAsync(_parentTagId, _otherTagId, CharacterPairingType.Romantic);
        // Both characters present but never paired — must NOT match.
        int unshipped = await SeedStoryAsync(_authorId);
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.StoryCharacters.AddRange(
                new StoryCharacter { StoryId = unshipped, CharacterTagId = _parentTagId },
                new StoryCharacter { StoryId = unshipped, CharacterTagId = _otherTagId });
            await db.SaveChangesAsync();
        }

        int[] ids = await QueryIdsAsync(new StoryFilterDto
        {
            IncludedShips = [new ShipFilterDto { MemberTagIds = [_parentTagId, _otherTagId] }]
        });

        ids.Should().Contain(shipped);
        ids.Should().NotContain(unshipped, "a ship needs ONE pairing covering all members — co-presence isn't a ship");
    }

    [Fact]
    public async Task ShipInclude_PairingTypeConstraint_Filters()
    {
        SetActiveUser(_authorId);
        int platonic = await SeedStoryWithPairingAsync(_parentTagId, _otherTagId, CharacterPairingType.Platonic);

        int[] romanticOnly = await QueryIdsAsync(new StoryFilterDto
        {
            IncludedShips = [new ShipFilterDto { MemberTagIds = [_parentTagId, _otherTagId], PairingType = CharacterPairingType.Romantic }]
        });
        romanticOnly.Should().NotContain(platonic);

        int[] platonicOnly = await QueryIdsAsync(new StoryFilterDto
        {
            IncludedShips = [new ShipFilterDto { MemberTagIds = [_parentTagId, _otherTagId], PairingType = CharacterPairingType.Platonic }]
        });
        platonicOnly.Should().Contain(platonic);
    }

    [Fact]
    public async Task ShipInclude_InheritsRollUp_OnMemberIds()
    {
        SetActiveUser(_authorId);
        // The pairing uses the CHILD tag; the filter names the PARENT — roll-up must bridge.
        int shipped = await SeedStoryWithPairingAsync(_childTagId, _otherTagId, CharacterPairingType.Romantic);

        int[] ids = await QueryIdsAsync(new StoryFilterDto
        {
            IncludedShips = [new ShipFilterDto { MemberTagIds = [_parentTagId, _otherTagId] }]
        });

        ids.Should().Contain(shipped, "ship members inherit hierarchy roll-up");
    }

    [Fact]
    public async Task ShipExclude_RemovesMatchingStories()
    {
        SetActiveUser(_authorId);
        int shipped = await SeedStoryWithPairingAsync(_parentTagId, _otherTagId, CharacterPairingType.Romantic);
        int plain = await SeedStoryWithCharacterAsync(_otherTagId);

        int[] ids = await QueryIdsAsync(new StoryFilterDto
        {
            ExcludedShips = [new ShipFilterDto { MemberTagIds = [_parentTagId, _otherTagId] }]
        });

        ids.Should().NotContain(shipped, "excluded ships mirror the tag exclude axis");
        ids.Should().Contain(plain);
    }
}
