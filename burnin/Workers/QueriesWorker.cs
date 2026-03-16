// Queries worker: RPC request/response via SendQueryAsync + SubscribeToQueriesAsync.
// Responder echoes body. Sender verifies response body CRC.

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

    public QueriesWorker(BurninConfig config, string runId)
        : base(PatternName, config, $"csharp_burnin_{runId}_{PatternName}_001", config.Rates.Queries)
    {
    }

    public override async Task StartConsumersAsync(KubeMQClient client)
    {
        int nResponders = Config.Concurrency.QueriesResponders;

        for (int i = 0; i < nResponders; i++)
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

                        try
                        {
                            // Echo body back via SendQueryResponseAsync
                            await client.SendQueryResponseAsync(
                                query.RequestId,
                                query.ReplyChannel,
                                body: query.Body, // echo the request body
                                executed: true,
                                tags: null,
                                errorMessage: string.Empty,
                                ConsumerCts.Token);
                        }
                        catch (Exception ex)
                        {
                            if (!isWarmup)
                            {
                                RecordError("response_send_failure");
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { /* normal shutdown */ }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"queries subscription error: {ex.Message}");
                    RecordError("subscription_error");
                    IncReconnection();
                }
            });

            _responderTasks.Add(task);
        }

        Console.WriteLine($"queries responders started on {ChannelName}");
        await Task.CompletedTask;
    }

    public override async Task StartProducersAsync(KubeMQClient client)
    {
        int nSenders = Config.Concurrency.QueriesSenders;
        for (int i = 0; i < nSenders; i++)
        {
            string senderId = $"p-{PatternName}-{i:D3}";
            _senderTasks.Add(Task.Run(() => RunSenderAsync(senderId, client)));
        }

        Console.WriteLine($"queries senders started on {ChannelName}");
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

                // Check for error/timeout
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

                    // Verify response body CRC — should match the original sent body CRC
                    if (resp.Body.Length > 0)
                    {
                        if (Payload.VerifyCrc(resp.Body.Span, encoded.CrcHex))
                        {
                            // Response body integrity verified — record received for RPC round-trip
                            RecordReceive(senderId, resp.Body, encoded.CrcHex,
                                senderId, seq);
                        }
                        else
                        {
                            // Response body corrupted
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
