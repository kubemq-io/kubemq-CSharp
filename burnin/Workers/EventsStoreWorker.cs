// Events Store worker: persistent pub/sub via CreateEventStoreStreamAsync + SubscribeToEventsStoreAsync.
// v2: accepts channelName, rate, channelIndex, patternLatencyAccum.
// Worker IDs: {role}-{pattern}-{4-digit-channel}-{3-digit-worker}

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.EventsStore;

namespace KubeMQ.Burnin.Workers;

/// <summary>
/// Events Store pattern worker: persistent publish/subscribe with delivery confirmation.
/// Producer uses CreateEventStoreStreamAsync -> EventStoreStream.SendAsync(message, clientId, ct).
/// Consumer uses SubscribeToEventsStoreAsync with EventStoreStartPosition.StartFromNew.
/// </summary>
public sealed class EventsStoreWorker : BaseWorker
{
    private const string Sdk = "csharp";
    private const string PatternName = "events_store";

    private EventStoreStream? _storeStream;
    private readonly List<Task> _consumerTasks = new();
    private readonly List<Task> _producerTasks = new();
    private long _unconfirmedLocal;
    private readonly int _numProducers;
    private readonly int _numConsumers;
    private readonly bool _useGroup;
    private readonly string _runId;

    public long UnconfirmedLocal => Interlocked.Read(ref _unconfirmedLocal);

    public EventsStoreWorker(BurninConfig config, string runId, string channelName, int channelIndex,
        int producers, int consumers, bool consumerGroup, int rate,
        LatencyAccumulator patternLatencyAccum)
        : base(PatternName, config, channelName, rate, channelIndex, patternLatencyAccum)
    {
        _numProducers = producers;
        _numConsumers = consumers;
        _useGroup = consumerGroup;
        _runId = runId;
    }

    public override async Task StartConsumersAsync(KubeMQClient client)
    {
        for (int i = 0; i < _numConsumers; i++)
        {
            string consumerId = $"c-{PatternName}-{ChannelIndex:D4}-{i:D3}";
            string group = _useGroup ? $"csharp_burnin_{_runId}_{PatternName}_{ChannelIndex:D4}_group" : string.Empty;

            var subscription = new EventStoreSubscription
            {
                Channel = ChannelName,
                Group = group,
                StartPosition = EventStoreStartPosition.StartFromNew,
            };

            var task = Task.Run(async () =>
            {
                string cid = consumerId; // capture
                try
                {
                    await foreach (var evt in client.SubscribeToEventsStoreAsync(subscription, ConsumerCts.Token))
                    {
                        if (ConsumerCts.IsCancellationRequested) break;

                        var tags = evt.Tags;
                        if (tags != null && tags.TryGetValue("warmup", out string? warmupVal)
                            && warmupVal == "true")
                        {
                            continue;
                        }

                        try
                        {
                            var decoded = Payload.Decode(evt.Body);
                            string crcTag = tags?.GetValueOrDefault("content_hash") ?? "";
                            RecordReceive(cid, evt.Body, crcTag, decoded.ProducerId, decoded.Sequence);
                        }
                        catch
                        {
                            RecordError("decode_failure");
                        }
                    }
                }
                catch (OperationCanceledException) { /* normal shutdown */ }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"events_store subscription error (ch{ChannelIndex:D4}): {ex.Message}");
                    RecordError("subscription_error");
                    IncReconnection();
                }
            });

            _consumerTasks.Add(task);
        }

        Console.WriteLine($"events_store consumers started on {ChannelName} ({_numConsumers} consumers)");
        await Task.CompletedTask;
    }

    public override async Task StartProducersAsync(KubeMQClient client)
    {
        _storeStream = await client.CreateEventStoreStreamAsync(ProducerCts.Token);

        for (int i = 0; i < _numProducers; i++)
        {
            string producerId = $"p-{PatternName}-{ChannelIndex:D4}-{i:D3}";
            _producerTasks.Add(Task.Run(() => RunProducerAsync(producerId)));
        }

        Console.WriteLine($"events_store producers started on {ChannelName} ({_numProducers} producers)");
    }

    private async Task RunProducerAsync(string producerId)
    {
        long seq = 0;
        var ct = ProducerCts.Token;
        string clientId = $"{Config.Broker.ClientIdPrefix}-{Config.RunId}";

        while (!ct.IsCancellationRequested)
        {
            if (!await WaitForRate(ct)) break;
            if (BackpressureCheck())
            {
                await Task.Delay(100, ct).ConfigureAwait(false);
                continue;
            }

            seq++;
            int size = MessageSize();
            var encoded = Payload.Encode(Sdk, PatternName, producerId, seq, size);

            try
            {
                var message = new EventStoreMessage
                {
                    Channel = ChannelName,
                    Body = encoded.Body.ToArray(),
                    Tags = new Dictionary<string, string> { ["content_hash"] = encoded.CrcHex },
                };

                var result = await _storeStream!.SendAsync(message, clientId, ct);
                if (result.Sent)
                {
                    RecordSend(producerId, seq, encoded.Body.Length);
                }
                else
                {
                    Interlocked.Increment(ref _unconfirmedLocal);
                    RecordError("send_failure");
                }
            }
            catch (OperationCanceledException) { break; }
            catch
            {
                Interlocked.Increment(ref _unconfirmedLocal);
                RecordError("send_failure");
            }
        }
    }

    public override void StopProducers()
    {
        base.StopProducers();
        try { if (_storeStream != null) _storeStream.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        catch { /* best effort */ }
        _storeStream = null;
    }

    public override void StopConsumers()
    {
        base.StopConsumers();
    }

    public override void Dispose()
    {
        if (_storeStream != null) _storeStream.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
    }
}
