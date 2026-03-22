// Engine: state machine, API-controlled lifecycle, full orchestrator.
// v2: uses PatternGroup (dictionary of pattern -> PatternGroup with N channel workers).
// Boots into Idle. Runs are started/stopped via API.

using System.Diagnostics;
using System.Runtime.InteropServices;
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

namespace KubeMQ.Burnin;

public static class AllPatterns
{
    public static readonly string[] Names =
    {
        "events", "events_store", "queue_stream", "queue_simple", "commands", "queries"
    };
}

/// <summary>
/// Full burn-in orchestrator with state machine and API-controlled lifecycle.
/// v2: uses PatternGroup architecture for multi-channel support.
/// </summary>
public sealed class Engine : IDisposable, IClientRecreator
{
    private const string ChannelPrefix = "csharp_burnin_";
    private const int WarmupCount = 3;
    private const int MemoryBaseline5Min = 300;
    private const int MemoryBaseline1Min = 60;

    // Startup config (immutable after construction)
    private readonly BurninConfig _startupCfg;
    private readonly DateTime _bootTime = DateTime.UtcNow;
    private readonly Stopwatch _bootUptime = Stopwatch.StartNew();

    // State machine (atomic via Interlocked)
    private int _state = (int)RunState.Idle;
    public RunState State => (RunState)Volatile.Read(ref _state);

    private bool TryTransition(RunState from, RunState to)
    {
        return Interlocked.CompareExchange(ref _state, (int)to, (int)from) == (int)from;
    }

    private void ForceState(RunState to)
    {
        Volatile.Write(ref _state, (int)to);
    }

    // Per-run state (reset on each new run)
    private BurninConfig? _runCfg;
    private string? _runId;
    public string? RunId => _runId;
    private HashSet<string> _enabledPatterns = new();
    private Dictionary<string, ResolvedPatternThreshold> _perPatternThresholds = new();
    private Dictionary<string, List<string>> _channelNames = new();
    private Dictionary<string, PatternState> _patternStates = new();
    // Per-pattern clients: each pattern gets its own KubeMQClient instance
    private readonly Dictionary<string, KubeMQClient> _patternClients = new();

    // v2: PatternGroups replace flat worker list
    private readonly Dictionary<string, PatternGroup> _patternGroups = new();

    private Task? _runTask;
    private CancellationTokenSource? _runCts;
    private int _startingTimeoutSeconds = 60;
    private string? _startedAt;
    private string? _endedAt;
    private string? _errorMessage;
    private Stopwatch? _testStopwatch;
    private double? _producersStoppedElapsed;
    private double _baselineRss;
    private string _baselineSetAt = "none"; // "none", "running-start", "1min", "5min"
    private double _peakRss;
    private int _peakWorkers;
    private readonly List<Timer> _timers = new();
    private BurninSummary? _lastReport;

    /// <summary>
    /// Snapshot of all pattern counters at producer-stop time (T2).
    /// When set, BuildSummary uses these instead of live values.
    /// </summary>
    private Dictionary<string, PatternSnapshot>? _producerStopSnapshot;

    public Engine(BurninConfig startupConfig)
    {
        _startupCfg = startupConfig;
    }

    // --- All workers across all pattern groups ---
    private IEnumerable<BaseWorker> AllWorkers =>
        _patternGroups.Values.SelectMany(pg => pg.ChannelWorkers);

    // --- Public API (called by HttpServer) ---

    /// <summary>
    /// Start a new run. Returns (runId, error). Error is null on success.
    /// </summary>
    public (string? runId, string? error) StartRun(RunStartRequest? apiReq)
    {
        var currentState = State;
        if (!currentState.CanStart())
        {
            return (null, $"Cannot start run in state '{currentState.ToApi()}'");
        }

        // Validate config (including v1 detection)
        var errors = ApiConfigTranslator.Validate(apiReq);
        if (errors.Count > 0)
            return (null, string.Join("; ", errors));

        // Translate API config to internal config
        var cfg = ApiConfigTranslator.Translate(apiReq, _startupCfg);
        var enabled = ApiConfigTranslator.ResolveEnabled(cfg);
        var thresholds = ApiConfigTranslator.ResolveThresholds(cfg);

        // At least one pattern must be enabled
        if (enabled.Count == 0)
            return (null, "at least one pattern must be enabled");

        // Atomic state transition
        if (!TryTransition(currentState, RunState.Starting))
        {
            return (null, "Run already starting (race condition)");
        }

        // Initialize per-run state
        _runCfg = cfg;
        _runId = cfg.RunId;
        _enabledPatterns = enabled;
        _perPatternThresholds = thresholds;
        _channelNames = new Dictionary<string, List<string>>();
        _patternStates = new Dictionary<string, PatternState>();
        _startedAt = DateTime.UtcNow.ToString("o");
        _endedAt = null;
        _errorMessage = null;
        _testStopwatch = null;
        _producersStoppedElapsed = null;
        _producerStopSnapshot = null;
        _baselineRss = 0;
        _baselineSetAt = "none";
        _peakRss = 0;
        _peakWorkers = 0;
        _lastReport = null;
        _startingTimeoutSeconds = apiReq?.StartingTimeoutSeconds ?? 60;

        foreach (string p in AllPatterns.Names)
            _patternStates[p] = _enabledPatterns.Contains(p) ? PatternState.Starting : PatternState.Stopped;

        // Start run in background
        _runCts = new CancellationTokenSource();
        _runTask = Task.Run(() => RunInternalAsync(_runCts.Token));

        // Count total channels across all patterns
        int totalChannels = 0;
        foreach (var (name, pc) in cfg.Patterns)
        {
            if (pc.Enabled)
                totalChannels += pc.Channels;
        }

        return (_runId, null);
    }

    /// <summary>
    /// Stop the active run. Returns (success, error).
    /// </summary>
    public (bool success, string? error) StopRun()
    {
        var currentState = State;
        if (!currentState.CanStop())
        {
            if (currentState == RunState.Stopping)
                return (false, "Run is already stopping");
            return (false, $"No active run to stop (state={currentState.ToApi()})");
        }

        _runCts?.Cancel();
        return (true, null);
    }

    /// <summary>
    /// Get full run state for GET /run.
    /// </summary>
    public object GetRunData()
    {
        var state = State;
        if (state == RunState.Idle)
            return new { run_id = (string?)null, state = "idle" };

        return BuildRunResponse(state);
    }

    /// <summary>
    /// Get lightweight status for GET /run/status.
    /// </summary>
    public object GetRunStatus()
    {
        var state = State;
        if (state == RunState.Idle)
            return new { run_id = (string?)null, state = "idle" };

        if (state == RunState.Error && _errorMessage != null)
            return new { run_id = _runId, state = state.ToApi(), error = _errorMessage };

        double elapsed = GetElapsed();
        double remaining = GetRemaining(elapsed);

        var patternStatesApi = new Dictionary<string, object>();
        foreach (var (k, v) in _patternStates)
        {
            if (_enabledPatterns.Contains(k))
            {
                int channels = _runCfg?.Patterns.GetValueOrDefault(k)?.Channels ?? 1;
                patternStatesApi[k] = new { state = v.ToApi(), channels };
            }
        }

        return new
        {
            run_id = _runId,
            state = state.ToApi(),
            started_at = _startedAt,
            elapsed_seconds = Math.Round(elapsed, 1),
            remaining_seconds = Math.Round(remaining, 1),
            warmup_active = IsWarmupActive(),
            totals = BuildTotals(),
            pattern_states = patternStatesApi,
        };
    }

    /// <summary>
    /// Get resolved config for GET /run/config.
    /// </summary>
    public object? GetRunConfig()
    {
        if (_runCfg == null) return null;
        return new
        {
            run_id = _runId,
            state = State.ToApi(),
            config = ApiConfigTranslator.BuildResolvedConfig(
                _runCfg, _perPatternThresholds, _channelNames),
        };
    }

