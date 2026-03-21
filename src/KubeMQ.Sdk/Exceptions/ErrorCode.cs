namespace KubeMQ.Sdk.Exceptions;

/// <summary>
/// Machine-readable error codes for all SDK errors.
/// New codes may be added in minor versions.
/// Existing codes are never removed or changed in meaning within a major version.
/// </summary>
public enum ErrorCode
{
    /// <summary>Unknown or unclassified error.</summary>
    Unknown = 0,

    /// <summary>Connection to the server was refused.</summary>
    ConnectionRefused,

    /// <summary>Authentication credentials were rejected.</summary>
    AuthenticationFailed,

    /// <summary>Authorization was denied for the requested operation.</summary>
    AuthorizationDenied,

    /// <summary>Operation exceeded its deadline.</summary>
    DeadlineExceeded,

    /// <summary>Requested resource was not found.</summary>
    NotFound,

    /// <summary>Resource already exists.</summary>
    AlreadyExists,

    /// <summary>Caller does not have permission.</summary>
    PermissionDenied,

    /// <summary>Server resource limits exceeded (rate limiting).</summary>
    ResourceExhausted,

    /// <summary>Operation precondition was not met.</summary>
    FailedPrecondition,

    /// <summary>Operation was aborted due to a transient conflict.</summary>
    Aborted,

    /// <summary>Value is out of acceptable range.</summary>
    OutOfRange,

    /// <summary>Operation is not supported by the server.</summary>
    Unimplemented,

    /// <summary>Internal server error.</summary>
    Internal,

    /// <summary>Server is temporarily unavailable.</summary>
    Unavailable,

    /// <summary>Unrecoverable data loss.</summary>
    DataLoss,

    /// <summary>Operation was cancelled.</summary>
    Cancelled,

    /// <summary>Invalid argument provided to the operation.</summary>
    InvalidArgument,

    /// <summary>Reconnect message buffer is full.</summary>
    BufferFull,

    /// <summary>All retry attempts have been exhausted.</summary>
    RetryExhausted,

    /// <summary>Bidirectional stream has broken.</summary>
    StreamBroken,

    /// <summary>Client configuration is invalid.</summary>
    ConfigurationInvalid,
}
