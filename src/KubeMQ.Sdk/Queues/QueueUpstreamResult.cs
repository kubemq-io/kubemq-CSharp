using System;
using System.Collections.Generic;

namespace KubeMQ.Sdk.Queues;

/// <summary>
/// Result from sending messages via the upstream stream.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This type is an immutable record. Instances are safe to read
/// from multiple threads.
/// </para>
/// </remarks>
/// <threadsafety static="true" instance="true"/>
public sealed record QueueUpstreamResult
{
    /// <summary>Gets the matching request ID.</summary>
    public string RefRequestId { get; init; } = string.Empty;

    /// <summary>Gets the per-message send results.</summary>
    public IReadOnlyList<QueueSendResult> Results { get; init; } = Array.Empty<QueueSendResult>();

    /// <summary>Gets a value indicating whether any error occurred.</summary>
    public bool IsError { get; init; }

    /// <summary>Gets the error message.</summary>
    public string Error { get; init; } = string.Empty;
}
