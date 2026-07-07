namespace Seneschal.Client.Models;

/// <summary>
/// Represents a runtime capability decision request.
/// </summary>
public sealed record DecisionRequest
{
    /// <summary>
    /// Gets the identity requesting the capability.
    /// </summary>
    public required string Identity { get; init; }

    /// <summary>
    /// Gets the capability being requested.
    /// </summary>
    public required string Capability { get; init; }

    /// <summary>
    /// Gets additional request context, such as resource or environment.
    /// </summary>
    public Dictionary<string, string> Context { get; init; } = new();
}
