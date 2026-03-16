// Events worker: fire-and-forget pub/sub via CreateEventStreamAsync + SubscribeToEventsAsync.
// Uses IAsyncEnumerable for consumption. SendAsync requires clientId parameter.

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;

namespace KubeMQ.Burnin.Workers;

/// <summary>
/// Events pattern worker: fire-and-forget publish/subscribe.
/// Producer uses CreateEventStreamAsync → EventStream.SendAsync(message, clientId, ct).
/// Consumer uses SubscribeToEventsAsync returning IAsyncEnumerable&lt;EventReceived&gt;.
/// </summary>
public sealed class EventsWorker : BaseWorker
{
    private const string Sdk = "csharp";
    private const string PatternName = "events";

    private EventStream? _eventStream;
    private readonly List<Task> _consumerTasks = new();
    private readonly List<Task> _producerTasks = new();
    private KubeMQClient? _client;

    public EventsWorker(BurninConfig config, string runId)
        : base(PatternName, config, $"csharp_burnin_{runId}_{PatternName}_001", config.Rates.Events)
    {
    }

    public override async Task StartConsumersAsync(KubeMQClient client)
    {
        _client = client;
        int nConsumers = Config.Concurrency.EventsConsumers;
        bool useGroup = Config.Concurrency.EventsConsumerGroup;

        for (int i = 0; i < nConsumers; i++)
        {
            string consumerId = $"c-{PatternName}-{i:D3}";
            string? group = useGroup ? $"csharp_burnin_{PatternName}_group" : null;

            var subscription = new EventsSubscription
            {
                Channel = ChannelName,
                Group = group ?? string.Empty,
            };

            var task = Task.Run(async () =>
            {
                string cid = consumerId; // capture for closure
                try
                {
                    await foreach (var evt in client.SubscribeToEventsAsync(subscription, ConsumerCts.Token))
                    {
                        if (ConsumerCts.IsCancellationRequested) break;

                        var tags = evt.Tags;
                        if (tags != null && tags.TryGetValue("warmup", out string? warmupVal)
                            && warmupVal == "true")
                        {
                            continue; // Go#18: skip warmup messages
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
                    Console.Error.WriteLine($"events subscription error: {ex.Message}");
                    RecordError("subscription_error");
                    IncReconnection();
                }
            });

            _consumerTasks.Add(task);
        }

        Console.WriteLine($"events consumers started on {ChannelName}");
        await Task.CompletedTask;
    }

    public override async Task StartProducersAsync(KubeMQClient client)
    {
        _eventStream = await client.CreateEventStreamAsync(null, ProducerCts.Token);

        int nProducers = Config.Concurrency.EventsProducers;
        for (int i = 0; i < nProducers; i++)
        {
            string producerId = $"p-{PatternName}-{i:D3}";
            _producerTasks.Add(Task.Run(() => RunProducerAsync(producerId)));
        }

        Console.WriteLine($"events producers started on {ChannelName}");
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
                var message = new EventMessage
                {
                    Channel = ChannelName,
                    Body = encoded.Body.ToArray(),
                    Tags = new Dictionary<string, string> { ["content_hash"] = encoded.CrcHex },
                };

                // F2: SendAsync requires clientId parameter
                await _eventStream!.SendAsync(message, clientId, ct);

                // Go#7: count after success (no exception = success for fire-and-forget)
                RecordSend(producerId, seq, encoded.Body.Length);
            }
            catch (OperationCanceledException) { break; }
            catch
            {
                RecordError("send_failure");
            }
        }
    }

    public override void StopProducers()
    {
        base.StopProducers();
        try { if (_eventStream != null) _eventStream.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        catch { /* best effort */ }
        _eventStream = null;
    }

    public override void StopConsumers()
    {
        base.StopConsumers();
    }

    public override void Dispose()
    {
        if (_eventStream != null) _eventStream.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
    }
}
