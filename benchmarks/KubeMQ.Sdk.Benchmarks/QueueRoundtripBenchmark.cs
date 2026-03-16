using BenchmarkDotNet.Attributes;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;

namespace KubeMQ.Sdk.Benchmarks;

/// <summary>
/// Measures queue send->poll->ack roundtrip latency at 1KB payload (p50, p99).
/// Requires a running KubeMQ server.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class QueueRoundtripBenchmark
{
    private KubeMQClient _client = null!;
    private QueueMessage _message = null!;

    [Params(1024)]
    public int PayloadSize { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        var options = new KubeMQClientOptions
        {
            Address = BenchmarkEnvironment.ServerAddress,
            ClientId = "bench-queue-rt",
        };

        _client = new KubeMQClient(options);
        await _client.ConnectAsync().ConfigureAwait(false);

        var body = new byte[PayloadSize];
        Random.Shared.NextBytes(body);
        _message = new QueueMessage
        {
            Channel = "bench-queue-roundtrip",
            Body = body,
        };
    }

    [Benchmark]
    public async Task SendPollAck()
    {
        await _client.SendQueueMessageAsync(_message).ConfigureAwait(false);

        var poll = new QueuePollRequest
        {
            Channel = "bench-queue-roundtrip",
            MaxMessages = 1,
            WaitTimeoutSeconds = 5,
            AutoAck = true,
        };

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var response = await _client.PollQueueAsync(poll).ConfigureAwait(false);
            if (response.HasMessages)
                return;

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            "Expected at least one message in roundtrip benchmark after 3 poll attempts");
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _client.DisposeAsync().ConfigureAwait(false);
    }
}
