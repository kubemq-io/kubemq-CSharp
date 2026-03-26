using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace KubeMQ.Burnin;

#region API Request Models

public sealed class RunStartRequest
{
    [JsonPropertyName("broker")]
    public ApiBrokerOverride? Broker { get; set; }

    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("run_id")]
    public string? RunId { get; set; }

    [JsonPropertyName("starting_timeout_seconds")]
    public int? StartingTimeoutSeconds { get; set; }

    [JsonPropertyName("patterns")]
    public Dictionary<string, ApiPatternConfig>? Patterns { get; set; }

    [JsonPropertyName("queue")]
    public ApiQueueConfig? Queue { get; set; }

    [JsonPropertyName("rpc")]
    public ApiRpcConfig? Rpc { get; set; }

    [JsonPropertyName("message")]
    public ApiMessageConfig? Message { get; set; }

    [JsonPropertyName("thresholds")]
    public ApiGlobalThresholds? Thresholds { get; set; }

    [JsonPropertyName("forced_disconnect")]
    public ApiForcedDisconnectConfig? ForcedDisconnect { get; set; }

    [JsonPropertyName("shutdown")]
    public ApiShutdownConfig? Shutdown { get; set; }

    [JsonPropertyName("metrics")]
    public ApiMetricsRunConfig? Metrics { get; set; }

    [JsonPropertyName("warmup")]
    public ApiWarmupConfig? Warmup { get; set; }

    // v1 detection fields -- these should NOT be present in v2 requests
    [JsonPropertyName("concurrency")]
    public object? Concurrency { get; set; }

    [JsonPropertyName("rates")]
    public object? Rates { get; set; }
}

public sealed class ApiBrokerOverride
{
    [JsonPropertyName("address")]
    public string? Address { get; set; }
}

/// <summary>
/// v2 per-pattern config with _per_channel fields.
/// </summary>
public sealed class ApiPatternConfig
{
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }

    [JsonPropertyName("channels")]
    public int? Channels { get; set; }

    [JsonPropertyName("rate")]
    public int? Rate { get; set; }

    [JsonPropertyName("producers_per_channel")]
    public int? ProducersPerChannel { get; set; }

    [JsonPropertyName("consumers_per_channel")]
    public int? ConsumersPerChannel { get; set; }

    [JsonPropertyName("consumer_group")]
    public bool? ConsumerGroup { get; set; }

    [JsonPropertyName("senders_per_channel")]
    public int? SendersPerChannel { get; set; }

    [JsonPropertyName("responders_per_channel")]
    public int? RespondersPerChannel { get; set; }

    [JsonPropertyName("thresholds")]
    public ApiPatternThresholds? Thresholds { get; set; }

    // v1 field detection: these old field names should trigger v1 rejection
    [JsonPropertyName("producers")]
    public int? Producers { get; set; }

    [JsonPropertyName("consumers")]
    public int? Consumers { get; set; }

    [JsonPropertyName("senders")]
    public int? Senders { get; set; }

    [JsonPropertyName("responders")]
    public int? Responders { get; set; }
}

public sealed class ApiPatternThresholds
{
    [JsonPropertyName("max_loss_pct")]
    public double? MaxLossPct { get; set; }

    [JsonPropertyName("max_duplication_pct")]
    public double? MaxDuplicationPct { get; set; }

    [JsonPropertyName("max_p99_latency_ms")]
    public int? MaxP99LatencyMs { get; set; }

    [JsonPropertyName("max_p999_latency_ms")]
    public int? MaxP999LatencyMs { get; set; }
}

public sealed class ApiQueueConfig
{
    [JsonPropertyName("poll_max_messages")]
    public int? PollMaxMessages { get; set; }

    [JsonPropertyName("poll_wait_timeout_seconds")]
    public int? PollWaitTimeoutSeconds { get; set; }

    [JsonPropertyName("auto_ack")]
    public bool? AutoAck { get; set; }

    [JsonPropertyName("max_depth")]
    public int? MaxDepth { get; set; }
}

