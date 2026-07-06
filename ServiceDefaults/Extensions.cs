using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    // Every Canalave domain meter (Core's CanalaveTelemetry.{Component}, named
                    // "TheCanalaveLibrary.{Component}") — wildcard so new components light up
                    // without touching this file. String literal on purpose: no ServiceDefaults →
                    // Core project reference (Core carries Identity/EF packages that don't belong
                    // in an Aspire shared project).
                    .AddMeter("TheCanalaveLibrary.*")
                    // Npgsql connection-pool + command metrics — pool saturation is the classic
                    // Blazor Server failure mode with per-method read-context factories.
                    .AddMeter("Npgsql")
                    // Blazor built-in meters (.NET 10): event/navigation duration, component
                    // lifecycle + render batches, circuit active/connected/duration.
                    .AddMeter(
                        "Microsoft.AspNetCore.Components",
                        "Microsoft.AspNetCore.Components.Lifecycle",
                        "Microsoft.AspNetCore.Components.Server.Circuits");
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    // Every Canalave domain ActivitySource — same wildcard/literal rationale as
                    // the meter subscription above.
                    .AddSource("TheCanalaveLibrary.*")
                    // Blazor built-in activities (.NET 10): circuit lifecycle, navigation, and
                    // event-handler spans — the app's real execution spine under InteractiveServer;
                    // custom + Npgsql spans parent under these.
                    .AddSource(
                        "Microsoft.AspNetCore.Components",
                        "Microsoft.AspNetCore.Components.Server.Circuits")
                    // Npgsql per-query spans (SQL text + duration), from Npgsql.OpenTelemetry.
                    .AddNpgsql()
                    .AddAspNetCoreInstrumentation(tracing =>
                    {
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath);
                        // Tag request spans with the authenticated user. Must be the RESPONSE
                        // hook: the request span starts before the auth middleware has populated
                        // HttpContext.User. Circuit dispatches (Blazor's main execution path)
                        // never pass through here — TelemetryCircuitHandler (Server) is their
                        // equivalent.
                        tracing.EnrichWithHttpResponse = (activity, response) =>
                        {
                            string? userId = response.HttpContext.User
                                .FindFirstValue(ClaimTypes.NameIdentifier);
                            if (userId is not null)
                            {
                                activity.SetTag("canalave.user.id", userId);
                            }
                        };
                    })
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks(HealthEndpointPath);

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}