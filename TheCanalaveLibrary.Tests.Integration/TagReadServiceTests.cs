using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="ServerTagReadService"/> (WU3). Covers the two public behaviours:
/// <list type="bullet">
///   <item><see cref="ITagReadService.SearchTagChipsAsync"/> — ILike case-insensitive substring search
///   against real Postgres, alphabetical ordering, MaxSearchResults cap (10), SpriteUrl resolution via
///   FakeActiveUserContext theme/animation flags, empty/whitespace short-circuit.</item>
///   <item><see cref="ITagReadService.GetTagsByTypeAsync"/> — type filter + alphabetical order,
///   exercised via GetAllGenreTagsAsync as a convenience-wrapper spot-check.</item>
/// </list>
/// Uses Guid-suffixed fixture names and asserts only relative order/presence — never absolute position
/// or total count — per the shared-accumulating-state rule in <c>canalave-conventions/testing.md</c>.
/// </summary>
[Collection("Postgres")]
public class TagReadServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    // Three Genre tags with predictable alphabetical ordering:
    // "Aardvark" < "Beluga" < "Cetacean"
    private int _genreTagAardvarkId;
    private int _genreTagBelugaId;
    private int _genreTagCetaceanId;

    // One Character tag — used to verify GetTagsByTypeAsync type-filter exclusion.
    private int _characterTagId;

    // Shared suffix keeps all fixture names globally unique within a test run.
    private string _suffix = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _suffix = Guid.NewGuid().ToString("N")[..8];

        (
            _genreTagAardvarkId,
            _genreTagBelugaId,
            _genreTagCetaceanId,
            _characterTagId
        ) = await SeedFixtureTagsAsync();
    }

    // ── SearchTagChipsAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SearchTagChipsAsync_WithEmptyTerm_ReturnsEmptyList()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ITagReadService tagReadService = scope.ServiceProvider.GetRequiredService<ITagReadService>();

        List<TagChipDto> result = await tagReadService.SearchTagChipsAsync(TagTypeEnum.Genre, string.Empty);

        result.Should().BeEmpty("the service short-circuits on null/empty term before hitting the DB");
    }

    [Fact]
    public async Task SearchTagChipsAsync_WithWhitespaceTerm_ReturnsEmptyList()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ITagReadService tagReadService = scope.ServiceProvider.GetRequiredService<ITagReadService>();

        List<TagChipDto> result = await tagReadService.SearchTagChipsAsync(TagTypeEnum.Genre, "   ");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchTagChipsAsync_MatchingTerm_ReturnsChipsForMatchingTags()
    {
        // All three fixture Genre tags contain the suffix — all three should appear.
        using IServiceScope scope = Factory.Services.CreateScope();
        ITagReadService tagReadService = scope.ServiceProvider.GetRequiredService<ITagReadService>();

        List<TagChipDto> result = await tagReadService.SearchTagChipsAsync(TagTypeEnum.Genre, _suffix);

        result.Select(c => c.TagId).Should().Contain([_genreTagAardvarkId, _genreTagBelugaId, _genreTagCetaceanId]);
    }

    [Fact]
    public async Task SearchTagChipsAsync_MatchingTerm_ReturnsResultsInAlphabeticalOrder()
    {
        // ILike match on suffix: Aardvark... < Beluga... < Cetacean... alphabetically.
        using IServiceScope scope = Factory.Services.CreateScope();
        ITagReadService tagReadService = scope.ServiceProvider.GetRequiredService<ITagReadService>();

        List<TagChipDto> result = await tagReadService.SearchTagChipsAsync(TagTypeEnum.Genre, _suffix);

        List<int> idsInResult = result.Select(c => c.TagId).ToList();

        // The three fixture ids must appear in strict ascending alphabetical order:
        int aardvarkIdx = idsInResult.IndexOf(_genreTagAardvarkId);
        int belugaIdx = idsInResult.IndexOf(_genreTagBelugaId);
        int cetaceanIdx = idsInResult.IndexOf(_genreTagCetaceanId);

        aardvarkIdx.Should().BeGreaterThan(-1, "Aardvark tag must appear in results");
        belugaIdx.Should().BeGreaterThan(aardvarkIdx, "Beluga must follow Aardvark alphabetically");
        cetaceanIdx.Should().BeGreaterThan(belugaIdx, "Cetacean must follow Beluga alphabetically");
    }

    [Fact]
    public async Task SearchTagChipsAsync_ILikeCaseInsensitive_MatchesRegardlessOfCase()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ITagReadService tagReadService = scope.ServiceProvider.GetRequiredService<ITagReadService>();

        // Uppercase the suffix — ILike must still match it.
        List<TagChipDto> result = await tagReadService.SearchTagChipsAsync(TagTypeEnum.Genre, _suffix.ToUpperInvariant());

        result.Select(c => c.TagId).Should().Contain(_genreTagAardvarkId,
            "ILike is case-insensitive; uppercase search term must still hit the lowercase fixture");
    }

    [Fact]
    public async Task SearchTagChipsAsync_OnlyMatchesRequestedTagType()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ITagReadService tagReadService = scope.ServiceProvider.GetRequiredService<ITagReadService>();

        // Search for the suffix in the Genre type — the Character tag with the same suffix must NOT appear.
        List<TagChipDto> result = await tagReadService.SearchTagChipsAsync(TagTypeEnum.Genre, _suffix);

        result.Select(c => c.TagId).Should().NotContain(_characterTagId,
            "type-filter must exclude tags of a different TagTypeId even when the name matches the term");
    }

    [Fact]
    public async Task SearchTagChipsAsync_SpriteIdentifierIsNull_WhenTagHasNoSpriteIdentifier()
    {
        // All fixture tags are seeded without a SpriteIdentifier (null) — chip must carry null identifier.
        using IServiceScope scope = Factory.Services.CreateScope();
        ITagReadService tagReadService = scope.ServiceProvider.GetRequiredService<ITagReadService>();

        List<TagChipDto> result = await tagReadService.SearchTagChipsAsync(TagTypeEnum.Genre, _suffix);

        result.Where(c => c.TagId == _genreTagAardvarkId)
            .Should().AllSatisfy(c => c.SpriteIdentifier.Should().BeNull(
                "SpriteIdentifier is null on the tag → TagChipDto.SpriteIdentifier must also be null"));
    }

    [Fact]
    public async Task SearchTagChipsAsync_MaxSearchResults_CapsAt10()
    {
        // Seed 12 tags with the same search substring, then verify at most 10 come back.
        string capSuffix = Guid.NewGuid().ToString("N")[..8];
        await SeedGenreTagsAsync(count: 12, namePart: $"Cap-{capSuffix}");

        using IServiceScope scope = Factory.Services.CreateScope();
        ITagReadService tagReadService = scope.ServiceProvider.GetRequiredService<ITagReadService>();

        List<TagChipDto> result = await tagReadService.SearchTagChipsAsync(TagTypeEnum.Genre, capSuffix);

        result.Should().HaveCountLessOrEqualTo(10, "MaxSearchResults caps the result at 10 rows");
    }

    // ── GetTagsByTypeAsync / convenience wrappers ────────────────────────────────

    [Fact]
    public async Task GetAllGenreTagsAsync_ReturnsGenreTagsOrderedByName()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ITagReadService tagReadService = scope.ServiceProvider.GetRequiredService<ITagReadService>();

        List<TagDropDownDTO> result = await tagReadService.GetAllGenreTagsAsync();

        // All three fixture Genre tags must appear (relative assertion — other tags may also be present).
        List<int> ids = result.Select(t => t.TagId).ToList();
        ids.Should().Contain([_genreTagAardvarkId, _genreTagBelugaId, _genreTagCetaceanId]);

        // They must be in alphabetical order relative to each other.
        int aardvarkIdx = ids.IndexOf(_genreTagAardvarkId);
        int belugaIdx = ids.IndexOf(_genreTagBelugaId);
        int cetaceanIdx = ids.IndexOf(_genreTagCetaceanId);
        belugaIdx.Should().BeGreaterThan(aardvarkIdx);
        cetaceanIdx.Should().BeGreaterThan(belugaIdx);
    }

    [Fact]
    public async Task GetAllGenreTagsAsync_DoesNotIncludeCharacterTags()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ITagReadService tagReadService = scope.ServiceProvider.GetRequiredService<ITagReadService>();

        List<TagDropDownDTO> result = await tagReadService.GetAllGenreTagsAsync();

        result.Select(t => t.TagId).Should().NotContain(_characterTagId,
            "GetAllGenreTagsAsync must filter to Genre tags only");
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private async Task<(int AardvarkId, int BelugaId, int CetaceanId, int CharacterId)> SeedFixtureTagsAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Alphabetical prefix determines sort order; suffix ensures global uniqueness.
        Tag aardvark = new() { TagName = $"Aardvark-{_suffix}", TagTypeId = TagTypeEnum.Genre };
        Tag beluga = new() { TagName = $"Beluga-{_suffix}", TagTypeId = TagTypeEnum.Genre };
        Tag cetacean = new() { TagName = $"Cetacean-{_suffix}", TagTypeId = TagTypeEnum.Genre };
        Tag characterTag = new() { TagName = $"Character-{_suffix}", TagTypeId = TagTypeEnum.Character };

        db.Tags.AddRange(aardvark, beluga, cetacean, characterTag);
        await db.SaveChangesAsync();

        return (aardvark.TagId, beluga.TagId, cetacean.TagId, characterTag.TagId);
    }

    private async Task SeedGenreTagsAsync(int count, string namePart)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Tag[] tags = Enumerable.Range(1, count)
            .Select(i => new Tag { TagName = $"{namePart}-{i:D2}", TagTypeId = TagTypeEnum.Genre })
            .ToArray();

        db.Tags.AddRange(tags);
        await db.SaveChangesAsync();
    }

    // ── GetTagDirectoryAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetTagDirectoryAsync_GroupsByType_AllFiveTypesPresent()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ITagReadService svc = scope.ServiceProvider.GetRequiredService<ITagReadService>();

        List<TagDirectoryGroupDto> result = await svc.GetTagDirectoryAsync();

        result.Should().HaveCount(5, "there are 5 TagTypeEnum values (Relationship removed in WU37)");
        result.Select(g => g.TagType)
            .Should().BeEquivalentTo(Enum.GetValues<TagTypeEnum>(), "all types are represented");
    }

    [Fact]
    public async Task GetTagDirectoryAsync_ParentAndChildNested_ChildNotTopLevel()
    {
        // Seed a parent Genre and a child Genre. The child must appear under the parent, not as a top-level node.
        string suffix = Guid.NewGuid().ToString("N")[..8];
        using IServiceScope setupScope = Factory.Services.CreateScope();
        ApplicationDbContext db = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Tag parent = new() { TagName = $"Parent-{suffix}", TagTypeId = TagTypeEnum.Genre };
        db.Tags.Add(parent);
        await db.SaveChangesAsync();

        Tag child = new() { TagName = $"Child-{suffix}", TagTypeId = TagTypeEnum.Genre, ParentTagId = parent.TagId };
        db.Tags.Add(child);
        await db.SaveChangesAsync();

        using IServiceScope readScope = Factory.Services.CreateScope();
        ITagReadService svc = readScope.ServiceProvider.GetRequiredService<ITagReadService>();
        List<TagDirectoryGroupDto> result = await svc.GetTagDirectoryAsync();

        TagDirectoryGroupDto genreGroup = result.Single(g => g.TagType == TagTypeEnum.Genre);

        // Parent must be a top-level node.
        TagDirectoryNodeDto parentNode = genreGroup.Nodes
            .Should().Contain(n => n.Tag.TagId == parent.TagId, "parent is top-level")
            .Which;

        // Child must be nested under the parent node — not a top-level node itself.
        parentNode.Children.Should().Contain(c => c.TagId == child.TagId, "child is nested under parent");
        genreGroup.Nodes.Should().NotContain(n => n.Tag.TagId == child.TagId,
            "child must not appear as a top-level node");
    }

    [Fact]
    public async Task GetTagDirectoryAsync_Nodes_OrderedAlphabetically()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        using IServiceScope setupScope = Factory.Services.CreateScope();
        ApplicationDbContext db = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Alpha < Beta < Gamma alphabetically.
        Tag alpha = new() { TagName = $"Alpha-{suffix}", TagTypeId = TagTypeEnum.Genre };
        Tag beta  = new() { TagName = $"Beta-{suffix}",  TagTypeId = TagTypeEnum.Genre };
        Tag gamma = new() { TagName = $"Gamma-{suffix}", TagTypeId = TagTypeEnum.Genre };
        db.Tags.AddRange(alpha, beta, gamma);
        await db.SaveChangesAsync();

        using IServiceScope readScope = Factory.Services.CreateScope();
        ITagReadService svc = readScope.ServiceProvider.GetRequiredService<ITagReadService>();
        List<TagDirectoryGroupDto> result = await svc.GetTagDirectoryAsync();

        TagDirectoryGroupDto genreGroup = result.Single(g => g.TagType == TagTypeEnum.Genre);
        List<int> ids = genreGroup.Nodes.Select(n => n.Tag.TagId).ToList();

        int alphaIdx = ids.IndexOf(alpha.TagId);
        int betaIdx  = ids.IndexOf(beta.TagId);
        int gammaIdx = ids.IndexOf(gamma.TagId);

        alphaIdx.Should().BeGreaterThan(-1);
        betaIdx.Should().BeGreaterThan(alphaIdx, "Beta follows Alpha alphabetically");
        gammaIdx.Should().BeGreaterThan(betaIdx, "Gamma follows Beta alphabetically");
    }

    [Fact]
    public async Task GetTagDirectoryAsync_SpriteUrlNull_WhenNoSpriteIdentifier()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        using IServiceScope setupScope = Factory.Services.CreateScope();
        ApplicationDbContext db = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Tags.Add(new Tag { TagName = $"NoSprite-{suffix}", TagTypeId = TagTypeEnum.Genre, SpriteIdentifier = null });
        await db.SaveChangesAsync();

        using IServiceScope readScope = Factory.Services.CreateScope();
        ITagReadService svc = readScope.ServiceProvider.GetRequiredService<ITagReadService>();
        List<TagDirectoryGroupDto> result = await svc.GetTagDirectoryAsync();

        TagDirectoryGroupDto genreGroup = result.Single(g => g.TagType == TagTypeEnum.Genre);
        genreGroup.Nodes
            .Where(n => n.Tag.TagName == $"NoSprite-{suffix}")
            .Should().AllSatisfy(n => n.Tag.SpriteIdentifier.Should().BeNull());
    }

    [Fact]
    public async Task GetTagDirectoryAsync_AdminFields_PopulatedCorrectly()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        using IServiceScope setupScope = Factory.Services.CreateScope();
        ApplicationDbContext db = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Tag tag = new()
        {
            TagName = $"Fanon-{suffix}",
            TagTypeId = TagTypeEnum.Character,
            IsFanon = true,
            AllowOCDetails = true
        };
        db.Tags.Add(tag);
        await db.SaveChangesAsync();

        using IServiceScope readScope = Factory.Services.CreateScope();
        ITagReadService svc = readScope.ServiceProvider.GetRequiredService<ITagReadService>();
        List<TagDirectoryGroupDto> result = await svc.GetTagDirectoryAsync();

        TagChipDto chip = result.Single(g => g.TagType == TagTypeEnum.Character)
            .Nodes.Single(n => n.Tag.TagId == tag.TagId).Tag;

        chip.IsFanon.Should().BeTrue();
        chip.AllowOCDetails.Should().BeTrue();
    }
}
