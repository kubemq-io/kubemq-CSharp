using BenchmarkDotNet.Attributes;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;

namespace KubeMQ.Sdk.Benchmarks;

/// <summary>
/// Measures per-message publish latency at 1KB payload (p50, p99).
/// Requires a running KubeMQ server.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class PublishLatencyBenchmark
{
    private KubeMQClient _client = null!;
    private EventMessage _message = null!;

    [Params(1024)]
    public int PayloadSize { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        var options = new KubeMQClientOptions
        {
            Address = BenchmarkEnvironment.ServerAddress,
            ClientId = "bench-latency",
        };

        _client = new KubeMQClient(options);
        await _client.ConnectAsync().ConfigureAwait(false);

        var body = new byte[PayloadSize];
        Random.Shared.NextBytes(body);
        _message = new EventMessage
        {
            Channel = "bench-latency",
            Body = body,
        };
    }

    [Benchmark]
    public async Task PublishSingleEvent()
    {
        await _client.PublishEventAsync(_message).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _client.DisposeAsync().ConfigureAwait(false);
    }
}
