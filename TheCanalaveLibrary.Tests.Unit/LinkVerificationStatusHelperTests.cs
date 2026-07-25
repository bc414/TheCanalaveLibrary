using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>Unit tests for <see cref="LinkVerificationStatusHelper"/> (Feature 53, WU39) — the author-facing per-link status derivation, pure/no DbContext.</summary>
public class LinkVerificationStatusHelperTests
{
    [Fact]
    public void Verified_IsAlwaysConfirmed_RegardlessOfRequestedFlag()
    {
        LinkVerificationStatusHelper.GetDisplayStatus(VerificationStatusEnum.Verified, verificationRequested: true)
            .Should().Be(LinkVerificationDisplayStatus.Confirmed);
        LinkVerificationStatusHelper.GetDisplayStatus(VerificationStatusEnum.Verified, verificationRequested: false)
            .Should().Be(LinkVerificationDisplayStatus.Confirmed);
    }

    [Fact]
    public void Rejected_IsAlwaysRejected_RegardlessOfRequestedFlag()
    {
        LinkVerificationStatusHelper.GetDisplayStatus(VerificationStatusEnum.Rejected, verificationRequested: true)
            .Should().Be(LinkVerificationDisplayStatus.Rejected);
        LinkVerificationStatusHelper.GetDisplayStatus(VerificationStatusEnum.Rejected, verificationRequested: false)
            .Should().Be(LinkVerificationDisplayStatus.Rejected);
    }

    [Fact]
    public void Unverified_Requested_IsPendingReview()
    {
        LinkVerificationStatusHelper.GetDisplayStatus(VerificationStatusEnum.Unverified, verificationRequested: true)
            .Should().Be(LinkVerificationDisplayStatus.PendingReview);
    }

    [Fact]
    public void Unverified_NotRequested_IsNotRequested()
    {
        LinkVerificationStatusHelper.GetDisplayStatus(VerificationStatusEnum.Unverified, verificationRequested: false)
            .Should().Be(LinkVerificationDisplayStatus.NotRequested);
    }
}
