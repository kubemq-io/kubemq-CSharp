// Configuration with YAML loading (YamlDotNet), environment variable overrides,
// and validation (unknown keys warn, version>1 exit 2, collect all errors, range checks).

using System.Security.Cryptography;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KubeMQ.Burnin;

#region Configuration Section Classes

public sealed class BrokerConfig
{
    public string Address { get; set; } = "localhost:50000";
    public string ClientIdPrefix { get; set; } = "burnin-csharp";
}

public sealed class RatesConfig
{
    public int Events { get; set; } = 100;
    public int EventsStore { get; set; } = 100;
    public int QueueStream { get; set; } = 50;
    public int QueueSimple { get; set; } = 50;
    public int Commands { get; set; } = 20;
    public int Queries { get; set; } = 20;
}

public sealed class ConcurrencyConfig
{
    public int EventsProducers { get; set; } = 1;
    public int EventsConsumers { get; set; } = 1;
    public bool EventsConsumerGroup { get; set; }
    public int EventsStoreProducers { get; set; } = 1;
    public int EventsStoreConsumers { get; set; } = 1;
    public bool EventsStoreConsumerGroup { get; set; }
    public int QueueStreamProducers { get; set; } = 1;
    public int QueueStreamConsumers { get; set; } = 1;
    public int QueueSimpleProducers { get; set; } = 1;
    public int QueueSimpleConsumers { get; set; } = 1;
    public int CommandsSenders { get; set; } = 1;
    public int CommandsResponders { get; set; } = 1;
    public int QueriesSenders { get; set; } = 1;
    public int QueriesResponders { get; set; } = 1;
}

public sealed class QueueConfig
{
    public int PollMaxMessages { get; set; } = 10;
    public int PollWaitTimeoutSeconds { get; set; } = 5;
    public int VisibilitySeconds { get; set; } = 30;
    public bool AutoAck { get; set; }
    public int MaxDepth { get; set; } = 1_000_000;
}

public sealed class RpcConfig
{
    public int TimeoutMs { get; set; } = 5000;
}

public sealed class MessageConfig
{
    public string SizeMode { get; set; } = "fixed";
    public int SizeBytes { get; set; } = 1024;
    public string SizeDistribution { get; set; } = "256:80,4096:15,65536:5";
    public int ReorderWindow { get; set; } = 10_000;
}

public sealed class MetricsConfig
{
    public int Port { get; set; } = 8888;
    public string ReportInterval { get; set; } = "30s";
}

public sealed class LoggingConfig
{
    public string Format { get; set; } = "text";
    public string Level { get; set; } = "info";
}

public sealed class ForcedDisconnectConfig
{
    public string Interval { get; set; } = "0";
    public string Duration { get; set; } = "5s";
}

public sealed class RecoveryConfig
{
    public string ReconnectInterval { get; set; } = "1s";
    public string ReconnectMaxInterval { get; set; } = "30s";
    public double ReconnectMultiplier { get; set; } = 2.0;
}

public sealed class ShutdownConfig
{
    public int DrainTimeoutSeconds { get; set; } = 10;
    public bool CleanupChannels { get; set; } = true;
}

public sealed class OutputConfig
{
    public string ReportFile { get; set; } = "";
    public string SdkVersion { get; set; } = "";
}

public sealed class ThresholdsConfig
{
    public double MaxLossPct { get; set; }
    public double MaxEventsLossPct { get; set; } = 5.0;
    public double MaxDuplicationPct { get; set; } = 0.1;
    public double MaxP99LatencyMs { get; set; } = 1000;
    public double MaxP999LatencyMs { get; set; } = 5000;
    public double MinThroughputPct { get; set; } = 90;
    public double MaxErrorRatePct { get; set; } = 1.0;
    public double MaxMemoryGrowthFactor { get; set; } = 2.0;
    public double MaxDowntimePct { get; set; } = 10;
    public string MaxDuration { get; set; } = "168h";
}

#endregion

