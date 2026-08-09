namespace Seneschal.Client;

/// <summary>
/// Defines the canonical interpretation of execution guidance returned by
/// Seneschal.
/// </summary>
public static class ExecutionGuidanceContract
{
    /// <summary>Guidance indicating that the governed action may execute.</summary>
    public const string Proceed = "Proceed";

    /// <summary>
    /// Guidance indicating that LogOnly recorded the result and the governed
    /// action may execute.
    /// </summary>
    public const string ContinueLogOnly = "ContinueLogOnly";

    /// <summary>Parses a raw server value without rejecting future values.</summary>
    public static ExecutionGuidanceKind Parse(string? executionGuidance)
    {
        if (string.Equals(
                executionGuidance,
                Proceed,
                StringComparison.OrdinalIgnoreCase))
        {
            return ExecutionGuidanceKind.Proceed;
        }

        if (string.Equals(
                executionGuidance,
                ContinueLogOnly,
                StringComparison.OrdinalIgnoreCase))
        {
            return ExecutionGuidanceKind.ContinueLogOnly;
        }

        if (string.Equals(executionGuidance, "Block", StringComparison.OrdinalIgnoreCase))
            return ExecutionGuidanceKind.Block;
        if (string.Equals(executionGuidance, "Pause", StringComparison.OrdinalIgnoreCase))
            return ExecutionGuidanceKind.Pause;
        if (string.Equals(executionGuidance, "Queue", StringComparison.OrdinalIgnoreCase))
            return ExecutionGuidanceKind.Queue;
        if (string.Equals(executionGuidance, "Retry", StringComparison.OrdinalIgnoreCase))
            return ExecutionGuidanceKind.Retry;

        return ExecutionGuidanceKind.Unknown;
    }

    /// <summary>Unknown, missing, and unsupported guidance fails closed.</summary>
    public static bool ShouldProceed(ExecutionGuidanceKind guidance) =>
        guidance is ExecutionGuidanceKind.Proceed or
            ExecutionGuidanceKind.ContinueLogOnly;

    /// <summary>
    /// Returns whether a raw server value permits execution. This overload is
    /// retained for compatibility and delegates to the typed contract.
    /// </summary>
    public static bool ShouldProceed(string? executionGuidance) =>
        ShouldProceed(Parse(executionGuidance));
}