public sealed class ApiRpcConfig
{
    [JsonPropertyName("timeout_ms")]
    public int? TimeoutMs { get; set; }
}

public sealed class ApiMessageConfig
{
    [JsonPropertyName("size_mode")]
    public string? SizeMode { get; set; }

    [JsonPropertyName("size_bytes")]
    public int? SizeBytes { get; set; }

    [JsonPropertyName("size_distribution")]
    public string? SizeDistribution { get; set; }

    [JsonPropertyName("reorder_window")]
    public int? ReorderWindow { get; set; }
}

public sealed class ApiGlobalThresholds
{
    [JsonPropertyName("max_loss_pct")]
    public double? MaxLossPct { get; set; }

    [JsonPropertyName("max_events_loss_pct")]
    public double? MaxEventsLossPct { get; set; }

    [JsonPropertyName("max_duplication_pct")]
    public double? MaxDuplicationPct { get; set; }

    [JsonPropertyName("max_error_rate_pct")]
    public double? MaxErrorRatePct { get; set; }

    [JsonPropertyName("max_memory_growth_factor")]
    public double? MaxMemoryGrowthFactor { get; set; }

    [JsonPropertyName("max_downtime_pct")]
    public double? MaxDowntimePct { get; set; }

    [JsonPropertyName("min_throughput_pct")]
    public double? MinThroughputPct { get; set; }

    [JsonPropertyName("max_p99_latency_ms")]
    public double? MaxP99LatencyMs { get; set; }

    [JsonPropertyName("max_p999_latency_ms")]
    public double? MaxP999LatencyMs { get; set; }

    [JsonPropertyName("max_duration")]
    public string? MaxDuration { get; set; }
}

public sealed class ApiForcedDisconnectConfig
{
    [JsonPropertyName("interval")]
    public string? Interval { get; set; }

    [JsonPropertyName("duration")]
    public string? Duration { get; set; }
}

public sealed class ApiShutdownConfig
{
    [JsonPropertyName("drain_timeout_seconds")]
    public int? DrainTimeoutSeconds { get; set; }

    [JsonPropertyName("cleanup_channels")]
    public bool? CleanupChannels { get; set; }
}

public sealed class ApiMetricsRunConfig
{
    [JsonPropertyName("report_interval")]
    public string? ReportInterval { get; set; }
}

public sealed class ApiWarmupConfig
{
    [JsonPropertyName("max_parallel_channels")]
    public int? MaxParallelChannels { get; set; }

    [JsonPropertyName("timeout_per_channel_ms")]
    public int? TimeoutPerChannelMs { get; set; }

    [JsonPropertyName("warmup_duration")]
    public string? WarmupDuration { get; set; }
}

#endregion

#region Per-Pattern Resolved Thresholds

public sealed class ResolvedPatternThreshold
{
    public double MaxLossPct { get; set; }
    public double MaxDuplicationPct { get; set; } = 0.1;
    public double MaxP99LatencyMs { get; set; } = 1000;
    public double MaxP999LatencyMs { get; set; } = 5000;
}

#endregion

#region Config Translation & Validation

public static partial class ApiConfigTranslator
{
    private static readonly Dictionary<string, int> DefaultRates = new()
    {
        ["events"] = 100, ["events_store"] = 100,
        ["queue_stream"] = 50, ["queue_simple"] = 100,
        ["commands"] = 100, ["queries"] = 100,
    };

    private static readonly Dictionary<string, double> DefaultLoss = new()
    {
        ["events"] = 5.0, ["events_store"] = 0.0,
        ["queue_stream"] = 0.0, ["queue_simple"] = 0.0,
    };

    [GeneratedRegex(@"^\d+(s|m|h|d)$")]
    private static partial Regex DurationRegex();

