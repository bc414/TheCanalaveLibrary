using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for the static Moderation model invariants (WU34 — Feature 46).
///
/// <para><b>What's tested:</b>
/// <list type="bullet">
///   <item>Target-type allow-set covers all intended types and excludes unintended ones.</item>
///   <item><c>AccountStatusEnum</c> does not contain <c>Shadowbanned</c> (settled design axiom,
///   tested permanently to prevent accidental re-introduction).</item>
///   <item><c>NotificationTypeEnum</c> includes <c>StoryApproved</c> (WU34 addition).</item>
/// </list>
/// </para>
///
/// Tier: <b>Unit</b> (no host, no DB — pure enum/type reflection).
/// </summary>
public class ModerationValidationsTests
{
    // ── Target-type allow-set ─────────────────────────────────────────────────────

    [Fact]
    public void ReportedEntityType_EnumValues_ExactlyMatchExpectedSet()
    {
        var defined = Enum.GetValues<ReportedEntityType>().ToHashSet();
        HashSet<ReportedEntityType> expected =
        [
            ReportedEntityType.Story,
            ReportedEntityType.User,
            ReportedEntityType.Comment,
            ReportedEntityType.BlogPost,
            ReportedEntityType.Recommendation,
            ReportedEntityType.Message,
        ];

        defined.Should().BeEquivalentTo(expected,
            "if a new ReportedEntityType is added, also add it to ServerModerationWriteService.AllowedReportTargets");
    }

    // ── AccountStatusEnum — shadowban axiom ───────────────────────────────────────

    [Fact]
    public void AccountStatusEnum_DoesNotContainShadowbanned()
    {
        // Permanent axiom: shadowban is deceptive and contradicts §13 transparency philosophy.
        // This test exists to block accidental re-introduction via enum addition.
        var names = Enum.GetNames<AccountStatusEnum>();
        names.Should().NotContain(n => n.Contains("Shadow", StringComparison.OrdinalIgnoreCase),
            "shadowbanning is a settled rejected design — never introduce it");
    }

    [Fact]
    public void AccountStatusEnum_ContainsExpectedValues()
    {
        var defined = Enum.GetValues<AccountStatusEnum>().ToHashSet();
        defined.Should().BeEquivalentTo(
            new HashSet<AccountStatusEnum>
            {
                AccountStatusEnum.Active,
                AccountStatusEnum.Warned,
                AccountStatusEnum.Suspended,
                AccountStatusEnum.Banned,
            });
    }

    // ── StoryApproved notification type (WU34 addition) ──────────────────────────

    [Fact]
    public void NotificationTypeEnum_ContainsStoryApproved()
    {
        var names = Enum.GetNames<NotificationTypeEnum>();
        names.Should().Contain(nameof(NotificationTypeEnum.StoryApproved),
            "WU34 added StoryApproved so approved-story notifications can reach authors");
    }

    [Fact]
    public void NotificationTypeEnum_StoryApproved_HasValue75()
    {
        ((int)NotificationTypeEnum.StoryApproved).Should().Be(75,
            "the value must match the seeded notification_types row (id=75)");
    }
}
