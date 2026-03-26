// Configuration with YAML loading (YamlDotNet), v2 patterns-based config,
// and validation (unknown keys warn, collect all errors, range checks).
// v1 config (concurrency/rates/env vars) has been removed per multi-channel spec v2.1.

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

/// <summary>
/// Per-pattern configuration for v2 multi-channel burn-in.
/// </summary>
public sealed class PatternConfig
{
    public bool Enabled { get; set; } = true;
    public int Channels { get; set; } = 1;
    public int ProducersPerChannel { get; set; } = 1;
    public int ConsumersPerChannel { get; set; } = 1;
    public bool ConsumerGroup { get; set; }
    public int SendersPerChannel { get; set; } = 1;
    public int RespondersPerChannel { get; set; } = 1;
    public int Rate { get; set; } = 100;
    public ThresholdsConfig? Thresholds { get; set; }
}

/// <summary>
/// Warmup configuration for multi-channel startup.
/// </summary>
public sealed class WarmupConfig
{
    public int MaxParallelChannels { get; set; } = 10;
    public int TimeoutPerChannelMs { get; set; } = 5000;
    public string WarmupDuration { get; set; } = "";
}

public sealed class QueueConfig
{
    public int PollMaxMessages { get; set; } = 10;
    public int PollWaitTimeoutSeconds { get; set; } = 5;
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

public sealed class CorsConfig
{
    public string Origins { get; set; } = "*";
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
/// v2: uses Patterns dictionary instead of Rates/Concurrency/EnabledPatterns.
/// </summary>
public sealed class BurninConfig
{
    public string Version { get; set; } = "2";
    public BrokerConfig Broker { get; set; } = new();
    public string Mode { get; set; } = "soak";
    public string Duration { get; set; } = "1h";
    public string RunId { get; set; } = "";
    public Dictionary<string, PatternConfig> Patterns { get; set; } = new();
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
    public CorsConfig Cors { get; set; } = new();
    public WarmupConfig Warmup { get; set; } = new();
    public int StartingTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Get PatternConfig for a given pattern name, with correct defaults applied.
    /// Returns null if the pattern is not in the dictionary (treated as disabled).
    /// </summary>
    public PatternConfig? GetPatternConfig(string pattern)
    {
        if (Patterns.TryGetValue(pattern, out var pc))
            return pc;
        return null;
    }

    /// <summary>
    /// Get rate for a pattern, applying the correct default rate.
    /// </summary>
    public int GetRate(string pattern)
    {
        if (Patterns.TryGetValue(pattern, out var pc))
            return pc.Rate;
        return pattern == "queue_stream" ? 50 : 100;
    }

    /// <summary>
    /// Get the set of enabled pattern names.
    /// </summary>
    public HashSet<string> GetEnabledPatterns()
    {
        var enabled = new HashSet<string>();
        foreach (var (name, pc) in Patterns)
        {
            if (pc.Enabled)
                enabled.Add(name);
        }
        return enabled;
    }
}

/// <summary>
/// Result of loading configuration: the config object plus any warnings generated.
/// </summary>
public sealed record ConfigLoadResult(BurninConfig Config, List<string> Warnings);

/// <summary>
/// Configuration loader with YAML parsing and validation.
/// v2: no env var overrides -- config via YAML or API only.
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
    public static double WarmupDurationSec(BurninConfig cfg) => ParseDuration(cfg.Warmup.WarmupDuration);
    public static double ForcedDisconnectIntervalSec(BurninConfig cfg) => ParseDuration(cfg.ForcedDisconnect.Interval);
    public static double ForcedDisconnectDurationSec(BurninConfig cfg) => ParseDuration(cfg.ForcedDisconnect.Duration);
    public static double ReconnectIntervalMs(BurninConfig cfg) => ParseDuration(cfg.Recovery.ReconnectInterval) * 1000;
    public static double ReconnectMaxIntervalMs(BurninConfig cfg) => ParseDuration(cfg.Recovery.ReconnectMaxInterval) * 1000;
    public static double MaxDurationSec(BurninConfig cfg) => ParseDuration(cfg.Thresholds.MaxDuration);

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
    /// Load configuration from YAML file.
    /// v2: no env var overrides.
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
                        "version", "broker", "mode", "duration", "run_id",
                        "patterns", "queue", "rpc", "message", "metrics",
                        "logging", "forced_disconnect", "recovery", "shutdown", "output", "thresholds",
                        "cors", "warmup", "starting_timeout_seconds"
                    };
                    foreach (string key in rawDict.Keys)
                    {
                        if (!knownTopKeys.Contains(key))
                        {
                            warnings.Add($"unknown config key '{key}' -- ignored");
                        }
                    }

