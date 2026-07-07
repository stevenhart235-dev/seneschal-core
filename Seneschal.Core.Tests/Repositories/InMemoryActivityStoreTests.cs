using Seneschal.Core.Enums;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Core.Tests.Repositories;

public sealed class InMemoryActivityStoreTests
{
    [Fact]
    public async Task RecordAsync_UpdatesCapabilityCountersAndAverageDuration()
    {
        var store = new InMemoryActivityStore();
        var timestamp = DateTimeOffset.Parse("2026-07-07T12:00:00Z");

        await store.RecordAsync(CreateEvent(
            timestamp,
            "payment-agent",
            "azure.keyvault.secret.read",
            DecisionType.Allow,
            durationMs: 10,
            matchedPolicies: ["allow-secret-read"]));
        await store.RecordAsync(CreateEvent(
            timestamp.AddMinutes(1),
            "payment-agent",
            "azure.keyvault.secret.read",
            DecisionType.Deny,
            durationMs: 20,
            matchedPolicies: ["deny-secret-read"]));
        await store.RecordAsync(CreateEvent(
            timestamp.AddMinutes(2),
            "support-agent",
            "azure.keyvault.secret.read",
            DecisionType.RequireApproval,
            durationMs: 30,
            matchedPolicies: ["approval-secret-read"]));

        var snapshot = await store.GetSnapshotAsync();
        var capability = Assert.Single(snapshot.Capabilities);

        Assert.Equal("azure.keyvault.secret.read", capability.CapabilityId);
        Assert.Equal(3, capability.TotalRequests);
        Assert.Equal(1, capability.AllowedCount);
        Assert.Equal(1, capability.DeniedCount);
        Assert.Equal(1, capability.PendingApprovalCount);
        Assert.Equal(timestamp.AddMinutes(2), capability.LastUsedUtc);
        Assert.Equal(20, capability.AverageEvaluationDurationMs);
    }

    [Fact]
    public async Task RecordAsync_UpdatesIdentityCountersAndDistinctCapabilities()
    {
        var store = new InMemoryActivityStore();
        var timestamp = DateTimeOffset.Parse("2026-07-07T12:00:00Z");

        await store.RecordAsync(CreateEvent(
            timestamp,
            "payment-agent",
            "capability-a",
            DecisionType.Allow));
        await store.RecordAsync(CreateEvent(
            timestamp.AddMinutes(1),
            "payment-agent",
            "capability-a",
            DecisionType.Deny));
        await store.RecordAsync(CreateEvent(
            timestamp.AddMinutes(2),
            "payment-agent",
            "capability-b",
            DecisionType.RequireApproval));

        var snapshot = await store.GetSnapshotAsync();
        var identity = Assert.Single(snapshot.Identities);

        Assert.Equal("payment-agent", identity.IdentityId);
        Assert.Equal(3, identity.TotalRequests);
        Assert.Equal(2, identity.DistinctCapabilitiesUsed.Count);
        Assert.Contains("capability-a", identity.DistinctCapabilitiesUsed);
        Assert.Contains("capability-b", identity.DistinctCapabilitiesUsed);
        Assert.Equal(1, identity.DeniedCount);
        Assert.Equal(1, identity.PendingApprovalCount);
        Assert.Equal(timestamp.AddMinutes(2), identity.LastUsedUtc);
    }

    [Fact]
    public async Task RecordAsync_UpdatesPolicyMatchCountsAndLastMatched()
    {
        var store = new InMemoryActivityStore();
        var timestamp = DateTimeOffset.Parse("2026-07-07T12:00:00Z");

        await store.RecordAsync(CreateEvent(
            timestamp,
            matchedPolicies: ["policy-a", "policy-b"]));
        await store.RecordAsync(CreateEvent(
            timestamp.AddMinutes(5),
            matchedPolicies: ["policy-a"]));

        var snapshot = await store.GetSnapshotAsync();

        var policyA = snapshot.Policies.Single(policy =>
            policy.PolicyId == "policy-a");
        var policyB = snapshot.Policies.Single(policy =>
            policy.PolicyId == "policy-b");

        Assert.Equal(2, policyA.MatchCount);
        Assert.Equal(timestamp.AddMinutes(5), policyA.LastMatchedUtc);
        Assert.Equal(1, policyB.MatchCount);
        Assert.Equal(timestamp, policyB.LastMatchedUtc);
    }

    [Fact]
    public async Task RecordAsync_IgnoresEmptyMatchedPolicyIds()
    {
        var store = new InMemoryActivityStore();

        await store.RecordAsync(CreateEvent(
            matchedPolicies: ["policy-a", "", "   "]));

        var snapshot = await store.GetSnapshotAsync();
        var policy = Assert.Single(snapshot.Policies);

        Assert.Equal("policy-a", policy.PolicyId);
    }

    [Fact]
    public async Task GetSnapshotAsync_DoesNotExposeAuditHistory()
    {
        var store = new InMemoryActivityStore();

        await store.RecordAsync(CreateEvent(
            "audit-event-1",
            matchedPolicies: ["policy-a"]));

        var snapshot = await store.GetSnapshotAsync();

        Assert.Single(snapshot.Capabilities);
        Assert.Single(snapshot.Identities);
        Assert.Single(snapshot.Policies);
        Assert.DoesNotContain(
            snapshot.Policies,
            policy => policy.PolicyId == "audit-event-1");
    }

    private static AuditEvent CreateEvent(
        DateTimeOffset? timestamp = null,
        string identityId = "payment-agent",
        string capabilityId = "azure.keyvault.secret.read",
        DecisionType decision = DecisionType.Allow,
        int durationMs = 12,
        IReadOnlyCollection<string>? matchedPolicies = null)
    {
        return CreateEvent(
            id: Guid.NewGuid().ToString("N"),
            timestamp,
            identityId,
            capabilityId,
            decision,
            durationMs,
            matchedPolicies);
    }

    private static AuditEvent CreateEvent(
        string id,
        DateTimeOffset? timestamp = null,
        string identityId = "payment-agent",
        string capabilityId = "azure.keyvault.secret.read",
        DecisionType decision = DecisionType.Allow,
        int durationMs = 12,
        IReadOnlyCollection<string>? matchedPolicies = null)
    {
        return new AuditEvent
        {
            Id = id,
            TimestampUtc = timestamp ??
                DateTimeOffset.Parse("2026-07-07T12:00:00Z"),
            IdentityId = identityId,
            CapabilityId = capabilityId,
            Decision = decision,
            EnforcementMode = EnforcementMode.LogOnly,
            MatchedPolicies = matchedPolicies?.ToList() ?? new List<string>
            {
                "policy-a"
            },
            Reason = "Test decision.",
            EvaluationDurationMs = durationMs
        };
    }
}
