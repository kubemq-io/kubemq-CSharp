// PatternGroup: coordinator for N ChannelWorkers (BaseWorker instances) per pattern.
// Provides aggregation, lifecycle management, and shared pattern-level LatencyAccumulator.

using KubeMQ.Burnin.Workers;

namespace KubeMQ.Burnin;

/// <summary>
/// Holds all ChannelWorkers for a single pattern and provides aggregation methods.
/// Each PatternGroup has one shared thread-safe LatencyAccumulator for pattern-level latency.
/// </summary>
public sealed class PatternGroup : IDisposable
{
    public string Pattern { get; }
    public PatternConfig Config { get; }
    public List<BaseWorker> ChannelWorkers { get; } = new();
    public LatencyAccumulator PatternLatencyAccum { get; } = new();
    public List<string> ChannelNames { get; } = new();

    private readonly BurninConfig _burninConfig;
    private readonly string _runId;

    public PatternGroup(string pattern, PatternConfig config, BurninConfig burninConfig, string runId)
    {
        Pattern = pattern;
        Config = config;
        _burninConfig = burninConfig;
        _runId = runId;

        CreateChannelWorkers();
    }

    private void CreateChannelWorkers()
    {
        bool isRpc = Pattern is "commands" or "queries";
        int channels = Config.Channels;

        for (int i = 0; i < channels; i++)
        {
            int channelIndex = i + 1; // 1-based index
            string channelName = $"csharp_burnin_{_runId}_{Pattern}_{channelIndex:D4}";
            ChannelNames.Add(channelName);

            BaseWorker worker = Pattern switch
            {
                "events" => new EventsWorker(
                    _burninConfig, _runId, channelName, channelIndex,
                    Config.ProducersPerChannel, Config.ConsumersPerChannel,
                    Config.ConsumerGroup, Config.Rate, PatternLatencyAccum),

                "events_store" => new EventsStoreWorker(
                    _burninConfig, _runId, channelName, channelIndex,
                    Config.ProducersPerChannel, Config.ConsumersPerChannel,
                    Config.ConsumerGroup, Config.Rate, PatternLatencyAccum),

                "queue_stream" => new QueueStreamWorker(
                    _burninConfig, _runId, channelName, channelIndex,
                    Config.ProducersPerChannel, Config.ConsumersPerChannel,
                    Config.Rate, PatternLatencyAccum),

                "queue_simple" => new QueueSimpleWorker(
                    _burninConfig, _runId, channelName, channelIndex,
                    Config.ProducersPerChannel, Config.ConsumersPerChannel,
                    Config.Rate, PatternLatencyAccum),

                "commands" => new CommandsWorker(
                    _burninConfig, _runId, channelName, channelIndex,
                    Config.SendersPerChannel, Config.RespondersPerChannel,
                    Config.Rate, PatternLatencyAccum),

                "queries" => new QueriesWorker(
                    _burninConfig, _runId, channelName, channelIndex,
                    Config.SendersPerChannel, Config.RespondersPerChannel,
                    Config.Rate, PatternLatencyAccum),

                _ => throw new ArgumentException($"Unknown pattern: {Pattern}"),
            };

            ChannelWorkers.Add(worker);
        }
    }

    // --- Lifecycle ---

    /// <summary>
    /// Start all consumers/responders across all channels.
    /// </summary>
    public async Task StartConsumersAsync(KubeMQ.Sdk.Client.KubeMQClient client)
    {
        foreach (var w in ChannelWorkers)
        {
            await w.StartConsumersAsync(client);
        }
    }

    /// <summary>
    /// Start all producers/senders across all channels.
    /// </summary>
    public async Task StartProducersAsync(KubeMQ.Sdk.Client.KubeMQClient client)
    {
        foreach (var w in ChannelWorkers)
        {
            await w.StartProducersAsync(client);
        }
    }

    /// <summary>
    /// Stop all producers across all channels (phase 1 of shutdown).
    /// </summary>
    public void StopProducers()
    {
        foreach (var w in ChannelWorkers)
            w.StopProducers();
    }

    /// <summary>
    /// Stop all consumers across all channels (phase 2 of shutdown).
    /// </summary>
    public void StopConsumers()
    {
        foreach (var w in ChannelWorkers)
            w.StopConsumers();
    }

    /// <summary>
    /// Reset all channel workers after warmup.
    /// Also resets the shared pattern-level latency accumulator.
    /// Queue patterns are skipped: their persistent broker channels retain
    /// in-flight messages across the reset boundary, and producers keep their
    /// sequence counters running, so wiping the Tracker mid-sequence causes
    /// false duplicates and a received-vs-sent mismatch.
    /// </summary>
    public void ResetAfterWarmup()
    {
        if (Pattern is "queue_stream" or "queue_simple")
        {
            Console.WriteLine($"skipping warmup reset for {Pattern} (persistent queue pattern)");
            return;
        }

        foreach (var w in ChannelWorkers)
            w.ResetAfterWarmup();
        PatternLatencyAccum.Reset();
    }

    // --- Aggregation ---

    public long TotalSent => ChannelWorkers.Sum(w => w.Sent);
    public long TotalReceived => ChannelWorkers.Sum(w => w.Received);
    public long TotalLost => ChannelWorkers.Sum(w => w.Tracker.TotalLost());
    public long TotalDuplicated => ChannelWorkers.Sum(w => w.Tracker.TotalDuplicates());
    public long TotalCorrupted => ChannelWorkers.Sum(w => w.Corrupted);
    public long TotalOutOfOrder => ChannelWorkers.Sum(w => w.Tracker.TotalOutOfOrder());
    public long TotalErrors => ChannelWorkers.Sum(w => w.Errors);
    public long TotalBytesSent => ChannelWorkers.Sum(w => w.BytesSent);
    public long TotalBytesReceived => ChannelWorkers.Sum(w => w.BytesReceived);
    public long TotalRpcSuccess => ChannelWorkers.Sum(w => w.RpcSuccess);
    public long TotalRpcTimeout => ChannelWorkers.Sum(w => w.RpcTimeout);
    public long TotalRpcError => ChannelWorkers.Sum(w => w.RpcError);
    public long TotalUnconfirmed => ChannelWorkers.Sum(w => w.Unconfirmed);
    public double MaxPeakRate => ChannelWorkers.Max(w => w.PeakRate.Peak);

    /// <summary>
    /// Total reconnections -- connection-level, not per-channel.
    /// Since channels share a gRPC client, we take the max across channels.
    /// </summary>
    public long TotalReconnections => ChannelWorkers.Count > 0
        ? ChannelWorkers.Max(w => w.Reconnections)
        : 0;

    /// <summary>
    /// Max downtime across all channels (not sum -- they share a connection).
    /// </summary>
    public double MaxDowntimeSeconds => ChannelWorkers.Count > 0
        ? ChannelWorkers.Max(w => w.DowntimeSeconds)
        : 0;

    /// <summary>
    /// Find the worst-case channel for a given metric selector.
    /// Returns (channelIndex, value, total) for the worst channel.
    /// </summary>
    public (int ChannelIndex, double Value, long Sent) FindWorstChannel(Func<BaseWorker, double> selector)
    {
        double worst = double.MinValue;
        int worstIdx = 0;
        long worstSent = 0;
        foreach (var w in ChannelWorkers)
        {
            double val = selector(w);
            if (val > worst)
            {
                worst = val;
                worstIdx = w.ChannelIndex;
                worstSent = w.Sent;
            }
        }
        return (worstIdx, worst, worstSent);
    }

    public void Dispose()
    {
        foreach (var w in ChannelWorkers)
            w.Dispose();
    }
}
