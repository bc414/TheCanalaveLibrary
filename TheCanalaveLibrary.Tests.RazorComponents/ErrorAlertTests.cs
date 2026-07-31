using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="ErrorAlert"/> (WU-ErrorHandling2, 2026-07-30) — the
/// session-expiry-aware wrapper around <see cref="InlineAlert"/>. Self-hides when empty (same
/// contract as <see cref="InlineAlert"/>); the Sign in affordance renders only when the catch
/// site sets <c>ShowSignIn</c> (i.e. the underlying failure was <c>SessionExpiredException</c>).
/// </summary>
public class ErrorAlertTests : BunitContext
{
    [Fact]
    public void NoMessage_RendersNothing()
    {
        IRenderedComponent<ErrorAlert> cut = Render<ErrorAlert>();

        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public void Message_WithShowSignInFalse_RendersAlertOnly_NoSignInLink()
    {
        IRenderedComponent<ErrorAlert> cut = Render<ErrorAlert>(p => p
            .Add(a => a.Message, "You don't have permission to do that.")
            .Add(a => a.ShowSignIn, false));

        cut.Find("[role='alert']").TextContent.Should().Be("You don't have permission to do that.");
        cut.FindAll("a").Should().BeEmpty();
    }

    [Fact]
    public void Message_WithShowSignInTrue_RendersSignInLink()
    {
        IRenderedComponent<ErrorAlert> cut = Render<ErrorAlert>(p => p
            .Add(a => a.Message, "Your session has expired — sign in again to continue.")
            .Add(a => a.ShowSignIn, true));

        cut.Find("[role='alert']").TextContent.Should().Contain("session has expired");
        var link = cut.Find("a");
        link.TextContent.Trim().Should().Be("Sign in");
        link.GetAttribute("href").Should().StartWith("/Account/Login?ReturnUrl=");
    }

    [Fact]
    public void ShowSignIn_WithNoMessage_StillRendersNothing()
    {
        // Self-hides on the message content, not the flag — a stray ShowSignIn=true with no
        // active error must not render a bare Sign in link with nothing to explain it.
        IRenderedComponent<ErrorAlert> cut = Render<ErrorAlert>(p => p.Add(a => a.ShowSignIn, true));

        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public void Messages_RenderAsList_WithSignInLinkBelow()
    {
        IRenderedComponent<ErrorAlert> cut = Render<ErrorAlert>(p => p
            .Add(a => a.Messages, new List<string> { "First problem.", "Second problem." })
            .Add(a => a.ShowSignIn, true));

        cut.FindAll("li").Should().HaveCount(2);
        cut.Find("a").TextContent.Trim().Should().Be("Sign in");
    }
}
