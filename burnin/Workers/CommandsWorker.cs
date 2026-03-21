// Commands worker: RPC request/response via SendCommandAsync + SubscribeToCommandsAsync.
// v2: accepts channelName, rate, channelIndex, patternLatencyAccum.
// Worker IDs: {role}-{pattern}-{4-digit-channel}-{3-digit-worker}

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
    private readonly int _numSenders;
    private readonly int _numResponders;

    public CommandsWorker(BurninConfig config, string runId, string channelName, int channelIndex,
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
                        bool isWarmup = tags != null
                            && tags.TryGetValue("warmup", out string? warmupVal)
                            && warmupVal == "true";

                        if (!isWarmup)
                        {
                            RecordBytesReceived(cmd.Body.Length);
                        }

                        var capturedCmd = cmd;
                        var capturedIsWarmup = isWarmup;
                        _ = client.SendCommandResponseAsync(new CommandResponse
                        {
                            RequestId = capturedCmd.RequestId,
                            ReplyChannel = capturedCmd.ReplyChannel,
                            Executed = true,
                            Body = capturedIsWarmup ? Array.Empty<byte>() : capturedCmd.Body,
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
                    Console.Error.WriteLine($"commands subscription error (ch{ChannelIndex:D4}): {ex.Message}");
                    RecordError("subscription_error");
                    IncReconnection();
                }
            });

            _responderTasks.Add(task);
        }

        Console.WriteLine($"commands responders started on {ChannelName} ({_numResponders} responders)");
        await Task.CompletedTask;
    }

    public override async Task StartProducersAsync(KubeMQClient client)
    {
        for (int i = 0; i < _numSenders; i++)
        {
            string senderId = $"s-{PatternName}-{ChannelIndex:D4}-{i:D3}";
            _senderTasks.Add(Task.Run(() => RunSenderAsync(senderId, client)));
        }

        Console.WriteLine($"commands senders started on {ChannelName} ({_numSenders} senders)");
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
