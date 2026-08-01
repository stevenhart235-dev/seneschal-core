using Seneschal.Core.Enums;
using Seneschal.Core.Exceptions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Core.Tests.Repositories;

/// <summary>
/// Provider-neutral behavioral suite. Future evidence-store providers should
/// derive from this class and supply fresh and failing stores.
/// </summary>
public abstract class AuditEventStoreContractTests
{
    protected abstract IAuditEventStore CreateStore();

    protected abstract IAuditEventStore CreateFailingStore(Exception failure);

    [Fact]
    public async Task AppendAndRetrieve_PreservesCommittedEvidence()
    {
        var store = CreateStore();
        var evidence = CreateEvent("decision-1", Timestamp, "Allowed.");

        await store.WriteAsync(evidence);

        var committed = await store.GetByIdAsync(evidence.Id);
        Assert.NotNull(committed);
        Assert.Equal(evidence.Id, committed.Id);
        Assert.Equal(evidence.RequestedAction, committed.RequestedAction);
        Assert.Equal(evidence.RequestContext, committed.RequestContext);
        Assert.Equal(evidence.EffectiveAction, committed.EffectiveAction);
    }

    [Fact]
    public async Task Ordering_IsNewestFirstAndStableForEqualTimestamps()
    {
        var store = CreateStore();

        await store.WriteAsync(CreateEvent("same-first", Timestamp, "First."));
        await store.WriteAsync(CreateEvent("older", Timestamp.AddMinutes(-1), "Older."));
        await store.WriteAsync(CreateEvent("same-second", Timestamp, "Second."));

        var committed = await store.GetRecentAsync();

        Assert.Equal(
            ["same-first", "same-second", "older"],
            committed.Select(item => item.Id));
    }

    [Fact]
    public async Task IdenticalDuplicate_IsIdempotent()
    {
        var store = CreateStore();
        var evidence = CreateEvent("decision-1", Timestamp, "Allowed.");

        await store.WriteAsync(evidence);
        await store.WriteAsync(evidence);

        Assert.Single(await store.GetRecentAsync());
    }

    [Fact]
    public async Task ConflictingDuplicate_FailsExplicitly()
    {
        var store = CreateStore();
        await store.WriteAsync(CreateEvent("decision-1", Timestamp, "Allowed."));

        var exception = await Assert.ThrowsAsync<EvaluationEvidenceConflictException>(
            () => store.WriteAsync(
                CreateEvent("decision-1", Timestamp, "Different reason.")));

        Assert.Equal("decision-1", exception.EvidenceId);
        Assert.Single(await store.GetRecentAsync());
    }

    [Fact]
    public async Task MissingRecord_ReturnsNull()
    {
        Assert.Null(await CreateStore().GetByIdAsync("missing"));
    }

    [Fact]
    public async Task CommittedEvidence_IsDefensivelyImmutable()
    {
        var store = CreateStore();
        var evidence = CreateEvent("decision-1", Timestamp, "Allowed.");
        await store.WriteAsync(evidence);

        evidence.MatchedPolicies.Add("mutated-original");
        evidence.RequestContext["mutated"] = "original";
        var firstRead = (await store.GetByIdAsync(evidence.Id))!;
        firstRead.MatchedPolicies.Add("mutated-read");
        firstRead.RequestContext["mutated"] = "read";

        var secondRead = (await store.GetByIdAsync(evidence.Id))!;
        Assert.Equal(["policy-1"], secondRead.MatchedPolicies);
        Assert.False(secondRead.RequestContext.ContainsKey("mutated"));
    }

    [Fact]
    public async Task ProviderFailure_PropagatesToCaller()
    {
        var failure = new InvalidOperationException("Provider failed.");
        var store = CreateFailingStore(failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.WriteAsync(
                CreateEvent("decision-1", Timestamp, "Allowed.")));

        Assert.Same(failure, actual);
    }

    [Fact]
    public async Task Cancellation_PreventsAppendAndRead()
    {
        var store = CreateStore();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.WriteAsync(
                CreateEvent("decision-1", Timestamp, "Allowed."),
                cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.GetRecentAsync(
                cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.GetByIdAsync("decision-1", cancellation.Token));
    }

    protected static AuditEvent CreateEvent(
        string id,
        DateTimeOffset timestamp,
        string reason)
    {
        return new AuditEvent
        {
            Id = id,
            RequestId = $"request-{id}",
            TimestampUtc = timestamp,
            IdentityId = "Developer",
            CapabilityId = "DeployApplication",
            RequestedAction = "DeployApplication",
            RequestContext = new Dictionary<string, string>
            {
                ["environment"] = "dev",
                ["resource"] = "payment-api"
            },
            ResourceId = "payment-api",
            Environment = "dev",
            Decision = DecisionType.Allow,
            EnforcementMode = EnforcementMode.LogOnly,
            EffectiveAction = "allow",
            MatchedPolicies = ["policy-1"],
            Obligations = ["audit"],
            Reason = reason,
            EvaluationDurationMs = 1
        };
    }

    private static readonly DateTimeOffset Timestamp =
        new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
}

public sealed class InMemoryAuditEventStoreContractTests :
    AuditEventStoreContractTests
{
    protected override IAuditEventStore CreateStore() =>
        new InMemoryAuditEventStore();

    protected override IAuditEventStore CreateFailingStore(Exception failure) =>
        new InMemoryAuditEventStore(_ => failure);
}
