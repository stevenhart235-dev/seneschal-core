using Seneschal.Core.Enums;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Core.Tests.Repositories;

public sealed class InMemoryApprovalStoreTests
{
    [Fact]
    public void GetOrCreate_ReusesExactPendingScope()
    {
        var store = new InMemoryApprovalStore();
        var first = store.GetOrCreate("person", "deploy", "prod", "api", "reason", DateTimeOffset.UtcNow);
        var second = store.GetOrCreate("person", "deploy", "prod", "api", "other", DateTimeOffset.UtcNow);
        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.Record.Id, second.Record.Id);
        Assert.Single(store.GetAll());
    }

    [Fact]
    public void GetOrCreate_DifferentResourceCreatesDifferentApproval()
    {
        var store = new InMemoryApprovalStore();
        var first = store.GetOrCreate("person", "deploy", "prod", "api-a", "reason", DateTimeOffset.UtcNow);
        var second = store.GetOrCreate("person", "deploy", "prod", "api-b", "reason", DateTimeOffset.UtcNow);
        Assert.NotEqual(first.Record.Id, second.Record.Id);
    }

    [Theory]
    [InlineData(ApprovalStatus.Approved)]
    [InlineData(ApprovalStatus.Rejected)]
    public void Resolve_RecordsResolution(ApprovalStatus status)
    {
        var store = new InMemoryApprovalStore();
        var pending = store.GetOrCreate("person", "deploy", "prod", "api", "reason", DateTimeOffset.UtcNow).Record;
        var resolved = store.Resolve(pending.Id, status, "operator", DateTimeOffset.UtcNow);
        Assert.NotNull(resolved);
        Assert.Equal(status, resolved.Status);
        Assert.Equal("operator", resolved.ResolvedBy);
    }

    [Fact]
    public void Consume_IsSingleUseAndNextLookupCreatesPendingHistory()
    {
        var store = new InMemoryApprovalStore();
        var approval = store.GetOrCreate("person", "deploy", "prod", "api", "reason", DateTimeOffset.UtcNow).Record;
        store.Resolve(approval.Id, ApprovalStatus.Approved, "operator", DateTimeOffset.UtcNow);

        var consumed = store.Consume(approval.Id, "decision-1", DateTimeOffset.UtcNow);
        var secondUse = store.Consume(approval.Id, "decision-2", DateTimeOffset.UtcNow);
        var next = store.GetOrCreate("person", "deploy", "prod", "api", "reason", DateTimeOffset.UtcNow);

        Assert.Equal(ApprovalStatus.Consumed, consumed!.Status);
        Assert.Equal("decision-1", consumed.ConsumedByDecisionId);
        Assert.Null(secondUse);
        Assert.True(next.Created);
        Assert.Equal(ApprovalStatus.Pending, next.Record.Status);
        Assert.Equal(2, store.GetAll().Count);
    }

    [Fact]
    public void OperationCorrelationSeparatesOperationsAndLegacyContext()
    {
        var store = new InMemoryApprovalStore();
        var now = DateTimeOffset.UtcNow;
        var first = store.GetOrCreate("person", "deploy", "prod", "api", "reason", now, "release-001");
        var retry = store.GetOrCreate("person", "deploy", "prod", "api", "reason", now, "release-001");
        var second = store.GetOrCreate("person", "deploy", "prod", "api", "reason", now, "release-002");
        var legacy = store.GetOrCreate("person", "deploy", "prod", "api", "reason", now);

        Assert.Equal(first.Record.Id, retry.Record.Id);
        Assert.NotEqual(first.Record.Id, second.Record.Id);
        Assert.NotEqual(first.Record.Id, legacy.Record.Id);
        Assert.Equal(ApprovalCorrelationMode.Operation, first.Record.CorrelationMode);
        Assert.Equal(ApprovalCorrelationMode.LegacyContext, legacy.Record.CorrelationMode);
    }
}