    /// <summary>
    /// v1 format detection: dual-layer approach.
    /// Layer 1: top-level concurrency/rates keys.
    /// Layer 2: old field names (producers/consumers/senders/responders without _per_channel).
    /// </summary>
    public static (bool IsV1, string Message, List<string> Errors) DetectV1Format(RunStartRequest? req)
    {
        if (req == null)
            return (false, "", new List<string>());

        var v1Errors = new List<string>();

        // Layer 1: top-level concurrency/rates keys
        if (req.Concurrency != null)
            v1Errors.Add("detected v1 field: concurrency -- use patterns block instead");
        if (req.Rates != null)
            v1Errors.Add("detected v1 field: rates -- use patterns block instead");

        // Layer 2: old field names in patterns
        if (req.Patterns != null)
        {
            foreach (var (name, pc) in req.Patterns)
            {
                if (pc.Producers.HasValue)
                    v1Errors.Add($"detected v1 field: patterns.{name}.producers -- use producers_per_channel");
                if (pc.Consumers.HasValue)
                    v1Errors.Add($"detected v1 field: patterns.{name}.consumers -- use consumers_per_channel");
                if (pc.Senders.HasValue)
                    v1Errors.Add($"detected v1 field: patterns.{name}.senders -- use senders_per_channel");
                if (pc.Responders.HasValue)
                    v1Errors.Add($"detected v1 field: patterns.{name}.responders -- use responders_per_channel");
            }
        }

        if (v1Errors.Count > 0)
            return (true, "v1 config format not supported. Update to v2 patterns format.", v1Errors);

        return (false, "", new List<string>());
    }

    public static List<string> Validate(RunStartRequest? req)
    {
        var errors = new List<string>();
        if (req == null)
        {
            errors.Add("request body is empty");
            return errors;
        }

        // v1 detection
        var (isV1, v1Msg, v1Errors) = DetectV1Format(req);
        if (isV1)
        {
            errors.Add(v1Msg);
            errors.AddRange(v1Errors);
            return errors;
        }

        if (req.Mode != null && req.Mode is not ("soak" or "benchmark"))
            errors.Add($"mode must be 'soak' or 'benchmark', got '{req.Mode}'");

        if (req.Duration != null && req.Duration != "0" && !DurationRegex().IsMatch(req.Duration))
            errors.Add($"duration must match '\\d+(s|m|h|d)' or '0', got '{req.Duration}'");

        if (req.StartingTimeoutSeconds is not null and <= 0)
            errors.Add("starting_timeout_seconds must be > 0");

        if (req.Message?.SizeBytes is not null and < 64)
            errors.Add($"message.size_bytes must be >= 64, got {req.Message.SizeBytes}");

        if (req.Message?.ReorderWindow is not null and < 100)
            errors.Add($"message.reorder_window must be >= 100, got {req.Message.ReorderWindow}");

        if (req.Message?.SizeMode != null && req.Message.SizeMode is not ("fixed" or "distribution"))
            errors.Add($"message.size_mode must be 'fixed' or 'distribution', got '{req.Message.SizeMode}'");

        if (req.Shutdown?.DrainTimeoutSeconds is not null and <= 0)
            errors.Add("shutdown.drain_timeout_seconds must be > 0");

        if (req.Rpc?.TimeoutMs is not null and <= 0)
            errors.Add("rpc.timeout_ms must be > 0");

        if (req.Queue?.PollWaitTimeoutSeconds is not null and <= 0)
            errors.Add("queue.poll_wait_timeout_seconds must be > 0");

        ValidateThresholds(req.Thresholds, errors);
        ValidatePatterns(req, errors);

        return errors;
    }

