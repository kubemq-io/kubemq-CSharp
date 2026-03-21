// Queries worker: RPC request/response via SendQueryAsync + SubscribeToQueriesAsync.
// v2: accepts channelName, rate, channelIndex, patternLatencyAccum.
// Worker IDs: {role}-{pattern}-{4-digit-channel}-{3-digit-worker}

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queries;

namespace KubeMQ.Burnin.Workers;

/// <summary>
/// Queries pattern worker: RPC with response body echo.
/// Sender uses SendQueryAsync and verifies that the response body CRC matches.
/// Responder uses SubscribeToQueriesAsync (IAsyncEnumerable&lt;QueryReceived&gt;)
/// and echoes the body via SendQueryResponseAsync.
/// </summary>
public sealed class QueriesWorker : BaseWorker
{
    private const string Sdk = "csharp";
    private const string PatternName = "queries";

    private readonly List<Task> _responderTasks = new();
    private readonly List<Task> _senderTasks = new();
    private readonly int _numSenders;
    private readonly int _numResponders;

    public QueriesWorker(BurninConfig config, string runId, string channelName, int channelIndex,
        int senders, int responders, int rate,
        LatencyAccumulator patternLatencyAccum)
        : base(PatternName, config, channelName, rate, channelIndex, patternLatencyAccum)
    {
        _numSenders = senders;
        _numResponders = responders;
    }

    public override async Task StartConsumersAsync(KubeMQClient client)
    {
        for (int i = 0; i < _numResponders; i++)
        {
            var subscription = new QueriesSubscription
            {
                Channel = ChannelName,
            };

            var task = Task.Run(async () =>
            {
                try
                {
                    await foreach (var query in client.SubscribeToQueriesAsync(subscription, ConsumerCts.Token))
                    {
                        if (ConsumerCts.IsCancellationRequested) break;

                        var tags = query.Tags;
                        bool isWarmup = tags != null
                            && tags.TryGetValue("warmup", out string? warmupVal)
                            && warmupVal == "true";

                        if (!isWarmup)
                        {
                            RecordBytesReceived(query.Body.Length);
                        }

                        var capturedQuery = query;
                        _ = client.SendQueryResponseAsync(new QueryResponse
                        {
                            RequestId = capturedQuery.RequestId,
                            ReplyChannel = capturedQuery.ReplyChannel,
                            Body = capturedQuery.Body,
                            Executed = true,
                        }, ConsumerCts.Token).ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                                RecordError("response_failure");
                        }, TaskContinuationOptions.OnlyOnFaulted);
                    }
                }
                catch (OperationCanceledException) { /* normal shutdown */ }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"queries subscription error (ch{ChannelIndex:D4}): {ex.Message}");
                    RecordError("subscription_error");
                    IncReconnection();
                }
            });

            _responderTasks.Add(task);
        }

        Console.WriteLine($"queries responders started on {ChannelName} ({_numResponders} responders)");
        await Task.CompletedTask;
    }

    public override async Task StartProducersAsync(KubeMQClient client)
    {
        for (int i = 0; i < _numSenders; i++)
        {
            string senderId = $"s-{PatternName}-{ChannelIndex:D4}-{i:D3}";
            _senderTasks.Add(Task.Run(() => RunSenderAsync(senderId, client)));
        }

        Console.WriteLine($"queries senders started on {ChannelName} ({_numSenders} senders)");
        await Task.CompletedTask;
    }

    private async Task RunSenderAsync(string senderId, KubeMQClient client)
    {
        long seq = 0;
        var ct = ProducerCts.Token;
        int timeoutSeconds = Math.Max(1, Config.Rpc.TimeoutMs / 1000);

        while (!ct.IsCancellationRequested)
        {
            if (!await WaitForRate(ct)) break;

            seq++;
            int size = MessageSize();
            var encoded = Payload.Encode(Sdk, PatternName, senderId, seq, size);

            try
            {
                long t0 = StopwatchTimestamp.GetNanoseconds();

                var query = new QueryMessage
                {
                    Channel = ChannelName,
                    Body = encoded.Body.ToArray(),
                    TimeoutInSeconds = timeoutSeconds,
                    Tags = new Dictionary<string, string> { ["content_hash"] = encoded.CrcHex },
                };

                var resp = await client.SendQueryAsync(query, ct);

                double rpcDuration = (StopwatchTimestamp.GetNanoseconds() - t0) / 1_000_000_000.0;
                RpcLatencyAccum.Record(rpcDuration);
                PatternLatencyAccum.Record(rpcDuration);

                if (!string.IsNullOrEmpty(resp.Error))
                {
                    if (resp.Error.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                    {
                        IncRpcTimeout();
                    }
                    else
                    {
                        IncRpcError();
                    }
                }
                else
                {
                    IncRpcSuccess();
                    RecordSend(senderId, seq, encoded.Body.Length);

                    if (resp.Body.Length > 0)
                    {
                        if (Payload.VerifyCrc(resp.Body.Span, encoded.CrcHex))
                        {
                            RecordReceive(senderId, resp.Body, encoded.CrcHex,
                                senderId, seq);
                        }
                        else
                        {
                            RecordError("response_corruption");
                        }
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                if (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                {
                    IncRpcTimeout();
                }
                else
                {
                    IncRpcError();
                }
                RecordError("send_failure");
            }
        }
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}
