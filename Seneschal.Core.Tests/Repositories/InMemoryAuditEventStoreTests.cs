using Seneschal.Core.Enums;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Core.Tests.Repositories;

public sealed class InMemoryAuditEventStoreTests
{
    [Fact]
    public async Task GetRecentAsync_ReturnsMostRecentEventsFirst()
    {
        var store = new InMemoryAuditEventStore();

        await store.WriteAsync(CreateEvent("older", -2));
        await store.WriteAsync(CreateEvent("newer", -1));

        var events = await store.GetRecentAsync();

        Assert.Equal(
            ["newer", "older"],
            events.Select(auditEvent => auditEvent.Id));
    }

    [Fact]
    public async Task GetRecentAsync_RespectsCount()
    {
        var store = new InMemoryAuditEventStore();

        await store.WriteAsync(CreateEvent("first", -3));
        await store.WriteAsync(CreateEvent("second", -2));
        await store.WriteAsync(CreateEvent("third", -1));

        var events = await store.GetRecentAsync(2);

        Assert.Equal(
            ["third", "second"],
            events.Select(auditEvent => auditEvent.Id));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsEventCaseInsensitively()
    {
        var store = new InMemoryAuditEventStore();

        await store.WriteAsync(CreateEvent("decision-1", -1));

        var auditEvent = await store.GetByIdAsync("DECISION-1");

        Assert.NotNull(auditEvent);
        Assert.Equal("decision-1", auditEvent.Id);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownIdReturnsNull()
    {
        var store = new InMemoryAuditEventStore();

        await store.WriteAsync(CreateEvent("decision-1", -1));

        var auditEvent = await store.GetByIdAsync("unknown");

        Assert.Null(auditEvent);
    }

    private static AuditEvent CreateEvent(
        string id,
        int minutes)
    {
        return new AuditEvent
        {
            Id = id,
            TimestampUtc = DateTimeOffset.UtcNow.AddMinutes(minutes),
            IdentityId = "Developer",
            CapabilityId = "DeployApplication",
            ResourceId = "payment-api",
            Environment = "dev",
            Decision = DecisionType.Allow,
            EnforcementMode = EnforcementMode.LogOnly,
            MatchedPolicies = ["policy-1"],
            Obligations = ["audit"],
            Reason = "Allowed.",
            EvaluationDurationMs = 1
        };
    }
}
