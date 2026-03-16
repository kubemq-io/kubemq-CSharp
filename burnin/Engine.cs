// Engine: full orchestrator for all 6 workers, warmup, periodic tasks, 2-phase shutdown.
// Creates KubeMQClient with ReconnectOptions (MaxAttempts=0=unlimited) and RetryPolicy (JitterMode on RetryPolicy).

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Config;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.EventsStore;
using KubeMQ.Sdk.Queues;
using KubeMQ.Sdk.Commands;
using KubeMQ.Sdk.Queries;
using KubeMQ.Burnin.Workers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace KubeMQ.Burnin;

/// <summary>
/// All 6 messaging patterns used in the burn-in test.
/// </summary>
public static class AllPatterns
{
    public static readonly string[] Names =
    {
        "events", "events_store", "queue_stream", "queue_simple", "commands", "queries"
    };
}

/// <summary>
/// Full burn-in test orchestrator. Manages the KubeMQ client, all workers,
/// warmup, periodic reporting, Kestrel HTTP server, and 2-phase shutdown.
/// </summary>
public sealed class Engine : IDisposable, IClientRecreator
{
    private const string ChannelPrefix = "csharp_burnin_";
    private const int WarmupCount = 10;
    private const int MemoryBaselineSec = 300;

    private readonly BurninConfig _cfg;
    private KubeMQClient? _client;
    private readonly List<BaseWorker> _workers = new();
    private BurninHttpServer? _httpServer;
    private readonly Stopwatch _uptime = new();
    private string _startedAt = "";
    private string _endedAt = "";
    private Stopwatch? _testStopwatch;
    private readonly Dictionary<string, string> _patternStatus = new();
    private double _baselineRss;
    private double _peakRss;
    private int _peakWorkers;
    private readonly List<Timer> _timers = new();

    public Engine(BurninConfig config)
    {
        _cfg = config;
    }

    /// <summary>
    /// Run the burn-in test. Blocks until duration expires or cancellation is requested.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        _uptime.Start();
        _startedAt = DateTime.UtcNow.ToString("o");

        // Step 1: Create client with reconnect + retry policies
        _client = CreateClient();
        await _client.ConnectAsync(ct);
        Console.WriteLine($"client created, pinging broker at {_cfg.Broker.Address}");
        var pingResult = await _client.PingAsync(ct);
        Console.WriteLine($"broker ping ok: {pingResult.Host} v{pingResult.Version}");

        // Step 2: Clean stale channels (broad prefix, no runId -- Go#8)
        await CleanStaleChannelsAsync(ct);

        // Step 3: Create channels (with runId)
        await CreateChannelsAsync(ct);

        // Step 4: Start Kestrel HTTP server
        _httpServer = new BurninHttpServer(
            _cfg.Metrics.Port,
            () => BuildSummary(),
            () => new Dictionary<string, string>(_patternStatus));
        await _httpServer.StartAsync(ct);
        Console.WriteLine($"HTTP server started on port {_cfg.Metrics.Port}");

        // Benchmark mode: auto-set all rates to 0 (unlimited) -- GAP-19
        if (_cfg.Mode == "benchmark")
        {
            _cfg.Rates.Events = 0;
            _cfg.Rates.EventsStore = 0;
            _cfg.Rates.QueueStream = 0;
            _cfg.Rates.QueueSimple = 0;
            _cfg.Rates.Commands = 0;
            _cfg.Rates.Queries = 0;
        }

        // Set target rates
        Metrics.SetTargetRate("events", _cfg.Rates.Events);
        Metrics.SetTargetRate("events_store", _cfg.Rates.EventsStore);
        Metrics.SetTargetRate("queue_stream", _cfg.Rates.QueueStream);
        Metrics.SetTargetRate("queue_simple", _cfg.Rates.QueueSimple);
        Metrics.SetTargetRate("commands", _cfg.Rates.Commands);
        Metrics.SetTargetRate("queries", _cfg.Rates.Queries);

        // Create workers
        CreateWorkers();
        foreach (string p in AllPatterns.Names)
            _patternStatus[p] = "starting";

        // Step 5: Start consumers ONLY
        foreach (var w in _workers)
        {
            await w.StartConsumersAsync(_client);
            Console.WriteLine($"consumers started: {w.Pattern}");
        }