/// <summary>
/// Top-level burn-in test configuration with all sections.
/// </summary>
public sealed class BurninConfig
{
    public int Version { get; set; } = 1;
    public BrokerConfig Broker { get; set; } = new();
    public string Mode { get; set; } = "soak";
    public string Duration { get; set; } = "1h";
    public string RunId { get; set; } = "";
    public string WarmupDuration { get; set; } = "";
    public RatesConfig Rates { get; set; } = new();
    public ConcurrencyConfig Concurrency { get; set; } = new();
    public QueueConfig Queue { get; set; } = new();
    public RpcConfig Rpc { get; set; } = new();
    public MessageConfig Message { get; set; } = new();
    public MetricsConfig Metrics { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
    public ForcedDisconnectConfig ForcedDisconnect { get; set; } = new();
    public RecoveryConfig Recovery { get; set; } = new();
    public ShutdownConfig Shutdown { get; set; } = new();
    public OutputConfig Output { get; set; } = new();
    public ThresholdsConfig Thresholds { get; set; } = new();
}

/// <summary>
/// Result of loading configuration: the config object plus any warnings generated.
/// </summary>
public sealed record ConfigLoadResult(BurninConfig Config, List<string> Warnings);

/// <summary>
/// Configuration loader with YAML parsing, env var overrides, and validation.
/// </summary>
public static class Config
{
    #region Duration Parsing

    /// <summary>
    /// Parse a duration string like "30s", "5m", "2h", "1d" to seconds.
    /// Returns 0 for null/empty/zero.
    /// </summary>
    public static double ParseDuration(string? s)
    {
        if (string.IsNullOrWhiteSpace(s) || s == "0") return 0;
        s = s.Trim();

        ReadOnlySpan<(string suffix, double multiplier)> units =
        [
            ("d", 86400), ("h", 3600), ("m", 60), ("s", 1)
        ];

        foreach (var (suffix, mult) in units)
        {
            if (s.EndsWith(suffix, StringComparison.Ordinal))
            {
                if (double.TryParse(s.AsSpan(0, s.Length - suffix.Length), out double n))
                    return n * mult;
                return 0;
            }
        }

        return double.TryParse(s, out double raw) ? raw : 0;
    }

    public static double DurationSec(string s) => ParseDuration(s);
    public static double ReportIntervalSec(BurninConfig cfg) => ParseDuration(cfg.Metrics.ReportInterval);
    public static double WarmupDurationSec(BurninConfig cfg) => ParseDuration(cfg.WarmupDuration);
    public static double ForcedDisconnectIntervalSec(BurninConfig cfg) => ParseDuration(cfg.ForcedDisconnect.Interval);
    public static double ForcedDisconnectDurationSec(BurninConfig cfg) => ParseDuration(cfg.ForcedDisconnect.Duration);
    public static double ReconnectIntervalMs(BurninConfig cfg) => ParseDuration(cfg.Recovery.ReconnectInterval) * 1000;
    public static double ReconnectMaxIntervalMs(BurninConfig cfg) => ParseDuration(cfg.Recovery.ReconnectMaxInterval) * 1000;
    public static double MaxDurationSec(BurninConfig cfg) => ParseDuration(cfg.Thresholds.MaxDuration);

    #endregion

    #region Environment Variable Override Table (56 explicit entries)

