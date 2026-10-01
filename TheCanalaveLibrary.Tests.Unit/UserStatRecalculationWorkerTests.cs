using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// One scheduled run of <see cref="UserStatRecalculationWorker"/> (Feature 58; owner ruling D21,
/// WU-CounterSymmetry). Two behaviors are pinned. The content pass runs <b>before</b> the UserStat pass,
/// because <c>words_written</c> sums the <c>stories.word_count</c> the content pass corrects. And a
/// failing pass is logged without blocking the next one. <c>TestAppFactory</c> removes this hosted
/// worker, so the Integration tier never reaches it.
/// <para>
/// The two recalculators resolve from a hand-built container whose factories record the order they were
/// asked for in, and then throw. No <c>DbContext</c> is ever constructed (testing.md's Unit placement
/// rule).
/// </para>
/// Tier: Unit.
/// </summary>
public class UserStatRecalculationWorkerTests
{
    [Fact]
    public async Task RunPasses_RunsTheContentPassFirst_AndAFailingPassDoesNotBlockTheNext()
    {
        List<string> resolved = [];
        (UserStatRecalculationWorker worker, FakeLogger<UserStatRecalculationWorker> logger, ServiceProvider provider) =
            Build(resolved, new InvalidOperationException("content pass down"));
        await using (provider)
        {
            bool keepRunning = await worker.RunPassesAsync(CancellationToken.None);

            keepRunning.Should().BeTrue("a failed pass is logged and the loop carries on to the next window");
        }

        resolved.Should().Equal(["content", "userstat"],
            "words_written sums stories.word_count, so the content pass must correct it first; the content " +
            "pass failing must not stop the UserStat pass");
        logger.Collector.GetSnapshot()
            .Where(r => r.Level == LogLevel.Error)
            .Select(r => r.Message)
            .Should().SatisfyRespectively(
                first => first.Should().StartWith("Daily content counter recalculation failed"),
                second => second.Should().StartWith("Daily UserStat recalculation failed"));
    }

    [Fact]
    public async Task RunPasses_WhenTheHostIsStopping_ReturnsFalse_AndSkipsTheUserStatPass()
    {
        using CancellationTokenSource stopping = new();
        await stopping.CancelAsync();

        List<string> resolved = [];
        (UserStatRecalculationWorker worker, _, ServiceProvider provider) =
            Build(resolved, new OperationCanceledException(stopping.Token));
        await using (provider)
        {
            (await worker.RunPassesAsync(stopping.Token)).Should().BeFalse("shutdown ends the loop");
        }

        resolved.Should().Equal(["content"]);
    }

    /// <summary>The content-pass factory throws <paramref name="contentFailure"/>; the UserStat factory
    /// always throws, so both passes fail without touching a database.</summary>
    private static (UserStatRecalculationWorker, FakeLogger<UserStatRecalculationWorker>, ServiceProvider) Build(
        List<string> resolved, Exception contentFailure)
    {
        ServiceCollection services = new();
        services.AddScoped<ContentCounterRecalculator>(_ =>
        {
            resolved.Add("content");
            throw contentFailure;
        });
        services.AddScoped<UserStatRecalculator>(_ =>
        {
            resolved.Add("userstat");
            throw new InvalidOperationException("userstat pass down");
        });
        ServiceProvider provider = services.BuildServiceProvider();
        FakeLogger<UserStatRecalculationWorker> logger = new();
        UserStatRecalculationWorker worker = new(
            provider.GetRequiredService<IServiceScopeFactory>(), new ConfigurationBuilder().Build(), logger);
        return (worker, logger, provider);
    }
}