    private static void ValidateThresholds(ApiGlobalThresholds? t, List<string> errors)
    {
        if (t == null) return;
        if (t.MaxDuplicationPct is < 0 or > 100)
            errors.Add($"thresholds.max_duplication_pct must be 0-100, got {t.MaxDuplicationPct}");
        if (t.MaxErrorRatePct is < 0 or > 100)
            errors.Add($"thresholds.max_error_rate_pct must be 0-100, got {t.MaxErrorRatePct}");
        if (t.MaxDowntimePct is < 0 or > 100)
            errors.Add($"thresholds.max_downtime_pct must be 0-100, got {t.MaxDowntimePct}");
        if (t.MinThroughputPct is < 0 or > 100)
            errors.Add($"thresholds.min_throughput_pct must be 0-100, got {t.MinThroughputPct}");
        if (t.MaxMemoryGrowthFactor is not null and < 1.0)
            errors.Add($"thresholds.max_memory_growth_factor must be >= 1.0, got {t.MaxMemoryGrowthFactor}");
    }

    private static void ValidatePatterns(RunStartRequest req, List<string> errors)
    {
        bool anyEnabled = false;
        string mode = req.Mode ?? "soak";

        if (req.Patterns == null)
        {
            anyEnabled = true; // default: all enabled
            return;
        }

        foreach (var (name, pc) in req.Patterns)
        {
            if (name is not ("events" or "events_store" or "queue_stream" or "queue_simple" or "commands" or "queries"))
            {
                continue; // silently ignore unknown patterns
            }

            bool enabled = pc.Enabled ?? true;
            if (enabled) anyEnabled = true;
            if (!enabled) continue;

            // Channels validation
            if (pc.Channels.HasValue && (pc.Channels.Value < 1 || pc.Channels.Value > 1000))
                errors.Add($"{name}.channels: must be 1-1000, got {pc.Channels.Value}");

            if (mode == "soak" && pc.Rate is not null and < 0)
                errors.Add($"patterns.{name}.rate: must be >= 0, got {pc.Rate}");

            bool isRpc = name is "commands" or "queries";
            if (isRpc)
            {
                if (pc.SendersPerChannel is not null and < 1)
                    errors.Add($"patterns.{name}.senders_per_channel must be >= 1, got {pc.SendersPerChannel}");
                if (pc.RespondersPerChannel is not null and < 1)
                    errors.Add($"patterns.{name}.responders_per_channel must be >= 1, got {pc.RespondersPerChannel}");
            }
            else
            {
                if (pc.ProducersPerChannel is not null and < 1)
                    errors.Add($"patterns.{name}.producers_per_channel must be >= 1, got {pc.ProducersPerChannel}");
                if (pc.ConsumersPerChannel is not null and < 1)
                    errors.Add($"patterns.{name}.consumers_per_channel must be >= 1, got {pc.ConsumersPerChannel}");
            }

            if (pc.Thresholds != null)
            {
                if (pc.Thresholds.MaxLossPct is < 0 or > 100)
                    errors.Add($"patterns.{name}.thresholds.max_loss_pct must be 0-100, got {pc.Thresholds.MaxLossPct}");
            }
        }

        if (req.Patterns.Count > 0 && !anyEnabled)
            errors.Add("at least one pattern must be enabled");
    }

