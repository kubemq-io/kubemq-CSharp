// HTTP server: /health /ready /status /summary /metrics
// Uses Kestrel minimal API with prometheus-net AspNetCore for /metrics.

using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace KubeMQ.Burnin;

/// <summary>
/// Burn-in HTTP server providing health, readiness, status, summary, and metrics endpoints.
/// Thread-safe ready flag for Kubernetes readiness probes.
/// </summary>
public sealed class BurninHttpServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly Func<object> _summaryFn;
    private readonly Func<object> _statusFn;
    private volatile bool _ready;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
    };

    /// <summary>
    /// Create and start the HTTP server on the given port.
    /// </summary>
    /// <param name="port">Port to listen on (0.0.0.0).</param>
    /// <param name="summaryFn">Function returning the summary object for /summary.</param>
    /// <param name="statusFn">Function returning the status object for /status.</param>
    public BurninHttpServer(int port, Func<object> summaryFn, Func<object> statusFn)
    {
        _summaryFn = summaryFn;
        _statusFn = statusFn;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(opts =>
        {
            opts.ListenAnyIP(port);
        });

        // Suppress excessive Kestrel logging.
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        _app = builder.Build();

        // Map endpoints.
        _app.MapGet("/health", () => Results.Json(new { status = "alive" }, s_jsonOptions));

        _app.MapGet("/ready", () =>
        {
            if (_ready)
                return Results.Json(new { status = "ready" }, s_jsonOptions, statusCode: 200);
            return Results.Json(new { status = "not_ready" }, s_jsonOptions, statusCode: 503);
        });

        _app.MapGet("/status", () => Results.Json(_statusFn(), s_jsonOptions));

        _app.MapGet("/summary", () => Results.Json(_summaryFn(), s_jsonOptions));

        // Use prometheus-net for /metrics endpoint.
        _app.UseRouting();
        _app.UseHttpMetrics();
        _app.MapMetrics("/metrics");
    }

    /// <summary>
    /// Start the HTTP server (non-blocking).
    /// </summary>
    public Task StartAsync(CancellationToken ct = default)
    {
        return _app.StartAsync(ct);
    }

    /// <summary>
    /// Set the readiness flag (thread-safe).
    /// </summary>
    public void SetReady(bool ready)
    {
        _ready = ready;
    }

    /// <summary>
    /// Whether the server is reporting ready.
    /// </summary>
    public bool IsReady => _ready;

    /// <summary>
    /// Stop the HTTP server gracefully.
    /// </summary>
    public async Task StopAsync(CancellationToken ct = default)
    {
        try
        {
            await _app.StopAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // Swallow shutdown errors.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync().ConfigureAwait(false);
    }
}
