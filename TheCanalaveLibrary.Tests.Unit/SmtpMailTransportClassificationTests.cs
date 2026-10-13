using System.Net.Sockets;
using FluentAssertions;
using MailKit;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="SmtpMailTransport"/>'s failure classification (WU-NotificationCorrectness,
/// 2026-09-30; service audit §2.8 "mid-batch SMTP death"). <see cref="SmtpMailTransport.IsPerMessageFailure"/>
/// is the one copy of the rule that decides whether a send failure drops one message or stops the
/// batch with <see cref="MailBatchInterruptedException"/>. Composition failures (a malformed address,
/// a bad header) are per-message by construction — they are caught in their own <c>try</c> — so they are
/// not this helper's input.
///
/// <para>Tier: Unit — the helper is pure, and the two transport cases below never open a socket (the
/// token is already cancelled when <c>ConnectAsync</c> starts).</para>
/// </summary>
public class SmtpMailTransportClassificationTests
{
    [Theory]
    [InlineData(SmtpStatusCode.MailboxUnavailable)]      // 550 — rejected recipient
    [InlineData(SmtpStatusCode.ExceededStorageAllocation)] // 552
    [InlineData(SmtpStatusCode.TransactionFailed)]       // 554
    public void A5xxReply_WhileConnected_IsPerMessage(SmtpStatusCode status)
    {
        var ex = new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, status, "rejected");

        SmtpMailTransport.IsPerMessageFailure(ex, clientStillConnected: true).Should().BeTrue(
            "the server judged this message on its merits and will judge it the same way on every retry");
    }

    [Fact]
    public void A5xxReply_AfterTheClientDisconnected_AbortsTheBatch()
    {
        var ex = new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.TransactionFailed, "rejected");

        SmtpMailTransport.IsPerMessageFailure(ex, clientStillConnected: false).Should().BeFalse(
            "every remaining message would fail for a reason that has nothing to do with it");
    }

    [Theory]
    [InlineData(SmtpStatusCode.ServiceNotAvailable)]      // 421 — closing / throttled
    [InlineData(SmtpStatusCode.MailboxBusy)]              // 450
    [InlineData(SmtpStatusCode.ErrorInProcessing)]        // 451 — local error / greylist
    [InlineData(SmtpStatusCode.InsufficientStorage)]      // 452
    public void A4xxReply_IsTransient_AndAbortsTheBatch(SmtpStatusCode status)
    {
        var ex = new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, status, "try later");

        SmtpMailTransport.IsPerMessageFailure(ex, clientStillConnected: true).Should().BeFalse(
            "a transient refusal (provider throttling included) is retried, not dropped");
    }

    public static TheoryData<Exception> ConnectionClassExceptions() =>
    [
        new SmtpProtocolException("unexpected reply"),
        new ServiceNotConnectedException("The SmtpClient is not connected."),
        new IOException("connection reset"),
        new SocketException((int)SocketError.ConnectionReset),
        new InvalidOperationException("anything else"),
    ];

    [Theory]
    [MemberData(nameof(ConnectionClassExceptions))]
    public void ConnectionClassFailures_AbortTheBatch_EvenWhileConnected(Exception ex)
    {
        SmtpMailTransport.IsPerMessageFailure(ex, clientStillConnected: true).Should().BeFalse();
    }

    [Fact]
    public async Task SendBatch_CancelledBeforeConnecting_WrapsWithNothingProcessed()
    {
        SmtpMailTransport transport = Create();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        Func<Task> act = () => transport.SendBatchAsync([Mail(), Mail()], cancelled.Token);

        MailBatchInterruptedException ex = (await act.Should().ThrowAsync<MailBatchInterruptedException>()).Which;
        ex.ProcessedCount.Should().Be(0, "nothing was sent, so the caller restores the whole batch");
        ex.InnerException.Should().BeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public async Task SendSingle_UnwrapsTheAbort_SoIdentitySeesTheUnderlyingException()
    {
        SmtpMailTransport transport = Create();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        Func<Task> act = () => transport.SendAsync(Mail(), cancelled.Token);

        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().NotBeOfType<MailBatchInterruptedException>();
    }

    // A host/port that would refuse a real connection — the cancelled token stops ConnectAsync before
    // it gets that far, so no socket opens.
    private static SmtpMailTransport Create() =>
        new(Options.Create(new EmailOptions
        {
            FromAddress = "noreply@test.invalid",
            Smtp = new EmailSmtpOptions { Host = "127.0.0.1", Port = 1 }
        }), NullLogger<SmtpMailTransport>.Instance);

    private static OutgoingMail Mail() =>
        new("someone@test.invalid", "Subject", "<p>Body</p>", "Test", 1);
}
