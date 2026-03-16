// Report generation: 10 verdict checks + memory_trend advisory, verdict, console + JSON output.

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
/// Full verdict with result string, passed flag, and all individual checks.
/// </summary>
public sealed class Verdict
{
    [JsonPropertyName("result")]
    public string Result { get; set; } = VerdictResult.Passed;

    [JsonPropertyName("passed")]
    public bool Passed { get; set; } = true;

    [JsonPropertyName("checks")]
    public Dictionary<string, CheckResult> Checks { get; set; } = new();
}

/// <summary>
/// Pattern-level summary data used by the report generator.
/// </summary>
public sealed class PatternSummary
{
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
    public double LatencyP50Ms { get; set; }
    public double LatencyP95Ms { get; set; }
    public double LatencyP99Ms { get; set; }
    public double LatencyP999Ms { get; set; }
    public double AvgThroughputMsgsSec { get; set; }
    public double PeakThroughputMsgsSec { get; set; }
    public double TargetRate { get; set; }

    // RPC-specific fields (commands/queries).
    public long ResponsesSuccess { get; set; }
    public long ResponsesTimeout { get; set; }
    public long ResponsesError { get; set; }
    public double RpcP50Ms { get; set; }
    public double RpcP95Ms { get; set; }
    public double RpcP99Ms { get; set; }
    public double RpcP999Ms { get; set; }
    public double AvgThroughputRpcSec { get; set; }
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
    public Dictionary<string, PatternSummary> Patterns { get; set; } = new();
    public ResourceSummary Resources { get; set; } = new();
    public Verdict? Verdict { get; set; }
}

/// <summary>
/// Report generator: 10 verdict checks + memory_trend advisory.
/// </summary>
public static class Report
{
    /// <summary>
    /// Generate verdict from summary data and configured thresholds.
    /// </summary>
    public static Verdict GenerateVerdict(BurninSummary summary, ThresholdsConfig thresholds, string mode)
    {
        var checks = new Dictionary<string, CheckResult>();
        bool allPassed = true;
        bool hasWarnings = false;

        void Check(string name, bool passed, string threshold, string actual)
        {
            checks[name] = new CheckResult { Passed = passed, Threshold = threshold, Actual = actual };
            if (!passed) allPassed = false;
        }

        var patterns = summary.Patterns;
        double durationSecs = summary.DurationSeconds;

        // 1. message_loss_persistent (max across events_store, queue_stream, queue_simple).
        double persistentLoss = 0;
        foreach (string p in new[] { "events_store", "queue_stream", "queue_simple" })
        {
            if (patterns.TryGetValue(p, out var ps))
                persistentLoss = Math.Max(persistentLoss, ps.LossPct);
        }
        Check("message_loss_persistent",
            persistentLoss <= thresholds.MaxLossPct,
            $"<= {thresholds.MaxLossPct}%",
            $"{persistentLoss:F4}%");

        // 2. message_loss_events.
        double eventsLoss = patterns.TryGetValue("events", out var evPs) ? evPs.LossPct : 0;
        Check("message_loss_events",
            eventsLoss <= thresholds.MaxEventsLossPct,
            $"<= {thresholds.MaxEventsLossPct}%",
            $"{eventsLoss:F4}%");

        // 3. duplication (max across all patterns).
        double maxDup = 0;
        foreach (var ps in patterns.Values)
        {
            if (ps.Sent > 0)
                maxDup = Math.Max(maxDup, (double)ps.Duplicated / ps.Sent * 100.0);
        }
        Check("duplication",
            maxDup <= thresholds.MaxDuplicationPct,
            $"<= {thresholds.MaxDuplicationPct}%",
            $"{maxDup:F4}%");

        // 4. corruption (absolute zero, not configurable).
        long totalCorrupted = 0;
        foreach (var ps in patterns.Values) totalCorrupted += ps.Corrupted;
        Check("corruption",
            totalCorrupted == 0,
            "== 0",
            totalCorrupted.ToString());

        // 5. p99_latency (max across all patterns).
        double maxP99 = 0;
        foreach (var ps in patterns.Values)
            maxP99 = Math.Max(maxP99, ps.LatencyP99Ms);
        Check("p99_latency",
            maxP99 <= thresholds.MaxP99LatencyMs,
            $"<= {thresholds.MaxP99LatencyMs}ms",
            $"{maxP99:F2}ms");

        // 6. p999_latency (max across all patterns).
        double maxP999 = 0;
        foreach (var ps in patterns.Values)
            maxP999 = Math.Max(maxP999, ps.LatencyP999Ms);
        Check("p999_latency",
            maxP999 <= thresholds.MaxP999LatencyMs,
            $"<= {thresholds.MaxP999LatencyMs}ms",
            $"{maxP999:F2}ms");

        // 7. throughput (soak only; skipped in benchmark mode).
        if (mode == "soak" && durationSecs > 0)
        {
            double minTp = 100;
            foreach (var ps in patterns.Values)
            {
                if (ps.TargetRate > 0)
                    minTp = Math.Min(minTp, ps.AvgThroughputMsgsSec / ps.TargetRate * 100.0);
            }
            Check("throughput",
                minTp >= thresholds.MinThroughputPct,
                $">= {thresholds.MinThroughputPct}%",
                $"{minTp:F2}%");
        }
        else
        {
            checks["throughput"] = new CheckResult
            {
                Passed = true,
                Threshold = "N/A (benchmark)",
                Actual = "N/A",
            };
        }

        // 8. error_rate = errors / (sent + received) * 100.
        double maxErr = 0;
        foreach (var ps in patterns.Values)
        {
            long ops = ps.Sent + ps.Received;
            if (ops > 0)
                maxErr = Math.Max(maxErr, (double)ps.Errors / ops * 100.0);
        }
        Check("error_rate",
            maxErr <= thresholds.MaxErrorRatePct,
            $"<= {thresholds.MaxErrorRatePct}%",
            $"{maxErr:F4}%");

        // 9. memory_stability.
        double growth = summary.Resources.MemoryGrowthFactor;
        Check("memory_stability",
            growth <= thresholds.MaxMemoryGrowthFactor,
            $"<= {thresholds.MaxMemoryGrowthFactor}x",
            $"{growth:F2}x");

        // 10. downtime (max across all patterns).
        double maxDowntime = 0;
        if (durationSecs > 0)
        {
            foreach (var ps in patterns.Values)
                maxDowntime = Math.Max(maxDowntime, ps.DowntimeSeconds / durationSecs * 100.0);
        }
        Check("downtime",
            maxDowntime <= thresholds.MaxDowntimePct,
            $"<= {thresholds.MaxDowntimePct}%",
            $"{maxDowntime:F4}%");

        // Advisory: memory_trend.
        if (growth > thresholds.MaxMemoryGrowthFactor * 0.5)
        {
            hasWarnings = true;
            checks["memory_trend"] = new CheckResult
            {
                Passed = true,
                Threshold = $"< {thresholds.MaxMemoryGrowthFactor * 0.5:F1}x (advisory)",
                Actual = $"{growth:F2}x",
                Advisory = true,
            };
        }

        string result = allPassed
            ? (hasWarnings ? VerdictResult.PassedWithWarnings : VerdictResult.Passed)
            : VerdictResult.Failed;

        return new Verdict { Result = result, Passed = allPassed, Checks = checks };
    }

