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
}