    /// <summary>
    /// Translate API request to internal BurninConfig (v2 format), merging with startup config.
    /// </summary>
    public static BurninConfig Translate(RunStartRequest? req, BurninConfig startup)
    {
        var cfg = new BurninConfig();

        // Copy startup-only settings
        cfg.Broker = new BrokerConfig
        {
            Address = !string.IsNullOrEmpty(req?.Broker?.Address) ? req.Broker.Address : startup.Broker.Address,
            ClientIdPrefix = startup.Broker.ClientIdPrefix,
        };
        cfg.Recovery = new RecoveryConfig
        {
            ReconnectInterval = startup.Recovery.ReconnectInterval,
            ReconnectMaxInterval = startup.Recovery.ReconnectMaxInterval,
            ReconnectMultiplier = startup.Recovery.ReconnectMultiplier,
        };
        cfg.Logging = new LoggingConfig
        {
            Format = startup.Logging.Format,
            Level = startup.Logging.Level,
        };
        cfg.Metrics = new MetricsConfig
        {
            Port = startup.Metrics.Port,
            ReportInterval = req?.Metrics?.ReportInterval ?? "30s",
        };
        cfg.Output = new OutputConfig
        {
            ReportFile = startup.Output.ReportFile,
            SdkVersion = startup.Output.SdkVersion,
        };

        if (req == null) return cfg;

        cfg.Mode = req.Mode ?? "soak";
        cfg.Duration = req.Duration ?? "1h";
        cfg.RunId = string.IsNullOrEmpty(req.RunId)
            ? Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant()
            : req.RunId;

        // Warmup
        cfg.Warmup = new WarmupConfig
        {
            MaxParallelChannels = req.Warmup?.MaxParallelChannels ?? 10,
            TimeoutPerChannelMs = req.Warmup?.TimeoutPerChannelMs ?? 5000,
            WarmupDuration = req.Warmup?.WarmupDuration ?? (cfg.Mode == "benchmark" ? "60s" : ""),
        };

        // Build v2 patterns from API request
        cfg.Patterns = BuildPatternsFromApi(req);

        // Queue
        cfg.Queue = new QueueConfig
        {
            PollMaxMessages = req.Queue?.PollMaxMessages ?? 10,
            PollWaitTimeoutSeconds = req.Queue?.PollWaitTimeoutSeconds ?? 5,
            AutoAck = req.Queue?.AutoAck ?? false,
            MaxDepth = req.Queue?.MaxDepth ?? 1_000_000,
        };

        // RPC
        cfg.Rpc = new RpcConfig
        {
            TimeoutMs = req.Rpc?.TimeoutMs ?? 5000,
        };

        // Message
        cfg.Message = new MessageConfig
        {
            SizeMode = req.Message?.SizeMode ?? "fixed",
            SizeBytes = req.Message?.SizeBytes ?? 1024,
            SizeDistribution = req.Message?.SizeDistribution ?? "256:80,4096:15,65536:5",
            ReorderWindow = req.Message?.ReorderWindow ?? 10_000,
        };

        // Global thresholds
        cfg.Thresholds = new ThresholdsConfig
        {
            MaxLossPct = req.Thresholds?.MaxLossPct ?? 0.0,
            MaxEventsLossPct = req.Thresholds?.MaxEventsLossPct ?? 5.0,
            MaxDuplicationPct = req.Thresholds?.MaxDuplicationPct ?? 0.1,
            MaxP99LatencyMs = req.Thresholds?.MaxP99LatencyMs ?? 1000,
            MaxP999LatencyMs = req.Thresholds?.MaxP999LatencyMs ?? 5000,
            MinThroughputPct = req.Thresholds?.MinThroughputPct ?? 90,
            MaxErrorRatePct = req.Thresholds?.MaxErrorRatePct ?? 1.0,
            MaxMemoryGrowthFactor = req.Thresholds?.MaxMemoryGrowthFactor ?? 2.0,
            MaxDowntimePct = req.Thresholds?.MaxDowntimePct ?? 10,
            MaxDuration = req.Thresholds?.MaxDuration ?? "168h",
        };

        // Forced disconnect
        cfg.ForcedDisconnect = new ForcedDisconnectConfig
        {
            Interval = req.ForcedDisconnect?.Interval ?? "0",
            Duration = req.ForcedDisconnect?.Duration ?? "5s",
        };

        // Shutdown
        cfg.Shutdown = new ShutdownConfig
        {
            DrainTimeoutSeconds = req.Shutdown?.DrainTimeoutSeconds ?? 10,
            CleanupChannels = req.Shutdown?.CleanupChannels ?? true,
        };

        return cfg;
    }