    /// <summary>
    /// Get the last completed run report for GET /run/report.
    /// </summary>
    public BurninSummary? GetReport()
    {
        return _lastReport;
    }

    /// <summary>
    /// Get info for GET /info.
    /// </summary>
    public object GetInfo()
    {
        string sdkVersion = _startupCfg.Output.SdkVersion;
        if (string.IsNullOrEmpty(sdkVersion)) sdkVersion = DetectSdkVersion();

        return new
        {
            sdk = "csharp",
            sdk_version = sdkVersion,
            burnin_version = "2.0.0",
            burnin_spec_version = "2",
            os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "darwin" : "linux",
            arch = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
            runtime = $"dotnet{Environment.Version}",
            cpus = Environment.ProcessorCount,
            memory_total_mb = (int)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024)),
            pid = Environment.ProcessId,
            uptime_seconds = Math.Round(_bootUptime.Elapsed.TotalSeconds, 1),
            started_at = _bootTime.ToString("o"),
            state = State.ToApi(),
            broker_address = _startupCfg.Broker.Address,
        };
    }

    /// <summary>
    /// Get the number of total channels and pattern count for the start message.
    /// </summary>
    public (int totalChannels, int patternCount) GetRunChannelInfo()
    {
        if (_runCfg == null) return (0, 0);
        int channels = 0, patterns = 0;
        foreach (var (_, pc) in _runCfg.Patterns)
        {
            if (pc.Enabled)
            {
                channels += pc.Channels;
                patterns++;
            }
        }
        return (channels, patterns);
    }

    /// <summary>
    /// Ping broker for GET /broker/status.
    /// </summary>
    public async Task<object> PingBrokerAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = new KubeMQClient(new KubeMQClientOptions
            {
                Address = _startupCfg.Broker.Address,
                ClientId = $"{_startupCfg.Broker.ClientIdPrefix}-ping",
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await client.ConnectAsync(cts.Token);
            var result = await client.PingAsync(cts.Token);
            sw.Stop();

            return new
            {
                connected = true,
                address = _startupCfg.Broker.Address,
                ping_latency_ms = Math.Round(sw.Elapsed.TotalMilliseconds, 1),
                server_version = result.Version ?? "unknown",
                last_ping_at = DateTime.UtcNow.ToString("o"),
            };
        }
        catch (Exception ex)
        {
            return new
            {
                connected = false,
                address = _startupCfg.Broker.Address,
                error = ex.Message,
            };
        }
    }

    /// <summary>
    /// Cleanup all burn-in channels for POST /cleanup.
    /// v2: deletes all N channels per pattern.
    /// </summary>
    public async Task<object> CleanupChannelsAsync()
    {
        var deleted = new List<string>();
        var failed = new List<string>();

        try
        {
            using var client = new KubeMQClient(new KubeMQClientOptions
            {
                Address = _startupCfg.Broker.Address,
                ClientId = $"{_startupCfg.Broker.ClientIdPrefix}-cleanup",
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await client.ConnectAsync(cts.Token);

            await CleanChannelType(() => client.ListEventsChannelsAsync(ChannelPrefix, cts.Token),
                n => client.DeleteEventsChannelAsync(n, cts.Token), deleted, failed);
            await CleanChannelType(() => client.ListEventsStoreChannelsAsync(ChannelPrefix, cts.Token),
                n => client.DeleteEventsStoreChannelAsync(n, cts.Token), deleted, failed);
            await CleanChannelType(() => client.ListQueuesChannelsAsync(ChannelPrefix, cts.Token),
                n => client.DeleteQueuesChannelAsync(n, cts.Token), deleted, failed);
            await CleanChannelType(() => client.ListCommandsChannelsAsync(ChannelPrefix, cts.Token),
                n => client.DeleteCommandsChannelAsync(n, cts.Token), deleted, failed);
            await CleanChannelType(() => client.ListQueriesChannelsAsync(ChannelPrefix, cts.Token),
                n => client.DeleteQueriesChannelAsync(n, cts.Token), deleted, failed);

            int patternCount = _enabledPatterns.Count;
            return new
            {
                deleted_channels = deleted,
                failed_channels = failed,
                message = $"cleaned {deleted.Count} channels across {patternCount} patterns",
            };
        }
        catch (Exception ex)
        {
            return new
            {
                deleted_channels = deleted,
                failed_channels = failed,
                message = $"Could not connect to broker: {ex.Message}",
            };
        }
    }

    /// <summary>
    /// Shutdown the app (SIGTERM flow): stop run, generate report, exit.
    /// Returns exit code: 0=PASSED, 1=FAILED.
    /// </summary>
    public async Task<int> GracefulShutdownAsync()
    {
        var state = State;

        if (state == RunState.Idle)
            return 0;

        if (state.CanStop())
        {
            _runCts?.Cancel();
            if (_runTask != null)
            {
                try { await _runTask.WaitAsync(TimeSpan.FromSeconds(30)); }
                catch { /* timeout or error */ }
            }
        }
        else if (state is RunState.Stopping)
        {
            if (_runTask != null)
            {
                try { await _runTask.WaitAsync(TimeSpan.FromSeconds(30)); }
                catch { /* timeout */ }
            }
        }

        // Write report file if configured
        if (_lastReport != null && !string.IsNullOrEmpty(_startupCfg.Output.ReportFile))
            Report.WriteJsonReport(_lastReport, _startupCfg.Output.ReportFile);

        if (_lastReport != null)
        {
            Report.PrintConsoleReport(_lastReport);
            return _lastReport.Verdict?.Passed == true ? 0 : 1;
        }

        return 0;
    }

    // --- IClientRecreator interface ---

    public async Task CloseClientAsync()
    {
        foreach (string p in AllPatterns.Names)
        {
            if (_enabledPatterns.Contains(p))
            {
                _patternStates[p] = PatternState.Recovering;
                Metrics.SetActiveConnections(p, 0);
            }
        }
        foreach (var (_, client) in _patternClients)
        {
            try { client.Dispose(); } catch { }
        }
    }

    public async Task RecreateClientAsync()
    {
        foreach (string p in _enabledPatterns)
        {
            var client = CreateClient();
            await client.ConnectAsync(CancellationToken.None);
            _patternClients[p] = client;
        }

        foreach (string p in AllPatterns.Names)
        {
            if (_enabledPatterns.Contains(p))
                _patternStates[p] = PatternState.Running;
        }
    }

    // --- Private: Internal Run Flow ---

    private async Task RunInternalAsync(CancellationToken ct)
    {
        if (_runCfg == null) return;

        using var startingTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_startingTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, startingTimeoutCts.Token);
        var startCt = linkedCts.Token;

        try
        {
            // Step 1: Create per-pattern clients
            foreach (string p in _enabledPatterns)
            {
                var client = CreateClient();
                await client.ConnectAsync(startCt);
                _patternClients[p] = client;
            }

            Console.WriteLine($"clients created ({string.Join("/", _enabledPatterns)}), pinging broker at {_runCfg.Broker.Address}");
            var pingResult = await _patternClients.Values.First().PingAsync(startCt);
            Console.WriteLine($"broker ping ok: {pingResult.Host} v{pingResult.Version}");

            // Step 2: Skip stale channel cleanup (channels auto-create on subscribe/send)
            Console.WriteLine("skipping stale channel cleanup at startup (channels auto-create on subscribe/send)");

            // Step 3: Benchmark mode: set all rates to 0 (unlimited)
            if (_runCfg.Mode == "benchmark")
            {
                foreach (var (_, pc) in _runCfg.Patterns)
                    pc.Rate = 0;
            }

            // Step 4: Create pattern groups (N channel workers each)
            CreatePatternGroups();

            // Set target rates
            foreach (string p in AllPatterns.Names)
            {
                if (_enabledPatterns.Contains(p) && _patternGroups.TryGetValue(p, out var pg))
                {
                    int targetRate = pg.Config.Rate * pg.Config.Channels;
                    Metrics.SetTargetRate(p, targetRate);
                }
                else
                {
                    Metrics.SetTargetRate(p, 0);
                }
            }

            // Step 6: Start ALL consumers/responders across ALL channels and ALL patterns
            foreach (var (_, pg) in _patternGroups)
            {
                await pg.StartConsumersAsync(GetClientForPattern(pg.Pattern));
                Console.WriteLine($"consumers started: {pg.Pattern} ({pg.Config.Channels} channels)");
            }

            // Step 7: Run warmup on ALL channels
            await RunWarmupAsync(startCt);

            // Step 8: Start ALL producers/senders across ALL channels and ALL patterns
            foreach (var (_, pg) in _patternGroups)
            {
                await pg.StartProducersAsync(GetClientForPattern(pg.Pattern));
                Console.WriteLine($"producers started: {pg.Pattern} ({pg.Config.Channels} channels)");
            }

            if (ct.IsCancellationRequested)
            {
                await PerformShutdown(ct);
                return;
            }

            PrintBanner();
        }
        catch (OperationCanceledException) when (startingTimeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            string error = $"Starting timeout exceeded ({_startingTimeoutSeconds}s)";
            _errorMessage = error;
            _endedAt = DateTime.UtcNow.ToString("o");
            foreach (string p in AllPatterns.Names)
                if (_enabledPatterns.Contains(p)) _patternStates[p] = PatternState.Error;

            _lastReport = BuildSummary("error");
            _lastReport.Verdict = Report.GenerateStartupFailedVerdict(error);
            ForceState(RunState.Error);
            await DisposeRunResources();
            Console.Error.WriteLine($"ERROR: {error}");
            return;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await PerformShutdown(ct);
            return;
        }
        catch (Exception ex)
        {
            string error = $"Startup failed: {ex.Message}";
            _errorMessage = error;
            _endedAt = DateTime.UtcNow.ToString("o");
            foreach (string p in AllPatterns.Names)
                if (_enabledPatterns.Contains(p)) _patternStates[p] = PatternState.Error;

            _lastReport = BuildSummary("error");
            _lastReport.Verdict = Report.GenerateStartupFailedVerdict(error);
            ForceState(RunState.Error);
            await DisposeRunResources();
            Console.Error.WriteLine($"ERROR: {error}");
            return;
        }

        // Warmup duration wait + reset
        double warmupSec = Config.WarmupDurationSec(_runCfg);
        if (warmupSec > 0)
        {
            Metrics.SetWarmupActive(1);
            Console.WriteLine($"warmup period: {warmupSec}s");
            await SafeDelay((int)(warmupSec * 1000), ct);
            if (ct.IsCancellationRequested)
            {
                Metrics.SetWarmupActive(0);
                await PerformShutdown(ct);
                return;
            }
            foreach (var (_, pg) in _patternGroups)
                pg.ResetAfterWarmup();
            Metrics.SetWarmupActive(0);
            Console.WriteLine("warmup complete, counters reset");
        }

        // Transition to Running
        if (!TryTransition(RunState.Starting, RunState.Running))
        {
            await PerformShutdown(ct);
            return;
        }

        _testStopwatch = Stopwatch.StartNew();
        foreach (string p in AllPatterns.Names)
            if (_enabledPatterns.Contains(p)) _patternStates[p] = PatternState.Running;

        // Start periodic tasks
        StartPeriodicTasks();

        // Disconnect manager
        var dm = new DisconnectManager(
            Config.ForcedDisconnectIntervalSec(_runCfg),
            Config.ForcedDisconnectDurationSec(_runCfg),
            this);
        if (dm.Enabled)
        {
            dm.Start();
            Console.WriteLine("disconnect manager enabled");
        }

        // Wait for duration or cancellation
        double dur = Config.DurationSec(_runCfg.Duration);
        double maxDur = Config.MaxDurationSec(_runCfg);
        double effectiveDur = dur > 0 ? dur : (maxDur > 0 ? maxDur : 604800);
        Console.WriteLine($"running for {effectiveDur}s (or until stopped)");
        await SafeDelay((int)(effectiveDur * 1000), ct);

        if (dm.Enabled) dm.Stop();

        // Perform shutdown (normal completion or stop)
        await PerformShutdown(ct);
    }

    private async Task PerformShutdown(CancellationToken ct)
    {
        if (!TryTransition(RunState.Running, RunState.Stopping) &&
            !TryTransition(RunState.Starting, RunState.Stopping))
        {
            return;
        }

        _endedAt = DateTime.UtcNow.ToString("o");
        Console.WriteLine("initiating 2-phase shutdown");

        int drainSec = _runCfg?.Shutdown.DrainTimeoutSeconds ?? 10;

        // Stop periodic timers
        foreach (var timer in _timers)
            await timer.DisposeAsync();
        _timers.Clear();

        // Phase 1: Stop ALL producers across ALL channels across ALL patterns
        foreach (string p in AllPatterns.Names)
            if (_enabledPatterns.Contains(p)) _patternStates[p] = PatternState.Stopped;

        // Snapshot all counters BEFORE stopping producers so the final report
        // reflects a clean measurement window and excludes drain-phase events.
        _producerStopSnapshot = CapturePatternSnapshots();
        Console.WriteLine("producer-stop snapshot captured");

        _producersStoppedElapsed = _testStopwatch?.Elapsed.TotalSeconds;
        foreach (var (_, pg) in _patternGroups)
            pg.StopProducers();

        Console.WriteLine($"producers stopped, draining for {drainSec}s");
        await Task.Delay(Math.Min(drainSec * 1000, 30000));

        // Phase 2: Stop ALL consumers across ALL channels across ALL patterns
        foreach (var (_, pg) in _patternGroups)
            pg.StopConsumers();
        Console.WriteLine("consumers stopped");

        if (_baselineRss == 0)
        {
            _baselineRss = GetRssMb();
            _baselineSetAt = "running-start";
        }

        // Cleanup channels
        if (_runCfg?.Shutdown.CleanupChannels == true)
        {
            try
            {
                using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await CleanStaleChannelsAsync(cleanupCts.Token);
            }
            catch { Console.Error.WriteLine("channel cleanup timed out"); }
        }

        // Build report with per-channel data for fail-on-any-channel verdict checks
        bool memoryBaselineAdvisory = _baselineSetAt != "5min";
        var perChannelData = BuildPerChannelData();
        var summary = BuildSummary("completed");
        summary.Verdict = Report.GenerateVerdict(
            summary, _runCfg!.Thresholds, _runCfg.Mode,
            _perPatternThresholds, _enabledPatterns, memoryBaselineAdvisory,
            perChannelData);
        _lastReport = summary;

        Report.PrintConsoleReport(summary);

        await DisposeRunResources();
        ForceState(RunState.Stopped);
        Console.WriteLine("run completed");
    }

    private async Task DisposeRunResources()
    {
        foreach (var (_, pg) in _patternGroups)
            pg.Dispose();
        _patternGroups.Clear();
        foreach (var (_, client) in _patternClients)
        {
            try { client.Dispose(); } catch { }
        }
        _patternClients.Clear();
    }

    // --- Private: Client Creation ---

    /// <summary>
    /// Returns the per-family client for the given pattern name.
    /// </summary>
    private KubeMQClient GetClientForPattern(string pattern)
    {
        if (_patternClients.TryGetValue(pattern, out var client))
            return client;
        throw new ArgumentException($"No client for pattern: {pattern}");
    }

    private KubeMQClient CreateClient()
    {
        var cfg = _runCfg ?? _startupCfg;
        int initMs = Math.Clamp((int)Config.ReconnectIntervalMs(cfg), 50, 5000);
        int maxMs = Math.Clamp((int)Config.ReconnectMaxIntervalMs(cfg), 1000, 120000);
        double mult = Math.Clamp(cfg.Recovery.ReconnectMultiplier, 1.5, 3.0);

        return new KubeMQClient(new KubeMQClientOptions
        {
            Address = cfg.Broker.Address,
            ClientId = $"{cfg.Broker.ClientIdPrefix}-{_runId ?? "idle"}",
            Reconnect = new ReconnectOptions
            {
                MaxAttempts = 0,
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
        });
    }

    /// <summary>
    /// Create PatternGroups with N channel workers each.
    /// </summary>
    private void CreatePatternGroups()
    {
        if (_runCfg == null) return;
        _patternGroups.Clear();

        foreach (var (name, pc) in _runCfg.Patterns)
        {
            if (!pc.Enabled || !_enabledPatterns.Contains(name)) continue;

            var pg = new PatternGroup(name, pc, _runCfg, _runCfg.RunId);
            _patternGroups[name] = pg;
            _channelNames[name] = pg.ChannelNames;
        }
    }

    // --- Private: Channels ---

    private async Task CleanStaleChannelsAsync(CancellationToken ct)
    {
        // Use any connected client for channel cleanup
        var cleanupClient = _patternClients.Values.FirstOrDefault();
        if (cleanupClient == null) return;
        Console.WriteLine($"cleaning stale channels with prefix '{ChannelPrefix}'");
        int count = 0;
        count += await CleanChannelTypeCount(
            () => cleanupClient.ListEventsChannelsAsync(ChannelPrefix, ct),
            n => cleanupClient.DeleteEventsChannelAsync(n, ct));
        count += await CleanChannelTypeCount(
            () => cleanupClient.ListEventsStoreChannelsAsync(ChannelPrefix, ct),
            n => cleanupClient.DeleteEventsStoreChannelAsync(n, ct));
        count += await CleanChannelTypeCount(
            () => cleanupClient.ListQueuesChannelsAsync(ChannelPrefix, ct),
            n => cleanupClient.DeleteQueuesChannelAsync(n, ct));
        count += await CleanChannelTypeCount(
            () => cleanupClient.ListCommandsChannelsAsync(ChannelPrefix, ct),
            n => cleanupClient.DeleteCommandsChannelAsync(n, ct));
        count += await CleanChannelTypeCount(
            () => cleanupClient.ListQueriesChannelsAsync(ChannelPrefix, ct),
            n => cleanupClient.DeleteQueriesChannelAsync(n, ct));
        Console.WriteLine($"cleaned {count} stale channels");
    }

    private static async Task<int> CleanChannelTypeCount(
        Func<Task<IReadOnlyList<KubeMQ.Sdk.Common.ChannelInfo>>> listFn, Func<string, Task> deleteFn)
    {
        int deleted = 0;
        try
        {
            var channels = await listFn();
            if (channels == null) return 0;
            foreach (var ch in channels)
            {
                try { await deleteFn(ch.Name); deleted++; } catch { }
            }
        }
        catch { }
        return deleted;
    }

    private static async Task CleanChannelType(
        Func<Task<IReadOnlyList<KubeMQ.Sdk.Common.ChannelInfo>>> listFn,
        Func<string, Task> deleteFn,
        List<string> deleted, List<string> failed)
    {
        try
        {
            var channels = await listFn();
            if (channels == null) return;
            foreach (var ch in channels)
            {
                try { await deleteFn(ch.Name); deleted.Add(ch.Name); }
                catch { failed.Add(ch.Name); }
            }
        }
        catch { }
    }

    // --- Private: Warmup ---

    /// <summary>
    /// v2: warmup sends messages to EVERY channel with parallel concurrency limit,
    /// per-channel timeout, 3 retries, and fail-fast.
    /// </summary>
    private async Task RunWarmupAsync(CancellationToken ct)
    {
        if (_patternClients.Count == 0 || _runCfg == null) return;
        Console.WriteLine("running warmup verification on all channels");

        int maxParallel = _runCfg.Warmup.MaxParallelChannels;
        int timeoutMs = _runCfg.Warmup.TimeoutPerChannelMs;
        var semaphore = new SemaphoreSlim(maxParallel, maxParallel);
        var warmupTasks = new List<Task>();
        var failedChannels = new List<string>();
        var failLock = new object();

        foreach (var (pattern, pg) in _patternGroups)
        {
            foreach (string chName in pg.ChannelNames)
            {
                string p = pattern; // capture
                string ch = chName;
                warmupTasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        bool success = false;
                        for (int retry = 0; retry < 3; retry++)
                        {
                            try
                            {
                                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                                timeoutCts.CancelAfter(timeoutMs);
                                success = await WarmupChannelAsync(p, ch, timeoutCts.Token);
                                if (success) break;
                            }
                            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                            {
                                Console.Error.WriteLine($"warmup timeout: {ch} (retry {retry + 1}/3)");
                            }
                        }

                        if (!success)
                        {
                            lock (failLock)
                                failedChannels.Add(ch);
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }
        }

        await Task.WhenAll(warmupTasks);

        if (failedChannels.Count > 0)
        {
            string error = $"Warmup failed for {failedChannels.Count} channels: {string.Join(", ", failedChannels.Take(5))}";
            throw new Exception(error);
        }

        Console.WriteLine("warmup verification complete on all channels");
    }

    /// <summary>
    /// Warmup a single channel: send WarmupCount messages, verify at least 1 received.
    /// </summary>
    private async Task<bool> WarmupChannelAsync(string pattern, string channelName, CancellationToken ct)
    {
        switch (pattern)
        {
            case "events":
                return await WarmupEventsChannelAsync(channelName, ct);
            case "events_store":
                return await WarmupEventsStoreChannelAsync(channelName, ct);
            case "queue_stream":
            case "queue_simple":
                // Skip warmup sends for queue patterns — queue channels
                // auto-create on first send/poll, and warmup messages leave
                // unacked items that cause false duplicates.
                Console.WriteLine($"skipping warmup for queue pattern {pattern} channel {channelName}");
                return true;
            case "commands":
            case "queries":
                return await WarmupRpcChannelAsync(pattern, channelName, ct);
            default:
                return true;
        }
    }

    private async Task<bool> WarmupEventsChannelAsync(string ch, CancellationToken ct)
    {
        int count = 0;
        string clientId = $"{_runCfg!.Broker.ClientIdPrefix}-{_runCfg.RunId}";

        using var warmupCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var subscription = new EventsSubscription { Channel = ch };
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var evt in GetClientForPattern("events").SubscribeToEventsAsync(subscription, warmupCts.Token))
                    if (evt.Tags?.GetValueOrDefault("warmup") == "true")
                        Interlocked.Increment(ref count);
            }
            catch { }
        });

        await Task.Delay(500, ct);
        var stream = await GetClientForPattern("events").CreateEventStreamAsync(null, ct);
        for (int i = 0; i < WarmupCount; i++)
        {
            try
            {
                await stream.SendAsync(new EventMessage
                {
                    Channel = ch,
                    Body = Encoding.UTF8.GetBytes($"warmup-{i}"),
                    Tags = new Dictionary<string, string> { ["warmup"] = "true", ["content_hash"] = "00000000" },
                }, clientId, ct);
            }
            catch { }
            await Task.Delay(100, ct);
        }
        await Task.Delay(1000, ct);
        warmupCts.Cancel();
        await stream.DisposeAsync();
        int received = Interlocked.CompareExchange(ref count, 0, 0);
        Console.WriteLine($"warmup events {ch}: sent={WarmupCount} received={received}");
        return received >= 1;
    }

    private async Task<bool> WarmupEventsStoreChannelAsync(string ch, CancellationToken ct)
    {
        int count = 0;
        string clientId = $"{_runCfg!.Broker.ClientIdPrefix}-{_runCfg.RunId}";

        using var warmupCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var subscription = new EventStoreSubscription { Channel = ch, StartPosition = EventStoreStartPosition.StartFromNew };
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var evt in GetClientForPattern("events_store").SubscribeToEventsStoreAsync(subscription, warmupCts.Token))
                    if (evt.Tags?.GetValueOrDefault("warmup") == "true")
                        Interlocked.Increment(ref count);
            }
            catch { }
        });

        await Task.Delay(500, ct);
        var stream = await GetClientForPattern("events_store").CreateEventStoreStreamAsync(ct);
        for (int i = 0; i < WarmupCount; i++)
        {
            try
            {
                await stream.SendAsync(new EventStoreMessage
                {
                    Channel = ch,
                    Body = Encoding.UTF8.GetBytes($"warmup-{i}"),
                    Tags = new Dictionary<string, string> { ["warmup"] = "true", ["content_hash"] = "00000000" },
                }, clientId, ct);
            }
            catch { }
            await Task.Delay(100, ct);
        }
        await Task.Delay(1000, ct);
        warmupCts.Cancel();
        await stream.DisposeAsync();
        int received = Interlocked.CompareExchange(ref count, 0, 0);
        Console.WriteLine($"warmup events_store {ch}: sent={WarmupCount} received={received}");
        return received >= 1;
    }

    private async Task<bool> WarmupQueueChannelAsync(string pattern, string ch, CancellationToken ct)
    {
        int sent = 0;
        for (int i = 0; i < WarmupCount; i++)
        {
            try
            {
                await GetClientForPattern(pattern).SendQueueMessageAsync(new QueueMessage
                {
                    Channel = ch,
                    Body = Encoding.UTF8.GetBytes($"warmup-{i}"),
                    Tags = new Dictionary<string, string> { ["warmup"] = "true", ["content_hash"] = "00000000" },
                }, ct);
                sent++;
            }
            catch { }
            await Task.Delay(100, ct);
        }

        Console.WriteLine($"warmup {pattern} {ch}: sent={sent}/{WarmupCount}");
        return sent >= 1;
    }

    private async Task<bool> WarmupRpcChannelAsync(string pattern, string ch, CancellationToken ct)
    {
        int responded = 0;
        int timeoutSec = Math.Max(_runCfg!.Rpc.TimeoutMs / 1000, 5);

        using var warmupCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        if (pattern == "commands")
        {
            var subscription = new CommandsSubscription { Channel = ch };
            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var cmd in GetClientForPattern(pattern).SubscribeToCommandsAsync(subscription, warmupCts.Token))
                    {
                        try
                        {
                            await GetClientForPattern(pattern).SendCommandResponseAsync(new CommandResponse
                            { RequestId = cmd.RequestId, ReplyChannel = cmd.ReplyChannel, Executed = true }, warmupCts.Token);
                            Interlocked.Increment(ref responded);
                        }
                        catch { }
                    }
                }
                catch { }
            });
        }
        else
        {
            var subscription = new QueriesSubscription { Channel = ch };
            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var q in GetClientForPattern(pattern).SubscribeToQueriesAsync(subscription, warmupCts.Token))
                    {
                        try
                        {
                            await GetClientForPattern(pattern).SendQueryResponseAsync(new QueryResponse
                            { RequestId = q.RequestId, ReplyChannel = q.ReplyChannel, Body = q.Body, Executed = true }, warmupCts.Token);
                            Interlocked.Increment(ref responded);
                        }
                        catch { }
                    }
                }
                catch { }
            });
        }

        await Task.Delay(500, ct);
        int success = 0;
        for (int i = 0; i < WarmupCount; i++)
        {
            try
            {
                if (pattern == "commands")
                    await GetClientForPattern(pattern).SendCommandAsync(new CommandMessage
                    {
                        Channel = ch, Body = Encoding.UTF8.GetBytes($"warmup-{i}"),
                        TimeoutInSeconds = timeoutSec,
                        Tags = new Dictionary<string, string> { ["warmup"] = "true", ["content_hash"] = "00000000" },
                    }, ct);
                else
                    await GetClientForPattern(pattern).SendQueryAsync(new QueryMessage
                    {
                        Channel = ch, Body = Encoding.UTF8.GetBytes($"warmup-{i}"),
                        TimeoutInSeconds = timeoutSec,
                        Tags = new Dictionary<string, string> { ["warmup"] = "true", ["content_hash"] = "00000000" },
                    }, ct);
                success++;
            }
            catch { }
            await Task.Delay(100, ct);
        }
        await Task.Delay(500, ct);
        warmupCts.Cancel();
        int resp = Interlocked.CompareExchange(ref responded, 0, 0);
        Console.WriteLine($"warmup {pattern} {ch}: sent={success} responded={resp}");
        return success >= 1;
    }

    // --- Private: Periodic Tasks ---

    private void StartPeriodicTasks()
    {
        if (_runCfg == null) return;
        int reportIntervalMs = (int)(Config.ReportIntervalSec(_runCfg) * 1000);

        _timers.Add(new Timer(_ => PeriodicReport(), null, reportIntervalMs, reportIntervalMs));
        _timers.Add(new Timer(_ =>
        {
            foreach (var w in AllWorkers)
            {
                w.PeakRate.Advance();
                w.SlidingRate.Advance();
            }
        }, null, 1000, 1000));
        _timers.Add(new Timer(_ =>
        {
            Metrics.SetUptime(_bootUptime.Elapsed.TotalSeconds);
            int threads = Process.GetCurrentProcess().Threads.Count;
            Metrics.SetActiveWorkers(threads);
            if (threads > _peakWorkers) _peakWorkers = threads;
        }, null, 1000, 1000));
        {
            double runDurSec = Config.DurationSec(_runCfg.Duration);
            double effectiveRunDurSec = runDurSec > 0 ? runDurSec : Config.MaxDurationSec(_runCfg);
            if (effectiveRunDurSec < MemoryBaseline1Min)
            {
                _baselineRss = GetRssMb();
                _baselineSetAt = "running-start";
                Console.WriteLine($"memory baseline set (running-start): {_baselineRss:F1} MB");
            }
            _timers.Add(new Timer(_ =>
            {
                double rss = GetRssMb();
                if (rss > _peakRss) _peakRss = rss;
                if (_baselineRss != 0) return;
                double runTime = _testStopwatch?.Elapsed.TotalSeconds ?? 0;
                if (effectiveRunDurSec < MemoryBaseline5Min && runTime >= MemoryBaseline1Min)
                {
                    _baselineRss = rss;
                    _baselineSetAt = "1min";
                    Console.WriteLine($"memory baseline set (1min): {rss:F1} MB at {runTime:F0}s");
                }
                else if (runTime >= MemoryBaseline5Min)
                {
                    _baselineRss = rss;
                    _baselineSetAt = "5min";
                    Console.WriteLine($"memory baseline set (5min): {rss:F1} MB at {runTime:F0}s");
                }
            }, null, 10000, 10000));
        }
        _timers.Add(new Timer(_ =>
        {
            foreach (var w in AllWorkers) w.TsStore.Purge(TimeSpan.FromMilliseconds(60000));
        }, null, 60000, 60000));

        _peakRss = GetRssMb();
    }

    private void PeriodicReport()
    {
        double elapsed = _testStopwatch?.Elapsed.TotalSeconds ?? _bootUptime.Elapsed.TotalSeconds;
        double rss = GetRssMb();

        foreach (var (_, pg) in _patternGroups)
        {
            foreach (var w in pg.ChannelWorkers)
            {
                var gaps = w.Tracker.DetectGaps();
                foreach (var (_, delta) in gaps) Metrics.IncLost(w.Pattern, delta);
            }

            long totalSent = pg.TotalSent;
            long totalReceived = pg.TotalReceived;
            Metrics.SetConsumerLag(pg.Pattern, Math.Max(0, totalSent - totalReceived));
            if (elapsed > 0) Metrics.SetActualRate(pg.Pattern, totalSent / elapsed);
        }

        if (_runCfg?.Logging.Format == "json")
        {
            var patternsObj = new Dictionary<string, object>();
            foreach (var (pattern, pg) in _patternGroups)
            {
                long rate = elapsed > 0 ? (long)(pg.TotalSent / elapsed) : 0;
                patternsObj[pattern] = new Dictionary<string, object>
                {
                    ["sent"] = pg.TotalSent, ["recv"] = pg.TotalReceived,
                    ["lost"] = pg.TotalLost, ["dup"] = pg.TotalDuplicated,
                    ["err"] = pg.TotalErrors,
                    ["p99_ms"] = pg.PatternLatencyAccum.PercentileMs(99),
                    ["rate"] = rate, ["channels"] = pg.Config.Channels,
                };
            }
            Console.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["ts"] = DateTime.UtcNow.ToString("o"), ["level"] = "info",
                ["msg"] = "periodic_status", ["uptime_s"] = (long)elapsed,
                ["mode"] = _runCfg.Mode, ["rss_mb"] = (long)rss, ["patterns"] = patternsObj,
            }));
        }
        else
        {
            string ts = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            var sb = new StringBuilder();
            sb.AppendLine($"[{ts}] BURN-IN STATUS | uptime={FormatDuration(elapsed)} mode={_runCfg?.Mode ?? "?"} rss={rss:F0}MB");
            foreach (var (pattern, pg) in _patternGroups)
            {
                string rate = elapsed > 0 ? $"{pg.TotalSent / elapsed:F0}" : "0";
                string chInfo = pg.Config.Channels > 1 ? $" ({pg.Config.Channels}ch)" : "";
                if (pattern is "commands" or "queries")
                    sb.AppendLine($"  {pattern,-14}{chInfo,-6} sent={pg.TotalSent,-8} resp={pg.TotalRpcSuccess,-8} tout={pg.TotalRpcTimeout,-4} err={pg.TotalErrors,-4} p99={pg.PatternLatencyAccum.PercentileMs(99):F1}ms rate={rate}/s");
                else
                    sb.AppendLine($"  {pattern,-14}{chInfo,-6} sent={pg.TotalSent,-8} recv={pg.TotalReceived,-8} lost={pg.TotalLost,-4} dup={pg.TotalDuplicated,-4} err={pg.TotalErrors,-4} p99={pg.PatternLatencyAccum.PercentileMs(99):F1}ms rate={rate}/s");
            }
            Console.Write(sb.ToString());
        }
    }

    // --- Private: Summary & Response Building ---

    /// <summary>
    /// Build per-channel data from PatternGroups for fail-on-any-channel verdict checks.
    /// </summary>
    private Dictionary<string, PatternChannelData> BuildPerChannelData()
    {
        var result = new Dictionary<string, PatternChannelData>();

        foreach (var (pattern, pg) in _patternGroups)
        {
            var pcd = new PatternChannelData { Pattern = pattern };

            foreach (var worker in pg.ChannelWorkers)
            {
                pcd.Channels.Add(new ChannelVerdictData
                {
                    ChannelIndex = worker.ChannelIndex,
                    Sent = worker.Sent,
                    Received = worker.Received,
                    Lost = worker.Tracker.TotalLost(),
                    Duplicated = worker.Tracker.TotalDuplicates(),
                    Corrupted = worker.Corrupted,
                    Errors = worker.Errors,
                    ConsumersPerChannel = pg.Config.ConsumersPerChannel,
                    ConsumerGroup = pg.Config.ConsumerGroup,
                });
            }

            result[pattern] = pcd;
        }

        return result;
    }

    /// <summary>
    /// Point-in-time snapshot of all PatternGroup counters, captured at producer-stop time (T2).
    /// </summary>
    private sealed class PatternSnapshot
    {
        public long Sent, Received, Lost, Duplicated, Corrupted, OutOfOrder;
        public long Errors, Reconnections, BytesSent, BytesReceived;
        public long RpcSuccess, RpcTimeout, RpcError, Unconfirmed;
        public double PeakRate, DowntimeSeconds;
        public double LatencyP50Ms, LatencyP95Ms, LatencyP99Ms, LatencyP999Ms;
        // Per-channel data for verdict checks
        public List<ChannelVerdictData> PerChannelData = new();
    }

    private Dictionary<string, PatternSnapshot> CapturePatternSnapshots()
    {
        var snapshots = new Dictionary<string, PatternSnapshot>();
        foreach (var (pattern, pg) in _patternGroups)
        {
            var latAccum = pg.PatternLatencyAccum;
            var snap = new PatternSnapshot
            {
                Sent = pg.TotalSent,
                Received = pg.TotalReceived,
                Lost = pg.TotalLost,
                Duplicated = pg.TotalDuplicated,
                Corrupted = pg.TotalCorrupted,
                OutOfOrder = pg.TotalOutOfOrder,
                Errors = pg.TotalErrors,
                Reconnections = pg.TotalReconnections,
                BytesSent = pg.TotalBytesSent,
                BytesReceived = pg.TotalBytesReceived,
                RpcSuccess = pg.TotalRpcSuccess,
                RpcTimeout = pg.TotalRpcTimeout,
                RpcError = pg.TotalRpcError,
                Unconfirmed = pg.TotalUnconfirmed,
                PeakRate = pg.MaxPeakRate,
                DowntimeSeconds = pg.MaxDowntimeSeconds,
                LatencyP50Ms = latAccum.PercentileMs(50),
                LatencyP95Ms = latAccum.PercentileMs(95),
                LatencyP99Ms = latAccum.PercentileMs(99),
                LatencyP999Ms = latAccum.PercentileMs(99.9),
            };

            // Per-channel snapshot
            foreach (var worker in pg.ChannelWorkers)
            {
                snap.PerChannelData.Add(new ChannelVerdictData
                {
                    ChannelIndex = worker.ChannelIndex,
                    Sent = worker.Sent,
                    Received = worker.Received,
                    Lost = worker.Tracker.TotalLost(),
                    Duplicated = worker.Tracker.TotalDuplicates(),
                    Corrupted = worker.Corrupted,
                    Errors = worker.Errors,
                    ConsumersPerChannel = pg.Config.ConsumersPerChannel,
                    ConsumerGroup = pg.Config.ConsumerGroup,
                });
            }

            snapshots[pattern] = snap;
        }
        return snapshots;
    }

    private BurninSummary BuildSummary(string status = "running")
    {
        double elapsed = _producersStoppedElapsed
            ?? _testStopwatch?.Elapsed.TotalSeconds
            ?? _bootUptime.Elapsed.TotalSeconds;
        var patterns = new Dictionary<string, PatternSummary>();

        foreach (var (pattern, pg) in _patternGroups)
        {
            // Use snapshot values if available (taken at producer-stop time T2),
            // otherwise fall back to live values.
            PatternSnapshot? snap = _producerStopSnapshot?.GetValueOrDefault(pattern);

            long sent, received, lost, duplicated, corrupted, outOfOrder;
            long errors, reconnections, bytesSent, bytesReceived;
            long rpcSuccess, rpcTimeout, rpcError, unconfirmed;
            double peakRate, downtimeSeconds;
            double latP50, latP95, latP99, latP999;

            if (snap != null)
            {
                sent = snap.Sent; received = snap.Received; lost = snap.Lost;
                duplicated = snap.Duplicated; corrupted = snap.Corrupted; outOfOrder = snap.OutOfOrder;
                errors = snap.Errors; reconnections = snap.Reconnections;
                bytesSent = snap.BytesSent; bytesReceived = snap.BytesReceived;
                rpcSuccess = snap.RpcSuccess; rpcTimeout = snap.RpcTimeout; rpcError = snap.RpcError;
                unconfirmed = snap.Unconfirmed; peakRate = snap.PeakRate;
                downtimeSeconds = snap.DowntimeSeconds;
                latP50 = snap.LatencyP50Ms; latP95 = snap.LatencyP95Ms;
                latP99 = snap.LatencyP99Ms; latP999 = snap.LatencyP999Ms;
            }
            else
            {
                var latAccum = pg.PatternLatencyAccum;
                sent = pg.TotalSent; received = pg.TotalReceived; lost = pg.TotalLost;
                duplicated = pg.TotalDuplicated; corrupted = pg.TotalCorrupted; outOfOrder = pg.TotalOutOfOrder;
                errors = pg.TotalErrors; reconnections = pg.TotalReconnections;
                bytesSent = pg.TotalBytesSent; bytesReceived = pg.TotalBytesReceived;
                rpcSuccess = pg.TotalRpcSuccess; rpcTimeout = pg.TotalRpcTimeout; rpcError = pg.TotalRpcError;
                unconfirmed = pg.TotalUnconfirmed; peakRate = pg.MaxPeakRate;
                downtimeSeconds = pg.MaxDowntimeSeconds;
                latP50 = latAccum.PercentileMs(50); latP95 = latAccum.PercentileMs(95);
                latP99 = latAccum.PercentileMs(99); latP999 = latAccum.PercentileMs(99.9);
            }

            double lossPct = sent > 0 ? (double)lost / sent * 100 : 0;
            double avgTp = elapsed > 0 ? sent / elapsed : 0;
            int targetRate = pg.Config.Rate * pg.Config.Channels;
            bool isRpc = pattern is "commands" or "queries";

            var latencyData = new LatencyData
            {
                P50Ms = latP50, P95Ms = latP95, P99Ms = latP99, P999Ms = latP999,
            };

            var ps = new PatternSummary
            {
                Enabled = true,
                Status = _patternStates.GetValueOrDefault(pattern, PatternState.Running).ToApi(),
                Sent = sent, Received = received, Lost = lost,
                Duplicated = duplicated, Corrupted = corrupted,
                OutOfOrder = outOfOrder, LossPct = lossPct,
                Errors = errors, Reconnections = reconnections,
                DowntimeSeconds = downtimeSeconds,
                LatencyP50Ms = latP50, LatencyP95Ms = latP95,
                LatencyP99Ms = latP99, LatencyP999Ms = latP999,
                Latency = latencyData,
                AvgRate = avgTp, PeakRate = peakRate,
                AvgThroughputMsgsSec = avgTp, PeakThroughputMsgsSec = peakRate,
                TargetRate = targetRate,
                BytesSent = bytesSent, BytesReceived = bytesReceived,
                // v2 multi-channel fields
                ChannelCount = pg.Config.Channels,
                ProducersPerChannel = isRpc ? 0 : pg.Config.ProducersPerChannel,
                ConsumersPerChannel = isRpc ? 0 : pg.Config.ConsumersPerChannel,
                SendersPerChannel = isRpc ? pg.Config.SendersPerChannel : 0,
                RespondersPerChannel = isRpc ? pg.Config.RespondersPerChannel : 0,
            };

            if (isRpc)
            {
                ps.ResponsesSuccess = rpcSuccess;
                ps.ResponsesTimeout = rpcTimeout;
                ps.ResponsesError = rpcError;
                ps.RpcP50Ms = latP50;
                ps.RpcP95Ms = latP95;
                ps.RpcP99Ms = latP99;
                ps.RpcP999Ms = latP999;
                if (elapsed > 0) ps.AvgThroughputRpcSec = rpcSuccess / elapsed;
            }

            if (pattern == "events_store")
                ps.Unconfirmed = unconfirmed;

            // Consumer group / broadcast metadata
            if (pattern is "events" or "events_store")
            {
                ps.ConsumerGroup = pg.Config.ConsumerGroup;
                ps.NumConsumers = pg.Config.ConsumersPerChannel;
            }

            patterns[pattern] = ps;
        }

        double baseline = _baselineRss > 0 ? _baselineRss : Math.Max(_peakRss, 1);
        double peak = _peakRss > 0 ? _peakRss : GetRssMb();
        double growth = baseline > 0 ? peak / baseline : 1;

        string version = _runCfg?.Output.SdkVersion ?? _startupCfg.Output.SdkVersion;
        if (string.IsNullOrEmpty(version)) version = DetectSdkVersion();

        return new BurninSummary
        {
            Sdk = "csharp", Version = version, Mode = _runCfg?.Mode ?? "soak",
            BrokerAddress = _runCfg?.Broker.Address ?? _startupCfg.Broker.Address,
            StartedAt = _startedAt ?? "", EndedAt = _endedAt ?? "",
            DurationSeconds = elapsed, Status = status, Patterns = patterns,
            AllPatternsEnabled = _enabledPatterns.Count == AllPatterns.Names.Length,
            Resources = new ResourceSummary
            {
                PeakRssMb = peak, BaselineRssMb = baseline,
                MemoryGrowthFactor = growth, PeakWorkers = _peakWorkers,
            },
        };
    }

    private object BuildRunResponse(RunState state)
    {
        double elapsed = GetElapsed();
        double remaining = GetRemaining(elapsed);
        var patterns = new Dictionary<string, object>();

        foreach (string name in AllPatterns.Names)
        {
            if (!_enabledPatterns.Contains(name))
            {
                patterns[name] = new { enabled = false };
                continue;
            }

            if (!_patternGroups.TryGetValue(name, out var pg))
            {
                patterns[name] = new { enabled = true, state = _patternStates.GetValueOrDefault(name, PatternState.Starting).ToApi() };
                continue;
            }

            long sent = pg.TotalSent, received = pg.TotalReceived, lost = pg.TotalLost;
            double lossPct = sent > 0 ? (double)lost / sent * 100 : 0;
            double actualRate = elapsed > 0 ? sent / elapsed : 0;

            if (name is "commands" or "queries")
            {
                patterns[name] = new
                {
                    enabled = true,
                    state = _patternStates.GetValueOrDefault(name, PatternState.Running).ToApi(),
                    channels = pg.Config.Channels,
                    senders_per_channel = pg.Config.SendersPerChannel,
                    responders_per_channel = pg.Config.RespondersPerChannel,
                    sent, responses_success = pg.TotalRpcSuccess, responses_timeout = pg.TotalRpcTimeout,
                    responses_error = pg.TotalRpcError, errors = pg.TotalErrors, reconnections = pg.TotalReconnections,
                    target_rate = pg.Config.Rate * pg.Config.Channels,
                    actual_rate = Math.Round(actualRate, 1),
                    peak_rate = Math.Round(pg.MaxPeakRate, 1),
                    bytes_sent = pg.TotalBytesSent, bytes_received = pg.TotalBytesReceived,
                    latency = new
                    {
                        p50_ms = pg.PatternLatencyAccum.PercentileMs(50),
                        p95_ms = pg.PatternLatencyAccum.PercentileMs(95),
                        p99_ms = pg.PatternLatencyAccum.PercentileMs(99),
                        p999_ms = pg.PatternLatencyAccum.PercentileMs(99.9),
                    },
                };
            }
            else
            {
                patterns[name] = new
                {
                    enabled = true,
                    state = _patternStates.GetValueOrDefault(name, PatternState.Running).ToApi(),
                    channels = pg.Config.Channels,
                    producers_per_channel = pg.Config.ProducersPerChannel,
                    consumers_per_channel = pg.Config.ConsumersPerChannel,
                    consumer_group = pg.Config.ConsumerGroup,
                    sent, received, lost, duplicated = pg.TotalDuplicated,
                    corrupted = pg.TotalCorrupted, out_of_order = pg.TotalOutOfOrder,
                    errors = pg.TotalErrors, reconnections = pg.TotalReconnections,
                    loss_pct = Math.Round(lossPct, 5),
                    target_rate = pg.Config.Rate * pg.Config.Channels,
                    actual_rate = Math.Round(actualRate, 1),
                    peak_rate = Math.Round(pg.MaxPeakRate, 1),
                    bytes_sent = pg.TotalBytesSent, bytes_received = pg.TotalBytesReceived,
                    latency = new
                    {
                        p50_ms = pg.PatternLatencyAccum.PercentileMs(50),
                        p95_ms = pg.PatternLatencyAccum.PercentileMs(95),
                        p99_ms = pg.PatternLatencyAccum.PercentileMs(99),
                        p999_ms = pg.PatternLatencyAccum.PercentileMs(99.9),
                    },
                };
            }
        }

        double rss = GetRssMb();
        var baseResponse = new Dictionary<string, object?>
        {
            ["run_id"] = _runId,
            ["state"] = state.ToApi(),
            ["mode"] = _runCfg?.Mode ?? "soak",
            ["started_at"] = _startedAt,
            ["elapsed_seconds"] = Math.Round(elapsed, 1),
            ["remaining_seconds"] = Math.Round(remaining, 1),
            ["duration"] = _runCfg?.Duration ?? "1h",
            ["warmup_active"] = IsWarmupActive(),
            ["broker_address"] = _runCfg?.Broker.Address ?? _startupCfg.Broker.Address,
            ["patterns"] = patterns,
            ["resources"] = state is RunState.Stopped or RunState.Error
                ? new { peak_rss_mb = Math.Round(_peakRss, 1), baseline_rss_mb = Math.Round(_baselineRss > 0 ? _baselineRss : rss, 1), memory_growth_factor = Math.Round(_baselineRss > 0 ? _peakRss / _baselineRss : 1.0, 2), peak_workers = _peakWorkers }
                : (object)new { rss_mb = Math.Round(rss, 1), baseline_rss_mb = Math.Round(_baselineRss > 0 ? _baselineRss : rss, 1), memory_growth_factor = Math.Round(_baselineRss > 0 ? rss / _baselineRss : 1.0, 2), active_workers = Process.GetCurrentProcess().Threads.Count },
        };

        if (state == RunState.Error && _errorMessage != null)
            baseResponse["error"] = _errorMessage;

        if (state is RunState.Stopped or RunState.Error)
            baseResponse["ended_at"] = _endedAt;

        return baseResponse;
    }

    private object BuildTotals()
    {
        long sent = 0, received = 0, lost = 0, duplicated = 0, corrupted = 0, outOfOrder = 0, errors = 0, reconnections = 0;

        foreach (var (pattern, pg) in _patternGroups)
        {
            sent += pg.TotalSent;
            if (pattern is "commands" or "queries")
            {
                received += pg.TotalRpcSuccess;
                lost += pg.TotalRpcTimeout + pg.TotalRpcError;
            }
            else
            {
                received += pg.TotalReceived;
                lost += pg.TotalLost;
            }
            duplicated += pg.TotalDuplicated;
            corrupted += pg.TotalCorrupted;
            outOfOrder += pg.TotalOutOfOrder;
            errors += pg.TotalErrors;
            reconnections += pg.TotalReconnections;
        }

        return new { sent, received, lost, duplicated, corrupted, out_of_order = outOfOrder, errors, reconnections };
    }

    // --- Private: Helpers ---

    private double GetElapsed()
    {
        return _testStopwatch?.Elapsed.TotalSeconds ?? (_startedAt != null ? (DateTime.UtcNow - DateTime.Parse(_startedAt)).TotalSeconds : 0);
    }

    private double GetRemaining(double elapsed)
    {
        if (_runCfg == null) return 0;
        double dur = Config.DurationSec(_runCfg.Duration);
        if (dur <= 0) return 0;
        return Math.Max(0, dur - elapsed);
    }

    private bool IsWarmupActive() => false; // warmup is synchronous, so if running it's done

    private static double GetRssMb() => Process.GetCurrentProcess().WorkingSet64 / (1024.0 * 1024.0);

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
        catch (OperationCanceledException) { }
    }

    private static string DetectSdkVersion()
    {
        try
        {
            string? dir = Path.GetDirectoryName(typeof(Engine).Assembly.Location);
            if (dir == null) return "unknown";
            string csproj = Path.Combine(dir, "..", "..", "..", "..", "src", "KubeMQ.Sdk", "KubeMQ.Sdk.csproj");
            if (File.Exists(csproj))
            {
                string content = File.ReadAllText(csproj);
                var match = System.Text.RegularExpressions.Regex.Match(content, @"<Version>(.*?)</Version>");
                if (match.Success) return match.Groups[1].Value;
            }
        }
        catch { }
        return "unknown";
    }

    private void PrintBanner()
    {
        int totalChannels = _patternGroups.Values.Sum(pg => pg.Config.Channels);
        Console.WriteLine(new string('=', 67));
        Console.WriteLine("  KUBEMQ BURN-IN TEST -- C# SDK (v2 Multi-Channel)");
        Console.WriteLine(new string('=', 67));
        Console.WriteLine($"  Mode:     {_runCfg?.Mode}");
        Console.WriteLine($"  Broker:   {_runCfg?.Broker.Address}");
        Console.WriteLine($"  Duration: {_runCfg?.Duration}");
        Console.WriteLine($"  Run ID:   {_runId}");
        Console.WriteLine($"  Patterns: {string.Join(", ", _enabledPatterns)}");
        Console.WriteLine($"  Channels: {totalChannels} total across {_enabledPatterns.Count} patterns");
        foreach (var (pattern, pg) in _patternGroups)
        {
            bool isRpc = pattern is "commands" or "queries";
            string workers = isRpc
                ? $"s={pg.Config.SendersPerChannel} r={pg.Config.RespondersPerChannel}"
                : $"p={pg.Config.ProducersPerChannel} c={pg.Config.ConsumersPerChannel}";
            Console.WriteLine($"    {pattern}: {pg.Config.Channels}ch {workers} rate={pg.Config.Rate}/ch target={pg.Config.Rate * pg.Config.Channels}/s");
        }
        Console.WriteLine(new string('=', 67));
        Console.WriteLine();
    }

    public void Dispose()
    {
        foreach (var (_, pg) in _patternGroups) pg.Dispose();
        foreach (var (_, client) in _patternClients)
        {
            try { client.Dispose(); } catch { }
        }
        _patternClients.Clear();
        _runCts?.Dispose();
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
        using var client = new KubeMQClient(new KubeMQClientOptions
        {
            Address = cfg.Broker.Address,
            ClientId = $"{cfg.Broker.ClientIdPrefix}-{cfg.RunId}",
        });
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
                    try { await deleteFn(ch.Name); count++; } catch { }
                }
            }
            catch { }
        }

        await CleanType(() => client.ListEventsChannelsAsync(prefix, CancellationToken.None), n => client.DeleteEventsChannelAsync(n, CancellationToken.None));
        await CleanType(() => client.ListEventsStoreChannelsAsync(prefix, CancellationToken.None), n => client.DeleteEventsStoreChannelAsync(n, CancellationToken.None));
        await CleanType(() => client.ListQueuesChannelsAsync(prefix, CancellationToken.None), n => client.DeleteQueuesChannelAsync(n, CancellationToken.None));
        await CleanType(() => client.ListCommandsChannelsAsync(prefix, CancellationToken.None), n => client.DeleteCommandsChannelAsync(n, CancellationToken.None));
        await CleanType(() => client.ListQueriesChannelsAsync(prefix, CancellationToken.None), n => client.DeleteQueriesChannelAsync(n, CancellationToken.None));

        Console.WriteLine($"cleaned {count} stale channels");
    }
}
