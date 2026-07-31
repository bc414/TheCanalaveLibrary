using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ApiExceptionHandler"/> (WU-ErrorHandling2, 2026-07-30) — constructed
/// directly over a minimal <see cref="IProblemDetailsService"/>, no host. The behavior worth
/// pinning: the handler answers only <c>/api/*</c> requests (everything else falls through to the
/// existing HTML <c>UseExceptionHandler("/Error")</c> path), and the JSON body it writes carries a
/// <c>traceId</c> extension the client reconstructs into <c>ServerFaultException</c>. Tier: Unit.
/// </summary>
public class ApiExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_ApiPath_Returns500WithTraceIdEnvelope()
    {
        ApiExceptionHandler handler = NewHandler();
        DefaultHttpContext httpContext = NewHttpContext("/api/stories/query");
        using MemoryStream body = new();
        httpContext.Response.Body = body;

        bool handled = await handler.TryHandleAsync(
            httpContext, new InvalidOperationException("boom"), CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        body.Position = 0;
        using JsonDocument doc = await JsonDocument.ParseAsync(body);
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(500);
        doc.RootElement.TryGetProperty("traceId", out JsonElement traceId).Should().BeTrue();
        traceId.GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TryHandleAsync_NonApiPath_ReturnsFalse_AndLeavesResponseUntouched()
    {
        ApiExceptionHandler handler = NewHandler();
        DefaultHttpContext httpContext = NewHttpContext("/status-code/500");
        using MemoryStream body = new();
        httpContext.Response.Body = body;

        bool handled = await handler.TryHandleAsync(
            httpContext, new InvalidOperationException("boom"), CancellationToken.None);

        // Falls through to the HTML UseExceptionHandler("/Error") path (Program.cs) — this handler
        // must not have written a response, or that fallback would double-write.
        handled.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK); // untouched default
        body.Length.Should().Be(0);
    }

    private static ApiExceptionHandler NewHandler()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddProblemDetails();
        ServiceProvider provider = services.BuildServiceProvider();
        return new ApiExceptionHandler(
            provider.GetRequiredService<IProblemDetailsService>(),
            NullLogger<ApiExceptionHandler>.Instance);
    }

    private static DefaultHttpContext NewHttpContext(string path)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddProblemDetails();
        ServiceProvider provider = services.BuildServiceProvider();

        DefaultHttpContext httpContext = new() { RequestServices = provider };
        httpContext.Request.Path = path;
        return httpContext;
    }
}
