// Queue Stream worker: SendQueueMessageAsync for sending, QueueDownstreamReceiver.PollAsync for receiving.
// v2: accepts channelName, rate, channelIndex, patternLatencyAccum.
// Worker IDs: {role}-{pattern}-{4-digit-channel}-{3-digit-worker}

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Queues;

namespace KubeMQ.Burnin.Workers;

/// <summary>
/// Queue Stream pattern worker: persistent downstream receiver with manual per-message ack.
/// Producer uses SendQueueMessageAsync (unary).
/// Consumer creates a QueueDownstreamReceiver and polls in a loop.
/// </summary>
public sealed class QueueStreamWorker : BaseWorker
{
    private const string Sdk = "csharp";
    private const string PatternName = "queue_stream";

    private readonly List<Task> _consumerTasks = new();
    private readonly List<Task> _producerTasks = new();
    private KubeMQClient? _client;
    private readonly int _numProducers;
    private readonly int _numConsumers;

    public QueueStreamWorker(BurninConfig config, string runId, string channelName, int channelIndex,
        int producers, int consumers, int rate,
        LatencyAccumulator patternLatencyAccum)
        : base(PatternName, config, channelName, rate, channelIndex, patternLatencyAccum)
    {
        _numProducers = producers;
        _numConsumers = consumers;
    }

    public override async Task StartConsumersAsync(KubeMQClient client)
    {
        _client = client;

        for (int i = 0; i < _numConsumers; i++)
        {
            string consumerId = $"c-{PatternName}-{ChannelIndex:D4}-{i:D3}";
            _consumerTasks.Add(Task.Run(() => RunConsumerAsync(consumerId, client)));
        }

        Console.WriteLine($"queue_stream consumers started on {ChannelName} ({_numConsumers} consumers)");
        await Task.CompletedTask;
    }

    public override async Task StartProducersAsync(KubeMQClient client)
    {
        for (int i = 0; i < _numProducers; i++)
        {
            string producerId = $"p-{PatternName}-{ChannelIndex:D4}-{i:D3}";
            _producerTasks.Add(Task.Run(() => RunProducerAsync(producerId, client)));
        }

        Console.WriteLine($"queue_stream producers started on {ChannelName} ({_numProducers} producers)");
        await Task.CompletedTask;
    }

    private async Task RunConsumerAsync(string consumerId, KubeMQClient client)
    {
        var ct = ConsumerCts.Token;
        int maxItems = Config.Queue.PollMaxMessages;
        int waitTimeoutSec = Config.Queue.PollWaitTimeoutSeconds;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var receiver = await client.CreateQueueDownstreamReceiverAsync(ct);

                receiver.OnError += (_, e) =>
                {
                    RecordError("settlement_error");
                };

                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var batch = await receiver.PollAsync(new QueuePollRequest
                        {
                            Channel = ChannelName,
                            MaxMessages = maxItems,
                            WaitTimeoutSeconds = waitTimeoutSec,
                            AutoAck = false,
                        }, ct);

                        if (batch.IsError)
                        {
                            string errMsg = batch.Error ?? "unknown downstream error";
                            if (!errMsg.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                            {
                                RecordError("receive_failure");
                            }
                            continue;
                        }

                        if (!batch.HasMessages)
                            continue;

                        // Process all messages first, then ack the entire batch
                        // in a single round-trip via AckAllAsync.
                        // Deduplicate within the batch — the broker/SDK can return
                        // the same message twice in a single response.
                        var seenInBatch = new HashSet<string>();
                        foreach (var msg in batch.Messages)
                        {
                            if (!seenInBatch.Add(msg.MessageId))
                                continue; // skip duplicate

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

                        // Batch ack all processed messages in a single round-trip
                        try
                        {
                            await batch.AckAllAsync(ct);
                        }
                        catch (KubeMQOperationException)
                        {
                            RecordError("ack_failure");
                            IncReconnection();
                            throw; // bubble up to trigger reconnect
                        }
                        catch
                        {
                            RecordError("ack_failure");
                        }
                    }
                    catch (KubeMQOperationException)
                    {
                        IncReconnection();
                        break;
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested) break;

                Console.Error.WriteLine($"queue_stream consumer error (ch{ChannelIndex:D4}): {ex.Message}");
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
