using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="MessagingValidations"/> — dependency-free, no host/DB.
/// Tier: Unit (directly constructed Core-only type — mirrors <c>CommentValidationsTests</c> pattern).
/// </summary>
public class MessagingValidationsTests
{
    private const int SenderId = 1;
    private const int RecipientId = 2;

    // ── StartConversationDto.Validate ────────────────────────────────────────────

    [Fact]
    public void Validate_EmptySubject_ReturnsSubjectError()
    {
        var dto = new StartConversationDto(RecipientId, Subject: "", MessageHtml: "<p>Hello</p>");
        dto.Validate(SenderId).Should().ContainSingle()
            .Which.Should().Contain("Subject");
    }

    [Fact]
    public void Validate_WhitespaceSubject_ReturnsSubjectError()
    {
        var dto = new StartConversationDto(RecipientId, Subject: "   ", MessageHtml: "<p>Hello</p>");
        dto.Validate(SenderId).Should().ContainSingle()
            .Which.Should().Contain("Subject");
    }

    [Fact]
    public void Validate_EmptyMessageHtml_ReturnsBodyError()
    {
        var dto = new StartConversationDto(RecipientId, Subject: "Greetings", MessageHtml: "");
        dto.Validate(SenderId).Should().ContainSingle()
            .Which.Should().Contain("empty");
    }

    [Fact]
    public void Validate_WhitespaceMessageHtml_ReturnsBodyError()
    {
        var dto = new StartConversationDto(RecipientId, Subject: "Greetings", MessageHtml: "  ");
        dto.Validate(SenderId).Should().ContainSingle()
            .Which.Should().Contain("empty");
    }

    [Fact]
    public void Validate_SelfMessage_ReturnsYourselfError()
    {
        // Sender and recipient are the same user.
        var dto = new StartConversationDto(SenderId, Subject: "Greetings", MessageHtml: "<p>Hello me!</p>");
        dto.Validate(SenderId).Should().ContainSingle()
            .Which.Should().Contain("yourself");
    }

    [Fact]
    public void Validate_AllValid_ReturnsNoErrors()
    {
        var dto = new StartConversationDto(RecipientId, Subject: "Greetings", MessageHtml: "<p>Hello!</p>");
        dto.Validate(SenderId).Should().BeEmpty();
    }

    [Fact]
    public void Validate_EmptySubjectAndSelfMessage_ReturnsTwoErrors()
    {
        // Both violations present simultaneously.
        var dto = new StartConversationDto(SenderId, Subject: "", MessageHtml: "<p>Text</p>");
        dto.Validate(SenderId).Should().HaveCount(2);
    }

    // ── ValidateMessageBody ──────────────────────────────────────────────────────

    [Fact]
    public void ValidateMessageBody_EmptyString_ReturnsError()
    {
        MessagingValidations.ValidateMessageBody("").Should().ContainSingle()
            .Which.Should().Contain("empty");
    }

    [Fact]
    public void ValidateMessageBody_NullString_ReturnsError()
    {
        MessagingValidations.ValidateMessageBody(null).Should().ContainSingle()
            .Which.Should().Contain("empty");
    }

    [Fact]
    public void ValidateMessageBody_WhitespaceOnly_ReturnsError()
    {
        MessagingValidations.ValidateMessageBody("   \t\n").Should().ContainSingle()
            .Which.Should().Contain("empty");
    }

    [Fact]
    public void ValidateMessageBody_ValidHtml_ReturnsNoErrors()
    {
        MessagingValidations.ValidateMessageBody("<p>Looks great so far!</p>").Should().BeEmpty();
    }
}
