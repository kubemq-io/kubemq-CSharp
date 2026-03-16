// All 26 Prometheus metrics + helper functions.
// Metric names and labels match spec Section 7.1 exactly.

using Prometheus;

namespace KubeMQ.Burnin;

/// <summary>
/// Central metrics registry with all 26 Prometheus metrics and helper methods.
/// SDK label = "csharp" for the C# implementation.
/// </summary>
public static class Metrics
{
    private const string SDK = "csharp";

    // --- Counters (15) ---

    private static readonly Counter MessagesSent = Prometheus.Metrics.CreateCounter(
        "burnin_messages_sent_total", "Total messages sent",
        new CounterConfiguration { LabelNames = ["sdk", "pattern", "producer_id"] });

    private static readonly Counter MessagesReceived = Prometheus.Metrics.CreateCounter(
        "burnin_messages_received_total", "Total messages received",
        new CounterConfiguration { LabelNames = ["sdk", "pattern", "consumer_id"] });

    private static readonly Counter MessagesLost = Prometheus.Metrics.CreateCounter(
        "burnin_messages_lost_total", "Confirmed lost messages",
        new CounterConfiguration { LabelNames = ["sdk", "pattern"] });

    private static readonly Counter MessagesDuplicated = Prometheus.Metrics.CreateCounter(
        "burnin_messages_duplicated_total", "Duplicate messages",
        new CounterConfiguration { LabelNames = ["sdk", "pattern"] });

    private static readonly Counter MessagesCorrupted = Prometheus.Metrics.CreateCounter(
        "burnin_messages_corrupted_total", "Corrupted messages",
        new CounterConfiguration { LabelNames = ["sdk", "pattern"] });

    private static readonly Counter MessagesOutOfOrder = Prometheus.Metrics.CreateCounter(
        "burnin_messages_out_of_order_total", "Out-of-order messages",
        new CounterConfiguration { LabelNames = ["sdk", "pattern"] });

    private static readonly Counter MessagesUnconfirmed = Prometheus.Metrics.CreateCounter(
        "burnin_messages_unconfirmed_total", "Unconfirmed messages",
        new CounterConfiguration { LabelNames = ["sdk", "pattern"] });

    private static readonly Counter ReconnDuplicates = Prometheus.Metrics.CreateCounter(
        "burnin_reconnection_duplicates_total", "Post-reconnection duplicates",
        new CounterConfiguration { LabelNames = ["sdk", "pattern"] });

    private static readonly Counter Errors = Prometheus.Metrics.CreateCounter(
        "burnin_errors_total", "Errors by type",
        new CounterConfiguration { LabelNames = ["sdk", "pattern", "error_type"] });

    private static readonly Counter Reconnections = Prometheus.Metrics.CreateCounter(
        "burnin_reconnections_total", "Reconnections",
        new CounterConfiguration { LabelNames = ["sdk", "pattern"] });

    private static readonly Counter BytesSent = Prometheus.Metrics.CreateCounter(
        "burnin_bytes_sent_total", "Bytes sent",
        new CounterConfiguration { LabelNames = ["sdk", "pattern"] });

    private static readonly Counter BytesReceived = Prometheus.Metrics.CreateCounter(
        "burnin_bytes_received_total", "Bytes received",
        new CounterConfiguration { LabelNames = ["sdk", "pattern"] });

    private static readonly Counter RpcResponses = Prometheus.Metrics.CreateCounter(
        "burnin_rpc_responses_total", "RPC responses by status",
        new CounterConfiguration { LabelNames = ["sdk", "pattern", "status"] });

    private static readonly Counter DowntimeSeconds = Prometheus.Metrics.CreateCounter(
        "burnin_downtime_seconds_total", "Downtime seconds",
        new CounterConfiguration { LabelNames = ["sdk", "pattern"] });

    private static readonly Counter ForcedDisconnects = Prometheus.Metrics.CreateCounter(
        "burnin_forced_disconnects_total", "Forced disconnects",
        new CounterConfiguration { LabelNames = ["sdk"] });

    // --- Histograms (3) with exact bucket values from spec ---

    private static readonly double[] LatencyBuckets =
        [0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5];

    private static readonly double[] RpcBuckets =
        [0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5];

    private static readonly Histogram MessageLatency = Prometheus.Metrics.CreateHistogram(
        "burnin_message_latency_seconds", "E2E message latency",
        new HistogramConfiguration { LabelNames = ["sdk", "pattern"], Buckets = LatencyBuckets });

    private static readonly Histogram SendDuration = Prometheus.Metrics.CreateHistogram(
        "burnin_send_duration_seconds", "Send duration",
        new HistogramConfiguration { LabelNames = ["sdk", "pattern"], Buckets = LatencyBuckets });

    private static readonly Histogram RpcDuration = Prometheus.Metrics.CreateHistogram(
        "burnin_rpc_duration_seconds", "RPC round-trip",
        new HistogramConfiguration { LabelNames = ["sdk", "pattern"], Buckets = RpcBuckets });

    // --- Gauges (8) ---

    private static readonly Gauge ActiveConnections = Prometheus.Metrics.CreateGauge(
        "burnin_active_connections", "Active connections",
        new GaugeConfiguration { LabelNames = ["sdk", "pattern"] });

