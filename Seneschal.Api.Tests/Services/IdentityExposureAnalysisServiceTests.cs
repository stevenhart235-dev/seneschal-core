using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests.Services;

public sealed class IdentityExposureAnalysisServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(),
        $"seneschal-exposure-{Guid.NewGuid():N}");
    private readonly DateTimeOffset _end = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    public IdentityExposureAnalysisServiceTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task Analyze_ComposesConfiguredObservedAndOutsideContextFacts()
    {
        var (service, audit) = CreateService();
        await Write(audit, "observed-db", "db.read", _end.AddDays(-2));
        await Write(audit, "observed-outside", "github.workflow.run", _end.AddDays(-1));
        await Write(audit, "too-old", "kube.deploy", _end.AddDays(-31));

        var result = await service.AnalyzeAsync(Query());

        Assert.Equal(3, result.Summary.ConfiguredCount);
        Assert.Equal(2, result.Summary.ObservedCount);
        Assert.Equal(1, result.Summary.ConfiguredAndObservedCount);
        Assert.Equal(2, result.Summary.ConfiguredNotObservedCount);
        Assert.Equal(1, result.Summary.ObservedNotConfiguredCount);
        Assert.Equal(2, result.Summary.CriticalConfiguredCount);
        Assert.Equal(1, result.Summary.CriticalObservedCount);
        Assert.Equal(1, result.Summary.CriticalNotObservedCount);

        var database = result.AllItems.Single(item => item.CapabilityId == "db.read");
        Assert.Equal(IdentityExposureState.ConfiguredAndObserved, database.State);
        Assert.Equal(2, database.Policies.Count);
        Assert.Equal(_end.AddDays(-2), database.MostRecentObservedUtc);
        Assert.Equal("Pack: postgres 1.0.0", database.Provenance);
        Assert.Equal(["db.read", "db.restore", "kube.deploy", "github.workflow.run"],
            result.AllItems.Select(item => item.CapabilityId));
        Assert.Contains(result.Summary.Technologies, item =>
            item.Technology == "postgresql" && item.ConfiguredCount == 2 && item.ObservedCount == 1);
    }

    [Fact]
    public async Task Analyze_UsesInclusiveWindowBoundariesAndMostRecentTimestamp()
    {
        var (service, audit) = CreateService();
        var start = _end.AddDays(-30);
        await Write(audit, "at-start", "db.read", start);
        await Write(audit, "at-end", "db.read", _end);
        await Write(audit, "before", "github.workflow.run", start.AddTicks(-1));
        await Write(audit, "after", "github.workflow.run", _end.AddTicks(1));

        var result = await service.AnalyzeAsync(Query());
        var database = result.AllItems.Single(item => item.CapabilityId == "db.read");

        Assert.Equal(2, database.ObservedCount);
        Assert.Equal(_end, database.MostRecentObservedUtc);
        Assert.DoesNotContain(result.AllItems, item => item.CapabilityId == "github.workflow.run");
    }

    [Theory]
    [InlineData("ConfiguredNotObserved", null, null, "kube.deploy")]
    [InlineData(null, "Low", null, "github.workflow.run")]
    [InlineData(null, null, "postgresql", "db.read")]
    public async Task Analyze_AppliesStateRiskAndTechnologyFilters(
        string? state, string? risk, string? technology, string expectedId)
    {
        var (service, audit) = CreateService();
        await Write(audit, "db", "db.read", _end.AddDays(-1));
        await Write(audit, "outside", "github.workflow.run", _end.AddDays(-1));

        var result = await service.AnalyzeAsync(Query(state, risk, technology));

        Assert.Contains(result.Items, item => item.CapabilityId == expectedId);
        Assert.All(result.Items, item =>
        {
            if (state is not null) Assert.Equal(state, item.State.ToString());
            if (risk is not null) Assert.Equal(risk, item.Risk);
            if (technology is not null) Assert.Equal(technology, item.Technology);
        });
    }

    [Fact]
    public async Task Analyze_ZeroActivityPreservesConfiguredGovernanceFacts()
    {
        var (service, _) = CreateService();

        var result = await service.AnalyzeAsync(Query());

        Assert.Equal(3, result.Summary.ConfiguredCount);
        Assert.Equal(0, result.Summary.ObservedCount);
        Assert.Equal(3, result.Summary.ConfiguredNotObservedCount);
        Assert.All(result.AllItems, item =>
            Assert.Equal(IdentityExposureState.ConfiguredNotObserved, item.State));
    }

    [Fact]
    public async Task Analyze_ObservedOnlyIdentitySurfacesEvidenceNeutrally()
    {
        var (service, audit) = CreateService();
        await audit.WriteAsync(new AuditEvent
        {
            Id = "observed-only", TimestampUtc = _end.AddDays(-1),
            IdentityId = "observed-only-identity",
            CapabilityId = "github.workflow.run", Decision = DecisionType.Allow,
            EnforcementMode = EnforcementMode.LogOnly, Reason = "Observed evaluation."
        });

        var result = await service.AnalyzeAsync(new IdentityExposureQuery(
            "observed-only-identity", _end.AddDays(-30), _end));

        var item = Assert.Single(result.Items);
        Assert.Equal(IdentityExposureState.ObservedNotConfigured, item.State);
        Assert.Equal(0, result.Summary.ConfiguredCount);
        Assert.Equal(1, result.Summary.ObservedCount);
    }
    private (IdentityExposureAnalysisService Service, InMemoryAuditEventStore Audit) CreateService()
    {
        var policyPath = Path.Combine(_directory, $"policies-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(policyPath, """
            policies:
            - name: database-read-primary
              identity: operator
              capability: db.read
              environment: prod
              decision: allow
              reason: Primary database governance context.
            - name: database-read-secondary
              identity: operator
              capability: db.read
              environment: staging
              decision: require_approval
              reason: Secondary database governance context.
            - name: kubernetes-deploy
              identity: operator
              capability: kube.deploy
              environment: prod
              decision: require_approval
              reason: Kubernetes deployment governance context.
            - name: database-restore
              identity: operator
              capability: db.restore
              environment: prod
              decision: deny
              reason: Database restore governance context.
            """);
        var catalog = InMemoryCapabilityCatalog.FromEntries([
            Entry("db.read", "postgresql", RiskLevel.Critical, "database", "postgres"),
            Entry("kube.deploy", "kubernetes", RiskLevel.High, "workload", "kubernetes"),
            Entry("db.restore", "postgresql", RiskLevel.Critical, "recovery", "postgres"),
            Entry("github.workflow.run", "github-actions", RiskLevel.Low, "automation", "github-actions")
        ]);
        var audit = new InMemoryAuditEventStore();
        var context = new OperatorGovernanceContextService(new PolicyLoader(policyPath), catalog);
        return (new IdentityExposureAnalysisService(context, catalog, audit), audit);
    }

    private IdentityExposureQuery Query(string? state = null, string? risk = null,
        string? technology = null) => new("operator", _end.AddDays(-30), _end,
            state, risk, technology);

    private static CapabilityCatalogEntry Entry(string id, string technology,
        RiskLevel risk, string category, string pack) => new()
    {
        Capability = new Capability { Id = id, DisplayName = id, Provider = pack,
            Technology = technology, Category = category, Description = id,
            RiskLevel = risk },
        Provenance = [new CapabilityProvenance { Kind = "CapabilityPack",
            PackId = pack, PackVersion = "1.0.0" }]
    };

    private static Task Write(InMemoryAuditEventStore audit, string id,
        string capability, DateTimeOffset timestamp) => audit.WriteAsync(new AuditEvent
    {
        Id = id, TimestampUtc = timestamp, IdentityId = "operator",
        CapabilityId = capability, Decision = DecisionType.Allow,
        EnforcementMode = EnforcementMode.LogOnly, Reason = "Observed evaluation."
    });

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}