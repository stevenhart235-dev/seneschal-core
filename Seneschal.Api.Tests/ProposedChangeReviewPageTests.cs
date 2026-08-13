using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Api.Services;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class ProposedChangeReviewPageTests : IDisposable
{
    private readonly string _directory=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
    private readonly ApiApplicationFactory _factory=new();
    public ProposedChangeReviewPageTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory,"policies.yaml"),"""
            policies:
              - name: developer-production-operations
                identity: Developer
                capabilities:
                  - DeleteProductionDatabase
                  - DeployApplication
                environment: prod
                decision: allow
                reason: review fixture
            """);
    }

    [Theory]
    [InlineData(-60,"Full")]
    [InlineData(-5,"Partial")]
    public async Task CompleteChainRendersReadOnlyReview(int completeSinceDays,string coverage)
    {
        await using var factory=CreateFactory(DateTimeOffset.UtcNow.AddDays(completeSinceDays));
        using var client=factory.CreateClient();
        var audit=factory.Services.GetRequiredService<IAuditEventStore>();
        var activity=factory.Services.GetRequiredService<IActivityStore>();
        var fingerprint=factory.Services.GetRequiredService<GovernanceConfigurationFingerprintService>();
        var beforeAudit=(await audit.GetRecentAsync()).Count;
        var beforeActivity=await activity.GetSnapshotAsync(); var beforeFingerprint=fingerprint.GetCurrentFingerprint();
        var html=await client.GetStringAsync("/proposed-change-review?identityId=Developer&capabilityId=DeleteProductionDatabase&days=30");
        Assert.Contains("Proposed Governance Change Review",html);
        Assert.Contains("PROPOSED — NOT APPLIED",html); Assert.Contains("This review is read-only",html);
        Assert.Contains($">{coverage}<",html); Assert.Contains("Critical capability with no observed use",html);
        Assert.Contains("Review current governance relationship",html);
        Assert.Contains("RemoveCapabilityFromPolicy",html); Assert.Contains("developer-production-operations",html);
        Assert.Contains("CURRENT",html); Assert.Contains("PROPOSED",html);
        Assert.Contains("allow",html); Assert.Contains("deny",html); Assert.Contains("Proceed",html); Assert.Contains("Block",html);
        Assert.Contains("Static governance context comparison",html);
        Assert.Contains("View capability",html); Assert.Contains("View policy",html); Assert.Contains("View evidence",html);
        foreach(var action in new[]{">Apply<",">Accept<",">Approve<",">Fix<",">Revoke<"}) Assert.DoesNotContain(action,html);
        Assert.Equal(beforeAudit,(await audit.GetRecentAsync()).Count);
        var afterActivity=await activity.GetSnapshotAsync(); Assert.Equal(beforeActivity.Capabilities.Count,afterActivity.Capabilities.Count);
        Assert.Equal(beforeFingerprint,fingerprint.GetCurrentFingerprint());
    }

    [Fact]
    public async Task NoQualifyingFindingExplainsNoCandidateWithoutDeadLink()
    {
        using var client=_factory.CreateClient();
        var html=await client.GetStringAsync("/proposed-change-review?identityId=Developer&capabilityId=DeployApplication&days=30");
        Assert.Contains("No deterministic candidate proposed change is available",html);
        Assert.Contains("No qualifying finding and recommendation",html);
        Assert.DoesNotContain("RemoveCapabilityFromPolicy",html);
    }

    [Fact]
    public async Task IdentityActivityShowsCandidateOrFactualUnavailableState()
    {
        await using var factory=CreateFactory(DateTimeOffset.UtcNow.AddDays(-60));
        using var client=factory.CreateClient();
        var html=await client.GetStringAsync("/identity-activity?identityId=Developer");
        Assert.Contains("Review candidate change",html);
        Assert.Contains("/proposed-change-review?identityId=Developer",html);
    }

    private WebApplicationFactory<Program> CreateFactory(DateTimeOffset completeSince) =>
        _factory.WithWebHostBuilder(builder=>
        {
            builder.UseSetting("Seneschal:Configuration:PoliciesPath",Path.Combine(_directory,"policies.yaml"));
            builder.ConfigureTestServices(services=>
            {
                services.RemoveAll<IAuditEventStore>();
                services.AddSingleton<IAuditEventStore>(new InMemoryAuditEventStore(completeSinceUtc:completeSince));
                services.RemoveAll<IGovernanceModeStore>();
                services.AddSingleton<IGovernanceModeStore>(new InMemoryGovernanceModeStore(
                    new Seneschal.Api.Services.RuntimeSettings{Mode=Seneschal.Core.Enums.EnforcementMode.Enforce}));
            });
        });
    public void Dispose(){_factory.Dispose();Directory.Delete(_directory,true);}
}


