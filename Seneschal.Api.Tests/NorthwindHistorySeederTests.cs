using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Seneschal.Api.Mappers;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class NorthwindHistorySeederTests
{
    private static readonly DateTimeOffset Anchor =
        new(2026, 7, 24, 16, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task SeedAsync_IsDisabledByDefault()
    {
        var audit = new InMemoryAuditEventStore();
        var activity = new InMemoryActivityStore();
        var seeder = CreateSeeder(audit, activity, enabled: false);

        var added = await seeder.SeedAsync();

        Assert.Equal(0, added);
        Assert.Empty(await audit.GetRecentAsync(int.MaxValue));
        Assert.Empty((await activity.GetSnapshotAsync()).Capabilities);
    }

    [Fact]
    public void Generate_ProducesDeterministicBelievableHistory()
    {
        var first = CreateSeeder(
            new InMemoryAuditEventStore(),
            new InMemoryActivityStore()).Generate(Anchor);
        var second = CreateSeeder(
            new InMemoryAuditEventStore(),
            new InMemoryActivityStore()).Generate(Anchor);

        Assert.InRange(first.Count, 300, 500);
        Assert.Equal(
            first.Select(EventSignature),
            second.Select(EventSignature));
        Assert.Equal(
            first.Count,
            first.Select(item => item.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
        Assert.True(
            first.Max(item => item.TimestampUtc) -
            first.Min(item => item.TimestampUtc) >= TimeSpan.FromDays(14));
        Assert.All(first, item => Assert.Equal(TimeSpan.Zero, item.TimestampUtc.Offset));

        Assert.Equal(320, first.Count(item => item.Decision == DecisionType.Allow));
        Assert.Equal(32, first.Count(item => item.Decision == DecisionType.Deny));
        Assert.Equal(28, first.Count(item => item.Decision == DecisionType.RequireApproval));
        Assert.Equal(20, first.Count(item => item.Decision == DecisionType.LogOnly));
        Assert.True(
            first.Count(item => item.Decision == DecisionType.Allow) >
            first.Count / 2);

        var representedIdentities = first
            .Select(item => item.IdentityId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("payments-api", representedIdentities);
        Assert.Contains("checkout-api", representedIdentities);
        Assert.Contains("fraud-detection-worker", representedIdentities);
        Assert.Contains("github-release-worker", representedIdentities);
        Assert.Contains("terraform-cloud", representedIdentities);
        Assert.Contains("argocd-production", representedIdentities);
        Assert.Contains("backup-automation", representedIdentities);
        Assert.Contains("support-ai", representedIdentities);
        Assert.Contains("security-automation", representedIdentities);
        Assert.Contains("incident-response-bot", representedIdentities);

        var catalog = new CapabilityLoader().GetCapabilities()
            .ToDictionary(item => item.Name, CapabilityMapper.ToCore,
                StringComparer.OrdinalIgnoreCase);
        var classifier = new TechnologyClassifier();
        var technologies = first
            .Select(item => classifier.Classify(
                item.CapabilityId,
                catalog[item.CapabilityId]).Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.True(technologies.Count >= 8);
        Assert.Contains("azure", technologies);
        Assert.Contains("github", technologies);
        Assert.Contains("terraform", technologies);
        Assert.Contains("kubernetes", technologies);
        Assert.Contains("openai", technologies);
        Assert.Contains("postgresql", technologies);
        Assert.Contains("slack", technologies);
        Assert.Contains("m365", technologies);

        var destructiveCount = first.Count(item =>
            item.CapabilityId.Contains("destroy", StringComparison.OrdinalIgnoreCase) ||
            item.CapabilityId.Contains("delete", StringComparison.OrdinalIgnoreCase) ||
            item.CapabilityId.Contains("drop", StringComparison.OrdinalIgnoreCase));
        Assert.InRange(destructiveCount, 1, first.Count / 20);
    }

    [Fact]
    public async Task SeedAsync_IsIdempotentAndPopulatesExistingStores()
    {
        var audit = new InMemoryAuditEventStore();
        var activity = new InMemoryActivityStore();
        var seeder = CreateSeeder(audit, activity);

        var firstAdded = await seeder.SeedAsync();
        var secondAdded = await seeder.SeedAsync();

        Assert.Equal(NorthwindHistorySeeder.PlannedRecordCount, firstAdded);
        Assert.Equal(0, secondAdded);
        Assert.Equal(
            NorthwindHistorySeeder.PlannedRecordCount,
            (await audit.GetRecentAsync(int.MaxValue)).Count);
        var snapshot = await activity.GetSnapshotAsync();
        Assert.True(snapshot.Identities.Count >= 10);
        Assert.True(snapshot.Capabilities.Count >= 15);
        Assert.Equal(
            NorthwindHistorySeeder.PlannedRecordCount,
            snapshot.Capabilities.Sum(item => item.TotalRequests));
    }

    [Fact]
    public async Task DemoProfile_PopulatesInvestigationPages()
    {
        await using var factory = new ApiApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(
                    "Seneschal:Demo:NorthwindHistory:Enabled",
                    "true");
                builder.UseSetting(
                    "Seneschal:Demo:NorthwindHistory:SeedVersion",
                    "s14-c6-v1");
            });
        using var client = factory.CreateClient();
        var seededEvents = await factory.Services
            .GetRequiredService<Seneschal.Core.Interfaces.IAuditEventStore>()
            .GetRecentAsync(int.MaxValue);
        Assert.Equal(NorthwindHistorySeeder.PlannedRecordCount, seededEvents.Count);
        var decisionId = seededEvents
            .OrderByDescending(item => item.TimestampUtc)
            .First()
            .Id;

        var routes = new[]
        {
            "/dashboard",
            "/monitor",
            "/technologies/openai",
            "/identity-activity?identityId=payments-api",
            "/capability-activity?capabilityId=payments.refund.create",
            "/audit",
            $"/audit/{Uri.EscapeDataString(decisionId)}"
        };

        foreach (var route in routes)
        {
            using var response = await client.GetAsync(route);
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"{route} returned {response.StatusCode}.");
            var content = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Decision not found", content);
        }

        var activity = await client.GetStringAsync("/activity");
        Assert.Contains("payments-api", activity);
        Assert.Contains("payments.refund.create", activity);
    }

    private static NorthwindHistorySeeder CreateSeeder(
        InMemoryAuditEventStore audit,
        InMemoryActivityStore activity,
        bool enabled = true) =>
        new(
            audit,
            activity,
            new IdentityLoader(),
            new CapabilityLoader(),
            new PolicyLoader(),
            new NorthwindHistorySeedOptions
            {
                Enabled = enabled,
                SeedVersion = "s14-c6-v1"
            },
            new FixedClock(Anchor),
            NullLogger<NorthwindHistorySeeder>.Instance);

    private static string EventSignature(Seneschal.Core.Models.AuditEvent item) =>
        string.Join(
            "|",
            item.Id,
            item.TimestampUtc.ToString("O"),
            item.IdentityId,
            item.CapabilityId,
            item.Decision,
            item.EnforcementMode,
            item.Reason,
            item.EvaluationDurationMs);

    private sealed class FixedClock(DateTimeOffset utcNow)
        : INorthwindHistoryClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
