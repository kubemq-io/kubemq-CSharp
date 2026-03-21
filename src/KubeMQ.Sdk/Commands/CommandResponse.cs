using System;
using System.Collections.Generic;

namespace KubeMQ.Sdk.Commands;

/// <summary>
/// Response to a command execution.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This type is an immutable record. Instances are safe to read
/// from multiple threads.
/// </para>
/// </remarks>
/// <threadsafety static="true" instance="true"/>
public record CommandResponse
{
    /// <summary>Gets the correlation ID matching the original command's request ID.</summary>
    public required string RequestId { get; init; }

    /// <summary>Gets the reply channel for sending the response back to the command sender.</summary>
    public string? ReplyChannel { get; init; }

    /// <summary>Gets a value indicating whether the command was executed successfully by the handler.</summary>
    public bool Executed { get; init; }

    /// <summary>Gets the server timestamp of the response.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets the error message if execution failed.</summary>
    public string? Error { get; init; }

    /// <summary>Gets the optional response payload body.</summary>
    public ReadOnlyMemory<byte> Body { get; init; }

    /// <summary>Gets the optional response metadata.</summary>
    public string? Metadata { get; init; }

    /// <summary>Gets the optional response key-value tags.</summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
}
