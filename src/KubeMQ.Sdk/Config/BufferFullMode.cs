namespace KubeMQ.Sdk.Config;

/// <summary>
/// Determines behavior when the reconnect message buffer is full.
/// </summary>
public enum BufferFullMode
{
    /// <summary>Block the caller until buffer space is available.</summary>
    Block,

    /// <summary>Throw <see cref="Exceptions.KubeMQBufferFullException"/> immediately.</summary>
    Error,
}
