using Seneschal.Core.Enums;
using Seneschal.Core.Exceptions;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Core.Tests.Repositories;

public sealed class InMemoryGovernanceIncidentStoreTests
{
    [Fact]
    public void StableKey_NormalizesCurrentGroupingFields()
    {
        var first = GovernanceIncidentKey.Create(
            " Deploy ", "Worker", " Denied ", "Policy-A");
        var second = GovernanceIncidentKey.Create(
            "deploy", " worker ", "denied", "policy-a");

        Assert.Equal(first, second);
        Assert.StartsWith("incident-", first);
        Assert.Equal(73, first.Length);
    }

    [Theory]
    [InlineData("other", "worker", "denied", "policy")]
    [InlineData("deploy", "other", "denied", "policy")]
    [InlineData("deploy", "worker", "other", "policy")]
    [InlineData("deploy", "worker", "denied", "other")]
    public void StableKey_DistinguishesDifferentGroupingFields(
        string capability, string identity, string reason, string policy)
    {
        var baseline = GovernanceIncidentKey.Create(
            "deploy", "worker", "denied", "policy");

        Assert.NotEqual(baseline, GovernanceIncidentKey.Create(
            capability, identity, reason, policy));
    }

    [Fact]
    public async Task ProjectionRefresh_PreservesOperatorState()
    {
        var store = new InMemoryGovernanceIncidentStore();
        await store.RecordAsync(Evidence("first", DateTimeOffset.UtcNow));
        var incident = Assert.Single(await store.GetAllAsync());
        Assert.True(await store.AcknowledgeAsync(incident.Id));

        await store.RecordAsync(Evidence("second", DateTimeOffset.UtcNow.AddMinutes(1)));
        var refreshed = Assert.Single(await store.GetAllAsync());

        Assert.Equal(incident.Id, refreshed.Id);
        Assert.Equal(2, refreshed.OccurrenceCount);
        Assert.Equal(GovernanceIncidentStatus.Acknowledged, refreshed.CurrentStatus);
        Assert.Equal(1, refreshed.OperatorStateVersion);
    }

    [Fact]
    public async Task CurrentLifecycleTransitions_AcceptOnlySupportedChanges()
    {
        var store = new InMemoryGovernanceIncidentStore();
        await store.RecordAsync(Evidence("lifecycle", DateTimeOffset.UtcNow));
        var incident = Assert.Single(await store.GetAllAsync());

        Assert.True(await store.AcknowledgeAsync(incident.Id));
        Assert.False(await store.AcknowledgeAsync(incident.Id));
        Assert.True(await store.ResolveAsync(incident.Id));
        Assert.False(await store.ResolveAsync(incident.Id));
        Assert.False(await store.AcknowledgeAsync(incident.Id));
    }

    [Fact]
    public async Task VersionedTransition_RejectsStaleVersion()
    {
        var store = new InMemoryGovernanceIncidentStore();
        await store.RecordAsync(Evidence("version", DateTimeOffset.UtcNow));
        var incident = Assert.Single(await store.GetAllAsync());
        var acknowledged = await store.AcknowledgeAsync(
            incident.Id, incident.OperatorStateVersion);

        Assert.NotNull(acknowledged);
        Assert.Equal(1, acknowledged.Version);
        await Assert.ThrowsAsync<OperationalControlConcurrencyException>(() =>
            store.ResolveAsync(incident.Id, expectedVersion: 0));
    }

    [Fact]
    public async Task OpenIncident_CanResolveDirectly()
    {
        var store = new InMemoryGovernanceIncidentStore();
        await store.RecordAsync(Evidence("resolve", DateTimeOffset.UtcNow));
        var incident = Assert.Single(await store.GetAllAsync());

        var resolved = await store.ResolveAsync(
            incident.Id, incident.OperatorStateVersion);

        Assert.Equal(GovernanceIncidentStatus.Resolved, resolved?.Status);
        Assert.Equal(1, resolved?.Version);
    }

    private static AuditEvent Evidence(string id, DateTimeOffset timestamp) => new()
    {
        Id = id,
        TimestampUtc = timestamp,
        IdentityId = "worker",
        CapabilityId = "deploy",
        Decision = DecisionType.Deny,
        PolicyDecision = DecisionType.Deny,
        EnforcementMode = EnforcementMode.Enforce,
        EffectiveAction = "deny",
        Reason = "denied",
        MatchedPolicies = ["policy"]
    };
}
