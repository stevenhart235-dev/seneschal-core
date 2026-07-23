using Seneschal.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Seneschal.Core.Enums;
using Seneschal.Core.Models;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class TechnologyActivityServiceTests
{
    private readonly TechnologyClassifier _classifier = new();

    [Theory]
    [InlineData("azure.keyvault.secret.read", "azure")]
    [InlineData("github.repository.write", "github")]
    [InlineData("terraform.production.apply", "terraform")]
    [InlineData("kubernetes.deployment.restart", "kubernetes")]
    [InlineData("aks.cluster.scale", "kubernetes")]
    [InlineData("openai.responses.create", "openai")]
    [InlineData("aws.lambda.invoke", "aws")]
    [InlineData("postgresql.schema.migrate", "postgresql")]
    [InlineData("business.invoice.approve", "unclassified")]
    [InlineData("infrastructure.production.destroy", "unclassified")]
    public void Classifier_UsesKnownNamespacesAndHonestFallback(string capability, string expected) =>
        Assert.Equal(expected, _classifier.Classify(capability).Key);

    [Fact]
    public void Classifier_PrefersStructuredMetadataForOtherwiseAmbiguousCapability()
    {
        var capability = Capability("infrastructure.production.apply", tags: ["terraform"]);
        Assert.Equal("terraform", _classifier.Classify(capability.Id, capability).Key);
    }

    [Fact]
    public void Classifier_ExplicitTechnologyOverridesNamespaceHeuristics()
    {
        var capability = Capability("azure.internal.workflow", technology: "custom");
        Assert.Equal("custom", _classifier.Classify(capability.Id, capability).Key);
    }

    [Theory]
    [InlineData("infrastructure.production.apply", "terraform")]
    [InlineData("infrastructure.production.destroy", "terraform")]
    [InlineData("production.deployment.execute", "github")]
    [InlineData("database.migration.execute", "custom")]
    [InlineData("production.release.approve", "custom")]
    public void Classifier_UsesExplicitDemoMetadata(string id, string technology)
    {
        var capability = Capability(id, technology: technology);
        Assert.Equal(technology, _classifier.Classify(id, capability).Key);
    }

    [Fact]
    public async Task Projection_AggregatesTechnologyApplicationsCapabilitiesAndDecisions()
    {
        await using var factory = new ApiApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<TechnologyActivityService>();
        var now = DateTimeOffset.UtcNow;
        var activity = new ActivitySnapshot
        {
            Capabilities =
            [
                new CapabilityActivity { CapabilityId = "azure.keyvault.secret.read", TotalRequests = 3, AllowedCount = 1, DeniedCount = 1, PendingApprovalCount = 1, LastUsedUtc = now }
            ]
        };
        var events = new[]
        {
            Event("allow", "app-one", DecisionType.Allow, now),
            Event("deny", "app-one", DecisionType.Deny, now.AddMinutes(-1)),
            Event("pending", "app-two", DecisionType.RequireApproval, now.AddMinutes(-2))
        };

        var result = Assert.Single(service.Build(
            [new CapabilityCatalogEntry { Capability = Capability("azure.keyvault.secret.read", tags: ["azure"]) }],
            activity, events, DisabledWindow()));

        Assert.Equal("azure", result.Key);
        Assert.Equal(2, result.ApplicationCount);
        Assert.Equal(1, result.CapabilityCount);
        Assert.Equal(3, result.EvaluationCount);
        Assert.Equal(1, result.AllowCount);
        Assert.Equal(1, result.DenyCount);
        Assert.Equal(1, result.PendingApprovalCount);
        Assert.Equal(2, result.Applications.Single(item => item.Name == "app-one").EvaluationCount);
        Assert.Equal(3, result.RecentDecisions.Count);
    }

    [Fact]
    public async Task Projection_CatalogOnlyTechnologyHasNoFalseRuntimeActivity()
    {
        await using var factory = new ApiApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<TechnologyActivityService>();
        var result = Assert.Single(service.Build(
            [new CapabilityCatalogEntry { Capability = Capability("openai.responses.create") }],
            new ActivitySnapshot(), [], DisabledWindow()));

        Assert.Equal("openai", result.Key);
        Assert.Equal(0, result.EvaluationCount);
        Assert.Equal(0, result.ApplicationCount);
        Assert.Null(result.LastObservedAt);
        Assert.Empty(result.RecentDecisions);
    }

    private static Capability Capability(string id, IReadOnlyCollection<string>? tags = null, string technology = "") => new()
    {
        Id = id, Name = id, DisplayName = id, Provider = "api", Category = "Test",
        Description = "Test capability", Tags = tags?.ToList() ?? [], Technology = technology
    };

    private static AuditEvent Event(string id, string identity, DecisionType decision, DateTimeOffset timestamp) => new()
    {
        Id = id, TimestampUtc = timestamp, IdentityId = identity,
        CapabilityId = "azure.keyvault.secret.read", Decision = decision,
        PolicyDecision = decision, EnforcementMode = EnforcementMode.LogOnly,
        Reason = $"{decision} for test", MatchedPolicies = ["Azure policy"]
    };

    private static GovernanceWindow DisabledWindow() => new()
    {
        Name = "None", Description = "No active window", Enabled = false,
        Mode = GovernanceWindowMode.Observe, Reason = "Not active"
    };
}
