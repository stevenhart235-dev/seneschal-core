namespace Seneschal.Core.Exceptions;

public class EvaluationCommitException : Exception
{
    public EvaluationCommitException(string message)
        : base(message)
    {
    }

    public EvaluationCommitException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
