using Seneschal.Core.Exceptions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Api.Services;

/// <summary>
/// Preserves direct-construction compatibility for callers that supply only an
/// audit store. Runtime composition uses InMemoryEvaluationCommitCoordinator.
/// Approval mutations and non-idempotent audit sinks require a provider-specific
/// atomic coordinator.
/// </summary>
internal sealed class CompatibilityEvaluationCommitCoordinator :
    IEvaluationCommitCoordinator
{
    private readonly IAuditSink _auditSink;

    public CompatibilityEvaluationCommitCoordinator(IAuditSink auditSink)
    {
        _auditSink = auditSink;
    }

    public async Task CommitAsync(
        EvaluationCommit evaluationCommit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evaluationCommit);
        ArgumentNullException.ThrowIfNull(evaluationCommit.Evidence);
        cancellationToken.ThrowIfCancellationRequested();

        if (evaluationCommit.ApprovalMutation is not null)
        {
            throw new EvaluationCommitException(
                "Approval mutations require an atomic evaluation commit coordinator.");
        }

        if (_auditSink is not IAuditEventStore evidenceStore)
        {
            throw new EvaluationCommitException(
                "Required evaluation evidence requires an idempotent audit event store.");
        }

        try
        {
            await evidenceStore.WriteAsync(
                evaluationCommit.Evidence,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (EvaluationCommitException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new EvaluationCommitException(
                "Required evaluation evidence could not be committed.",
                exception);
        }
    }
}
