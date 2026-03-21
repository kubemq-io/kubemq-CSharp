using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Internal.Logging;
using Microsoft.Extensions.Logging;

namespace KubeMQ.Sdk.Internal.Transport;

/// <summary>
/// Manages connection state transitions with thread safety.
/// Uses <see cref="SemaphoreSlim"/> for transitions involving async work
/// and <see cref="Interlocked.CompareExchange(ref int, int, int)"/> for
/// lock-free terminal transitions.
/// <para>
/// This class does NOT fire state-change events. Every call site that performs
/// a transition MUST also raise the event on the owning client.
/// </para>
/// </summary>
internal sealed class StateMachine : IDisposable
{
    private readonly SemaphoreSlim _transitionLock = new(1, 1);
    private readonly ILogger _logger;
    private int _state = (int)ConnectionState.Idle;

    /// <summary>
    /// Initializes a new instance of the <see cref="StateMachine"/> class.
    /// </summary>
    /// <param name="logger">Logger for transition diagnostics.</param>
    internal StateMachine(ILogger logger)
    {
        _logger = logger;
    }

    internal ConnectionState Current =>
        (ConnectionState)Volatile.Read(ref _state);

    /// <summary>
    /// Disposes the transition semaphore.
    /// </summary>
    public void Dispose()
    {
        _transitionLock.Dispose();
    }

    /// <summary>
    /// Acquires the semaphore, verifies <paramref name="from"/> matches current state,
    /// runs optional async work, then atomically moves to <paramref name="to"/>.
    /// </summary>
    /// <param name="from">Expected current state.</param>
    /// <param name="to">Target state.</param>
    /// <param name="onTransition">Optional async work to run during the transition.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the transition succeeded.</returns>
    internal async Task<bool> TransitionAsync(
        ConnectionState from,
        ConnectionState to,
        Func<Task>? onTransition,
        CancellationToken ct)
    {
        await _transitionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var previous = (ConnectionState)_state;
            if (previous != from)
            {
                Log.InvalidTransitionIgnored(_logger, previous, to);
                return false;
            }

            if (onTransition != null)
            {
                await onTransition().ConfigureAwait(false);
            }

            Interlocked.Exchange(ref _state, (int)to);
            return true;
        }
        finally
        {
            _transitionLock.Release();
        }
    }

    /// <summary>
    /// Lock-free compare-and-swap transition. Use ONLY for terminal transitions
    /// or transitions where no async work is needed.
    /// </summary>
    /// <param name="from">Expected current state.</param>
    /// <param name="to">Target state.</param>
    /// <returns>True if the transition succeeded.</returns>
    internal bool TryTransition(ConnectionState from, ConnectionState to)
    {
        int result = Interlocked.CompareExchange(ref _state, (int)to, (int)from);
        if (result == (int)from)
        {
            return true;
        }

        Log.InvalidTransitionIgnored(_logger, (ConnectionState)result, to);
        return false;
    }

    /// <summary>
    /// Forces the state to <see cref="ConnectionState.Closed"/> regardless of current state.
    /// </summary>
    /// <returns>The state before the forced transition.</returns>
    internal ConnectionState ForceDisposed()
    {
        int previous = Interlocked.Exchange(ref _state, (int)ConnectionState.Closed);
        return (ConnectionState)previous;
    }
}
