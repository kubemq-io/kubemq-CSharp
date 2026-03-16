using System;
using System.Collections.Generic;

namespace KubeMQ.Sdk.Queries;

/// <summary>
/// Response to a query execution, containing the result data.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This type is an immutable record. Instances are safe to read
/// from multiple threads.
/// </para>
/// </remarks>
/// <threadsafety static="true" instance="true"/>
public record QueryResponse
{
    /// <summary>Gets the correlation ID matching the original query's request ID.</summary>
    public required string RequestId { get; init; }

    /// <summary>Gets a value indicating whether the query was executed successfully by the handler.</summary>
    public bool Executed { get; init; }

    /// <summary>Gets the response payload from the handler.</summary>
    public ReadOnlyMemory<byte> Body { get; init; }

    /// <summary>Gets the optional key-value metadata in the response.</summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }

    /// <summary>Gets the server timestamp of the response.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets the error message if execution failed.</summary>
    public string? Error { get; init; }

    /// <summary>Gets a value indicating whether this response was served from the server cache.</summary>
    public bool CacheHit { get; init; }
}
