using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using MailKit;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="SmtpMailTransport"/>'s failure classification (WU-NotificationCorrectness,
/// 2026-10-12; service audit §2.8 "mid-batch SMTP death"; the deferral class from its review fixes,
/// 2026-10-13). <see cref="SmtpMailTransport.IsPerMessageFailure"/> and
/// <see cref="SmtpMailTransport.IsDeferral"/> are the one copy of the rule that decides whether a send
/// failure drops one message, returns it for a later retry, or stops the batch with
/// <see cref="MailBatchInterruptedException"/>. Composition failures (a malformed address, a bad
/// header) are per-message by construction — they are caught in their own <c>try</c> — so they are
/// not these helpers' input.
///
/// <para>Tier: Unit — the helpers are pure; the transport cases either never open a socket (the token
/// is already cancelled when <c>ConnectAsync</c> starts) or talk to <see cref="StubSmtpServer"/>, an
/// in-process loopback SMTP responder, so the real MailKit client is driven with no host, container
/// or database. Each wire test runs under a deadline, so a protocol mismatch fails instead of hanging
/// (<c>testing.md</c>, hang regressions).</para>
/// </summary>
public class SmtpMailTransportClassificationTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(15);

    // ── The pure rules ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(SmtpStatusCode.MailboxUnavailable)]      // 550 — rejected recipient
    [InlineData(SmtpStatusCode.ExceededStorageAllocation)] // 552
    [InlineData(SmtpStatusCode.TransactionFailed)]       // 554
    public void A5xxReply_WhileConnected_IsPerMessage(SmtpStatusCode status)
    {
        var ex = new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, status, "rejected");

        SmtpMailTransport.IsPerMessageFailure(ex, clientStillConnected: true).Should().BeTrue(
            "the server judged this message on its merits and will judge it the same way on every retry");
        SmtpMailTransport.IsDeferral(ex, clientStillConnected: true).Should().BeFalse();
    }

    [Fact]
    public void A5xxReply_AfterTheClientDisconnected_AbortsTheBatch()
    {
        var ex = new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.TransactionFailed, "rejected");

        SmtpMailTransport.IsPerMessageFailure(ex, clientStillConnected: false).Should().BeFalse(
            "every remaining message would fail for a reason that has nothing to do with it");
        SmtpMailTransport.IsDeferral(ex, clientStillConnected: false).Should().BeFalse();
    }

    [Theory]
    [InlineData(SmtpStatusCode.MailboxBusy)]              // 450 — greylisting, mailbox busy
    [InlineData(SmtpStatusCode.ErrorInProcessing)]        // 451 — greylisting, local error
    [InlineData(SmtpStatusCode.InsufficientStorage)]      // 452 — mailbox over quota
    public void A4xxToTheRecipient_WhileConnected_IsADeferral_NotAnAbort(SmtpStatusCode status)
    {
        // Before the review fixes this aborted the batch: the restored mail then led the retried tail,
        // so one mailbox that kept answering 4xx stalled every mail queued behind it.
        var ex = new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, status, "try later");

        SmtpMailTransport.IsDeferral(ex, clientStillConnected: true).Should().BeTrue(
            "the reply is about one recipient, and the connection serves the rest of the batch");
        SmtpMailTransport.IsPerMessageFailure(ex, clientStillConnected: true).Should().BeFalse(
            "a transient refusal is retried, not dropped");
    }

    [Theory]
    [InlineData(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.ServiceNotAvailable)] // 421 — closing the channel
    [InlineData(SmtpErrorCode.SenderNotAccepted, SmtpStatusCode.ErrorInProcessing)]     // 451 to MAIL FROM
    [InlineData(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.ErrorInProcessing)]    // 451 to DATA
    [InlineData(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.InsufficientStorage)]  // 452 to DATA — the server's disk
    [InlineData(SmtpErrorCode.UnexpectedStatusCode, SmtpStatusCode.ServiceNotAvailable)]
    public void A4xxAboutTheServerOrTheSender_AbortsTheBatch(SmtpErrorCode code, SmtpStatusCode status)
    {
        var ex = new SmtpCommandException(code, status, "try later");

        SmtpMailTransport.IsDeferral(ex, clientStillConnected: true).Should().BeFalse(
            "it would fail every remaining message the same way, so the batch stops and is retried whole");
        SmtpMailTransport.IsPerMessageFailure(ex, clientStillConnected: true).Should().BeFalse();
    }

    [Fact]
    public void A4xxToTheRecipient_AfterTheClientDisconnected_AbortsTheBatch()
    {
        var ex = new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.InsufficientStorage, "try later");

        SmtpMailTransport.IsDeferral(ex, clientStillConnected: false).Should().BeFalse();
        SmtpMailTransport.IsPerMessageFailure(ex, clientStillConnected: false).Should().BeFalse();
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
        SmtpMailTransport.IsDeferral(ex, clientStillConnected: true).Should().BeFalse();
    }

    // ── The transport, with no socket ─────────────────────────────────────────────

    [Fact]
    public async Task SendBatch_CancelledBeforeConnecting_WrapsWithNothingProcessed()
    {
        SmtpMailTransport transport = Create(port: 1);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        Func<Task> act = () => transport.SendBatchAsync([Mail(), Mail()], cancelled.Token);

        MailBatchInterruptedException ex = (await act.Should().ThrowAsync<MailBatchInterruptedException>()).Which;
        ex.ProcessedCount.Should().Be(0, "nothing was sent, so the caller restores the whole batch");
        ex.DeferredIndices.Should().BeEmpty();
        ex.InnerException.Should().BeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public async Task SendSingle_UnwrapsTheAbort_SoIdentitySeesTheUnderlyingException()
    {
        SmtpMailTransport transport = Create(port: 1);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        Func<Task> act = () => transport.SendAsync(Mail(), cancelled.Token);

        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().NotBeOfType<MailBatchInterruptedException>();
    }

    // ── The transport, over the wire (loopback stub) ──────────────────────────────

    [Fact]
    public async Task SendBatch_ARecipientAnsweredWith452_IsReturnedForRetry_AndTheRestOfTheBatchGoesOut()
    {
        await using var server = new StubSmtpServer(rcpt => rcpt == "full@test.invalid"
            ? "452 4.2.2 Mailbox full"
            : null);
        SmtpMailTransport transport = Create(server.Port);
        using var deadline = new CancellationTokenSource(Deadline);

        IReadOnlyList<int> deferred = await transport
            .SendBatchAsync([Mail("a@test.invalid"), Mail("full@test.invalid"), Mail("c@test.invalid")], deadline.Token)
            .WaitAsync(Deadline);

        deferred.Should().Equal([1], "the full mailbox's message is handed back for a later retry");
        server.Delivered.Should().Equal(["a@test.invalid", "c@test.invalid"],
            "the 452 is about one recipient; it must not stop the messages behind it");
    }

    [Fact]
    public async Task SendBatch_ARecipientAnsweredWith550_IsDropped_AndTheRestOfTheBatchGoesOut()
    {
        await using var server = new StubSmtpServer(rcpt => rcpt == "gone@test.invalid"
            ? "550 5.1.1 No such user"
            : null);
        SmtpMailTransport transport = Create(server.Port);
        using var deadline = new CancellationTokenSource(Deadline);

        IReadOnlyList<int> deferred = await transport
            .SendBatchAsync([Mail("a@test.invalid"), Mail("gone@test.invalid"), Mail("c@test.invalid")], deadline.Token)
            .WaitAsync(Deadline);

        deferred.Should().BeEmpty("a 5xx is terminal: dropped, never retried");
        server.Delivered.Should().Equal(["a@test.invalid", "c@test.invalid"]);
    }

    [Fact]
    public async Task SendBatch_A421AfterADeferral_AbortsWithTheDeferralReported()
    {
        await using var server = new StubSmtpServer(rcpt => rcpt switch
        {
            "full@test.invalid" => "452 4.2.2 Mailbox full",
            "b@test.invalid" => "421 4.7.0 Try again later, closing connection",
            _ => null,
        });
        SmtpMailTransport transport = Create(server.Port);
        using var deadline = new CancellationTokenSource(Deadline);

        Func<Task> act = () => transport
            .SendBatchAsync([Mail("full@test.invalid"), Mail("b@test.invalid"), Mail("c@test.invalid")], deadline.Token)
            .WaitAsync(Deadline);

        MailBatchInterruptedException ex = (await act.Should().ThrowAsync<MailBatchInterruptedException>()).Which;
        ex.ProcessedCount.Should().Be(1, "only the deferred first message was finished with");
        ex.DeferredIndices.Should().Equal([0], "the caller must retry the deferred message too, not just the tail");
        server.Delivered.Should().BeEmpty();
    }

    [Fact]
    public async Task SendSingle_ARecipientAnsweredWith452_IsDropped_NotThrown()
    {
        // Identity's single-message path has no retry queue: a deferral there is terminal (logged,
        // counted, dropped), as every send failure on this path was before WU-NotificationCorrectness.
        await using var server = new StubSmtpServer(_ => "452 4.2.2 Mailbox full");
        SmtpMailTransport transport = Create(server.Port);
        using var deadline = new CancellationTokenSource(Deadline);

        Func<Task> act = () => transport.SendAsync(Mail("full@test.invalid"), deadline.Token).WaitAsync(Deadline);

        await act.Should().NotThrowAsync();
        server.Delivered.Should().BeEmpty();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    // Port 1 refuses a real connection; the no-socket tests cancel before ConnectAsync gets that far.
    private static SmtpMailTransport Create(int port) =>
        new(Options.Create(new EmailOptions
        {
            FromAddress = "noreply@test.invalid",
            Smtp = new EmailSmtpOptions { Host = "127.0.0.1", Port = port, UseStartTls = false }
        }), NullLogger<SmtpMailTransport>.Instance);

    private static OutgoingMail Mail(string to = "someone@test.invalid") =>
        new(to, "Subject", "<p>Body</p>", "Test", 1);

    /// <summary>
    /// A minimal in-process SMTP responder on a loopback ephemeral port, serving one connection: it
    /// advertises no extensions (so MailKit neither pipelines, authenticates nor upgrades to TLS),
    /// answers every command 2xx/3xx except <c>RCPT TO</c>, whose reply the test chooses per address,
    /// and records the recipient of every message whose <c>DATA</c> completed.
    /// </summary>
    private sealed class StubSmtpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly Func<string, string?> _rcptReply;
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _serving;
        private readonly ConcurrentQueue<string> _delivered = new();

        /// <param name="rcptReply">The reply line for a recipient address, or null for "250 OK".</param>
        public StubSmtpServer(Func<string, string?> rcptReply)
        {
            _rcptReply = rcptReply;
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _serving = ServeOneConnectionAsync(_stop.Token);
        }

        public int Port { get; }

        /// <summary>Recipients whose message the stub accepted, in order.</summary>
        public IReadOnlyList<string> Delivered => [.. _delivered];

        private async Task ServeOneConnectionAsync(CancellationToken ct)
        {
            using TcpClient client = await _listener.AcceptTcpClientAsync(ct);
            NetworkStream stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

            await writer.WriteLineAsync("220 stub ESMTP");
            string? recipient = null;
            while (await reader.ReadLineAsync(ct) is string line)
            {
                string verb = (line.Length >= 4 ? line[..4] : line).ToUpperInvariant();
                switch (verb)
                {
                    case "EHLO":
                    case "HELO":
                        await writer.WriteLineAsync("250 stub");
                        break;
                    case "RCPT":
                        int open = line.IndexOf('<');
                        string address = line[(open + 1)..line.IndexOf('>', open)];
                        string? reply = _rcptReply(address);
                        if (reply is null) recipient = address;
                        await writer.WriteLineAsync(reply ?? "250 OK");
                        break;
                    case "DATA":
                        await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                        while (await reader.ReadLineAsync(ct) is string body && body != ".") { }
                        _delivered.Enqueue(recipient!);
                        await writer.WriteLineAsync("250 OK queued");
                        break;
                    case "QUIT":
                        await writer.WriteLineAsync("221 bye");
                        return;
                    default: // MAIL, RSET, NOOP
                        recipient = verb == "MAIL" || verb == "RSET" ? null : recipient;
                        await writer.WriteLineAsync("250 OK");
                        break;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            _listener.Stop();
            try
            {
                await _serving;
            }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or IOException or ObjectDisposedException)
            {
                // The client closed the connection, or the test ended before it connected — the stub
                // has nothing left to serve. Any other exception is a stub bug and fails the test.
            }
            _stop.Dispose();
        }
    }
}