                    // v1 detection: if concurrency or rates keys are present, warn
                    if (rawDict.ContainsKey("concurrency") || rawDict.ContainsKey("rates") || rawDict.ContainsKey("enabled_patterns"))
                    {
                        warnings.Add("v1 config format detected (concurrency/rates/enabled_patterns). Update to v2 patterns format.");
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

        // Environment variable override for broker address
        string? envAddr = Environment.GetEnvironmentVariable("KUBEMQ_BROKER_ADDRESS");
        if (!string.IsNullOrEmpty(envAddr))
        {
            cfg.Broker.Address = envAddr;
        }

        // Auto-generate run_id if empty.
        if (string.IsNullOrEmpty(cfg.RunId))
        {
            cfg.RunId = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        }

        // Mode-dependent warmup default.
        if (cfg.Mode == "benchmark" && string.IsNullOrEmpty(cfg.Warmup.WarmupDuration))
        {
            cfg.Warmup.WarmupDuration = "60s";
        }

        // Apply default patterns if none specified
        if (cfg.Patterns.Count == 0)
        {
            cfg.Patterns = BuildDefaultPatterns();
        }

        // Apply correct default rate for queue_stream
        if (cfg.Patterns.TryGetValue("queue_stream", out var qsPc) && qsPc.Rate == 100)
        {
            // If rate was not explicitly set and is still default 100, set to 50
            // (we can't distinguish "explicitly set to 100" from "default 100" in YAML,
            //  so queue_stream default is 50 only when creating defaults)
        }

        return new ConfigLoadResult(cfg, warnings);
    }

    /// <summary>
    /// Build default patterns dictionary with all 6 patterns enabled at defaults.
    /// </summary>
    public static Dictionary<string, PatternConfig> BuildDefaultPatterns()
    {
        return new Dictionary<string, PatternConfig>
        {
            ["events"] = new PatternConfig { Rate = 100 },
            ["events_store"] = new PatternConfig { Rate = 100 },
            ["queue_stream"] = new PatternConfig { Rate = 50 },
            ["queue_simple"] = new PatternConfig { Rate = 100 },
            ["commands"] = new PatternConfig { Rate = 100 },
            ["queries"] = new PatternConfig { Rate = 100 },
        };
    }

