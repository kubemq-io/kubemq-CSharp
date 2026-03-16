// Queue Simple worker: SendQueueMessageAsync for sending, ReceiveQueueMessagesAsync for receiving.
// Auto-consumed messages. Check result.IsError before processing.

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;

namespace KubeMQ.Burnin.Workers;

/// <summary>
/// Queue Simple pattern worker: unary send/receive with auto-ack.
/// Producer uses SendQueueMessageAsync (single message).
/// Consumer uses ReceiveQueueMessagesAsync (batch poll with auto-consumption).
/// </summary>
public sealed class QueueSimpleWorker : BaseWorker
{
    private const string Sdk = "csharp";
    private const string PatternName = "queue_simple";

    private readonly List<Task> _consumerTasks = new();
    private readonly List<Task> _producerTasks = new();

    public QueueSimpleWorker(BurninConfig config, string runId)
        : base(PatternName, config, $"csharp_burnin_{runId}_{PatternName}_001", config.Rates.QueueSimple)
    {
    }

    public override async Task StartConsumersAsync(KubeMQClient client)
    {
        int nConsumers = Config.Concurrency.QueueSimpleConsumers;

        for (int i = 0; i < nConsumers; i++)
        {
            string consumerId = $"c-{PatternName}-{i:D3}";
            _consumerTasks.Add(Task.Run(() => RunConsumerAsync(consumerId, client)));
        }

        Console.WriteLine($"queue_simple consumers started on {ChannelName}");
        await Task.CompletedTask;
    }

    public override async Task StartProducersAsync(KubeMQClient client)
    {
        int nProducers = Config.Concurrency.QueueSimpleProducers;
        for (int i = 0; i < nProducers; i++)
        {
            string producerId = $"p-{PatternName}-{i:D3}";
            _producerTasks.Add(Task.Run(() => RunProducerAsync(producerId, client)));
        }

        Console.WriteLine($"queue_simple producers started on {ChannelName}");
        await Task.CompletedTask;
    }

    private async Task RunConsumerAsync(string consumerId, KubeMQClient client)
    {
        var ct = ConsumerCts.Token;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // ReceiveQueueMessagesAsync: returns QueueReceiveResult
                var result = await client.ReceiveQueueMessagesAsync(
                    ChannelName,
                    Config.Queue.PollMaxMessages,
                    Config.Queue.PollWaitTimeoutSeconds,
                    ct);

                // Check result.IsError
                if (result.IsError)
                {
                    string errMsg = result.Error ?? "unknown";
                    if (!errMsg.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                    {
                        RecordError("receive_failure");
                    }
                    continue;
                }

                if (result.Messages == null || result.Messages.Count == 0)
                    continue;

                foreach (var msg in result.Messages)
                {
                    var tags = msg.Tags;
                    if (tags != null && tags.TryGetValue("warmup", out string? warmupVal)
                        && warmupVal == "true")
                    {
                        continue;
                    }

                    try
                    {
                        var decoded = Payload.Decode(msg.Body);
                        string crcTag = tags?.GetValueOrDefault("content_hash") ?? "";
                        RecordReceive(consumerId, msg.Body, crcTag,
                            decoded.ProducerId, decoded.Sequence);
                    }
                    catch
                    {
                        RecordError("decode_failure");
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested) break;
                if (!ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                {
                    RecordError("receive_failure");
                }
                await Task.Delay(1000, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task RunProducerAsync(string producerId, KubeMQClient client)
    {
        long seq = 0;
        var ct = ProducerCts.Token;

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
                var message = new QueueMessage
                {
                    Channel = ChannelName,
                    Body = encoded.Body.ToArray(),
                    Tags = new Dictionary<string, string> { ["content_hash"] = encoded.CrcHex },
                };

                // SendQueueMessageAsync for single message send
                var result = await client.SendQueueMessageAsync(message, ct);

                if (result.IsError)
                {
                    RecordError("send_failure");
                }
                else
                {
                    RecordSend(producerId, seq, encoded.Body.Length);
                }
            }
            catch (OperationCanceledException) { break; }
            catch
            {
                RecordError("send_failure");
            }
        }
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}
