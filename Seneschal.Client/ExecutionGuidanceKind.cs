namespace Seneschal.Client;

/// <summary>
/// Represents execution guidance values understood by this SDK version.
/// </summary>
public enum ExecutionGuidanceKind
{
    /// <summary>The server value is missing or is not understood by this SDK.</summary>
    Unknown,

    /// <summary>The governed action may execute.</summary>
    Proceed,

    /// <summary>LogOnly recorded the result and the governed action may execute.</summary>
    ContinueLogOnly,

    /// <summary>The governed action must not execute.</summary>
    Block,

    /// <summary>The governed action must pause pending another evaluation.</summary>
    Pause,

    /// <summary>The governed action must be queued rather than executed now.</summary>
    Queue,

    /// <summary>The governed action must not execute until another evaluation.</summary>
    Retry
}