    /// <summary>
    /// Validate config and return a list of errors/warnings.
    /// </summary>
    public static (List<string> Errors, List<string> Warnings) ValidateConfig(BurninConfig cfg)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(cfg.Broker.Address))
            errors.Add("broker.address is required");

        if (cfg.Mode is not ("soak" or "benchmark"))
            errors.Add($"mode must be 'soak' or 'benchmark', got '{cfg.Mode}'");

        if (cfg.Mode == "soak" && DurationSec(cfg.Duration) <= 0)
            errors.Add("duration must be > 0 for soak mode");

        if (cfg.Message.SizeMode is not ("fixed" or "distribution"))
            errors.Add($"message.size_mode must be 'fixed' or 'distribution'");

        if (cfg.Message.SizeBytes < 64)
            errors.Add($"message.size_bytes: must be >= 64, got {cfg.Message.SizeBytes}");

        if (cfg.Rpc.TimeoutMs <= 0)
            errors.Add("rpc.timeout_ms must be > 0");

        if (cfg.Queue.PollWaitTimeoutSeconds <= 0)
            errors.Add("queue.poll_wait_timeout_seconds must be > 0");

        if (cfg.Shutdown.DrainTimeoutSeconds <= 0)
            errors.Add("shutdown.drain_timeout_seconds: must be > 0, got " + cfg.Shutdown.DrainTimeoutSeconds);

        // Port validation
        if (cfg.Metrics.Port < 1 || cfg.Metrics.Port > 65535)
            errors.Add($"api.port: must be 1-65535, got {cfg.Metrics.Port}");

        // Duration string parsability
        if (cfg.Mode == "soak" && !string.IsNullOrEmpty(cfg.Duration) && cfg.Duration != "0")
        {
            double parsed = DurationSec(cfg.Duration);
            if (parsed <= 0)
                errors.Add($"invalid duration format: {cfg.Duration}");
        }

        // Validate patterns
        ValidatePatterns(cfg, errors, warnings);

        // Validate thresholds
        ValidateThresholds(cfg.Thresholds, errors);

        // Resource guard warnings
        ComputeResourceWarnings(cfg, warnings);

        return (errors, warnings);
    }

    private static void ValidatePatterns(BurninConfig cfg, List<string> errors, List<string> warnings)
    {
        bool anyEnabled = false;

        foreach (var (name, pc) in cfg.Patterns)
        {
            if (!pc.Enabled) continue;
            anyEnabled = true;

            bool isRpc = name is "commands" or "queries";

            // Channels validation
            if (pc.Channels < 1 || pc.Channels > 1000)
                errors.Add($"{name}.channels: must be 1-1000, got {pc.Channels}");

            // Rate validation
            if (pc.Rate < 0)
                errors.Add($"{name}.rate: must be >= 0, got {pc.Rate}");

            if (isRpc)
            {
                if (pc.SendersPerChannel < 1)
                    errors.Add($"{name}.senders_per_channel: must be >= 1, got {pc.SendersPerChannel}");
                if (pc.RespondersPerChannel < 1)
                    errors.Add($"{name}.responders_per_channel: must be >= 1, got {pc.RespondersPerChannel}");
            }
            else
            {
                if (pc.ProducersPerChannel < 1)
                    errors.Add($"{name}.producers_per_channel: must be >= 1, got {pc.ProducersPerChannel}");
                if (pc.ConsumersPerChannel < 1)
                    errors.Add($"{name}.consumers_per_channel: must be >= 1, got {pc.ConsumersPerChannel}");
                if (pc.ProducersPerChannel > 100)
                    warnings.Add($"{name}.producers_per_channel: {pc.ProducersPerChannel} exceeds recommended max 100");
                if (pc.ConsumersPerChannel > 100)
                    warnings.Add($"{name}.consumers_per_channel: {pc.ConsumersPerChannel} exceeds recommended max 100");
            }

            // Per-pattern threshold validation
            if (pc.Thresholds != null)
                ValidateThresholds(pc.Thresholds, errors);
        }

        if (cfg.Patterns.Count > 0 && !anyEnabled)
            errors.Add("at least one pattern must be enabled");
    }

    private static void ValidateThresholds(ThresholdsConfig t, List<string> errors)
    {
        if (t.MaxLossPct < 0 || t.MaxLossPct > 100)
            errors.Add($"thresholds.max_loss_pct: must be 0-100, got {t.MaxLossPct}");
        if (t.MaxDuplicationPct < 0 || t.MaxDuplicationPct > 100)
            errors.Add($"thresholds.max_duplication_pct: must be 0-100, got {t.MaxDuplicationPct}");
        if (t.MaxP99LatencyMs <= 0)
            errors.Add($"thresholds.max_p99_latency_ms: must be > 0, got {t.MaxP99LatencyMs}");
        if (t.MaxP999LatencyMs <= 0)
            errors.Add($"thresholds.max_p999_latency_ms: must be > 0, got {t.MaxP999LatencyMs}");
        if (t.MinThroughputPct <= 0 || t.MinThroughputPct > 100)
            errors.Add($"thresholds.min_throughput_pct: must be 0-100, got {t.MinThroughputPct}");
        if (t.MaxMemoryGrowthFactor < 1.0)
            errors.Add($"thresholds.max_memory_growth_factor: must be >= 1.0, got {t.MaxMemoryGrowthFactor}");
        if (t.MaxErrorRatePct < 0 || t.MaxErrorRatePct > 100)
            errors.Add($"thresholds.max_error_rate_pct: must be 0-100, got {t.MaxErrorRatePct}");
        if (t.MaxDowntimePct < 0 || t.MaxDowntimePct > 100)
            errors.Add($"thresholds.max_downtime_pct: must be 0-100, got {t.MaxDowntimePct}");
    }

    /// <summary>
    /// Compute resource guard warnings per spec.
    /// </summary>
    private static void ComputeResourceWarnings(BurninConfig cfg, List<string> warnings)
    {
        int totalWorkers = 0;
        long pubsubRate = 0, queuesRate = 0, cqRate = 0;

        foreach (var (name, pc) in cfg.Patterns)
        {
            if (!pc.Enabled) continue;
            bool isRpc = name is "commands" or "queries";

            if (isRpc)
            {
                totalWorkers += pc.Channels * (pc.SendersPerChannel + pc.RespondersPerChannel);
                cqRate += (long)pc.Channels * pc.Rate;
            }
            else
            {
                totalWorkers += pc.Channels * (pc.ProducersPerChannel + pc.ConsumersPerChannel);
                if (name is "events" or "events_store")
                    pubsubRate += (long)pc.Channels * pc.Rate;
                else
                    queuesRate += (long)pc.Channels * pc.Rate;
            }
        }

        if (totalWorkers > 500)
            warnings.Add($"high worker count: {totalWorkers} -- may impact system resources");

        double estMemoryMb = totalWorkers * (cfg.Message.ReorderWindow * 8.0 / 1024.0 / 1024.0 + 0.5);
        if (estMemoryMb > 4096)
            warnings.Add($"memory warning: estimated {estMemoryMb / 1024.0:F1}GB overhead for {totalWorkers} workers -- ensure sufficient system memory");

        if (pubsubRate > 50000)
            warnings.Add($"high aggregate rate {pubsubRate} msgs/s through single gRPC connection -- may cause transport bottleneck");
        if (queuesRate > 50000)
            warnings.Add($"high aggregate rate {queuesRate} msgs/s through single gRPC connection -- may cause transport bottleneck");
        if (cqRate > 50000)
            warnings.Add($"high aggregate rate {cqRate} msgs/s through single gRPC connection -- may cause transport bottleneck");
    }

    #endregion
}
