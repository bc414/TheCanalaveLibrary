using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="TreeSearchPathParser"/> (WU44) — dependency-free parsing of the raw
/// Postgres <c>CYCLE ... USING path</c> text (see the type doc for the composite-array shape).
/// </summary>
public sealed class TreeSearchPathParserTests
{
    [Fact]
    public void Parse_NullOrWhitespace_ReturnsEmpty()
    {
        TreeSearchPathParser.Parse(null).Should().BeEmpty();
        TreeSearchPathParser.Parse("").Should().BeEmpty();
        TreeSearchPathParser.Parse("   ").Should().BeEmpty();
    }

    [Fact]
    public void Parse_TypicalPath_ExtractsIsStoryAndNodeIdInOrder()
    {
        var nodes = TreeSearchPathParser.Parse("""{"(t,12)","(f,45)","(t,88)"}""");

        nodes.Should().Equal(
            new TreeSearchPathParser.PathNode(true, 12),
            new TreeSearchPathParser.PathNode(false, 45),
            new TreeSearchPathParser.PathNode(true, 88));
    }

    [Fact]
    public void Parse_AcceptsLongFormBooleanText()
    {
        // Defensive: Postgres text casts normally emit t/f, but the parser should not choke if a
        // driver ever emits the long form.
        var nodes = TreeSearchPathParser.Parse("""{"(true,7)","(false,9)"}""");

        nodes.Should().Equal(
            new TreeSearchPathParser.PathNode(true, 7),
            new TreeSearchPathParser.PathNode(false, 9));
    }

    [Fact]
    public void StoryIdsOnly_DropsUserHops_PreservingOrder()
    {
        var ids = TreeSearchPathParser.StoryIdsOnly("""{"(t,1)","(f,2)","(t,3)","(f,4)","(t,5)"}""");

        ids.Should().Equal([1, 3, 5], "user-typed hops (f) must never be surfaced — privacy model");
    }

    [Fact]
    public void StoryIdsOnly_NullPath_ReturnsEmpty() =>
        TreeSearchPathParser.StoryIdsOnly(null).Should().BeEmpty();

    [Fact]
    public void SanityCheck_MutationOfIsStoryFlag_WouldSwapWhichHopsAreKept()
    {
        // Confirms the is_story flag (not just position) drives StoryIdsOnly — flipping which
        // tuple position means "story" would change the output, proving the parser reads the
        // flag rather than assuming an alternating story/user/story pattern by index.
        var storyFirst = TreeSearchPathParser.StoryIdsOnly("""{"(t,1)","(f,2)"}""");
        var userFirst = TreeSearchPathParser.StoryIdsOnly("""{"(f,1)","(t,2)"}""");

        storyFirst.Should().Equal(1);
        userFirst.Should().Equal(2);
    }
}
