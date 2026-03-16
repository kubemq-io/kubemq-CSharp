// Commands worker: RPC request/response via SendCommandAsync + SubscribeToCommandsAsync.
// Responder checks warmup FIRST, then sends response via SendCommandResponseAsync.
// Check resp.Error for timeout detection.

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Commands;

namespace KubeMQ.Burnin.Workers;

/// <summary>
/// Commands pattern worker: synchronous RPC with timeout.
/// Sender uses SendCommandAsync with TimeoutInSeconds.
/// Responder uses SubscribeToCommandsAsync (IAsyncEnumerable&lt;CommandReceived&gt;)
/// and replies via SendCommandResponseAsync.
/// </summary>
public sealed class CommandsWorker : BaseWorker
{
    private const string Sdk = "csharp";
    private const string PatternName = "commands";

    private readonly List<Task> _responderTasks = new();
    private readonly List<Task> _senderTasks = new();

    public CommandsWorker(BurninConfig config, string runId)
        : base(PatternName, config, $"csharp_burnin_{runId}_{PatternName}_001", config.Rates.Commands)
    {
    }

    public override async Task StartConsumersAsync(KubeMQClient client)
    {
        int nResponders = Config.Concurrency.CommandsResponders;

        for (int i = 0; i < nResponders; i++)
        {
            var subscription = new CommandsSubscription
            {
                Channel = ChannelName,
            };

            var task = Task.Run(async () =>
            {
                try
                {
                    await foreach (var cmd in client.SubscribeToCommandsAsync(subscription, ConsumerCts.Token))
                    {
                        if (ConsumerCts.IsCancellationRequested) break;

                        var tags = cmd.Tags;
                        // Go#18: check warmup FIRST
                        bool isWarmup = tags != null
                            && tags.TryGetValue("warmup", out string? warmupVal)
                            && warmupVal == "true";

                        try
                        {
                            // SendCommandResponseAsync with required parameters
                            await client.SendCommandResponseAsync(
                                cmd.RequestId,
                                cmd.ReplyChannel,
                                executed: true,
                                errorMessage: string.Empty,
                                body: isWarmup ? Array.Empty<byte>() : cmd.Body,
                                metadata: string.Empty,
                                tags: null,
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
                    Console.Error.WriteLine($"commands subscription error: {ex.Message}");
                    RecordError("subscription_error");
                    IncReconnection();
                }
            });

            _responderTasks.Add(task);
        }

        Console.WriteLine($"commands responders started on {ChannelName}");
        await Task.CompletedTask;
    }

    public override async Task StartProducersAsync(KubeMQClient client)
    {
        int nSenders = Config.Concurrency.CommandsSenders;
        for (int i = 0; i < nSenders; i++)
        {
            string senderId = $"p-{PatternName}-{i:D3}";
            _senderTasks.Add(Task.Run(() => RunSenderAsync(senderId, client)));
        }

        Console.WriteLine($"commands senders started on {ChannelName}");
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

                var command = new CommandMessage
                {
                    Channel = ChannelName,
                    Body = encoded.Body.ToArray(),
                    TimeoutInSeconds = timeoutSeconds,
                    Tags = new Dictionary<string, string> { ["content_hash"] = encoded.CrcHex },
                };

                var resp = await client.SendCommandAsync(command, ct);

                double rpcDuration = (StopwatchTimestamp.GetNanoseconds() - t0) / 1_000_000_000.0;
                RpcLatencyAccum.Record(rpcDuration);

                // Check for error/timeout in response
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
