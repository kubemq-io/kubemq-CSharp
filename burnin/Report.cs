// Report generation: 10 verdict checks + memory_trend advisory, verdict, console + JSON output.
// v2: per-channel verdict checks (fail-on-any), multi-channel fields in PatternSummary.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KubeMQ.Burnin;

/// <summary>
/// Verdict constants.
/// </summary>
public static class VerdictResult
{
    public const string Passed = "PASSED";
    public const string PassedWithWarnings = "PASSED_WITH_WARNINGS";
    public const string Failed = "FAILED";
}

/// <summary>
/// Result of a single verdict check.
/// </summary>
public sealed class CheckResult
{
    [JsonPropertyName("passed")]
    public bool Passed { get; set; }

    [JsonPropertyName("threshold")]
    public string Threshold { get; set; } = "";

    [JsonPropertyName("actual")]
    public string Actual { get; set; } = "";

    [JsonPropertyName("advisory")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Advisory { get; set; }
}

/// <summary>
/// Full verdict with result string, warnings, and all individual checks.
/// </summary>
public sealed class Verdict
{
    [JsonPropertyName("result")]
    public string Result { get; set; } = VerdictResult.Passed;

    [JsonPropertyName("passed")]
    public bool Passed { get; set; } = true;

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    [JsonPropertyName("checks")]
    public Dictionary<string, CheckResult> Checks { get; set; } = new();
}

/// <summary>
/// Nested latency object for JSON serialization in reports and live responses.
/// </summary>
public sealed class LatencyData
{
    [JsonPropertyName("p50_ms")]
    public double P50Ms { get; set; }
    [JsonPropertyName("p95_ms")]
    public double P95Ms { get; set; }
    [JsonPropertyName("p99_ms")]
    public double P99Ms { get; set; }
    [JsonPropertyName("p999_ms")]
    public double P999Ms { get; set; }
}

public sealed class ProducerWorkerData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    [JsonPropertyName("sent")]
    public long Sent { get; set; }
    [JsonPropertyName("errors")]
    public long Errors { get; set; }
    [JsonPropertyName("avg_rate")]
    public double AvgRate { get; set; }
    [JsonPropertyName("latency")]
    public LatencyData Latency { get; set; } = new();
}

public sealed class ConsumerWorkerData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    [JsonPropertyName("received")]
    public long Received { get; set; }
    [JsonPropertyName("lost")]
    public long Lost { get; set; }
    [JsonPropertyName("duplicated")]
    public long Duplicated { get; set; }
    [JsonPropertyName("corrupted")]
    public long Corrupted { get; set; }
    [JsonPropertyName("errors")]
    public long Errors { get; set; }
    [JsonPropertyName("latency")]
    public LatencyData Latency { get; set; } = new();
}

public sealed class SenderWorkerData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    [JsonPropertyName("sent")]
    public long Sent { get; set; }
    [JsonPropertyName("responses_success")]
    public long ResponsesSuccess { get; set; }
    [JsonPropertyName("responses_timeout")]
    public long ResponsesTimeout { get; set; }
    [JsonPropertyName("responses_error")]
    public long ResponsesError { get; set; }
    [JsonPropertyName("avg_rate")]
    public double AvgRate { get; set; }
    [JsonPropertyName("latency")]
    public LatencyData Latency { get; set; } = new();
}

public sealed class ResponderWorkerData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    [JsonPropertyName("responded")]
    public long Responded { get; set; }
    [JsonPropertyName("errors")]
    public long Errors { get; set; }
}

/// <summary>
/// Pattern-level summary data used by the report generator.
/// v2: includes multi-channel fields.
/// </summary>
public sealed class PatternSummary
{
    public bool Enabled { get; set; } = true;
    public string Status { get; set; } = "unknown";
    public long Sent { get; set; }
    public long Received { get; set; }
    public long Lost { get; set; }
    public long Duplicated { get; set; }
    public long Corrupted { get; set; }
    public long OutOfOrder { get; set; }
    public double LossPct { get; set; }
    public long Errors { get; set; }
    public long Reconnections { get; set; }
    public double DowntimeSeconds { get; set; }

