namespace KubeMQ.Sdk.EventsStore;

/// <summary>Result of publishing an event.</summary>
public sealed record EventStoreResult
{
    /// <summary>Gets the event ID.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the event was sent successfully.</summary>
    public bool Sent { get; init; }

    /// <summary>Gets the error message, empty on success.</summary>
    public string Error { get; init; } = string.Empty;
}
