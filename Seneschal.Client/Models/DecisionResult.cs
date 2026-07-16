namespace Seneschal.Client.Models;

/// <summary>
/// Represents the decision returned by the Seneschal runtime.
/// </summary>
public sealed record DecisionResult
{
    /// <summary>
    /// Gets the resolved decision.
    /// </summary>
    public string Decision { get; init; } = "";

    /// <summary>
    /// Gets the human-readable reason for the decision.
    /// </summary>
    public string Reason { get; init; } = "";

    /// <summary>
    /// Gets the primary matched policy name or identifier.
    /// </summary>
    public string PolicyMatched { get; init; } = "";

    /// <summary>
    /// Gets the evaluation duration in milliseconds.
    /// </summary>
    public long DurationMs { get; init; }

    /// <summary>
    /// Gets the effective action after enforcement-mode projection.
    /// </summary>
    public string EffectiveAction { get; init; } = "";

    /// <summary>
    /// Gets the enforcement mode returned by Seneschal.
    /// </summary>
    public string Mode { get; init; } = "";

    /// <summary>
    /// Gets the caller-oriented guidance. Seneschal does not execute it.
    /// </summary>
    public string ExecutionGuidance { get; init; } = "";

    /// <summary>Gets the related approval identifier, when applicable.</summary>
    public string? ApprovalId { get; init; }

    /// <summary>Gets the approval lifecycle status, when applicable.</summary>
    public string? ApprovalStatus { get; init; }

    /// <summary>Gets the caller-owned operation identifier.</summary>
    public string? OperationId { get; init; }

    /// <summary>Gets whether approval correlation used Operation or LegacyContext.</summary>
    public string? ApprovalCorrelationMode { get; init; }

    /// <summary>Gets a human-readable caller message, when applicable.</summary>
    public string? Message { get; init; }

    /// <summary>Gets descriptive retry guidance, when applicable.</summary>
    public string? RetryGuidance { get; init; }

    /// <summary>
    /// Gets matched policies when returned by a runtime endpoint.
    /// </summary>
    public List<string> MatchedPolicies { get; init; } = new();

    /// <summary>
    /// Gets obligations attached to the decision.
    /// </summary>
    public List<string> Obligations { get; init; } = new();

    /// <summary>
    /// Gets a future correlation identifier, when returned.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets a future audit event identifier, when returned.
    /// </summary>
    public string? EventId { get; init; }

    /// <summary>
    /// Gets whether application code should proceed with the governed action.
    /// </summary>
    public bool ShouldProceed =>
        string.Equals(Mode, "LogOnly", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Mode, "Monitor", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Decision, "allow", StringComparison.OrdinalIgnoreCase);
}
