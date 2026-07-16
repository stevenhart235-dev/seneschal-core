using Xunit;

namespace Seneschal.Api.Tests;

public sealed class ApprovalWorkerBehaviorTests
{
    [Fact]
    public void WorkerKeepsOperationIdStableUntilAllowCompletesOperation()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "labs", "multi-application-adoption", "ApprovalWorker", "Program.cs"));
        var source = File.ReadAllText(path);

        Assert.Contains("var operationId = OperationId(operationSequence);", source);
        Assert.Contains("OperationId = operationId", source);
        Assert.Contains("if (allowed)", source);
        Assert.Contains("operationSequence++;", source);
        Assert.Contains("operationId = OperationId(operationSequence);", source);
        Assert.DoesNotContain("Guid.NewGuid", source);
    }
}