    private static readonly (string EnvKey, string AttrPath, string CastType)[] EnvOverrides =
    [
        ("BURNIN_BROKER_ADDRESS", "broker.address", "string"),
        ("BURNIN_CLIENT_ID_PREFIX", "broker.client_id_prefix", "string"),
        ("BURNIN_MODE", "mode", "string"),
        ("BURNIN_DURATION", "duration", "string"),
        ("BURNIN_RUN_ID", "run_id", "string"),
        ("BURNIN_WARMUP_DURATION", "warmup_duration", "string"),
        ("BURNIN_EVENTS_RATE", "rates.events", "number"),
        ("BURNIN_EVENTS_STORE_RATE", "rates.events_store", "number"),
        ("BURNIN_QUEUE_STREAM_RATE", "rates.queue_stream", "number"),
        ("BURNIN_QUEUE_SIMPLE_RATE", "rates.queue_simple", "number"),
        ("BURNIN_COMMANDS_RATE", "rates.commands", "number"),
        ("BURNIN_QUERIES_RATE", "rates.queries", "number"),
        ("BURNIN_EVENTS_PRODUCERS", "concurrency.events_producers", "number"),
        ("BURNIN_EVENTS_CONSUMERS", "concurrency.events_consumers", "number"),
        ("BURNIN_EVENTS_CONSUMER_GROUP", "concurrency.events_consumer_group", "boolean"),
        ("BURNIN_EVENTS_STORE_PRODUCERS", "concurrency.events_store_producers", "number"),
        ("BURNIN_EVENTS_STORE_CONSUMERS", "concurrency.events_store_consumers", "number"),
        ("BURNIN_EVENTS_STORE_CONSUMER_GROUP", "concurrency.events_store_consumer_group", "boolean"),
        ("BURNIN_QUEUE_STREAM_PRODUCERS", "concurrency.queue_stream_producers", "number"),
        ("BURNIN_QUEUE_STREAM_CONSUMERS", "concurrency.queue_stream_consumers", "number"),
        ("BURNIN_QUEUE_SIMPLE_PRODUCERS", "concurrency.queue_simple_producers", "number"),
        ("BURNIN_QUEUE_SIMPLE_CONSUMERS", "concurrency.queue_simple_consumers", "number"),
        ("BURNIN_COMMANDS_SENDERS", "concurrency.commands_senders", "number"),
        ("BURNIN_COMMANDS_RESPONDERS", "concurrency.commands_responders", "number"),
        ("BURNIN_QUERIES_SENDERS", "concurrency.queries_senders", "number"),
        ("BURNIN_QUERIES_RESPONDERS", "concurrency.queries_responders", "number"),
        ("BURNIN_QUEUE_POLL_MAX_MESSAGES", "queue.poll_max_messages", "number"),
        ("BURNIN_QUEUE_POLL_WAIT_TIMEOUT_SECONDS", "queue.poll_wait_timeout_seconds", "number"),
        ("BURNIN_QUEUE_VISIBILITY_SECONDS", "queue.visibility_seconds", "number"),
        ("BURNIN_QUEUE_AUTO_ACK", "queue.auto_ack", "boolean"),
        ("BURNIN_MAX_QUEUE_DEPTH", "queue.max_depth", "number"),
        ("BURNIN_RPC_TIMEOUT_MS", "rpc.timeout_ms", "number"),
        ("BURNIN_MSG_SIZE_MODE", "message.size_mode", "string"),
        ("BURNIN_MSG_SIZE_BYTES", "message.size_bytes", "number"),
        ("BURNIN_MSG_SIZE_DISTRIBUTION", "message.size_distribution", "string"),
        ("BURNIN_REORDER_WINDOW", "message.reorder_window", "number"),
        ("BURNIN_METRICS_PORT", "metrics.port", "number"),
        ("BURNIN_REPORT_INTERVAL", "metrics.report_interval", "string"),
        ("BURNIN_LOG_FORMAT", "logging.format", "string"),
        ("BURNIN_LOG_LEVEL", "logging.level", "string"),
        ("BURNIN_FORCED_DISCONNECT_INTERVAL", "forced_disconnect.interval", "string"),
        ("BURNIN_FORCED_DISCONNECT_DURATION", "forced_disconnect.duration", "string"),
        ("BURNIN_RECONNECT_INTERVAL", "recovery.reconnect_interval", "string"),
        ("BURNIN_RECONNECT_MAX_INTERVAL", "recovery.reconnect_max_interval", "string"),
        ("BURNIN_RECONNECT_MULTIPLIER", "recovery.reconnect_multiplier", "number"),
        ("BURNIN_SHUTDOWN_DRAIN_SECONDS", "shutdown.drain_timeout_seconds", "number"),
        ("BURNIN_CLEANUP_CHANNELS", "shutdown.cleanup_channels", "boolean"),
        ("BURNIN_REPORT_OUTPUT_FILE", "output.report_file", "string"),
        ("BURNIN_SDK_VERSION", "output.sdk_version", "string"),
        ("BURNIN_MAX_LOSS_PCT", "thresholds.max_loss_pct", "number"),
        ("BURNIN_MAX_EVENTS_LOSS_PCT", "thresholds.max_events_loss_pct", "number"),
        ("BURNIN_MAX_DUPLICATION_PCT", "thresholds.max_duplication_pct", "number"),
        ("BURNIN_MAX_P99_LATENCY_MS", "thresholds.max_p99_latency_ms", "number"),
        ("BURNIN_MAX_P999_LATENCY_MS", "thresholds.max_p999_latency_ms", "number"),
        ("BURNIN_MIN_THROUGHPUT_PCT", "thresholds.min_throughput_pct", "number"),
        ("BURNIN_MAX_ERROR_RATE_PCT", "thresholds.max_error_rate_pct", "number"),
        ("BURNIN_MAX_MEMORY_GROWTH_FACTOR", "thresholds.max_memory_growth_factor", "number"),
        ("BURNIN_MAX_DOWNTIME_PCT", "thresholds.max_downtime_pct", "number"),
        ("BURNIN_MAX_DURATION", "thresholds.max_duration", "string"),
    ];

    #endregion

    #region Config Discovery

