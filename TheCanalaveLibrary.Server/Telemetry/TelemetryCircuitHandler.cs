using System.Diagnostics;
using Microsoft.AspNetCore.Components.Server.Circuits;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Attaches user/circuit correlation to every piece of telemetry a circuit dispatch produces.
///
/// Blazor Server's real execution path is the SignalR circuit, not HTTP — after the initial
/// request, event handlers and JS-interop callbacks never pass through middleware, so an HTTP
/// enrichment hook can't see them. <see cref="CircuitHandler.CreateInboundActivityHandler"/> is
/// the dispatch-boundary equivalent: it wraps <i>every</i> inbound circuit activity, which is
/// exactly where ambient correlation belongs (per logging.md §"Context Scopes" — never
/// per-callsite <c>BeginScope</c> in services).
///
/// Two effects per dispatch:
/// <list type="bullet">
/// <item>A logger scope carrying <c>CircuitId</c> + <c>UserId</c> — flows onto every log record
/// emitted during the dispatch (ServiceDefaults sets <c>IncludeScopes = true</c>).</item>
/// <item><c>canalave.user.id</c> tagged onto <c>Activity.Current</c> (the framework's ambient
/// circuit/event span, subscribed in ServiceDefaults) so traces are user-attributable too. The
/// non-circuit counterpart for minimal-API requests is the <c>EnrichWithHttpResponse</c> hook in
/// ServiceDefaults/Extensions.cs.</item>
/// </list>
///
/// Scoped like every CircuitHandler — one instance per circuit, same scope as the
/// <see cref="IActiveUserContext"/> it reads. The user id is resolved lazily at first dispatch
/// (auth state is established by then) and cached: a circuit never changes identity mid-life
/// (revalidation kicks a signed-out user to a fresh circuit).
/// </summary>
public class TelemetryCircuitHandler(
    IActiveUserContext activeUser,
    ILogger<TelemetryCircuitHandler> logger) : CircuitHandler
{
    private string? _circuitId;
    private int? _userId;

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _circuitId = circuit.Id;
        return Task.CompletedTask;
    }

    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next)
    {
        return async context =>
        {
            // Anonymous circuits re-check each dispatch (cheap claims parse, no I/O); once a
            // user id is seen it's cached for the circuit's lifetime.
            _userId ??= activeUser.UserId;

            if (_userId is int userId)
            {
                Activity.Current?.SetTag("canalave.user.id", userId);
            }

            using (logger.BeginScope(new Dictionary<string, object?>
                   {
                       ["CircuitId"] = _circuitId ?? context.Circuit.Id,
                       ["UserId"] = _userId,
                   }))
            {
                await next(context);
            }
        };
    }
}
