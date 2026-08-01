using Seneschal.Core.Enums;
using Seneschal.Core.Exceptions;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Core.Tests.Repositories;

public abstract class ApprovalStoreContractTests
{
    protected abstract Seneschal.Core.Interfaces.IApprovalStore CreateStore();

    [Fact]
    public void GetOrCreate_ReusesExactPendingScope()
    {
        var store = CreateStore();
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
        var store = CreateStore();
        var first = store.GetOrCreate("person", "deploy", "prod", "api-a", "reason", DateTimeOffset.UtcNow);
        var second = store.GetOrCreate("person", "deploy", "prod", "api-b", "reason", DateTimeOffset.UtcNow);
        Assert.NotEqual(first.Record.Id, second.Record.Id);
    }

    [Theory]
    [InlineData(ApprovalStatus.Approved)]
    [InlineData(ApprovalStatus.Rejected)]
    public void Resolve_RecordsResolution(ApprovalStatus status)
    {
        var store = CreateStore();
        var pending = store.GetOrCreate("person", "deploy", "prod", "api", "reason", DateTimeOffset.UtcNow).Record;
        var resolved = store.Resolve(pending.Id, status, "operator", DateTimeOffset.UtcNow);
        Assert.NotNull(resolved);
        Assert.Equal(status, resolved.Status);
        Assert.Equal("operator", resolved.ResolvedBy);
    }

    [Fact]
    public void Consume_IsSingleUseAndNextLookupCreatesPendingHistory()
    {
        var store = CreateStore();
        var approval = store.GetOrCreate("person", "deploy", "prod", "api", "reason", DateTimeOffset.UtcNow).Record;
        store.Resolve(approval.Id, ApprovalStatus.Approved, "operator", DateTimeOffset.UtcNow);

        var consumed = store.Consume(approval.Id, "decision-1", DateTimeOffset.UtcNow);
        var secondUse = Assert.Throws<Seneschal.Core.Exceptions.ApprovalTransitionException>(
            () => store.Consume(approval.Id, "decision-2", DateTimeOffset.UtcNow));
        var next = store.GetOrCreate("person", "deploy", "prod", "api", "reason", DateTimeOffset.UtcNow);

        Assert.Equal(ApprovalStatus.Consumed, consumed!.Status);
        Assert.Equal("decision-1", consumed.ConsumedByDecisionId);
        Assert.Equal(ApprovalStatus.Consumed, secondUse.CurrentStatus);
        Assert.True(next.Created);
        Assert.Equal(ApprovalStatus.Pending, next.Record.Status);
        Assert.Equal(2, store.GetAll().Count);
    }

    [Fact]
    public void OperationCorrelationSeparatesOperationsAndLegacyContext()
    {
        var store = CreateStore();
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

    [Fact]
    public void GetByIdPendingAndHistoryPreserveLifecycleMetadata()
    {
        var store = CreateStore();
        var firstTime = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
        var secondTime = firstTime.AddMinutes(1);
        var approved = store.GetOrCreate("one", "deploy", "prod", "api", "first", firstTime).Record;
        var pending = store.GetOrCreate("two", "deploy", "prod", "api", "second", secondTime).Record;
        var resolvedAt = secondTime.AddMinutes(1);
        store.Resolve(approved.Id, ApprovalStatus.Approved, "reviewer", resolvedAt);
        var consumedAt = resolvedAt.AddMinutes(1);
        store.Consume(approved.Id, "decision-1", consumedAt);

        Assert.Equal(pending.Id, Assert.Single(store.GetPending()).Id);
        var history = Assert.Single(store.GetHistory());
        Assert.Equal(approved.Id, history.Id);
        Assert.Equal("reviewer", history.ResolvedBy);
        Assert.Equal(resolvedAt, history.ResolvedAt);
        Assert.Equal("decision-1", history.ConsumedByDecisionId);
        Assert.Equal(consumedAt, history.ConsumedAt);
        Assert.Equal(history, store.GetById(approved.Id));
    }

    [Fact]
    public void PendingAndHistoryUseDeterministicNewestFirstOrdering()
    {
        var store = CreateStore();
        var now = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
        var older = store.GetOrCreate("old", "deploy", "prod", "api", "old", now).Record;
        var newer = store.GetOrCreate("new", "deploy", "prod", "api", "new", now.AddMinutes(1)).Record;
        Assert.Equal([newer.Id, older.Id], store.GetPending().Select(item => item.Id));
        store.Resolve(older.Id, ApprovalStatus.Rejected, "reviewer", now.AddMinutes(2));
        store.Resolve(newer.Id, ApprovalStatus.Approved, "reviewer", now.AddMinutes(3));
        Assert.Equal([newer.Id, older.Id], store.GetHistory().Select(item => item.Id));
    }

    [Fact]
    public void InvalidTransitionsFailExplicitly()
    {
        var store = CreateStore();
        var now = DateTimeOffset.UtcNow;
        var rejected = store.GetOrCreate("rejected", "deploy", "prod", "api", "reason", now).Record;
        store.Resolve(rejected.Id, ApprovalStatus.Rejected, "reviewer", now);
        Assert.Throws<ApprovalTransitionException>(() =>
            store.Resolve(rejected.Id, ApprovalStatus.Approved, "reviewer", now));
        Assert.Throws<ApprovalTransitionException>(() =>
            store.Consume(rejected.Id, "decision", now));

        var approved = store.GetOrCreate("approved", "deploy", "prod", "api", "reason", now).Record;
        store.Resolve(approved.Id, ApprovalStatus.Approved, "reviewer", now);
        Assert.Throws<ApprovalTransitionException>(() =>
            store.Resolve(approved.Id, ApprovalStatus.Rejected, "reviewer", now));

        var pending = store.GetOrCreate("pending", "deploy", "prod", "api", "reason", now).Record;
        Assert.Throws<ApprovalTransitionException>(() =>
            store.Consume(pending.Id, "decision", now));
    }

    [Fact]
    public async Task ConcurrentResolutionHasOneWinner()
    {
        var store = CreateStore();
        var approval = store.GetOrCreate("person", "deploy", "prod", "api", "reason", DateTimeOffset.UtcNow).Record;
        var results = await Task.WhenAll(new[] { ApprovalStatus.Approved, ApprovalStatus.Rejected }
            .Select(async status =>
            {
                await Task.Yield();
                try { store.Resolve(approval.Id, status, "reviewer", DateTimeOffset.UtcNow); return true; }
                catch (ApprovalTransitionException) { return false; }
            }));
        Assert.Single(results, result => result);
    }

    [Fact]
    public async Task ConcurrentConsumptionHasOneWinner()
    {
        var store = CreateStore();
        var approval = store.GetOrCreate("person", "deploy", "prod", "api", "reason", DateTimeOffset.UtcNow).Record;
        store.Resolve(approval.Id, ApprovalStatus.Approved, "reviewer", DateTimeOffset.UtcNow);
        var results = await Task.WhenAll(new[] { "decision-1", "decision-2" }
            .Select(async decision =>
            {
                await Task.Yield();
                try { store.Consume(approval.Id, decision, DateTimeOffset.UtcNow); return true; }
                catch (ApprovalTransitionException) { return false; }
            }));
        Assert.Single(results, result => result);
    }
}

public sealed class InMemoryApprovalStoreTests : ApprovalStoreContractTests
{
    protected override Seneschal.Core.Interfaces.IApprovalStore CreateStore() =>
        new InMemoryApprovalStore();
}
