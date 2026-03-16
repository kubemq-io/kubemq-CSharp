using System.Collections.Generic;

namespace KubeMQ.Sdk.Queues;

/// <summary>
/// Response from a queue poll operation containing zero or more messages.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This type is an immutable record. Instances are safe to read
/// from multiple threads.
/// </para>
/// </remarks>
/// <threadsafety static="true" instance="true"/>
public record QueuePollResponse
{
    /// <summary>Gets the messages received from the queue.</summary>
    public required IReadOnlyList<QueueMessageReceived> Messages { get; init; }

    /// <summary>Gets a value indicating whether any messages were returned.</summary>
    public bool HasMessages => Messages.Count > 0;

    /// <summary>Gets the error message from the server, if any.</summary>
    public string? Error { get; init; }
}
