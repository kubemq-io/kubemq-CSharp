using BenchmarkDotNet.Attributes;
using KubeMQ.Sdk.Config;

namespace KubeMQ.Sdk.Benchmarks;

/// <summary>
/// Benchmarks the retry backoff delay calculation to verify it adds negligible overhead
/// to the retry hot path. Does NOT require a running KubeMQ server.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class RetryPolicyBenchmarks
{
    private RetryPolicy _policy = null!;

    [Params(1, 3, 5)]
    public int Attempt { get; set; }

    [Params(JitterMode.None, JitterMode.Full)]
    public JitterMode Jitter { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _policy = new RetryPolicy
        {
            Enabled = true,
            MaxRetries = 5,
            InitialBackoff = TimeSpan.FromMilliseconds(500),
            MaxBackoff = TimeSpan.FromSeconds(30),
            BackoffMultiplier = 2.0,
            JitterMode = Jitter,
        };
    }

    [Benchmark]
    public TimeSpan CalculateDelay()
    {
        return CalculateBackoffDelay(_policy, Attempt);
    }

    [Benchmark]
    public void ValidatePolicy()
    {
        _policy.Validate();
    }

    /// <summary>
    /// Mirrors the delay calculation from RetryHandler.CalculateDelay (internal)
    /// to benchmark the pure math without needing InternalsVisibleTo access.
    /// </summary>
    private static TimeSpan CalculateBackoffDelay(RetryPolicy policy, int attempt)
    {
        double baseMs = policy.InitialBackoff.TotalMilliseconds;
        double maxMs = policy.MaxBackoff.TotalMilliseconds;
        double exponential = Math.Min(maxMs, baseMs * Math.Pow(policy.BackoffMultiplier, attempt - 1));

        return policy.JitterMode switch
        {
            JitterMode.Full => TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * exponential),
            JitterMode.Equal => TimeSpan.FromMilliseconds(
                (exponential / 2.0) + (Random.Shared.NextDouble() * (exponential / 2.0))),
            JitterMode.None => TimeSpan.FromMilliseconds(exponential),
            _ => TimeSpan.FromMilliseconds(exponential),
        };
    }
}