    /// <summary>
    /// Build patterns dictionary from API request.
    /// </summary>
    private static Dictionary<string, PatternConfig> BuildPatternsFromApi(RunStartRequest req)
    {
        var patterns = new Dictionary<string, PatternConfig>();

        foreach (string name in AllPatterns.Names)
        {
            int defaultRate = DefaultRates.GetValueOrDefault(name, 100);
            bool isRpc = name is "commands" or "queries";

            if (req.Patterns == null || !req.Patterns.TryGetValue(name, out var apiPc))
            {
                // Pattern not specified in request: create with defaults, enabled
                patterns[name] = new PatternConfig
                {
                    Enabled = true,
                    Channels = 1,
                    ProducersPerChannel = 1,
                    ConsumersPerChannel = 1,
                    SendersPerChannel = 1,
                    RespondersPerChannel = 1,
                    Rate = defaultRate,
                };
                continue;
            }

            var pc = new PatternConfig
            {
                Enabled = apiPc.Enabled ?? true,
                Channels = apiPc.Channels ?? 1,
                Rate = apiPc.Rate ?? defaultRate,
                ConsumerGroup = apiPc.ConsumerGroup ?? false,
            };

            if (isRpc)
            {
                pc.SendersPerChannel = apiPc.SendersPerChannel ?? 1;
                pc.RespondersPerChannel = apiPc.RespondersPerChannel ?? 1;
            }
            else
            {
                pc.ProducersPerChannel = apiPc.ProducersPerChannel ?? 1;
                pc.ConsumersPerChannel = apiPc.ConsumersPerChannel ?? 1;
            }

            // Per-pattern threshold overrides
            if (apiPc.Thresholds != null)
            {
                pc.Thresholds = new ThresholdsConfig();
                if (apiPc.Thresholds.MaxLossPct.HasValue)
                    pc.Thresholds.MaxLossPct = apiPc.Thresholds.MaxLossPct.Value;
                if (apiPc.Thresholds.MaxDuplicationPct.HasValue)
                    pc.Thresholds.MaxDuplicationPct = apiPc.Thresholds.MaxDuplicationPct.Value;
                if (apiPc.Thresholds.MaxP99LatencyMs.HasValue)
                    pc.Thresholds.MaxP99LatencyMs = apiPc.Thresholds.MaxP99LatencyMs.Value;
                if (apiPc.Thresholds.MaxP999LatencyMs.HasValue)
                    pc.Thresholds.MaxP999LatencyMs = apiPc.Thresholds.MaxP999LatencyMs.Value;
            }

            patterns[name] = pc;
        }

        return patterns;
    }

    /// <summary>
    /// Resolve per-pattern thresholds from the config with defaults.
    /// Merges pattern-level overrides with global thresholds.
    /// </summary>
    public static Dictionary<string, ResolvedPatternThreshold> ResolveThresholds(BurninConfig cfg)
    {
        var result = new Dictionary<string, ResolvedPatternThreshold>();

        foreach (string name in AllPatterns.Names)
        {
            var pt = new ResolvedPatternThreshold
            {
                MaxLossPct = DefaultLoss.GetValueOrDefault(name, cfg.Thresholds.MaxLossPct),
                MaxDuplicationPct = cfg.Thresholds.MaxDuplicationPct,
                MaxP99LatencyMs = cfg.Thresholds.MaxP99LatencyMs,
                MaxP999LatencyMs = cfg.Thresholds.MaxP999LatencyMs,
            };

            // Apply per-pattern threshold overrides
            if (cfg.Patterns.TryGetValue(name, out var pc) && pc.Thresholds != null)
            {
                if (pc.Thresholds.MaxLossPct != 0)
                    pt.MaxLossPct = pc.Thresholds.MaxLossPct;
                if (pc.Thresholds.MaxDuplicationPct != 0.1)
                    pt.MaxDuplicationPct = pc.Thresholds.MaxDuplicationPct;
                if (pc.Thresholds.MaxP99LatencyMs != 1000)
                    pt.MaxP99LatencyMs = pc.Thresholds.MaxP99LatencyMs;
                if (pc.Thresholds.MaxP999LatencyMs != 5000)
                    pt.MaxP999LatencyMs = pc.Thresholds.MaxP999LatencyMs;
            }

            result[name] = pt;
        }

        return result;
    }

