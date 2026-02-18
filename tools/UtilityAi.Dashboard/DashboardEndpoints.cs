using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace UtilityAi.Dashboard;

/// <summary>
/// Extension methods to map the UtilityAI Dashboard endpoints onto an ASP.NET Core application.
/// </summary>
public static class DashboardEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>
    /// Maps the UtilityAI Dashboard API endpoints and serves the embedded HTML dashboard.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder (e.g., <c>app</c>).</param>
    /// <param name="state">The shared <see cref="DashboardState"/> that the <see cref="DashboardSink"/> writes to.</param>
    /// <param name="prefix">URL prefix for the dashboard. Defaults to <c>/utilityai</c>.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    /// <example>
    /// <code>
    /// var state = new DashboardState();
    /// var sink = new DashboardSink(state);
    ///
    /// var app = builder.Build();
    /// app.MapUtilityAiDashboard(state);
    ///
    /// await orchestrator.RunAsync(intent, 10, ct, sink: sink);
    /// </code>
    /// </example>
    public static IEndpointRouteBuilder MapUtilityAiDashboard(
        this IEndpointRouteBuilder endpoints,
        DashboardState state,
        string prefix = "/utilityai")
    {
        prefix = prefix.TrimEnd('/');

        // API: Get current state snapshot
        endpoints.MapGet($"{prefix}/api/state", () =>
        {
            var snapshot = new
            {
                version = state.Version,
                activeProposalId = state.ActiveProposalId,
                stopReason = state.StopReason?.ToString(),
                currentTick = state.CurrentTick,
                ticks = state.Ticks,
                priorOverrides = state.PriorOverrides,
                temperatureOverrides = state.TemperatureOverrides
            };
            return Results.Json(snapshot, JsonOptions);
        });

        // API: Get tick history
        endpoints.MapGet($"{prefix}/api/ticks", () =>
        {
            return Results.Json(state.Ticks, JsonOptions);
        });

        // API: Set prior override
        endpoints.MapPost($"{prefix}/api/overrides/prior", async (HttpContext ctx) =>
        {
            var body = await JsonSerializer.DeserializeAsync<OverrideRequest>(ctx.Request.Body, JsonOptions);
            if (body is null || string.IsNullOrWhiteSpace(body.ProposalId))
                return Results.BadRequest("proposalId is required");
            state.SetPriorOverride(body.ProposalId, body.Value);
            return Results.Ok();
        });

        // API: Set temperature override
        endpoints.MapPost($"{prefix}/api/overrides/temperature", async (HttpContext ctx) =>
        {
            var body = await JsonSerializer.DeserializeAsync<OverrideRequest>(ctx.Request.Body, JsonOptions);
            if (body is null || string.IsNullOrWhiteSpace(body.ProposalId))
                return Results.BadRequest("proposalId is required");
            state.SetTemperatureOverride(body.ProposalId, body.Value);
            return Results.Ok();
        });

        // API: Remove override
        endpoints.MapDelete($"{prefix}/api/overrides/{{proposalId}}", (string proposalId) =>
        {
            state.RemovePriorOverride(proposalId);
            state.RemoveTemperatureOverride(proposalId);
            return Results.Ok();
        });

        // API: Reset state
        endpoints.MapPost($"{prefix}/api/reset", () =>
        {
            state.Reset();
            return Results.Ok();
        });

        // SSE: Server-Sent Events stream for real-time updates
        endpoints.MapGet($"{prefix}/api/events", async (HttpContext ctx) =>
        {
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers["Cache-Control"] = "no-cache";
            ctx.Response.Headers["Connection"] = "keep-alive";

            var ct = ctx.RequestAborted;
            long lastVersion = -1;

            while (!ct.IsCancellationRequested)
            {
                var currentVersion = state.Version;
                if (currentVersion != lastVersion)
                {
                    lastVersion = currentVersion;
                    var snapshot = new
                    {
                        version = state.Version,
                        activeProposalId = state.ActiveProposalId,
                        stopReason = state.StopReason?.ToString(),
                        currentTick = state.CurrentTick,
                        tickCount = state.Ticks.Count,
                        priorOverrides = state.PriorOverrides,
                        temperatureOverrides = state.TemperatureOverrides
                    };
                    var json = JsonSerializer.Serialize(snapshot, JsonOptions);
                    await ctx.Response.WriteAsync($"data: {json}\n\n", ct);
                    await ctx.Response.Body.FlushAsync(ct);
                }

                await Task.Delay(200, ct);
            }
        });

        // Serve the embedded HTML dashboard
        endpoints.MapGet($"{prefix}", (HttpContext ctx) =>
        {
            ctx.Response.Redirect($"{prefix}/dashboard");
            return Task.CompletedTask;
        });

        endpoints.MapGet($"{prefix}/dashboard", async (HttpContext ctx) =>
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("index.html"));

            if (resourceName is null)
            {
                ctx.Response.StatusCode = 500;
                var availableResources = string.Join(", ", assembly.GetManifestResourceNames());
                await ctx.Response.WriteAsync(
                    $"Dashboard HTML resource not found. Available resources: {availableResources}");
                return;
            }

            ctx.Response.ContentType = "text/html; charset=utf-8";
            var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.WriteAsync($"Failed to load resource stream: {resourceName}");
                return;
            }

            await using (stream)
            {
                await stream.CopyToAsync(ctx.Response.Body);
            }
        });

        return endpoints;
    }

    private sealed class OverrideRequest
    {
        public string ProposalId { get; set; } = "";
        public double Value { get; set; }
    }
}
