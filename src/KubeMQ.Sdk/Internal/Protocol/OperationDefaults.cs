namespace KubeMQ.Sdk.Internal.Protocol;

/// <summary>
/// Default timeout values for SDK operations.
/// </summary>
internal static class OperationDefaults
{
    /// <summary>Default timeout for send/publish operations.</summary>
    internal static readonly TimeSpan SendPublishTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Default timeout for initial subscription establishment.</summary>
    internal static readonly TimeSpan SubscribeInitialTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Default timeout for command/query RPC operations.</summary>
    internal static readonly TimeSpan RpcTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Default timeout for single queue message receive.</summary>
    internal static readonly TimeSpan QueueReceiveSingleTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Default timeout for queue receive stream operations.</summary>
    internal static readonly TimeSpan QueueReceiveStreamTimeout = TimeSpan.FromSeconds(30);
}
