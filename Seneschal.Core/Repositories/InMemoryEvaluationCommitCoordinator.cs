using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using Seneschal.Core.Exceptions;

namespace Seneschal.Core.Repositories;

public sealed class InMemoryEvaluationCommitCoordinator :
    IEvaluationCommitCoordinator
{
    private readonly InMemoryAuditEventStore _evidenceStore;
    private readonly InMemoryApprovalStore _approvalStore;

    public InMemoryEvaluationCommitCoordinator(
        InMemoryAuditEventStore evidenceStore,
        InMemoryApprovalStore approvalStore)
    {
        _evidenceStore = evidenceStore;
        _approvalStore = approvalStore;
    }

    public Task CommitAsync(
        EvaluationCommit evaluationCommit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evaluationCommit);
        ArgumentNullException.ThrowIfNull(evaluationCommit.Evidence);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            lock (_evidenceStore.SyncRoot)
            {
                lock (_approvalStore.SyncRoot)
                {
                    var pendingEvidence = _evidenceStore.PrepareAppendNoLock(
                        evaluationCommit.Evidence);
                    var pendingApproval = _approvalStore.PrepareMutationNoLock(
                        evaluationCommit.ApprovalMutation);

                    cancellationToken.ThrowIfCancellationRequested();

                    _approvalStore.ApplyMutationNoLock(pendingApproval);
                    _evidenceStore.ApplyAppendNoLock(pendingEvidence);
                }
            }
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
                "Required evaluation effects could not be committed.",
                exception);
        }

        return Task.CompletedTask;
    }
}
