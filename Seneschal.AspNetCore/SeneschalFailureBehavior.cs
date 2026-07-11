namespace Seneschal.AspNetCore;

/// <summary>
/// Controls endpoint behavior when Seneschal cannot return a valid decision.
/// </summary>
public enum SeneschalFailureBehavior
{
    /// <summary>
    /// Blocks the governed operation when evaluation fails.
    /// </summary>
    FailClosed,

    /// <summary>
    /// Allows the governed operation when evaluation fails.
    /// </summary>
    FailOpen
}
