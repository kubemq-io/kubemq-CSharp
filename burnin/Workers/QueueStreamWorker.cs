// Queue Stream worker: SendQueueMessagesUpstreamAsync for sending, ReceiveQueueDownstreamAsync for receiving.
// After processing batch: AckAllDownstreamAsync(transactionId). Check result.IsError.

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;

namespace KubeMQ.Burnin.Workers;

/// <summary>
/// Queue Stream pattern worker: bidirectional streaming with manual batch ack.
/// Producer uses SendQueueMessagesUpstreamAsync(IEnumerable&lt;QueueMessage&gt;, ct).
/// Consumer uses ReceiveQueueDownstreamAsync then AckAllDownstreamAsync(transactionId).
/// </summary>
public sealed class QueueStreamWorker : BaseWorker
{
    private const string Sdk = "csharp";
    private const string PatternName = "queue_stream";

    private readonly List<Task> _consumerTasks = new();
    private readonly List<Task> _producerTasks = new();
    private KubeMQClient? _client;

    public QueueStreamWorker(BurninConfig config, string runId)
        : base(PatternName, config, $"csharp_burnin_{runId}_{PatternName}_001", config.Rates.QueueStream)
    {
    }

    public override async Task StartConsumersAsync(KubeMQClient client)
    {
        _client = client;
        int nConsumers = Config.Concurrency.QueueStreamConsumers;

        for (int i = 0; i < nConsumers; i++)
        {
            string consumerId = $"c-{PatternName}-{i:D3}";
            _consumerTasks.Add(Task.Run(() => RunConsumerAsync(consumerId, client)));
        }

        Console.WriteLine($"queue_stream consumers started on {ChannelName}");
        await Task.CompletedTask;
    }

    public override async Task StartProducersAsync(KubeMQClient client)
    {
        int nProducers = Config.Concurrency.QueueStreamProducers;
        for (int i = 0; i < nProducers; i++)
        {
            string producerId = $"p-{PatternName}-{i:D3}";
            _producerTasks.Add(Task.Run(() => RunProducerAsync(producerId, client)));
        }

        Console.WriteLine($"queue_stream producers started on {ChannelName}");
        await Task.CompletedTask;
    }

    private async Task RunConsumerAsync(string consumerId, KubeMQClient client)
    {
        var ct = ConsumerCts.Token;
        int maxItems = Config.Queue.PollMaxMessages;
        int waitTimeoutMs = Config.Queue.PollWaitTimeoutSeconds * 1000;
        bool autoAck = false; // manual ack for stream pattern

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // ReceiveQueueDownstreamAsync returns QueueDownstreamResult
                var result = await client.ReceiveQueueDownstreamAsync(
                    ChannelName, maxItems, waitTimeoutMs, autoAck, ct);

                // Check for error before processing
                if (result.IsError)
                {
                    string errMsg = result.Error ?? "unknown downstream error";
                    if (!errMsg.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                    {
                        RecordError("receive_failure");
                    }
                    continue;
                }

                if (result.Messages == null || result.Messages.Count == 0)
                    continue;

                // Process all messages in the batch (Go#19: drain entire batch)
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

                // Ack the entire batch via AckAllDownstreamAsync with the transaction ID
                if (!string.IsNullOrEmpty(result.TransactionId))
                {
                    try
                    {
                        await client.AckAllDownstreamAsync(result.TransactionId, ct);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"queue_stream ack error: {ex.Message}");
                        RecordError("receive_failure");
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested) break;

                // GAP-3 FIX: CloseByServer is a reconnection event, not an error.
                // SDK throws KubeMQOperationException("Server closed the downstream stream.")
                bool isServerClose = ex.Message.Contains("Server closed", StringComparison.OrdinalIgnoreCase);
                if (isServerClose)
                {
                    IncReconnection();
                    // Retry immediately — no error, no delay
                    continue;
                }

                Console.Error.WriteLine($"queue_stream consumer error: {ex.Message}");
                RecordError("receive_failure");
                IncReconnection();
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

                // C# SDK's SendQueueMessagesUpstreamAsync opens a fresh QueuesUpstream
                // stream per call (stream-per-request, not persistent stream). At 50 msg/s
                // the per-stream overhead (~1s) makes it impractical. Use SendQueueMessageAsync
                // (unary) for production rate, which the SDK routes through the gRPC channel
                // efficiently. The downstream side correctly uses ReceiveQueueDownstreamAsync
                // (persistent QueuesDownstream bidi stream) for the receive path.
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var result = await client.SendQueueMessageAsync(message, ct);
                sw.Stop();
                Metrics.ObserveSendDuration(PatternName, sw.Elapsed.TotalSeconds);

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
