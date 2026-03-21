namespace KubeMQ.Sdk.Common;

/// <summary>
/// Represents the connection state of a KubeMQ client.
/// </summary>
public enum ConnectionState
{
    /// <summary>The client is not connected.</summary>
    Idle = 0,

    /// <summary>The client is establishing a connection.</summary>
    Connecting = 1,

    /// <summary>The client is connected and ready.</summary>
    Ready = 2,

    /// <summary>The client is reconnecting after a connection loss.</summary>
    Reconnecting = 3,

    /// <summary>The client has been disposed and cannot be reused.</summary>
    Closed = 4,
}