    #region Console Report

    private static readonly Dictionary<string, string> CheckLabels = new()
    {
        ["message_loss_persistent"] = "Message loss (persistent):",
        ["message_loss_events"] = "Message loss (events):",
        ["duplication"] = "Duplication:",
        ["corruption"] = "Corruption:",
        ["p99_latency"] = "P99 latency:",
        ["p999_latency"] = "P999 latency:",
        ["throughput"] = "Throughput:",
        ["error_rate"] = "Error rate:",
        ["memory_stability"] = "Memory stability:",
        ["downtime"] = "Downtime:",
        ["memory_trend"] = "Memory trend:",
    };

    /// <summary>
    /// Print the full console report with TOTALS row, RESOURCES, and P999 column.
    /// Timestamps formatted as "YYYY-MM-DD HH:MM:SS UTC".
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
        sb.AppendLine(new string('=', 67));
        sb.AppendLine($"  KUBEMQ BURN-IN TEST REPORT -- C# SDK v{summary.Version}");
        sb.AppendLine(new string('=', 67));
        sb.AppendLine($"  Mode:     {summary.Mode}");
        sb.AppendLine($"  Broker:   {summary.BrokerAddress}");
        sb.AppendLine($"  Duration: {durStr}");
        sb.AppendLine($"  Started:  {FormatTimestamp(summary.StartedAt)}");
        sb.AppendLine($"  Ended:    {FormatTimestamp(summary.EndedAt)}");
        sb.AppendLine(new string('-', 67));

        // Header with P999 column.
        string hdr = "  " + "PATTERN".PadRight(16)
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

            sb.AppendLine("  " + name.PadRight(16)
                + ps.Sent.ToString().PadLeft(10)
                + ps.Received.ToString().PadLeft(10)
                + ps.Lost.ToString().PadLeft(6)
                + ps.Duplicated.ToString().PadLeft(6)
                + ps.Errors.ToString().PadLeft(6)
                + p99.ToString("F1").PadLeft(9)
                + p999.ToString("F1").PadLeft(10));
        }

        sb.AppendLine(new string('-', 67));
        sb.AppendLine("  " + "TOTALS".PadRight(16)
            + tSent.ToString().PadLeft(10)
            + tRecv.ToString().PadLeft(10)
            + tLost.ToString().PadLeft(6)
            + tDup.ToString().PadLeft(6)
            + tErr.ToString().PadLeft(6));

        var res = summary.Resources;
        sb.AppendLine($"  RESOURCES       RSS: {res.BaselineRssMb:F0}MB -> {res.PeakRssMb:F0}MB ({res.MemoryGrowthFactor:F2}x)  Workers: {res.PeakWorkers}");

        sb.AppendLine(new string('-', 67));
        sb.AppendLine($"  VERDICT: {v.Result}");

        foreach (var (name, c) in v.Checks)
        {
            string mk = c.Passed ? "+" : "!";
            string adv = c.Advisory ? " (advisory)" : "";
            string label = CheckLabels.GetValueOrDefault(name, name + ":");
            sb.AppendLine($"    {mk} {label.PadRight(30)}{c.Actual.PadRight(12)}(threshold: {c.Threshold}){adv}");
        }

        sb.AppendLine(new string('=', 67));
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
        // Replace 'T' with space and strip fractional seconds + 'Z', append " UTC".
        string formatted = iso
            .Replace("T", " ")
            .Replace("Z", "");
        // Remove fractional seconds if present.
        int dotIdx = formatted.LastIndexOf('.');
        if (dotIdx >= 0)
        {
            formatted = formatted[..dotIdx];
        }
        return formatted + " UTC";
    }

    #endregion
}
