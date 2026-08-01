using Seneschal.Core.Models;

namespace Seneschal.Core.Interfaces;

/// <summary>
/// Commits the authoritative evidence and approval effect for one evaluation.
/// Implementations must commit all supplied effects atomically or commit none.
/// </summary>
public interface IEvaluationCommitCoordinator
{
    Task CommitAsync(
        EvaluationCommit evaluationCommit,
        CancellationToken cancellationToken = default);
}
