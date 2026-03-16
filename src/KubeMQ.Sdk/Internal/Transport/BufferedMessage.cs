namespace KubeMQ.Sdk.Internal.Transport;

/// <summary>
/// Serialized message waiting for replay after reconnection.
/// </summary>
internal readonly record struct BufferedMessage(
    byte[] Payload,
    string Channel,
    string OperationType,
    int EstimatedSizeBytes);
