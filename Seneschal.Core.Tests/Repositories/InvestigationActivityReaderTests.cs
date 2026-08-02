using Seneschal.Core.Enums;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Core.Tests.Repositories;

public sealed class InvestigationActivityReaderTests
{
    [Fact]
    public async Task InMemoryDetailMatchesSnapshotAndCanonicalEvidenceOrder()
    {
        var activity = new InMemoryActivityStore();
        var audit = new InMemoryAuditEventStore();
        var timestamp = new DateTimeOffset(2026, 8, 2, 12, 0, 0,
            TimeSpan.Zero);
        var evidence = new[]
        {
            Event("first", timestamp, "identity-a", "capability-a",
                DecisionType.Allow),
            Event("second", timestamp, "identity-a", "capability-a",
                DecisionType.Deny),
            Event("third", timestamp.AddMinutes(1), "identity-b",
                "capability-a", DecisionType.RequireApproval)
        };
        foreach (var item in evidence)
        {
            await audit.WriteAsync(item);
            await activity.RecordAsync(item);
        }
        var administrative = Event("administrative", timestamp.AddMinutes(2),
            "identity-a", "capability-a", DecisionType.Allow) with
        {
            EffectiveAction = "approval_approved",
            ApprovalAction = "Approved"
        };
        await audit.WriteAsync(administrative);
        var reader = new ActivityStoreInvestigationActivityReader(activity, audit);

        var capability = await reader.GetCapabilityAsync("capability-a", 3);
        Assert.NotNull(capability);
        Assert.Equal(3, capability.Activity.TotalRequests);
        Assert.Equal(["identity-a", "identity-b"],
            capability.ObservedIdentities);
        Assert.Equal(["administrative", "third", "first"],
            capability.RecentEvidence.Select(item => item.Id));
        var identity = await reader.GetIdentityAsync("identity-a", 3);
        Assert.NotNull(identity);
        Assert.Equal(2, identity.Activity.TotalRequests);
        Assert.Equal(["capability-a"],
            identity.Activity.DistinctCapabilitiesUsed);
        Assert.Equal(["administrative", "first", "second"],
            identity.RecentEvidence.Select(item => item.Id));
    }

    [Fact]
    public async Task EmptyReaderReturnsEmptySnapshotAndMissingDetails()
    {
        var reader = new ActivityStoreInvestigationActivityReader(
            new InMemoryActivityStore(), new InMemoryAuditEventStore());
        var snapshot = await reader.GetSnapshotAsync();
        Assert.Empty(snapshot.Capabilities);
        Assert.Empty(snapshot.Identities);
        Assert.Null(await reader.GetCapabilityAsync("missing"));
        Assert.Null(await reader.GetIdentityAsync("missing"));
    }

    private static AuditEvent Event(string id, DateTimeOffset timestamp,
        string identity, string capability, DecisionType decision) => new()
    {
        Id = id,
        TimestampUtc = timestamp,
        IdentityId = identity,
        CapabilityId = capability,
        Environment = "dev",
        ResourceId = "resource",
        Decision = decision,
        EffectiveAction = decision == DecisionType.Allow ? "allow" : "logged_only",
        EnforcementMode = EnforcementMode.LogOnly,
        Reason = "Test."
    };
}
