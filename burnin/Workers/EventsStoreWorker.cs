// Events Store worker: persistent pub/sub via CreateEventStoreStreamAsync + SubscribeToEventStoreAsync.
// SendAsync returns EventSendResult — check .Sent before RecordSend. IncUnconfirmed on failure.

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.EventsStore;

namespace KubeMQ.Burnin.Workers;

/// <summary>
/// Events Store pattern worker: persistent publish/subscribe with delivery confirmation.
/// Producer uses CreateEventStoreStreamAsync → EventStoreStream.SendAsync(message, clientId, ct).
/// Consumer uses SubscribeToEventStoreAsync with EventStoreStartPosition.FromNew.
/// </summary>
public sealed class EventsStoreWorker : BaseWorker
{
    private const string Sdk = "csharp";
    private const string PatternName = "events_store";

    private EventStoreStream? _storeStream;
    private readonly List<Task> _consumerTasks = new();
    private readonly List<Task> _producerTasks = new();
    private long _unconfirmed;

    public long Unconfirmed => Interlocked.Read(ref _unconfirmed);

    public EventsStoreWorker(BurninConfig config, string runId)
        : base(PatternName, config, $"csharp_burnin_{runId}_{PatternName}_001", config.Rates.EventsStore)
    {
    }

    public override async Task StartConsumersAsync(KubeMQClient client)
    {
        int nConsumers = Config.Concurrency.EventsStoreConsumers;
        bool useGroup = Config.Concurrency.EventsStoreConsumerGroup;

        for (int i = 0; i < nConsumers; i++)
        {
            string consumerId = $"c-{PatternName}-{i:D3}";
            string? group = useGroup ? $"csharp_burnin_{PatternName}_group" : null;

            var subscription = new EventStoreSubscription
            {
                Channel = ChannelName,
                Group = group ?? string.Empty,
                StartPosition = EventStoreStartPosition.FromNew,
            };

            var task = Task.Run(async () =>
            {
                string cid = consumerId; // capture
                try
                {
                    await foreach (var evt in client.SubscribeToEventStoreAsync(subscription, ConsumerCts.Token))
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
                    Console.Error.WriteLine($"events_store subscription error: {ex.Message}");
                    RecordError("subscription_error");
                    IncReconnection();
                }
            });

            _consumerTasks.Add(task);
        }

        Console.WriteLine($"events_store consumers started on {ChannelName}");
        await Task.CompletedTask;
    }

    public override async Task StartProducersAsync(KubeMQClient client)
    {
        _storeStream = await client.CreateEventStoreStreamAsync(ProducerCts.Token);

        int nProducers = Config.Concurrency.EventsStoreProducers;
        for (int i = 0; i < nProducers; i++)
        {
            string producerId = $"p-{PatternName}-{i:D3}";
            _producerTasks.Add(Task.Run(() => RunProducerAsync(producerId)));
        }

        Console.WriteLine($"events_store producers started on {ChannelName}");
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

                // SendAsync returns EventSendResult — check .Sent for confirmation
                EventSendResult result = await _storeStream!.SendAsync(message, clientId, ct);

                if (result.Sent)
                {
                    // Go#7: count only after confirmation
                    RecordSend(producerId, seq, encoded.Body.Length);
                }
                else
                {
                    // Unconfirmed: server did not confirm persistence
                    Interlocked.Increment(ref _unconfirmed);
                    RecordError("send_failure");
                }
            }
            catch (OperationCanceledException) { break; }
            catch
            {
                Interlocked.Increment(ref _unconfirmed);
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
