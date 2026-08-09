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

    /// <summary>Unknown, missing, and unsupported guidance fails closed.</summary>
    public static bool ShouldProceed(string? executionGuidance) =>
        string.Equals(executionGuidance, Proceed, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(executionGuidance, ContinueLogOnly, StringComparison.OrdinalIgnoreCase);
}
