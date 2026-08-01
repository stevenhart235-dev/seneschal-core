using Seneschal.Core.Enums;

namespace Seneschal.Core.Models;

/// <summary>
/// Describes the authoritative effects that must commit atomically for one
/// evaluation. Recomputable projections are intentionally excluded.
/// </summary>
public sealed record EvaluationCommit
{
    public required AuditEvent Evidence { get; init; }

    public ApprovalMutation? ApprovalMutation { get; init; }
}

public sealed record ApprovalMutation
{
    public required ApprovalMutationKind Kind { get; init; }

    public required ApprovalRecord Record { get; init; }

    public ApprovalStatus? ExpectedStatus { get; init; }
}

public enum ApprovalMutationKind
{
    Create,
    Consume
}
