namespace Seneschal.Core.Exceptions;

public sealed class EvaluationEvidenceConflictException : EvaluationCommitException
{
    public EvaluationEvidenceConflictException(string evidenceId)
        : base(
            $"Evaluation evidence '{evidenceId}' is already committed with different content.")
    {
        EvidenceId = evidenceId;
    }

    public string EvidenceId { get; }
}