    /// <summary>
    /// Discover config file path. Priority: BURNIN_CONFIG_FILE env > --config CLI > auto-discover.
    /// </summary>
    public static string FindConfigFile(string cliPath)
    {
        string? envPath = Environment.GetEnvironmentVariable("BURNIN_CONFIG_FILE");
        if (!string.IsNullOrEmpty(envPath)) return envPath;
        if (!string.IsNullOrEmpty(cliPath)) return cliPath;

        string[] candidates = ["./burnin-config.yaml", "/etc/burnin/config.yaml"];
        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate)) return candidate;
        }
        return "";
    }

    #endregion

    #region Load and Validate

    /// <summary>
    /// Load configuration from YAML file with env var overrides.
    /// </summary>
    public static ConfigLoadResult LoadConfig(string cliPath)
    {
        string configPath = FindConfigFile(cliPath);
        var cfg = new BurninConfig();
        var warnings = new List<string>();

        if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
        {
            string yamlText = File.ReadAllText(configPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            // First pass: detect unknown keys by deserializing to dictionary.
            try
            {
                var rawDict = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .Build()
                    .Deserialize<Dictionary<string, object?>>(yamlText);

                if (rawDict != null)
                {
                    var knownTopKeys = new HashSet<string>(StringComparer.Ordinal)
                    {
                        "version", "broker", "mode", "duration", "run_id", "warmup_duration",
                        "rates", "concurrency", "queue", "rpc", "message", "metrics",
                        "logging", "forced_disconnect", "recovery", "shutdown", "output", "thresholds"
                    };
                    foreach (string key in rawDict.Keys)
                    {
                        if (!knownTopKeys.Contains(key))
                        {
                            warnings.Add($"unknown config key '{key}' -- ignored");
                        }
                    }
                }
            }
            catch
            {
                // Ignore parse errors in the raw pass; the typed pass will catch them.
            }

            // Second pass: typed deserialization.
            try
            {
                var parsed = deserializer.Deserialize<BurninConfig>(yamlText);
                if (parsed != null)
                {
                    cfg = parsed;
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"YAML parse error: {ex.Message}");
            }
        }

        // Apply environment variable overrides.
        ApplyEnvOverrides(cfg);

        // Auto-generate run_id if empty.
        if (string.IsNullOrEmpty(cfg.RunId))
        {
            cfg.RunId = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        }

        // Mode-dependent warmup default.
        if (cfg.Mode == "benchmark" && string.IsNullOrEmpty(cfg.WarmupDuration))
        {
            cfg.WarmupDuration = "60s";
        }

        return new ConfigLoadResult(cfg, warnings);
    }

    /// <summary>
    /// Validate config and return a list of errors/warnings.
    /// Version > 1 should cause exit code 2.
    /// </summary>
    public static List<string> ValidateConfig(BurninConfig cfg)
    {
        var errors = new List<string>();

        if (cfg.Version > 1)
            errors.Add($"unsupported config version {cfg.Version} (max: 1)");

        if (string.IsNullOrWhiteSpace(cfg.Broker.Address))
            errors.Add("broker.address is required");

        if (cfg.Mode is not ("soak" or "benchmark"))
            errors.Add($"mode must be 'soak' or 'benchmark', got '{cfg.Mode}'");

        if (DurationSec(cfg.Duration) <= 0)
            errors.Add("duration must be > 0");

        if (cfg.Message.SizeMode is not ("fixed" or "distribution"))
            errors.Add($"message.size_mode must be 'fixed' or 'distribution'");

        if (cfg.Message.SizeMode == "fixed" && cfg.Message.SizeBytes <= 0)
            errors.Add("message.size_bytes must be > 0");

        if (cfg.Metrics.Port is <= 0 or > 65535)
            errors.Add("metrics.port must be 1-65535");

        if (cfg.Rpc.TimeoutMs <= 0)
            errors.Add("rpc.timeout_ms must be > 0");

        if (cfg.Queue.PollWaitTimeoutSeconds <= 0)
            errors.Add("queue.poll_wait_timeout_seconds must be > 0");

        // Rate range checks.
        if (cfg.Rates.Events < 0) errors.Add("rates.events must be >= 0");
        if (cfg.Rates.EventsStore < 0) errors.Add("rates.events_store must be >= 0");
        if (cfg.Rates.QueueStream < 0) errors.Add("rates.queue_stream must be >= 0");
        if (cfg.Rates.QueueSimple < 0) errors.Add("rates.queue_simple must be >= 0");
        if (cfg.Rates.Commands < 0) errors.Add("rates.commands must be >= 0");
        if (cfg.Rates.Queries < 0) errors.Add("rates.queries must be >= 0");

        // Concurrency range checks.
        if (cfg.Concurrency.EventsProducers < 1) errors.Add("concurrency.events_producers must be >= 1");
        if (cfg.Concurrency.EventsConsumers < 1) errors.Add("concurrency.events_consumers must be >= 1");
        if (cfg.Concurrency.EventsStoreProducers < 1) errors.Add("concurrency.events_store_producers must be >= 1");
        if (cfg.Concurrency.EventsStoreConsumers < 1) errors.Add("concurrency.events_store_consumers must be >= 1");
        if (cfg.Concurrency.QueueStreamProducers < 1) errors.Add("concurrency.queue_stream_producers must be >= 1");
        if (cfg.Concurrency.QueueStreamConsumers < 1) errors.Add("concurrency.queue_stream_consumers must be >= 1");
        if (cfg.Concurrency.QueueSimpleProducers < 1) errors.Add("concurrency.queue_simple_producers must be >= 1");
        if (cfg.Concurrency.QueueSimpleConsumers < 1) errors.Add("concurrency.queue_simple_consumers must be >= 1");
        if (cfg.Concurrency.CommandsSenders < 1) errors.Add("concurrency.commands_senders must be >= 1");
        if (cfg.Concurrency.CommandsResponders < 1) errors.Add("concurrency.commands_responders must be >= 1");
        if (cfg.Concurrency.QueriesSenders < 1) errors.Add("concurrency.queries_senders must be >= 1");
        if (cfg.Concurrency.QueriesResponders < 1) errors.Add("concurrency.queries_responders must be >= 1");

        // Contradictory value warnings.
        if (cfg.Queue.AutoAck && cfg.Queue.VisibilitySeconds > 0)
            errors.Add("WARNING: auto_ack=true with visibility_seconds > 0 has no effect");

        int totalRate = cfg.Rates.Events + cfg.Rates.EventsStore + cfg.Rates.QueueStream
                        + cfg.Rates.QueueSimple + cfg.Rates.Commands + cfg.Rates.Queries;
        if (totalRate > 50000)
            errors.Add($"WARNING: total rate {totalRate} msg/s is very high");

        return errors;
    }

    /// <summary>
    /// Returns true if the config has a version > 1 error, indicating exit code 2.
    /// </summary>
    public static bool HasVersionError(BurninConfig cfg) => cfg.Version > 1;

    #endregion

    #region Private Helpers

    private static void ApplyEnvOverrides(BurninConfig cfg)
    {
        foreach (var (envKey, attrPath, castType) in EnvOverrides)
        {
            string? val = Environment.GetEnvironmentVariable(envKey);
            if (val is null) continue;

            SetNested(cfg, attrPath, val, castType);
        }
    }

    private static void SetNested(BurninConfig cfg, string path, string rawValue, string castType)
    {
        object? parsedValue = castType switch
        {
            "boolean" => rawValue.ToLowerInvariant() is "true" or "1" or "yes",
            "number" when double.TryParse(rawValue, out double d) => d,
            "number" => 0.0,
            _ => rawValue,
        };

        string[] parts = path.Split('.');
        object target = cfg;

        // Navigate to the parent object.
        for (int i = 0; i < parts.Length - 1; i++)
        {
            string propName = SnakeToPascal(parts[i]);
            var prop = target.GetType().GetProperty(propName);
            if (prop is null) return;
            target = prop.GetValue(target)!;
        }

        // Set the leaf property.
        string leafName = SnakeToPascal(parts[^1]);
        var leafProp = target.GetType().GetProperty(leafName);
        if (leafProp is null) return;

        try
        {
            object? converted = Convert.ChangeType(parsedValue, leafProp.PropertyType);
            leafProp.SetValue(target, converted);
        }
        catch
        {
            // If conversion fails for int properties (from double), try explicit cast.
            if (leafProp.PropertyType == typeof(int) && parsedValue is double dv)
            {
                leafProp.SetValue(target, (int)dv);
            }
        }
    }

    /// <summary>
    /// Convert snake_case to PascalCase (e.g., "client_id_prefix" -> "ClientIdPrefix").
    /// </summary>
    private static string SnakeToPascal(string snake)
    {
        var sb = new System.Text.StringBuilder(snake.Length);
        bool capitalize = true;
        foreach (char c in snake)
        {
            if (c == '_')
            {
                capitalize = true;
                continue;
            }
            sb.Append(capitalize ? char.ToUpperInvariant(c) : c);
            capitalize = false;
        }
        return sb.ToString();
    }

    #endregion
}
