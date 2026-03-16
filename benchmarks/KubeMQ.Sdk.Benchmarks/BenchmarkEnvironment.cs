namespace KubeMQ.Sdk.Benchmarks;

/// <summary>
/// Reads benchmark configuration from environment variables.
/// </summary>
internal static class BenchmarkEnvironment
{
    public static string ServerAddress
    {
        get
        {
            var addr = Environment.GetEnvironmentVariable("KUBEMQ_BENCH_ADDRESS")
                ?? "localhost:50000";
            if (string.IsNullOrWhiteSpace(addr) || !addr.Contains(':'))
                throw new InvalidOperationException(
                    $"KUBEMQ_BENCH_ADDRESS must be in 'host:port' format, got '{addr}'");
            return addr;
        }
    }
}
