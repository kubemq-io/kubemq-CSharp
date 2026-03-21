// HTTP server: full REST API per spec.
// v2: POST /run/start accepts v2 patterns format, rejects v1 with 400.
// GET /info returns burnin_spec_version: "2".
// GET /run, /run/status include channels count per pattern.
// POST /cleanup deletes all N channels per pattern.

using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace KubeMQ.Burnin;

public sealed class BurninHttpServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    private static readonly JsonSerializerOptions s_jsonWrite = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions s_jsonRead = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private volatile bool _statusDeprecationLogged;
    private volatile bool _summaryDeprecationLogged;

    public BurninHttpServer(int port, Engine engine, string corsOrigins = "*")
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(opts => opts.ListenAnyIP(port));
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        _app = builder.Build();

        // CORS middleware
        _app.Use(async (ctx, next) =>
        {
            ctx.Response.Headers.Append("Access-Control-Allow-Origin", corsOrigins);
            ctx.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            ctx.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type");

            if (ctx.Request.Method == "OPTIONS")
            {
                ctx.Response.StatusCode = 204;
                return;
            }
            await next();
        });

        // --- Health & Ready ---

        _app.MapGet("/health", () => Results.Json(new { status = "alive" }, s_jsonWrite));

        _app.MapGet("/ready", () =>
        {
            var state = engine.State;
            bool ready = state is RunState.Idle or RunState.Running or RunState.Stopped or RunState.Error;
            int code = ready ? 200 : 503;
            return Results.Json(new { status = ready ? "ready" : "not_ready", state = state.ToApi() }, s_jsonWrite, statusCode: code);
        });

        // --- GET /info (v2: burnin_spec_version: "2") ---

        _app.MapGet("/info", () => Results.Json(engine.GetInfo(), s_jsonWrite));

        // --- GET /broker/status ---

        _app.MapGet("/broker/status", async () =>
        {
            var result = await engine.PingBrokerAsync();
            return Results.Json(result, s_jsonWrite);
        });

        // --- POST /run/start (v2: accepts patterns block, rejects v1 with 400, returns 202) ---

        _app.MapPost("/run/start", async (HttpContext ctx) =>
        {
            var state = engine.State;
            if (!state.CanStart())
            {
                ctx.Response.StatusCode = 409;
                return Results.Json(new { message = "Run already active", run_id = engine.RunId, state = state.ToApi() }, s_jsonWrite, statusCode: 409);
            }

            RunStartRequest? req;
            try
            {
                string body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(body))
                    req = new RunStartRequest();
                else
                    req = JsonSerializer.Deserialize<RunStartRequest>(body, s_jsonRead);
            }
            catch (JsonException ex)
            {
                return Results.Json(new { message = $"Invalid JSON: {ex.Message}" }, s_jsonWrite, statusCode: 400);
            }

            // v1 format detection (dual-layer)
            var (isV1, v1Msg, v1Errors) = ApiConfigTranslator.DetectV1Format(req);
            if (isV1)
            {
                return Results.Json(new { message = v1Msg, errors = v1Errors }, s_jsonWrite, statusCode: 400);
            }

            var errors = ApiConfigTranslator.Validate(req);
            if (errors.Count > 0)
            {
                return Results.Json(new { message = "Configuration validation failed", errors }, s_jsonWrite, statusCode: 400);
            }

            var (runId, error) = engine.StartRun(req);
            if (error != null)
            {
                if (error.Contains("Cannot start") || error.Contains("race condition"))
                    return Results.Json(new { message = error, run_id = engine.RunId, state = engine.State.ToApi() }, s_jsonWrite, statusCode: 409);
                return Results.Json(new { message = "Configuration validation failed", errors = new[] { error } }, s_jsonWrite, statusCode: 400);
            }

            var (totalChannels, patternCount) = engine.GetRunChannelInfo();
            return Results.Json(new
            {
                status = "starting",
                run_id = runId,
                message = $"run starting with {totalChannels} channels across {patternCount} patterns",
            }, s_jsonWrite, statusCode: 202);
        });

        // --- POST /run/stop ---

        _app.MapPost("/run/stop", () =>
        {
            var state = engine.State;
            if (!state.CanStop())
            {
                string msg = state == RunState.Stopping ? "Run is already stopping" : "No active run to stop";
                return Results.Json(new { message = msg, run_id = engine.RunId, state = state.ToApi() }, s_jsonWrite, statusCode: 409);
            }

            var (success, error) = engine.StopRun();
            if (!success)
                return Results.Json(new { message = error, run_id = engine.RunId, state = engine.State.ToApi() }, s_jsonWrite, statusCode: 409);

            return Results.Json(new
            {
                state = "stopping",
                message = "Graceful shutdown initiated",
            }, s_jsonWrite, statusCode: 202);
        });

        // --- GET /run (v2: includes channels count per pattern) ---

        _app.MapGet("/run", () => Results.Json(engine.GetRunData(), s_jsonWrite));

        // --- GET /run/status (v2: includes channels count per pattern) ---

        _app.MapGet("/run/status", () => Results.Json(engine.GetRunStatus(), s_jsonWrite));

        // --- GET /run/config ---

        _app.MapGet("/run/config", () =>
        {
            var config = engine.GetRunConfig();
            if (config == null)
                return Results.Json(new { message = "No run configuration available" }, s_jsonWrite, statusCode: 404);
            return Results.Json(config, s_jsonWrite);
        });

        // --- GET /run/report ---

        _app.MapGet("/run/report", () =>
        {
            var report = engine.GetReport();
            if (report == null)
                return Results.Json(new { message = "No completed run report available" }, s_jsonWrite, statusCode: 404);
            return Results.Json(report, s_jsonWrite);
        });

        // --- POST /cleanup (v2: deletes all N channels per pattern) ---

        _app.MapPost("/cleanup", async () =>
        {
            var state = engine.State;
            if (state is RunState.Starting or RunState.Running or RunState.Stopping)
            {
                return Results.Json(new { message = "Cannot cleanup while a run is active", run_id = engine.RunId, state = state.ToApi() }, s_jsonWrite, statusCode: 409);
            }
            var result = await engine.CleanupChannelsAsync();
            return Results.Json(result, s_jsonWrite);
        });

        // --- Legacy aliases ---

        _app.MapGet("/status", () =>
        {
            if (!_statusDeprecationLogged)
            {
                Console.Error.WriteLine("DEPRECATION WARNING: /status is deprecated, use /run/status");
                _statusDeprecationLogged = true;
            }
            return Results.Json(engine.GetRunStatus(), s_jsonWrite);
        });

        _app.MapGet("/summary", () =>
        {
            if (!_summaryDeprecationLogged)
            {
                Console.Error.WriteLine("DEPRECATION WARNING: /summary is deprecated, use /run/report");
                _summaryDeprecationLogged = true;
            }
            var report = engine.GetReport();
            if (report == null)
                return Results.Json(new { message = "No completed run report available" }, s_jsonWrite, statusCode: 404);
            return Results.Json(report, s_jsonWrite);
        });

        // --- Prometheus /metrics ---

        _app.UseRouting();
        _app.UseHttpMetrics();
        _app.MapMetrics("/metrics");
    }

    public Task StartAsync(CancellationToken ct = default) => _app.StartAsync(ct);

    public async Task StopAsync(CancellationToken ct = default)
    {
        try { await _app.StopAsync(ct).ConfigureAwait(false); } catch { }
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync().ConfigureAwait(false);
    }
}
