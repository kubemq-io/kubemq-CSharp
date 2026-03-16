// Forced disconnect manager: close client, wait configured duration, recreate.
// Increments forced_disconnects counter on each disconnect cycle.

namespace KubeMQ.Burnin;

/// <summary>
/// Interface for objects that can close and recreate the KubeMQ client connection.
/// </summary>
public interface IClientRecreator
{
    /// <summary>
    /// Close the current client connection. Should not throw.
    /// </summary>
    Task CloseClientAsync();

    /// <summary>
    /// Recreate and reconnect the client. Should not throw.
    /// </summary>
    Task RecreateClientAsync();
}

/// <summary>
/// Manages forced disconnections at a configurable interval and duration.
/// Calls CloseClientAsync, waits the configured duration, then calls RecreateClientAsync.
/// Increments the burnin_forced_disconnects_total counter on each cycle.
/// </summary>
public sealed class DisconnectManager : IAsyncDisposable
{
    private readonly double _intervalSec;
    private readonly double _durationSec;
    private readonly IClientRecreator _recreator;
    private CancellationTokenSource? _cts;
    private Task? _runTask;

    /// <summary>
    /// Create a disconnect manager.
    /// </summary>
    /// <param name="intervalSec">Seconds between forced disconnections. 0 = disabled.</param>
    /// <param name="durationSec">Seconds to remain disconnected.</param>
    /// <param name="recreator">The client recreator to call during disconnect cycles.</param>
    public DisconnectManager(double intervalSec, double durationSec, IClientRecreator recreator)
    {
        _intervalSec = intervalSec;
        _durationSec = durationSec;
        _recreator = recreator;
    }

    /// <summary>
    /// Whether forced disconnection is enabled (interval > 0).
    /// </summary>
    public bool Enabled => _intervalSec > 0;

    /// <summary>
    /// Start the disconnect cycle loop. Does nothing if not enabled.
    /// </summary>
    public void Start()
    {
        if (!Enabled) return;
        _cts = new CancellationTokenSource();
        _runTask = RunLoopAsync(_cts.Token);
    }

    /// <summary>
    /// Stop the disconnect cycle loop gracefully.
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
    }

    public async ValueTask DisposeAsync()
    {
        Stop();
        if (_runTask is not null)
        {
            try { await _runTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        _cts?.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Wait for the configured interval before disconnecting.
                await Task.Delay(TimeSpan.FromSeconds(_intervalSec), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (ct.IsCancellationRequested) break;

            await DisconnectCycleAsync(ct).ConfigureAwait(false);
        }
    }

    private async Task DisconnectCycleAsync(CancellationToken ct)
    {
        Console.WriteLine("forced disconnect: closing client");
        Metrics.IncForcedDisconnects();

        try
        {
            await _recreator.CloseClientAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"disconnect close error: {ex.Message}");
        }

        // Wait the configured disconnect duration.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(_durationSec), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (ct.IsCancellationRequested) return;

        Console.WriteLine("forced disconnect: recreating client");

        try
        {
            await _recreator.RecreateClientAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"disconnect recreate error: {ex.Message}");
        }
    }
}
