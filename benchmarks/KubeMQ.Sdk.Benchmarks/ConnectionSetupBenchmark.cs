using BenchmarkDotNet.Attributes;
using KubeMQ.Sdk.Client;

namespace KubeMQ.Sdk.Benchmarks;

/// <summary>
/// Measures connection setup time: construction -> connect -> first successful ping.
/// Requires a running KubeMQ server.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class ConnectionSetupBenchmark
{
    [Benchmark]
    public async Task ConnectAndPing()
    {
        var options = new KubeMQClientOptions
        {
            Address = BenchmarkEnvironment.ServerAddress,
            ClientId = "bench-connect",
        };

        var client = new KubeMQClient(options);
        try
        {
            await client.ConnectAsync().ConfigureAwait(false);
            await client.PingAsync().ConfigureAwait(false);
        }
        finally
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
    }
}
