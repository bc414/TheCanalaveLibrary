using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// The exception-message discipline (cross-cutting.md §"Error Handling Strategy"): only typed
/// user-facing exceptions surface their own message; BCL messages map to fixed friendly text;
/// everything else maps to the generic message. These tests pin the mapping so a new exception
/// type that should be user-facing fails loudly here when it silently falls to generic.
/// </summary>
public class ExceptionPresenterTests
{
    // ── validation family: own error lists surface verbatim ──────────────────────

    [Fact]
    public void ChapterValidationException_SurfacesItsErrorList()
    {
        var ex = new ChapterValidationException(["Too short.", "Rating below story floor."]);

        ExceptionPresenter.IsUserFacing(ex).Should().BeTrue();
        ExceptionPresenter.GetUserMessages(ex).Should().Equal("Too short.", "Rating below story floor.");
    }

    [Fact]
    public void StoryValidationException_SurfacesValidationErrors_NotItsGenericMessage()
    {
        // StoryValidationException.Message is "Story validation failed." — the list is the payload.
        var ex = new StoryValidationException(["Title is required."]);

        ExceptionPresenter.GetUserMessages(ex).Should().Equal("Title is required.");
    }

    // ── single-message user-ready types ───────────────────────────────────────────

    [Fact]
    public void WriteRateLimitExceededException_SurfacesItsUserReadyMessage()
    {
        var ex = new WriteRateLimitExceededException(WriteActionKind.Comment, TimeSpan.FromSeconds(30));

        ExceptionPresenter.IsUserFacing(ex).Should().BeTrue();
        ExceptionPresenter.GetUserMessage(ex).Should().Contain("too fast");
    }

    [Fact]
    public void VouchLimitException_SurfacesItsUserReadyMessage()
    {
        ExceptionPresenter.GetUserMessage(new VouchLimitException()).Should().Contain("maximum");
    }

    // ── session vs. server fault (WU-ErrorHandling2, 2026-07-30) ──────────────────

    [Fact]
    public void SessionExpiredException_IsUserFacing_AndSurfacesItsOwnMessage()
    {
        var ex = new SessionExpiredException();

        ExceptionPresenter.IsUserFacing(ex).Should().BeTrue();
        ExceptionPresenter.GetUserMessage(ex).Should().Be(ex.Message);
        ExceptionPresenter.GetUserMessage(ex).Should().Contain("session has expired");
    }

    [Fact]
    public void ServerFaultException_IsNotUserFacing_AndMapsToGenericWithTheSuppliedTraceId()
    {
        // Deliberately not user-facing (error-handling.md §"The API error envelope"): the server
        // already logged this at Error when it produced the envelope, so a catch site must not
        // double-log it — it's the generic-message path, just with the failing request's own id.
        var ex = new ServerFaultException("server-trace-id-123");

        ExceptionPresenter.IsUserFacing(ex).Should().BeFalse();
        ExceptionPresenter.GetUserMessage(ex).Should()
            .Be($"{ExceptionPresenter.GenericMessage} (Error ID: server-trace-id-123)");
    }

    [Fact]
    public void ServerFaultException_WithNoTraceId_FallsBackToPlainGenericMessage()
    {
        var ex = new ServerFaultException(traceId: null);

        ExceptionPresenter.GetUserMessage(ex).Should().Be(ExceptionPresenter.GenericMessage);
    }

    [Fact]
    public void WithErrorId_PrefersTheExplicitId_OverAmbientActivity()
    {
        using var activity = new System.Diagnostics.Activity("test");
        activity.SetIdFormat(System.Diagnostics.ActivityIdFormat.W3C);
        activity.Start();

        // The server-produced id must win — it's the id of the request that actually failed,
        // correct under the WASM hop where there is no ambient Activity at all.
        string message = ExceptionPresenter.WithErrorId("Oops.", "server-trace-id-456");

        message.Should().Be("Oops. (Error ID: server-trace-id-456)");
    }

    // ── BCL conventions: fixed friendly text, never the developer message ─────────

    [Fact]
    public void UnauthorizedAccessException_MapsToFixedPermissionText()
    {
        var ex = new UnauthorizedAccessException("You can only edit your own comments.");

        ExceptionPresenter.IsUserFacing(ex).Should().BeTrue();
        ExceptionPresenter.GetUserMessage(ex).Should().Be(ExceptionPresenter.PermissionMessage);
    }

    [Fact]
    public void KeyNotFoundException_MapsToFixedNotFoundText_NotTheFrameworkMessage()
    {
        var ex = new KeyNotFoundException(); // "The given key was not present…" — dev text

        ExceptionPresenter.GetUserMessage(ex).Should().Be(ExceptionPresenter.NotFoundMessage);
    }

    // ── everything else: generic, flagged not-user-facing so catch sites log ──────

    [Fact]
    public void UnexpectedException_IsNotUserFacing_AndMapsToGeneric()
    {
        var ex = new InvalidOperationException("Npgsql connection torn down mid-query.");

        ExceptionPresenter.IsUserFacing(ex).Should().BeFalse();
        ExceptionPresenter.GetUserMessage(ex).Should().StartWith(ExceptionPresenter.GenericMessage);
        ExceptionPresenter.GetUserMessage(ex).Should().NotContain("Npgsql");
    }

    [Fact]
    public void WithErrorId_AppendsTraceId_WhenAmbientActivityExists()
    {
        using var activity = new System.Diagnostics.Activity("test");
        activity.SetIdFormat(System.Diagnostics.ActivityIdFormat.W3C);
        activity.Start();

        string message = ExceptionPresenter.WithErrorId("Oops.");

        message.Should().Be($"Oops. (Error ID: {activity.TraceId})");
    }

    [Fact]
    public void WithErrorId_ReturnsPlainMessage_WithNoAmbientActivity()
    {
        System.Diagnostics.Activity.Current.Should().BeNull("test isolation assumption");
        ExceptionPresenter.WithErrorId("Oops.").Should().Be("Oops.");
    }
}