    /// <summary>
    /// Determine which patterns are enabled.
    /// </summary>
    public static HashSet<string> ResolveEnabled(BurninConfig cfg)
    {
        var enabled = new HashSet<string>();
        foreach (var (name, pc) in cfg.Patterns)
        {
            if (pc.Enabled)
                enabled.Add(name);
        }
        return enabled;
    }

    /// <summary>
    /// Build the resolved config object for GET /run/config response.
    /// </summary>
    public static object BuildResolvedConfig(
        BurninConfig cfg,
        Dictionary<string, ResolvedPatternThreshold> thresholds,
        Dictionary<string, List<string>> channelNames)
    {
        var patterns = new Dictionary<string, object>();
        foreach (string name in AllPatterns.Names)
        {
            if (!cfg.Patterns.TryGetValue(name, out var pc) || !pc.Enabled)
            {
                patterns[name] = new { enabled = false };
                continue;
            }

            bool isRpc = name is "commands" or "queries";
            var channels = channelNames.GetValueOrDefault(name, new List<string>());
            var pt = thresholds.GetValueOrDefault(name, new ResolvedPatternThreshold());

            if (isRpc)
            {
                patterns[name] = new
                {
                    enabled = true,
                    channels = pc.Channels,
                    rate = pc.Rate,
                    senders_per_channel = pc.SendersPerChannel,
                    responders_per_channel = pc.RespondersPerChannel,
                    channel_names = channels,
                    thresholds = new { max_p99_latency_ms = pt.MaxP99LatencyMs, max_p999_latency_ms = pt.MaxP999LatencyMs },
                };
            }
            else
            {
                patterns[name] = new
                {
                    enabled = true,
                    channels = pc.Channels,
                    rate = pc.Rate,
                    producers_per_channel = pc.ProducersPerChannel,
                    consumers_per_channel = pc.ConsumersPerChannel,
                    consumer_group = pc.ConsumerGroup,
                    channel_names = channels,
                    thresholds = new { max_loss_pct = pt.MaxLossPct, max_p99_latency_ms = pt.MaxP99LatencyMs, max_p999_latency_ms = pt.MaxP999LatencyMs },
                };
            }
        }

        return new
        {
            version = "2",
            mode = cfg.Mode,
            duration = cfg.Duration,
            run_id = cfg.RunId,
            broker = new { address = cfg.Broker.Address, client_id_prefix = cfg.Broker.ClientIdPrefix },
            patterns,
            queue = new { poll_max_messages = cfg.Queue.PollMaxMessages, poll_wait_timeout_seconds = cfg.Queue.PollWaitTimeoutSeconds, auto_ack = cfg.Queue.AutoAck, max_depth = cfg.Queue.MaxDepth },
            rpc = new { timeout_ms = cfg.Rpc.TimeoutMs },
            message = new { size_mode = cfg.Message.SizeMode, size_bytes = cfg.Message.SizeBytes, size_distribution = cfg.Message.SizeDistribution, reorder_window = cfg.Message.ReorderWindow },
            thresholds = new { max_duplication_pct = cfg.Thresholds.MaxDuplicationPct, max_error_rate_pct = cfg.Thresholds.MaxErrorRatePct, max_memory_growth_factor = cfg.Thresholds.MaxMemoryGrowthFactor, max_downtime_pct = cfg.Thresholds.MaxDowntimePct, min_throughput_pct = cfg.Thresholds.MinThroughputPct, max_duration = cfg.Thresholds.MaxDuration },
            forced_disconnect = new { interval = cfg.ForcedDisconnect.Interval, duration = cfg.ForcedDisconnect.Duration },
            warmup = new { max_parallel_channels = cfg.Warmup.MaxParallelChannels, timeout_per_channel_ms = cfg.Warmup.TimeoutPerChannelMs, warmup_duration = cfg.Warmup.WarmupDuration },
            shutdown = new { drain_timeout_seconds = cfg.Shutdown.DrainTimeoutSeconds, cleanup_channels = cfg.Shutdown.CleanupChannels },
            metrics = new { report_interval = cfg.Metrics.ReportInterval },
        };
    }
}

#endregion