        // Step 6: Run warmup (10 messages per pattern using production APIs)
        await RunWarmupAsync(ct);
        _httpServer.SetReady(true);

        // Step 7: Start producers (after consumers + warmup)
        foreach (var w in _workers)
        {
            await w.StartProducersAsync(_client);
            Console.WriteLine($"producers started: {w.Pattern}");
        }

        // Step 8: Print banner immediately after producers start (GAP-C/Q)
        PrintBanner();

        // Warmup period wait + reset
        double warmupSec = Config.WarmupDurationSec(_cfg);
        if (warmupSec > 0)
        {
            Metrics.SetWarmupActive(1);
            Console.WriteLine($"warmup period: {warmupSec}s");
            await SafeDelay((int)(warmupSec * 1000), ct);
            if (ct.IsCancellationRequested) return;
            foreach (var w in _workers)
                w.ResetAfterWarmup();
            Metrics.SetWarmupActive(0);
            Console.WriteLine("warmup complete, counters reset");
        }

        // Record test start, set running
        _testStopwatch = Stopwatch.StartNew();
        foreach (string p in AllPatterns.Names)
            _patternStatus[p] = "running";

        // Step 9: Start periodic tasks
        StartPeriodicTasks();

        // Disconnect manager
        var dm = new DisconnectManager(
            Config.ForcedDisconnectIntervalSec(_cfg),
            Config.ForcedDisconnectDurationSec(_cfg),
            this);
        if (dm.Enabled)
        {
            dm.Start();
            Console.WriteLine("disconnect manager enabled");
        }

        // Wait for duration or signal
        double dur = Config.DurationSec(_cfg.Duration);
        Console.WriteLine($"running for {dur}s (or until stopped)");
        await SafeDelay((int)(dur * 1000), ct);