    [JsonIgnore]
    public double LatencyP50Ms { get; set; }
    [JsonIgnore]
    public double LatencyP95Ms { get; set; }
    [JsonIgnore]
    public double LatencyP99Ms { get; set; }
    [JsonIgnore]
    public double LatencyP999Ms { get; set; }

    public LatencyData? Latency { get; set; }

    public double AvgRate { get; set; }
    public double PeakRate { get; set; }
    public double TargetRate { get; set; }

    [JsonIgnore]
    public double AvgThroughputMsgsSec { get; set; }
    [JsonIgnore]
    public double PeakThroughputMsgsSec { get; set; }

    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long Unconfirmed { get; set; }

    // RPC-specific fields (commands/queries)
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long ResponsesSuccess { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long ResponsesTimeout { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long ResponsesError { get; set; }
    [JsonIgnore]
    public double RpcP50Ms { get; set; }
    [JsonIgnore]
    public double RpcP95Ms { get; set; }
    [JsonIgnore]
    public double RpcP99Ms { get; set; }
    [JsonIgnore]
    public double RpcP999Ms { get; set; }
    [JsonIgnore]
    public double AvgThroughputRpcSec { get; set; }

    // Per-worker arrays for report
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ProducerWorkerData>? Producers { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ConsumerWorkerData>? Consumers { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SenderWorkerData>? Senders { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ResponderWorkerData>? Responders { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ConsumerGroup { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int NumConsumers { get; set; }

    // v2 multi-channel fields
    [JsonPropertyName("channels")]
    public int ChannelCount { get; set; } = 1;

    [JsonPropertyName("producers_per_channel")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ProducersPerChannel { get; set; }

    [JsonPropertyName("consumers_per_channel")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ConsumersPerChannel { get; set; }

    [JsonPropertyName("senders_per_channel")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int SendersPerChannel { get; set; }

    [JsonPropertyName("responders_per_channel")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int RespondersPerChannel { get; set; }
}

/// <summary>
/// Per-channel data for use in per-channel verdict checks (fail-on-any).
/// </summary>
public sealed class ChannelVerdictData
{
    public int ChannelIndex { get; set; }
    public long Sent { get; set; }
    public long Received { get; set; }
    public long Lost { get; set; }
    public long Duplicated { get; set; }
    public long Corrupted { get; set; }
    public long Errors { get; set; }
    public int ConsumersPerChannel { get; set; }
    public bool ConsumerGroup { get; set; }
}

/// <summary>
/// Per-pattern channel data for verdict checks.
/// </summary>
public sealed class PatternChannelData
{
    public string Pattern { get; set; } = "";
    public List<ChannelVerdictData> Channels { get; set; } = new();
}

/// <summary>
/// Resource metrics for the summary.
/// </summary>
public sealed class ResourceSummary
{
    public double PeakRssMb { get; set; }
    public double BaselineRssMb { get; set; }
    public double MemoryGrowthFactor { get; set; } = 1.0;
    public int PeakWorkers { get; set; }
}

/// <summary>
/// Full burn-in summary containing all data needed for the report.
/// </summary>
public sealed class BurninSummary
{
    public string Sdk { get; set; } = "csharp";
    public string Version { get; set; } = "";
    public string Mode { get; set; } = "";
    public string BrokerAddress { get; set; } = "";
    public string StartedAt { get; set; } = "";
    public string EndedAt { get; set; } = "";
    public double DurationSeconds { get; set; }
    public string Status { get; set; } = "running";
    public bool AllPatternsEnabled { get; set; } = true;
    public Dictionary<string, PatternSummary> Patterns { get; set; } = new();
    public ResourceSummary Resources { get; set; } = new();
    public Verdict? Verdict { get; set; }
}

/// <summary>
/// Report generator: per-pattern verdict checks with map keys per spec.
/// v2: per-channel checks (fail-on-any), throughput formula = aggregate / (rate * channels).
/// </summary>
public static class Report
{
    /// <summary>
    /// Generate verdict from summary data with per-pattern checks.
    /// v2: uses pattern-level latency accumulator, fail-on-any-channel for loss/broadcast/duplication.
    /// </summary>
    public static Verdict GenerateVerdict(
        BurninSummary summary,
        ThresholdsConfig thresholds,
        string mode,
        Dictionary<string, ResolvedPatternThreshold>? perPatternThresholds = null,
        HashSet<string>? enabledPatterns = null,
        bool memoryBaselineAdvisory = false,
        Dictionary<string, PatternChannelData>? perChannelData = null)
    {
        var checks = new Dictionary<string, CheckResult>();
        var warnings = new List<string>();
        bool anyNonAdvisoryFailed = false;
        bool anyAdvisoryFailed = false;
        var patterns = summary.Patterns;
        double durationSecs = summary.DurationSeconds;

        enabledPatterns ??= new HashSet<string>(patterns.Keys);
        perPatternThresholds ??= new Dictionary<string, ResolvedPatternThreshold>();
        perChannelData ??= new Dictionary<string, PatternChannelData>();

        // Ensure all enabled patterns have threshold entries
        foreach (string name in enabledPatterns)
        {
            if (!perPatternThresholds.ContainsKey(name))
            {
                perPatternThresholds[name] = new ResolvedPatternThreshold
                {
                    MaxLossPct = name == "events" ? thresholds.MaxEventsLossPct : thresholds.MaxLossPct,
                };
            }
        }

        void Check(string key, bool passed, string threshold, string actual, bool advisory = false)
        {
            checks[key] = new CheckResult { Passed = passed, Threshold = threshold, Actual = actual, Advisory = advisory };
            if (!passed)
            {
                if (advisory) anyAdvisoryFailed = true;
                else anyNonAdvisoryFailed = true;
            }
        }

        string[] pubSubQueue = ["events", "events_store", "queue_stream", "queue_simple"];

        // 1. message_loss per enabled pub/sub+queue pattern
        // v2: per-channel check (fail-on-any-channel), report worst-case channel
        foreach (string name in pubSubQueue)
        {
            if (!enabledPatterns.Contains(name) || !patterns.TryGetValue(name, out var ps)) continue;
            double lossThreshold = perPatternThresholds.TryGetValue(name, out var pt) ? pt.MaxLossPct
                : (name == "events" ? thresholds.MaxEventsLossPct : thresholds.MaxLossPct);

            if (perChannelData.TryGetValue(name, out var pcd) && pcd.Channels.Count > 1)
            {
                // Per-channel check: fail if ANY channel exceeds threshold
                bool allPassed = true;
                int worstIdx = 0;
                double worstPct = 0;
                long worstLost = 0, worstSent = 0;

                foreach (var ch in pcd.Channels)
                {
                    double chLossPct = ch.Sent > 0 ? (double)ch.Lost / ch.Sent * 100.0 : 0;
                    if (chLossPct > worstPct || (worstSent == 0 && ch.Sent > 0))
                    {
                        worstPct = chLossPct;
                        worstIdx = ch.ChannelIndex;
                        worstLost = ch.Lost;
                        worstSent = ch.Sent;
                    }
                    if (chLossPct > lossThreshold)
                        allPassed = false;
                }

                string actual = allPassed
                    ? $"{ps.LossPct:F5}%"
                    : $"ch_{worstIdx:D4}: {worstPct:F1}% ({worstLost}/{worstSent})";
                Check($"message_loss:{name}", allPassed, $"{lossThreshold:G}%", actual);
            }
            else
            {
                // Single channel or no per-channel data: aggregate check
                Check($"message_loss:{name}", ps.LossPct <= lossThreshold,
                    $"{lossThreshold:G}%", $"{ps.LossPct:F5}%");
            }
        }

        // 2. duplication / broadcast per enabled pub/sub+queue pattern
        foreach (string name in pubSubQueue)
        {
            if (!enabledPatterns.Contains(name) || !patterns.TryGetValue(name, out var ps)) continue;
            bool isEventPattern = name == "events" || name == "events_store";

            if (isEventPattern && !ps.ConsumerGroup && ps.NumConsumers > 1)
            {
                // Broadcast mode: per-channel arithmetic check
                if (perChannelData.TryGetValue(name, out var pcd) && pcd.Channels.Count > 1)
                {
                    bool allPassed = true;
                    int worstIdx = 0;
                    long worstReceived = 0, worstExpected = 0;

                    foreach (var ch in pcd.Channels)
                    {
                        long expected = ch.Sent * ch.ConsumersPerChannel;
                        if (ch.Received != expected)
                        {
                            allPassed = false;
                            if (worstExpected == 0 || Math.Abs(ch.Received - expected) > Math.Abs(worstReceived - worstExpected))
                            {
                                worstIdx = ch.ChannelIndex;
                                worstReceived = ch.Received;
                                worstExpected = expected;
                            }
                        }
                    }

                    string actual = allPassed
                        ? ps.Received.ToString()
                        : $"ch_{worstIdx:D4}: {worstReceived}/{worstExpected}";
                    Check($"broadcast:{name}", allPassed,
                        $"{ps.Sent}\u00d7{ps.NumConsumers}", actual);
                }
                else
                {
                    // Single channel: aggregate arithmetic check
                    long expectedTotal = ps.Sent * ps.NumConsumers;
                    bool broadcastOk = ps.Received == expectedTotal;
                    Check($"broadcast:{name}", broadcastOk,
                        $"{ps.Sent}\u00d7{ps.NumConsumers}", ps.Received.ToString());
                }
            }
            else if (isEventPattern && ps.ConsumerGroup)
            {
                // Consumer group mode: per-channel strict 0% duplication
                if (perChannelData.TryGetValue(name, out var pcd) && pcd.Channels.Count > 1)
                {
                    bool allPassed = true;
                    int worstIdx = 0;
                    double worstDupPct = 0;
                    long worstDuplicated = 0, worstSent = 0;

                    foreach (var ch in pcd.Channels)
                    {
                        double chDupPct = ch.Sent > 0 ? (double)ch.Duplicated / ch.Sent * 100.0 : 0;
                        if (chDupPct > 0)
                        {
                            allPassed = false;
                            if (chDupPct > worstDupPct)
                            {
                                worstDupPct = chDupPct;
                                worstIdx = ch.ChannelIndex;
                                worstDuplicated = ch.Duplicated;
                                worstSent = ch.Sent;
                            }
                        }
                    }

                    string actual = allPassed
                        ? "0.0000%"
                        : $"ch_{worstIdx:D4}: {worstDupPct:F4}% ({worstDuplicated}/{worstSent})";
                    Check($"duplication:{name}", allPassed, "0.0%", actual);
                }
                else
                {
                    double dupPct = ps.Sent > 0 ? (double)ps.Duplicated / ps.Sent * 100.0 : 0;
                    Check($"duplication:{name}", dupPct == 0,
                        "0.0%", $"{dupPct:F4}%");
                }
            }
            else
            {
                // Queue patterns or single-consumer events: per-channel threshold check
                double dupThreshold = perPatternThresholds.TryGetValue(name, out var dpt)
                    ? dpt.MaxDuplicationPct : thresholds.MaxDuplicationPct;

                if (perChannelData.TryGetValue(name, out var pcd) && pcd.Channels.Count > 1)
                {
                    bool allPassed = true;
                    int worstIdx = 0;
                    double worstDupPct = 0;
                    long worstDuplicated = 0, worstSent = 0;

                    foreach (var ch in pcd.Channels)
                    {
                        double chDupPct = ch.Sent > 0 ? (double)ch.Duplicated / ch.Sent * 100.0 : 0;
                        if (chDupPct > dupThreshold)
                        {
                            allPassed = false;
                        }
                        if (chDupPct > worstDupPct)
                        {
                            worstDupPct = chDupPct;
                            worstIdx = ch.ChannelIndex;
                            worstDuplicated = ch.Duplicated;
                            worstSent = ch.Sent;
                        }
                    }

                    string actual = allPassed
                        ? $"{(ps.Sent > 0 ? (double)ps.Duplicated / ps.Sent * 100.0 : 0):F4}%"
                        : $"ch_{worstIdx:D4}: {worstDupPct:F4}% ({worstDuplicated}/{worstSent})";
                    Check($"duplication:{name}", allPassed, $"{dupThreshold:G}%", actual);
                }
                else
                {
                    double dupPct = ps.Sent > 0 ? (double)ps.Duplicated / ps.Sent * 100.0 : 0;
                    Check($"duplication:{name}", dupPct <= dupThreshold,
                        $"{dupThreshold:G}%", $"{dupPct:F4}%");
                }
            }
        }

        // 3. corruption (per-channel: fail if ANY channel has corruption; global check as well)
        {
            bool anyCorruption = false;
            int worstCorruptIdx = 0;
            long worstCorruptCount = 0;
            long totalCorrupted = 0;

            foreach (var ps in patterns.Values)
                totalCorrupted += ps.Corrupted;

            // Check per-channel corruption
            foreach (var (pName, pcd) in perChannelData)
            {
                foreach (var ch in pcd.Channels)
                {
                    if (ch.Corrupted > 0)
                    {
                        anyCorruption = true;
                        if (ch.Corrupted > worstCorruptCount)
                        {
                            worstCorruptCount = ch.Corrupted;
                            worstCorruptIdx = ch.ChannelIndex;
                        }
                    }
                }
            }

            bool passed = totalCorrupted == 0 && !anyCorruption;
            string actual = passed ? "0"
                : anyCorruption ? $"ch_{worstCorruptIdx:D4}: {worstCorruptCount}"
                : totalCorrupted.ToString();
            Check("corruption", passed, "0", actual);
        }

        // 4. p99_latency per enabled pattern (uses pattern-level accumulator)
        foreach (string name in AllPatterns.Names)
        {
            if (!enabledPatterns.Contains(name) || !patterns.TryGetValue(name, out var ps)) continue;
            double p99 = name is "commands" or "queries" ? ps.RpcP99Ms : ps.LatencyP99Ms;
            double p99Thresh = perPatternThresholds.TryGetValue(name, out var pt99) ? pt99.MaxP99LatencyMs : thresholds.MaxP99LatencyMs;
            Check($"p99_latency:{name}", p99 <= p99Thresh,
                $"{p99Thresh:G}ms", $"{p99:F1}ms");
        }

        // 5. p999_latency per enabled pattern
        foreach (string name in AllPatterns.Names)
        {
            if (!enabledPatterns.Contains(name) || !patterns.TryGetValue(name, out var ps)) continue;
            double p999 = name is "commands" or "queries" ? ps.RpcP999Ms : ps.LatencyP999Ms;
            double p999Thresh = perPatternThresholds.TryGetValue(name, out var pt999) ? pt999.MaxP999LatencyMs : thresholds.MaxP999LatencyMs;
            Check($"p999_latency:{name}", p999 <= p999Thresh,
                $"{p999Thresh:G}ms", $"{p999:F1}ms");
        }

        // 6. throughput (global, soak only)
        // v2: throughput_pct = aggregate_avg_throughput / (rate * channels) * 100
        if (mode == "soak" && durationSecs > 0)
        {
            double minTp = 100;
            foreach (string name in AllPatterns.Names)
            {
                if (!enabledPatterns.Contains(name) || !patterns.TryGetValue(name, out var ps)) continue;
                if (ps.TargetRate > 0)
                    minTp = Math.Min(minTp, ps.AvgThroughputMsgsSec / ps.TargetRate * 100.0);
            }
            Check("throughput", minTp >= thresholds.MinThroughputPct,
                $"{thresholds.MinThroughputPct:G}%", $"{minTp:F1}%");
        }

        // 7. error_rate per enabled pattern
        foreach (string name in AllPatterns.Names)
        {
            if (!enabledPatterns.Contains(name) || !patterns.TryGetValue(name, out var ps)) continue;
            long denominator = name is "commands" or "queries"
                ? ps.Sent + ps.ResponsesSuccess
                : ps.Sent + ps.Received;
            double errPct = denominator > 0 ? (double)ps.Errors / denominator * 100.0 : 0;
            Check($"error_rate:{name}", errPct <= thresholds.MaxErrorRatePct,
                $"{thresholds.MaxErrorRatePct:G}%", $"{errPct:F4}%");
        }

        // 8. memory_stability (global; advisory for runs shorter than 5 minutes)
        double growth = summary.Resources.MemoryGrowthFactor;
        Check("memory_stability", growth <= thresholds.MaxMemoryGrowthFactor,
            $"{thresholds.MaxMemoryGrowthFactor:G}x", $"{growth:F2}x", advisory: memoryBaselineAdvisory);
        if (memoryBaselineAdvisory && growth > thresholds.MaxMemoryGrowthFactor)
            warnings.Add("Memory stability check is advisory (run shorter than 5 minutes, baseline unreliable)");

        // 9. memory_trend advisory: fires at 1.0 + (max_factor - 1.0) * 0.5
        double trendThreshold = 1.0 + (thresholds.MaxMemoryGrowthFactor - 1.0) * 0.5;
        bool trendPassed = growth <= trendThreshold;
        Check("memory_trend", trendPassed,
            $"{trendThreshold:F1}x", $"{growth:F2}x", advisory: true);

        // 10. downtime (global, max across all patterns -- not sum)
        double maxDowntime = 0;
        if (durationSecs > 0)
        {
            foreach (var ps in patterns.Values)
                maxDowntime = Math.Max(maxDowntime, ps.DowntimeSeconds / durationSecs * 100.0);
        }
        Check("downtime", maxDowntime <= thresholds.MaxDowntimePct,
            $"{thresholds.MaxDowntimePct:G}%", $"{maxDowntime:F4}%");

        // all_patterns_enabled warning
        bool allEnabled = enabledPatterns.Count == AllPatterns.Names.Length;
        if (!allEnabled)
            warnings.Add("Not all patterns enabled -- not valid for production certification");

        if (anyAdvisoryFailed && !anyNonAdvisoryFailed && !trendPassed)
            warnings.Add($"Memory growth trend: {growth:F1}x (advisory threshold: {trendThreshold:F1}x)");

        string result = anyNonAdvisoryFailed ? VerdictResult.Failed
            : anyAdvisoryFailed ? VerdictResult.PassedWithWarnings
            : VerdictResult.Passed;

        return new Verdict { Result = result, Passed = !anyNonAdvisoryFailed, Warnings = warnings, Checks = checks };
    }

    /// <summary>
    /// Generate a startup-failure verdict.
    /// </summary>
    public static Verdict GenerateStartupFailedVerdict(string errorMessage)
    {
        return new Verdict
        {
            Result = VerdictResult.Failed,
            Passed = false,
            Checks = new Dictionary<string, CheckResult>
            {
                ["startup"] = new CheckResult { Passed = false, Threshold = "success", Actual = errorMessage }
            },
        };
    }

    #region Console Report

    private static string GetCheckLabel(string key)
    {
        if (key.StartsWith("message_loss:")) return $"Loss ({key[13..]}):";
        if (key.StartsWith("duplication:")) return $"Duplication ({key[12..]}):";
        if (key.StartsWith("broadcast:")) return $"Broadcast ({key[10..]}):";
        if (key.StartsWith("p99_latency:")) return $"P99 ({key[12..]}):";
        if (key.StartsWith("p999_latency:")) return $"P999 ({key[13..]}):";
        if (key.StartsWith("error_rate:")) return $"Error rate ({key[11..]}):";
        return key switch
        {
            "corruption" => "Corruption:",
            "throughput" => "Throughput:",
            "memory_stability" => "Memory stability:",
            "memory_trend" => "Memory trend:",
            "downtime" => "Downtime:",
            "startup" => "Startup:",
            _ => key + ":",
        };
    }

    /// <summary>
    /// Print the full console report with TOTALS row, RESOURCES, and P999 column.
    /// v2: shows channel count per pattern.
    /// </summary>
    public static void PrintConsoleReport(BurninSummary summary)
    {
        var v = summary.Verdict!;
        double dur = summary.DurationSeconds;
        int h = (int)(dur / 3600);
        int m = (int)((dur % 3600) / 60);
        int s = (int)(dur % 60);
        string durStr = h > 0 ? $"{h}h {m}m {s}s" : $"{m}m {s}s";

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine(new string('=', 74));
        sb.AppendLine($"  KUBEMQ BURN-IN TEST REPORT -- C# SDK v{summary.Version} (v2 Multi-Channel)");
        sb.AppendLine(new string('=', 74));
        sb.AppendLine($"  Mode:     {summary.Mode}");
        sb.AppendLine($"  Broker:   {summary.BrokerAddress}");
        sb.AppendLine($"  Duration: {durStr}");
        sb.AppendLine($"  Started:  {FormatTimestamp(summary.StartedAt)}");
        sb.AppendLine($"  Ended:    {FormatTimestamp(summary.EndedAt)}");
        sb.AppendLine(new string('-', 74));

        // Header with channel count and P999 column.
        string hdr = "  " + "PATTERN".PadRight(20)
            + "SENT".PadLeft(10) + "RECV".PadLeft(10)
            + "LOST".PadLeft(6) + "DUP".PadLeft(6) + "ERR".PadLeft(6)
            + "P99(ms)".PadLeft(9) + "P999(ms)".PadLeft(10);
        sb.AppendLine(hdr);

        long tSent = 0, tRecv = 0, tLost = 0, tDup = 0, tErr = 0;
        foreach (var (name, ps) in summary.Patterns)
        {
            tSent += ps.Sent;
            tRecv += ps.Received;
            tLost += ps.Lost;
            tDup += ps.Duplicated;
            tErr += ps.Errors;

            double p99 = name is "commands" or "queries" ? ps.RpcP99Ms : ps.LatencyP99Ms;
            double p999 = name is "commands" or "queries" ? ps.RpcP999Ms : ps.LatencyP999Ms;

            // v2: show channel count
            string patternLabel = ps.ChannelCount > 1
                ? $"{name} ({ps.ChannelCount}ch)"
                : name;

            sb.AppendLine("  " + patternLabel.PadRight(20)
                + ps.Sent.ToString().PadLeft(10)
                + ps.Received.ToString().PadLeft(10)
                + ps.Lost.ToString().PadLeft(6)
                + ps.Duplicated.ToString().PadLeft(6)
                + ps.Errors.ToString().PadLeft(6)
                + p99.ToString("F1").PadLeft(9)
                + p999.ToString("F1").PadLeft(10));
        }

        sb.AppendLine(new string('-', 74));
        sb.AppendLine("  " + "TOTALS".PadRight(20)
            + tSent.ToString().PadLeft(10)
            + tRecv.ToString().PadLeft(10)
            + tLost.ToString().PadLeft(6)
            + tDup.ToString().PadLeft(6)
            + tErr.ToString().PadLeft(6));

        var res = summary.Resources;
        sb.AppendLine($"  RESOURCES         RSS: {res.BaselineRssMb:F0}MB -> {res.PeakRssMb:F0}MB ({res.MemoryGrowthFactor:F2}x)  Workers: {res.PeakWorkers}");

        sb.AppendLine(new string('-', 74));
        sb.AppendLine($"  VERDICT: {v.Result}");

        if (v.Warnings.Count > 0)
        {
            foreach (string w in v.Warnings)
                sb.AppendLine($"    WARNING: {w}");
        }

        foreach (var (name, c) in v.Checks)
        {
            string mk = c.Passed ? "+" : "!";
            string adv = c.Advisory ? " (advisory)" : "";
            string label = GetCheckLabel(name);
            sb.AppendLine($"    {mk} {label.PadRight(30)}{c.Actual.PadRight(12)}(threshold: {c.Threshold}){adv}");
        }

        sb.AppendLine(new string('=', 74));
        Console.WriteLine(sb.ToString());
    }

    #endregion

    #region JSON Report

    private static readonly JsonSerializerOptions s_jsonWriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Write the summary as a formatted JSON file.
    /// </summary>
    public static void WriteJsonReport(BurninSummary summary, string path)
    {
        string json = JsonSerializer.Serialize(summary, s_jsonWriteOptions);
        File.WriteAllText(path, json);
        Console.WriteLine($"report written to {path}");
    }

    #endregion

    #region Timestamp Formatting

    /// <summary>
    /// Format an ISO 8601 timestamp as "YYYY-MM-DD HH:MM:SS UTC".
    /// </summary>
    public static string FormatTimestamp(string iso)
    {
        if (string.IsNullOrEmpty(iso)) return "N/A";
        string formatted = iso
            .Replace("T", " ")
            .Replace("Z", "");
        int dotIdx = formatted.LastIndexOf('.');
        if (dotIdx >= 0)
        {
            formatted = formatted[..dotIdx];
        }
        return formatted + " UTC";
    }

    #endregion
}
