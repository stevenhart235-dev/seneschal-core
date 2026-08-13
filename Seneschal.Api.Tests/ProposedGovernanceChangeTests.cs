using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Api.Models;
using Seneschal.Api.Services;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class ProposedGovernanceChangeTests : IClassFixture<ApiApplicationFactory>, IDisposable
{
    private readonly ApiApplicationFactory _factory;
    private readonly string _directory=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
    private readonly string _policyPath;
    public ProposedGovernanceChangeTests(ApiApplicationFactory factory)
    {
        _factory=factory; Directory.CreateDirectory(_directory);
        _policyPath=Path.Combine(_directory,"policies.yaml");
        File.WriteAllText(_policyPath,"""
            policies:
              - name: Developers can deploy to dev
                identity: Developer
                capabilities:
                  - DeployApplication
                  - DeleteProductionDatabase
                environment: dev
                decision: allow
                reason: proposal test relationship
            """);
    }
    private WebApplicationFactory<Program> ProposalFactory() => _factory.WithWebHostBuilder(
        builder=>builder.UseSetting("Seneschal:Configuration:PoliciesPath",_policyPath));
    public void Dispose() => Directory.Delete(_directory,true);

    [Fact]
    public void ContractManifestAndFixtureAreValidAndStrict()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..",".."));
        var dir=Path.Combine(root,"integrations","contracts","proposed-governance-change");
        var schemaPath=Path.Combine(dir,"proposed-governance-change.v1.schema.json");
        using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"proposed-governance-change-contract.json")));
        Assert.Equal("v1",manifest.RootElement.GetProperty("contractVersion").GetString());
        Assert.Equal(1,manifest.RootElement.GetProperty("schemaRevision").GetInt32());
        Assert.Equal(manifest.RootElement.GetProperty("schemaSha256").GetString(),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(schemaPath))).ToLowerInvariant());
        using var scope=_factory.Services.CreateScope();
        var validator=scope.ServiceProvider.GetRequiredService<ProposedGovernanceChangeContractValidator>();
        using var valid=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"fixtures","valid.json")));
        Assert.Empty(validator.Validate(valid.RootElement));
        foreach(var invalid in new[] {
            valid.RootElement.GetRawText().Replace("\"v1\"","\"v2\""),
            valid.RootElement.GetRawText().Replace("\"revision\": 1","\"revision\": 2"),
            valid.RootElement.GetRawText().Replace("RemoveCapabilityFromPolicy","AddPolicy"),
            valid.RootElement.GetRawText().Replace("sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","bad"),
            valid.RootElement.GetRawText().Replace("\"proposalId\"","\"unknown\": true, \"proposalId\"") })
        { using var document=JsonDocument.Parse(invalid); Assert.NotEmpty(validator.Validate(document.RootElement)); }
        Assert.Contains("v1 revision 1",File.ReadAllText(Path.Combine(dir,"CHANGELOG.md")));
    }

    [Fact]
    public void CandidateGenerationRequiresExactUnambiguousCurrentRelationship()
    {
        using var factory=ProposalFactory();
        using var scope=factory.Services.CreateScope();
        var service=scope.ServiceProvider.GetRequiredService<ProposedGovernanceChangeCandidateService>();
        var fingerprint=scope.ServiceProvider.GetRequiredService<GovernanceConfigurationFingerprintService>().GetCurrentFingerprint();
        var recommendation=Recommendation(fingerprint, IdentityEvidenceCoverageStatus.Full,
            ["Developers can deploy to dev"]);
        var full=service.Offer(recommendation);
        var partial=service.Offer(recommendation with { EvidenceCoverageStatus=IdentityEvidenceCoverageStatus.Partial });
        Assert.True(full.IsAvailable); Assert.True(partial.IsAvailable);
        Assert.Equal("RemoveCapabilityFromPolicy",full.Proposal!.Change.Operation);
        Assert.Equal("DeployApplication",full.Proposal.Change.Capability);
        Assert.Equal("Developer",full.Proposal.Source.Identity);
        Assert.False(service.Offer(recommendation with { RelevantPolicies=["one","two"] }).IsAvailable);
        Assert.False(service.Offer(recommendation with { EvidenceCoverageStatus=IdentityEvidenceCoverageStatus.Unknown }).IsAvailable);
        Assert.False(service.Offer(recommendation with { CurrentConfigurationFingerprint="sha256:"+new string('0',64) }).IsAvailable);
        Assert.False(service.Offer(recommendation with { CapabilityId="absent", RelevantPolicies=["Developers can deploy to dev"] }).IsAvailable);
    }

    [Fact]
    public async Task SimulationComparesCanonicalPreviewAndMutatesNothing()
    {
        var mode=new InMemoryGovernanceModeStore(new RuntimeSettings { Mode=Seneschal.Core.Enums.EnforcementMode.Enforce });
        await using var factory=ProposalFactory().WithWebHostBuilder(builder=>builder.ConfigureTestServices(services=>
        { services.RemoveAll<IGovernanceModeStore>(); services.AddSingleton<IGovernanceModeStore>(mode); }));
        using var client=factory.CreateClient();
        var services=factory.Services;
        var fingerprint=services.GetRequiredService<GovernanceConfigurationFingerprintService>();
        var loader=services.GetRequiredService<PolicyLoader>();
        var audit=services.GetRequiredService<IAuditEventStore>();
        var activity=services.GetRequiredService<IActivityStore>();
        var approvals=services.GetRequiredService<IApprovalStore>();
        var incidents=services.GetRequiredService<IGovernanceIncidentStore>();
        var metrics=services.GetRequiredService<IDecisionMetrics>();
        var window=services.GetRequiredService<IGovernanceWindowStore>();
        var beforePolicies=JsonSerializer.Serialize(loader.GetPolicies()); var beforeFingerprint=fingerprint.GetCurrentFingerprint();
        var beforeAudit=(await audit.GetRecentAsync()).Count; var beforeActivity=await activity.GetSnapshotAsync();
        var beforeIncidents=(await incidents.GetAllAsync()).Count; var beforeApprovals=approvals.GetAll().Count;
        var beforeMetrics=Assert.IsType<InMemoryDecisionMetrics>(metrics).RenderPrometheus(); var beforeWindow=window.GetWindow();
        var proposal=Proposal(beforeFingerprint);
        using var response=await client.PostAsJsonAsync("/policy-changes/simulate",new ProposedGovernanceChangeSimulationRequest
        { Proposal=proposal,Identity="Developer",Capability="DeployApplication",Context=new(){["environment"]="dev",["resource"]="proposal-test"} });
        var responseText=await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode==HttpStatusCode.OK,responseText);
        var result=JsonSerializer.Deserialize<ProposedChangeSimulationOutcome>(responseText,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("allow",result!.Current!.Decision); Assert.Equal("Proceed",result.Current.ExecutionGuidance);
        Assert.Equal("deny",result.Proposed!.Decision); Assert.Equal("Block",result.Proposed.ExecutionGuidance);
        Assert.NotEqual(result.CurrentGovernanceConfigurationFingerprint,result.ProposedGovernanceConfigurationFingerprint);
        Assert.Contains(result.Differences,d=>d.Field=="ExecutionGuidance"&&d.Current=="Proceed"&&d.Proposed=="Block");
        Assert.Equal(-1,result.StaticGovernanceContextComparison!.ConfiguredCapabilityDifference);
        Assert.Equal(beforePolicies,JsonSerializer.Serialize(loader.GetPolicies())); Assert.Equal(beforeFingerprint,fingerprint.GetCurrentFingerprint());
        Assert.Equal(beforeAudit,(await audit.GetRecentAsync()).Count); Assert.Equal(beforeApprovals,approvals.GetAll().Count);
        Assert.Equal(beforeIncidents,(await incidents.GetAllAsync()).Count); Assert.Equal(beforeMetrics,Assert.IsType<InMemoryDecisionMetrics>(metrics).RenderPrometheus());
        var afterActivity=await activity.GetSnapshotAsync(); Assert.Equal(beforeActivity.Capabilities.Count,afterActivity.Capabilities.Count);
        var afterWindow=window.GetWindow(); Assert.Equal(beforeWindow.Enabled,afterWindow.Enabled);
        Assert.Equal(beforeWindow.Mode,afterWindow.Mode); Assert.Equal(beforeWindow.Version,afterWindow.Version);
        Assert.Equal(beforeWindow.AffectedCapabilities,afterWindow.AffectedCapabilities);
        Assert.Equal(Seneschal.Core.Enums.EnforcementMode.Enforce,mode.GetMode());
    }

    [Fact]
    public async Task SimulationCanReportNoRuntimeOutcomeChangeForExplicitContext()
    {
        await using var factory=ProposalFactory(); using var client=factory.CreateClient();
        var fingerprint=factory.Services.GetRequiredService<GovernanceConfigurationFingerprintService>().GetCurrentFingerprint();
        var proposal=Proposal(fingerprint) with
        {
            Source=Proposal(fingerprint).Source with { Capability="DeleteProductionDatabase" },
            Change=new() { Operation="RemoveCapabilityFromPolicy",
                Policy="Developers can deploy to dev", Capability="DeleteProductionDatabase" }
        };
        using var response=await client.PostAsJsonAsync("/policy-changes/simulate",Request(proposal));
        var result=await response.Content.ReadFromJsonAsync<ProposedChangeSimulationOutcome>();
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Equal(result!.Current!.Decision,result.Proposed!.Decision);
        Assert.Equal(result.Current.ExecutionGuidance,result.Proposed.ExecutionGuidance);
        Assert.Empty(result.Differences);
    }
    [Fact]
    public async Task StaleAndInvalidProposalsAreRejectedBeforeEvaluation()
    {
        await using var factory=ProposalFactory();
        using var client=factory.CreateClient();
        var stale=Proposal("sha256:"+new string('0',64));
        using var staleResponse=await client.PostAsJsonAsync("/policy-changes/simulate",Request(stale));
        var staleText=await staleResponse.Content.ReadAsStringAsync();
        Assert.True(staleResponse.StatusCode==HttpStatusCode.BadRequest,staleText);
        var staleResult=JsonSerializer.Deserialize<ProposedChangeSimulationOutcome>(staleText,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("stale_proposal",staleResult!.ErrorCode);
        var current=factory.Services.GetRequiredService<GovernanceConfigurationFingerprintService>().GetCurrentFingerprint();
        var invalid=Proposal(current) with { Change=new(){Operation="RemoveCapabilityFromPolicy",Policy="missing",Capability="DeployApplication"} };
        using var invalidResponse=await client.PostAsJsonAsync("/policy-changes/simulate",Request(invalid));
        var invalidResult=await invalidResponse.Content.ReadFromJsonAsync<ProposedChangeSimulationOutcome>();
        Assert.Equal("invalid_proposed_configuration",invalidResult!.ErrorCode);
    }

    private static ProposedGovernanceChangeSimulationRequest Request(ProposedGovernanceChange p)=>new()
    { Proposal=p,Identity="Developer",Capability="DeployApplication",Context=new(){["environment"]="dev",["resource"]="test"} };
    private static ProposedGovernanceChange Proposal(string fingerprint)=>new(){ContractVersion="v1",Revision=1,ProposalId="proposal-developer-deploy",BaseGovernanceConfigurationFingerprint=fingerprint,
        Source=new(){RecommendationType="ReviewCurrentGovernanceRelationship",FindingType="HighRiskConfiguredNotObserved",Identity="Developer",Capability="DeployApplication",EvidenceCoverage="Full",ObservationWindow=new(){StartUtc=DateTimeOffset.Parse("2026-07-01T00:00:00Z"),EndUtc=DateTimeOffset.Parse("2026-08-01T00:00:00Z")}},
        Change=new(){Operation="RemoveCapabilityFromPolicy",Policy="Developers can deploy to dev",Capability="DeployApplication"}};
    private static IdentityExposureRecommendation Recommendation(string fingerprint,IdentityEvidenceCoverageStatus coverage,IReadOnlyCollection<string> policies)=>new(
        IdentityExposureRecommendationType.ReviewCurrentGovernanceRelationship,"Review","Why","Consider","Limits",IdentityExposureFindingType.HighRiskConfiguredNotObserved,
        "Developer","DeployApplication","GitHub","High",coverage,DateTimeOffset.Parse("2026-07-01T00:00:00Z"),DateTimeOffset.Parse("2026-08-01T00:00:00Z"),[new("Observed","0")],policies,"Local catalog",fingerprint,[],0);
}