        if (dm.Enabled) dm.Stop();
    }

    /// <summary>
    /// Execute 2-phase shutdown with hard deadline (drain + 5s).
    /// Shutdown order: cleanup channels -> write report -> print console -> dispose client.
    /// Returns true if verdict passed.
    /// </summary>
    public async Task<bool> ShutdownAsync()
    {
        _endedAt = DateTime.UtcNow.ToString("o");

        // Stop periodic timers
        foreach (var timer in _timers)
            await timer.DisposeAsync();
        _timers.Clear();

        Console.WriteLine("initiating 2-phase shutdown");

        int drainSec = _cfg.Shutdown.DrainTimeoutSeconds;
        DateTime hardDeadline = DateTime.UtcNow.AddSeconds(drainSec + 5);

        // Phase 1: Stop producers
        foreach (string p in AllPatterns.Names)
            _patternStatus[p] = "draining";
        foreach (var w in _workers)
            w.StopProducers();
        Console.WriteLine($"producers stopped, draining for {drainSec}s");
        await Task.Delay(drainSec * 1000);

        // Phase 2: Stop consumers (with timeout enforcement)
        foreach (var w in _workers)
            w.StopConsumers();
        foreach (string p in AllPatterns.Names)
            _patternStatus[p] = "stopped";
        Console.WriteLine("consumers stopped");

        if (_baselineRss == 0)
            _baselineRss = GetRssMb();

        // Cleanup channels BEFORE report (F14, with timeout guard)
        int remaining = Math.Max(1000, (int)(hardDeadline - DateTime.UtcNow).TotalMilliseconds);
        if (_cfg.Shutdown.CleanupChannels)
        {
            using var cleanupCts = new CancellationTokenSource(remaining);
            try
            {
                await CleanStaleChannelsAsync(cleanupCts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("channel cleanup timed out");
            }
        }

        // Build summary + verdict
        var summary = BuildSummary("completed");
        var verdict = Report.GenerateVerdict(summary, _cfg.Thresholds, _cfg.Mode);
        summary.Verdict = verdict;

        // Write report THEN print console (spec order F14)
        if (!string.IsNullOrEmpty(_cfg.Output.ReportFile))
            Report.WriteJsonReport(summary, _cfg.Output.ReportFile);
        Report.PrintConsoleReport(summary);

        // Dispose client and stop HTTP server (with timeout guard)
        int closeRemaining = Math.Max(500, (int)(hardDeadline - DateTime.UtcNow).TotalMilliseconds);
        using var closeCts = new CancellationTokenSource(closeRemaining);
        try
        {
            _client?.Dispose();
            if (_httpServer != null)
                await _httpServer.StopAsync(closeCts.Token);
        }
        catch { /* best effort */ }

        return verdict.Passed;
    }

    // --- IClientRecreator interface ---

    public async Task CloseClientAsync()
    {
        foreach (string p in AllPatterns.Names)
        {
            _patternStatus[p] = "disconnected";
            Metrics.SetActiveConnections(p, 0);
        }
        try { _client?.Dispose(); } catch { }
    }

    public async Task RecreateClientAsync()
    {
        _client = CreateClient();
        foreach (string p in AllPatterns.Names)
            _patternStatus[p] = "running";
    }

    // --- Private: Client Creation ---

    private KubeMQClient CreateClient()
    {
        int initMs = Math.Clamp((int)Config.ReconnectIntervalMs(_cfg), 50, 5000);
        int maxMs = Math.Clamp((int)Config.ReconnectMaxIntervalMs(_cfg), 1000, 120000);
        double mult = Math.Clamp(_cfg.Recovery.ReconnectMultiplier, 1.5, 3.0);

        var clientConfig = new KubeMQClientOptions
        {
            Address = _cfg.Broker.Address,
            ClientId = $"{_cfg.Broker.ClientIdPrefix}-{_cfg.RunId}",
            Reconnect = new ReconnectOptions
            {
                MaxAttempts = 0, // 0 = unlimited
                InitialDelay = TimeSpan.FromMilliseconds(initMs),
                MaxDelay = TimeSpan.FromMilliseconds(maxMs),
                BackoffMultiplier = mult,
            },
            Retry = new RetryPolicy
            {
                MaxRetries = 5,
                InitialBackoff = TimeSpan.FromMilliseconds(initMs),
                MaxBackoff = TimeSpan.FromMilliseconds(maxMs),
                BackoffMultiplier = mult,
                JitterMode = JitterMode.Full,
            },
        };

        return new KubeMQClient(clientConfig);
    }

    private void CreateWorkers()
    {
        string rid = _cfg.RunId;
        _workers.Add(new EventsWorker(_cfg, rid));
        _workers.Add(new EventsStoreWorker(_cfg, rid));
        _workers.Add(new QueueStreamWorker(_cfg, rid));
        _workers.Add(new QueueSimpleWorker(_cfg, rid));
        _workers.Add(new CommandsWorker(_cfg, rid));
        _workers.Add(new QueriesWorker(_cfg, rid));
    }

    // --- Private: Channels ---

    private async Task CreateChannelsAsync(CancellationToken ct)
    {
        if (_client == null) return;
        string rid = _cfg.RunId;

        var ops = new (string name, Func<Task> create)[]
        {
            ("events", () => _client.CreateEventsChannelAsync($"csharp_burnin_{rid}_events_001", ct)),
            ("events_store", () => _client.CreateEventsStoreChannelAsync($"csharp_burnin_{rid}_events_store_001", ct)),
            ("queue_stream", () => _client.CreateQueuesChannelAsync($"csharp_burnin_{rid}_queue_stream_001", ct)),
            ("queue_simple", () => _client.CreateQueuesChannelAsync($"csharp_burnin_{rid}_queue_simple_001", ct)),
            ("commands", () => _client.CreateCommandsChannelAsync($"csharp_burnin_{rid}_commands_001", ct)),
            ("queries", () => _client.CreateQueriesChannelAsync($"csharp_burnin_{rid}_queries_001", ct)),
        };

        foreach (var (name, createFn) in ops)
        {
            try { await createFn(); }
            catch { /* idempotent -- channel may already exist */ }
        }

        Console.WriteLine($"channels created/verified for run_id={rid}");
    }

    private async Task CleanStaleChannelsAsync(CancellationToken ct)
    {
        if (_client == null) return;
        Console.WriteLine($"cleaning stale channels with prefix '{ChannelPrefix}'");
        int count = 0;

        count += await CleanChannelTypeAsync(
            () => _client.ListEventsChannelsAsync(ChannelPrefix, ct),
            n => _client.DeleteEventsChannelAsync(n, ct));
        count += await CleanChannelTypeAsync(
            () => _client.ListEventsStoreChannelsAsync(ChannelPrefix, ct),
            n => _client.DeleteEventsStoreChannelAsync(n, ct));
        count += await CleanChannelTypeAsync(
            () => _client.ListQueuesChannelsAsync(ChannelPrefix, ct),
            n => _client.DeleteQueuesChannelAsync(n, ct));
        count += await CleanChannelTypeAsync(
            () => _client.ListCommandsChannelsAsync(ChannelPrefix, ct),
            n => _client.DeleteCommandsChannelAsync(n, ct));
        count += await CleanChannelTypeAsync(
            () => _client.ListQueriesChannelsAsync(ChannelPrefix, ct),
            n => _client.DeleteQueriesChannelAsync(n, ct));

        Console.WriteLine($"cleaned {count} stale channels");
    }

    private static async Task<int> CleanChannelTypeAsync(
        Func<Task<IReadOnlyList<KubeMQ.Sdk.Common.ChannelInfo>>> listFn, Func<string, Task> deleteFn)
    {
        int deleted = 0;
        try
        {
            var channels = await listFn();
            if (channels == null) return 0;
            foreach (var ch in channels)
            {
                try
                {
                    string name = ch.Name;
                    await deleteFn(name);
                    deleted++;
                }
                catch { /* best effort */ }
            }
        }
        catch { /* channel type may not exist */ }
        return deleted;
    }

    // --- Private: Warmup ---

    private async Task RunWarmupAsync(CancellationToken ct)
    {
        if (_client == null) return;
        Console.WriteLine("running warmup verification");

        await WarmupEventsAsync(ct);
        await WarmupEventsStoreAsync(ct);
        await WarmupQueueAsync("queue_stream", ct);
        await WarmupQueueAsync("queue_simple", ct);
        await WarmupRpcAsync("commands", ct);
        await WarmupRpcAsync("queries", ct);

        Console.WriteLine("warmup verification complete");
    }

    private async Task WarmupEventsAsync(CancellationToken ct)
    {
        string ch = $"csharp_burnin_{_cfg.RunId}_events_001";
        int count = 0;
        string clientId = $"{_cfg.Broker.ClientIdPrefix}-{_cfg.RunId}";

        using var warmupCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var subscription = new EventsSubscription { Channel = ch };
        var consumerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var evt in _client!.SubscribeToEventsAsync(subscription, warmupCts.Token))
                {
                    if (evt.Tags != null && evt.Tags.TryGetValue("warmup", out var v) && v == "true")
                        Interlocked.Increment(ref count);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        });

        await Task.Delay(1000, ct);

        // Use stream API (same as production -- GAP-X)
        var stream = await _client!.CreateEventStreamAsync(null, ct);
        for (int i = 0; i < WarmupCount; i++)
        {
            try
            {
                var msg = new EventMessage
                {
                    Channel = ch,
                    Body = Encoding.UTF8.GetBytes($"warmup-{i}"),
                    Tags = new Dictionary<string, string>
                        { ["warmup"] = "true", ["content_hash"] = "00000000" },
                };
                await stream.SendAsync(msg, clientId, ct);
            }
            catch { }
        }

        await Task.Delay(2000, ct);
        warmupCts.Cancel();
        await stream.DisposeAsync();

        int received = Interlocked.CompareExchange(ref count, 0, 0);
        if (received < WarmupCount)
            Console.Error.WriteLine($"warmup events: only {received}/{WarmupCount} received");
        else
            Console.WriteLine($"warmup events: sent={WarmupCount} received={received}");
    }

    private async Task WarmupEventsStoreAsync(CancellationToken ct)
    {
        string ch = $"csharp_burnin_{_cfg.RunId}_events_store_001";
        int count = 0;
        string clientId = $"{_cfg.Broker.ClientIdPrefix}-{_cfg.RunId}";

        using var warmupCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var subscription = new EventStoreSubscription
        {
            Channel = ch,
            StartPosition = EventStoreStartPosition.FromNew,
        };

        var consumerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var evt in _client!.SubscribeToEventStoreAsync(subscription, warmupCts.Token))
                {
                    if (evt.Tags != null && evt.Tags.TryGetValue("warmup", out var v) && v == "true")
                        Interlocked.Increment(ref count);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        });

        await Task.Delay(1000, ct);

        var stream = await _client!.CreateEventStoreStreamAsync(ct);
        for (int i = 0; i < WarmupCount; i++)
        {
            try
            {
                var msg = new EventStoreMessage
                {
                    Channel = ch,
                    Body = Encoding.UTF8.GetBytes($"warmup-{i}"),
                    Tags = new Dictionary<string, string>
                        { ["warmup"] = "true", ["content_hash"] = "00000000" },
                };
                await stream.SendAsync(msg, clientId, ct);
            }
            catch { }
        }

        await Task.Delay(2000, ct);
        warmupCts.Cancel();
        await stream.DisposeAsync();

        int received = Interlocked.CompareExchange(ref count, 0, 0);
        if (received < WarmupCount)
            Console.Error.WriteLine($"warmup events_store: only {received}/{WarmupCount} received");
        else
            Console.WriteLine($"warmup events_store: sent={WarmupCount} received={received}");
    }

    private async Task WarmupQueueAsync(string pattern, CancellationToken ct)
    {
        string ch = $"csharp_burnin_{_cfg.RunId}_{pattern}_001";

        for (int i = 0; i < WarmupCount; i++)
        {
            try
            {
                var msg = new QueueMessage
                {
                    Channel = ch,
                    Body = Encoding.UTF8.GetBytes($"warmup-{i}"),
                    Tags = new Dictionary<string, string>
                        { ["warmup"] = "true", ["content_hash"] = "00000000" },
                };
                await _client!.SendQueueMessageAsync(msg, ct);
            }
            catch { }
        }

        // Drain warmup messages
        try
        {
            var result = await _client!.ReceiveQueueMessagesAsync(ch, WarmupCount, 5, ct);
            int drained = result.Messages?.Count ?? 0;
            if (drained < WarmupCount)
                Console.Error.WriteLine($"warmup {pattern}: only {drained}/{WarmupCount} drained");
            else
                Console.WriteLine($"warmup {pattern}: sent={WarmupCount} drained={drained}");
        }
        catch
        {
            Console.Error.WriteLine($"warmup {pattern}: sent={WarmupCount} drained=0");
        }
    }

    private async Task WarmupRpcAsync(string pattern, CancellationToken ct)
    {
        string ch = $"csharp_burnin_{_cfg.RunId}_{pattern}_001";
        int responded = 0;
        int timeoutSeconds = Math.Max(_cfg.Rpc.TimeoutMs / 1000, 5);

        using var warmupCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Start a temporary responder
        Task responderTask;
        if (pattern == "commands")
        {
            var subscription = new CommandsSubscription { Channel = ch };
            responderTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var cmd in _client!.SubscribeToCommandsAsync(subscription, warmupCts.Token))
                    {
                        try
                        {
                            await _client!.SendCommandResponseAsync(
                                cmd.RequestId, cmd.ReplyChannel, true, string.Empty,
                                Array.Empty<byte>(), string.Empty, null, warmupCts.Token);
                            Interlocked.Increment(ref responded);
                        }
                        catch { }
                    }
                }
                catch (OperationCanceledException) { }
                catch { }
            });
        }
        else
        {
            var subscription = new QueriesSubscription { Channel = ch };
            responderTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var q in _client!.SubscribeToQueriesAsync(subscription, warmupCts.Token))
                    {
                        try
                        {
                            await _client!.SendQueryResponseAsync(
                                q.RequestId, q.ReplyChannel, q.Body, true,
                                null, string.Empty, warmupCts.Token);
                            Interlocked.Increment(ref responded);
                        }
                        catch { }
                    }
                }
                catch (OperationCanceledException) { }
                catch { }
            });
        }

        await Task.Delay(1000, ct);

        int success = 0;
        for (int i = 0; i < WarmupCount; i++)
        {
            try
            {
                if (pattern == "commands")
                {
                    var cmd = new CommandMessage
                    {
                        Channel = ch,
                        Body = Encoding.UTF8.GetBytes($"warmup-{i}"),
                        TimeoutInSeconds = timeoutSeconds,
                        Tags = new Dictionary<string, string>
                            { ["warmup"] = "true", ["content_hash"] = "00000000" },
                    };
                    await _client!.SendCommandAsync(cmd, ct);
                }
                else
                {
                    var query = new QueryMessage
                    {
                        Channel = ch,
                        Body = Encoding.UTF8.GetBytes($"warmup-{i}"),
                        TimeoutInSeconds = timeoutSeconds,
                        Tags = new Dictionary<string, string>
                            { ["warmup"] = "true", ["content_hash"] = "00000000" },
                    };
                    await _client!.SendQueryAsync(query, ct);
                }
                success++;
            }
            catch { }
        }

        await Task.Delay(1000, ct);
        warmupCts.Cancel();

        int respondedCount = Interlocked.CompareExchange(ref responded, 0, 0);
        Console.WriteLine($"warmup {pattern}: sent={success} responded={respondedCount}");
    }

    // --- Private: Periodic Tasks ---

    private void StartPeriodicTasks()
    {
        int reportIntervalMs = (int)(Config.ReportIntervalSec(_cfg) * 1000);

        // Periodic report
        _timers.Add(new Timer(_ => PeriodicReport(), null, reportIntervalMs, reportIntervalMs));

        // Peak rate advance (every 1s)
        _timers.Add(new Timer(_ =>
        {
            foreach (var w in _workers)
                w.PeakRate.Advance();
        }, null, 1000, 1000));

        // Uptime + active workers (every 1s)
        _timers.Add(new Timer(_ =>
        {
            Metrics.SetUptime(_uptime.Elapsed.TotalSeconds);
            int threads = Process.GetCurrentProcess().Threads.Count;
            Metrics.SetActiveWorkers(threads);
            if (threads > _peakWorkers) _peakWorkers = threads;
        }, null, 1000, 1000));

        // Memory tracking (every 10s)
        _timers.Add(new Timer(_ =>
        {
            double rss = GetRssMb();
            if (rss > _peakRss) _peakRss = rss;
            if (_uptime.Elapsed.TotalSeconds >= MemoryBaselineSec && _baselineRss == 0)
            {
                _baselineRss = rss;
                Console.WriteLine($"memory baseline set: {rss:F1} MB at {_uptime.Elapsed.TotalSeconds:F0}s");
            }
        }, null, 10000, 10000));

        // Timestamp store purge (every 60s)
        _timers.Add(new Timer(_ =>
        {
            foreach (var w in _workers)
                w.TsStore.Purge(TimeSpan.FromMilliseconds(60000));
        }, null, 60000, 60000));

        // Seed initial memory
        _peakRss = GetRssMb();
    }

    private void PeriodicReport()
    {
        double elapsed = _testStopwatch?.Elapsed.TotalSeconds ?? _uptime.Elapsed.TotalSeconds;
        double rss = GetRssMb();

        // Detect gaps and update metrics
        foreach (var w in _workers)
        {
            var gaps = w.Tracker.DetectGaps();
            foreach (var (_, delta) in gaps)
                Metrics.IncLost(w.Pattern, delta);

            Metrics.SetConsumerLag(w.Pattern, Math.Max(0, w.Sent - w.Received));
            if (elapsed > 0)
                Metrics.SetActualRate(w.Pattern, w.Sent / elapsed);

            // GAP-17: consumer group balance ratio
            if (w.ConsumerCounts.Count > 1)
            {
                var counts = w.ConsumerCounts.Values.ToArray();
                long min = counts.Min(), max = counts.Max();
                Metrics.SetGroupBalance(w.Pattern, max > 0 ? (double)min / max : 1.0);
            }
        }

        if (_cfg.Logging.Format == "json")
        {
            // Structured JSON logging (spec Section 8.1)
            var patternsObj = new Dictionary<string, object>();
            foreach (var w in _workers)
            {
                long rate = elapsed > 0 ? (long)(w.Sent / elapsed) : 0;
                patternsObj[w.Pattern] = new Dictionary<string, object>
                {
                    ["sent"] = w.Sent, ["recv"] = w.Received,
                    ["lost"] = w.Tracker.TotalLost(), ["dup"] = w.Tracker.TotalDuplicates(),
                    ["err"] = w.Errors, ["p99_ms"] = w.LatencyAccum.PercentileMs(99),
                    ["rate"] = rate,
                };
            }

            Console.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["ts"] = DateTime.UtcNow.ToString("o"), ["level"] = "info",
                ["msg"] = "periodic_status", ["uptime_s"] = (long)elapsed,
                ["mode"] = _cfg.Mode, ["rss_mb"] = (long)rss, ["patterns"] = patternsObj,
            }));
        }
        else
        {
            // Text format with timestamp (spec Section 8.1)
            string ts = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            string uptimeStr = FormatDuration(elapsed);
            var sb = new StringBuilder();
            sb.AppendLine(
                $"[{ts}] BURN-IN STATUS | uptime={uptimeStr} mode={_cfg.Mode} rss={rss:F0}MB");

            foreach (var w in _workers)
            {
                string rate = elapsed > 0 ? $"{w.Sent / elapsed:F0}" : "0";
                if (w.Pattern is "commands" or "queries")
                {
                    sb.AppendLine(
                        $"  {w.Pattern,-14} sent={w.Sent,-8} resp={w.RpcSuccess,-8} " +
                        $"tout={w.RpcTimeout,-4} err={w.Errors,-4} " +
                        $"p99={w.RpcLatencyAccum.PercentileMs(99):F1}ms rate={rate}/s");
                }
                else
                {
                    sb.AppendLine(
                        $"  {w.Pattern,-14} sent={w.Sent,-8} recv={w.Received,-8} " +
                        $"lost={w.Tracker.TotalLost(),-4} dup={w.Tracker.TotalDuplicates(),-4} " +
                        $"err={w.Errors,-4} p99={w.LatencyAccum.PercentileMs(99):F1}ms rate={rate}/s");
                }
            }

            Console.Write(sb.ToString());
        }
    }

    // --- Private: Summary ---

    private BurninSummary BuildSummary(string status = "running")
    {
        double elapsed = _testStopwatch?.Elapsed.TotalSeconds ?? _uptime.Elapsed.TotalSeconds;
        var patterns = new Dictionary<string, PatternSummary>();

        foreach (var w in _workers)
        {
            long sent = w.Sent, received = w.Received, lost = w.Tracker.TotalLost();
            double lossPct = sent > 0 ? (double)lost / sent * 100 : 0;
            double avgTp = elapsed > 0 ? sent / elapsed : 0;

            int targetRate = w.Pattern switch
            {
                "events" => _cfg.Rates.Events,
                "events_store" => _cfg.Rates.EventsStore,
                "queue_stream" => _cfg.Rates.QueueStream,
                "queue_simple" => _cfg.Rates.QueueSimple,
                "commands" => _cfg.Rates.Commands,
                "queries" => _cfg.Rates.Queries,
                _ => 0,
            };

            var ps = new PatternSummary
            {
                Status = _patternStatus.GetValueOrDefault(w.Pattern, "unknown"),
                Sent = sent, Received = received, Lost = lost,
                Duplicated = w.Tracker.TotalDuplicates(),
                Corrupted = w.Corrupted,
                OutOfOrder = w.Tracker.TotalOutOfOrder(),
                LossPct = lossPct,
                Errors = w.Errors,
                Reconnections = w.Reconnections,
                DowntimeSeconds = w.DowntimeSeconds,
                LatencyP50Ms = w.LatencyAccum.PercentileMs(50),
                LatencyP95Ms = w.LatencyAccum.PercentileMs(95),
                LatencyP99Ms = w.LatencyAccum.PercentileMs(99),
                LatencyP999Ms = w.LatencyAccum.PercentileMs(99.9),
                AvgThroughputMsgsSec = avgTp,
                PeakThroughputMsgsSec = w.PeakRate.Peak,
                TargetRate = targetRate,
            };

            if (w.Pattern is "commands" or "queries")
            {
                ps.ResponsesSuccess = w.RpcSuccess;
                ps.ResponsesTimeout = w.RpcTimeout;
                ps.ResponsesError = w.RpcError;
                ps.RpcP50Ms = w.RpcLatencyAccum.PercentileMs(50);
                ps.RpcP95Ms = w.RpcLatencyAccum.PercentileMs(95);
                ps.RpcP99Ms = w.RpcLatencyAccum.PercentileMs(99);
                ps.RpcP999Ms = w.RpcLatencyAccum.PercentileMs(99.9);
                if (elapsed > 0)
                    ps.AvgThroughputRpcSec = w.RpcSuccess / elapsed;
            }

            patterns[w.Pattern] = ps;
        }

        double baseline = _baselineRss > 0 ? _baselineRss : Math.Max(_peakRss, 1);
        double peak = _peakRss > 0 ? _peakRss : GetRssMb();
        double growth = baseline > 0 ? peak / baseline : 1;

        string version = _cfg.Output.SdkVersion;
        if (string.IsNullOrEmpty(version))
            version = "unknown";

        var summary = new BurninSummary
        {
            Sdk = "csharp",
            Version = version,
            Mode = _cfg.Mode,
            BrokerAddress = _cfg.Broker.Address,
            StartedAt = _startedAt,
            EndedAt = _endedAt,
            DurationSeconds = elapsed,
            Status = status,
            Patterns = patterns,
            Resources = new ResourceSummary
            {
                PeakRssMb = peak,
                BaselineRssMb = baseline,
                MemoryGrowthFactor = growth,
                PeakWorkers = _peakWorkers,
            },
        };

        // F15: live verdict
        summary.Verdict = Report.GenerateVerdict(summary, _cfg.Thresholds, _cfg.Mode);
        return summary;
    }

    // --- Private: Banner ---

    private void PrintBanner()
    {
        Console.WriteLine(new string('=', 67));
        Console.WriteLine("  KUBEMQ BURN-IN TEST -- C# SDK");
        Console.WriteLine(new string('=', 67));
        Console.WriteLine($"  Mode:     {_cfg.Mode}");
        Console.WriteLine($"  Broker:   {_cfg.Broker.Address}");
        Console.WriteLine($"  Duration: {_cfg.Duration}");
        Console.WriteLine($"  Run ID:   {_cfg.RunId}");
        Console.WriteLine(
            $"  Rates:    events={_cfg.Rates.Events} es={_cfg.Rates.EventsStore} " +
            $"qs={_cfg.Rates.QueueStream} qq={_cfg.Rates.QueueSimple} " +
            $"cmd={_cfg.Rates.Commands} qry={_cfg.Rates.Queries}");
        Console.WriteLine(new string('=', 67));
        Console.WriteLine();
    }

    // --- Private: Utilities ---

    private static double GetRssMb()
    {
        return Process.GetCurrentProcess().WorkingSet64 / (1024.0 * 1024.0);
    }

    private static string FormatDuration(double secs)
    {
        int s = (int)secs;
        if (s >= 3600) return $"{s / 3600}h{s % 3600 / 60}m{s % 60}s";
        if (s >= 60) return $"{s / 60}m{s % 60}s";
        return $"{s}s";
    }

    private static async Task SafeDelay(int ms, CancellationToken ct = default)
    {
        try { await Task.Delay(ms, ct); }
        catch (OperationCanceledException) { /* normal */ }
    }

    public void Dispose()
    {
        foreach (var w in _workers) w.Dispose();
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Standalone cleanup-only mode: create client, clean stale channels, close.
/// </summary>
public static class CleanupRunner
{
    public static async Task RunAsync(BurninConfig cfg)
    {
        var clientConfig = new KubeMQClientOptions
        {
            Address = cfg.Broker.Address,
            ClientId = $"{cfg.Broker.ClientIdPrefix}-{cfg.RunId}",
        };

        using var client = new KubeMQClient(clientConfig);
        await client.ConnectAsync(CancellationToken.None);
        await client.PingAsync(CancellationToken.None);
        Console.WriteLine("cleaning stale channels");

        const string prefix = "csharp_burnin_";
        int count = 0;

        async Task CleanType(Func<Task<IReadOnlyList<KubeMQ.Sdk.Common.ChannelInfo>>> listFn, Func<string, Task> deleteFn)
        {
            try
            {
                var channels = await listFn();
                if (channels == null) return;
                foreach (var ch in channels)
                {
                    try
                    {
                        string name = ch.Name;
                        await deleteFn(name);
                        count++;
                    }
                    catch { }
                }
            }
            catch { }
        }

        await CleanType(
            () => client.ListEventsChannelsAsync(prefix, CancellationToken.None),
            n => client.DeleteEventsChannelAsync(n, CancellationToken.None));
        await CleanType(
            () => client.ListEventsStoreChannelsAsync(prefix, CancellationToken.None),
            n => client.DeleteEventsStoreChannelAsync(n, CancellationToken.None));
        await CleanType(
            () => client.ListQueuesChannelsAsync(prefix, CancellationToken.None),
            n => client.DeleteQueuesChannelAsync(n, CancellationToken.None));
        await CleanType(
            () => client.ListCommandsChannelsAsync(prefix, CancellationToken.None),
            n => client.DeleteCommandsChannelAsync(n, CancellationToken.None));
        await CleanType(
            () => client.ListQueriesChannelsAsync(prefix, CancellationToken.None),
            n => client.DeleteQueriesChannelAsync(n, CancellationToken.None));

        Console.WriteLine($"cleaned {count} stale channels");
    }
}
