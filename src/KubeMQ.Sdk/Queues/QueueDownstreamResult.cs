using System;
using System.Collections.Generic;

namespace KubeMQ.Sdk.Queues;

/// <summary>
/// Result from a downstream queue operation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This type is an immutable record. Instances are safe to read
/// from multiple threads.
/// </para>
/// </remarks>
/// <threadsafety static="true" instance="true"/>
public sealed record QueueDownstreamResult
{
    /// <summary>Gets the server-assigned transaction ID.</summary>
    public string TransactionId { get; init; } = string.Empty;

    /// <summary>Gets the matching request ID.</summary>
    public string RefRequestId { get; init; } = string.Empty;

    /// <summary>Gets the echoed request type.</summary>
    public int RequestTypeData { get; init; }

    /// <summary>Gets the received messages (for Get response).</summary>
    public IReadOnlyList<QueueMessageReceived> Messages { get; init; } = Array.Empty<QueueMessageReceived>();

    /// <summary>Gets the active sequence numbers (for ActiveOffsets response).</summary>
    public IReadOnlyList<long> ActiveOffsets { get; init; } = Array.Empty<long>();

    /// <summary>Gets a value indicating whether an error occurred.</summary>
    public bool IsError { get; init; }

    /// <summary>Gets the error message.</summary>
    public string Error { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the transaction has concluded.</summary>
    public bool TransactionComplete { get; init; }

    /// <summary>Gets the metadata map returned by the server.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