    private static readonly Gauge UptimeGauge = Prometheus.Metrics.CreateGauge(
        "burnin_uptime_seconds", "Uptime seconds",
        new GaugeConfiguration { LabelNames = ["sdk"] });

    private static readonly Gauge TargetRateGauge = Prometheus.Metrics.CreateGauge(
        "burnin_target_rate", "Target rate",
        new GaugeConfiguration { LabelNames = ["sdk", "pattern"] });

    private static readonly Gauge ActualRateGauge = Prometheus.Metrics.CreateGauge(
        "burnin_actual_rate", "Actual rate",
        new GaugeConfiguration { LabelNames = ["sdk", "pattern"] });

    private static readonly Gauge ConsumerLag = Prometheus.Metrics.CreateGauge(
        "burnin_consumer_lag_messages", "Consumer lag",
        new GaugeConfiguration { LabelNames = ["sdk", "pattern"] });

    private static readonly Gauge GroupBalance = Prometheus.Metrics.CreateGauge(
        "burnin_consumer_group_balance_ratio", "Group balance ratio",
        new GaugeConfiguration { LabelNames = ["sdk", "pattern"] });

    private static readonly Gauge WarmupGauge = Prometheus.Metrics.CreateGauge(
        "burnin_warmup_active", "Warmup active",
        new GaugeConfiguration { LabelNames = ["sdk"] });

    private static readonly Gauge ActiveWorkersGauge = Prometheus.Metrics.CreateGauge(
        "burnin_active_workers", "Active workers",
        new GaugeConfiguration { LabelNames = ["sdk"] });

    // --- Helper functions ---

    public static void IncSent(string pattern, string producerId, int bytes = 0)
    {
        MessagesSent.WithLabels(SDK, pattern, producerId).Inc();
        if (bytes > 0) BytesSent.WithLabels(SDK, pattern).Inc(bytes);
    }

    public static void IncReceived(string pattern, string consumerId, int bytes = 0)
    {
        MessagesReceived.WithLabels(SDK, pattern, consumerId).Inc();
        if (bytes > 0) BytesReceived.WithLabels(SDK, pattern).Inc(bytes);
    }

    public static void IncLost(string pattern, long count = 1)
    {
        MessagesLost.WithLabels(SDK, pattern).Inc(count);
    }

    public static void IncDuplicated(string pattern)
    {
        MessagesDuplicated.WithLabels(SDK, pattern).Inc();
    }

    public static void IncCorrupted(string pattern)
    {
        MessagesCorrupted.WithLabels(SDK, pattern).Inc();
    }

    public static void IncOutOfOrder(string pattern)
    {
        MessagesOutOfOrder.WithLabels(SDK, pattern).Inc();
    }

    public static void IncUnconfirmed(string pattern)
    {
        MessagesUnconfirmed.WithLabels(SDK, pattern).Inc();
    }

    public static void IncReconnDuplicates(string pattern)
    {
        ReconnDuplicates.WithLabels(SDK, pattern).Inc();
    }

    public static void IncError(string pattern, string errorType)
    {
        Errors.WithLabels(SDK, pattern, errorType).Inc();
    }

    public static void IncReconnections(string pattern)
    {
        Reconnections.WithLabels(SDK, pattern).Inc();
    }

    public static void IncRpcResponse(string pattern, string status)
    {
        RpcResponses.WithLabels(SDK, pattern, status).Inc();
    }

    public static void AddDowntime(string pattern, double seconds)
    {
        if (seconds > 0) DowntimeSeconds.WithLabels(SDK, pattern).Inc(seconds);
    }

    public static void IncForcedDisconnects()
    {
        ForcedDisconnects.WithLabels(SDK).Inc();
    }

    public static void ObserveLatency(string pattern, double seconds)
    {
        MessageLatency.WithLabels(SDK, pattern).Observe(seconds);
    }

    public static void ObserveSendDuration(string pattern, double seconds)
    {
        SendDuration.WithLabels(SDK, pattern).Observe(seconds);
    }

    public static void ObserveRpcDuration(string pattern, double seconds)
    {
        RpcDuration.WithLabels(SDK, pattern).Observe(seconds);
    }

    public static void SetActiveConnections(string pattern, double count)
    {
        ActiveConnections.WithLabels(SDK, pattern).Set(count);
    }

    public static void SetUptime(double seconds)
    {
        UptimeGauge.WithLabels(SDK).Set(seconds);
    }

    public static void SetTargetRate(string pattern, double rate)
    {
        TargetRateGauge.WithLabels(SDK, pattern).Set(rate);
    }

    public static void SetActualRate(string pattern, double rate)
    {
        ActualRateGauge.WithLabels(SDK, pattern).Set(rate);
    }

    public static void SetConsumerLag(string pattern, double lag)
    {
        ConsumerLag.WithLabels(SDK, pattern).Set(lag);
    }

    public static void SetGroupBalance(string pattern, double ratio)
    {
        GroupBalance.WithLabels(SDK, pattern).Set(ratio);
    }

    public static void SetWarmupActive(double val)
    {
        WarmupGauge.WithLabels(SDK).Set(val);
    }

    public static void SetActiveWorkers(double count)
    {
        ActiveWorkersGauge.WithLabels(SDK).Set(count);
    }
}
