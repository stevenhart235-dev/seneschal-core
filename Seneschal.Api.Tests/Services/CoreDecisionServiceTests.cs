using ApiDecisionRequest = Seneschal.Api.Models.DecisionRequest;
using Seneschal.Api.Services;
using CoreEnforcementMode = Seneschal.Core.Enums.EnforcementMode;
using CorePolicyEvaluator = Seneschal.Core.Services.PolicyEvaluator;
using Xunit;

namespace Seneschal.Api.Tests.Services;

public sealed class CoreDecisionServiceTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly CoreDecisionService _service;

    public CoreDecisionServiceTests(ApiApplicationFactory factory)
    {
        _ = factory;
        _service = new CoreDecisionService(
            new PolicyLoader(),
            new CorePolicyEvaluator(),
            new RuntimeSettings
            {
                Mode = CoreEnforcementMode.LogOnly
            });
    }

    [Fact]
    public void Evaluate_MapsAllowDecision()
    {
        var result = _service.Evaluate(
            CreateRequest(
                "Developer",
                "DeployApplication",
                "dev"));

        Assert.Equal("allow", result.Decision);
        Assert.Equal("allow", result.EffectiveAction);
        Assert.Equal("LogOnly", result.Mode);
        Assert.True(result.DurationMs >= 0);
    }

    [Fact]
    public void Evaluate_MapsCompatibilityDefaultDenyFallback()
    {
        var result = _service.Evaluate(
            CreateRequest(
                "UnknownIdentity",
                "UnknownCapability",
                "dev"));

        Assert.Equal("deny", result.Decision);
        Assert.Equal("default-deny", result.PolicyMatched);
        Assert.Equal("No matching allow policy found", result.Reason);
    }

    [Fact]
    public void Evaluate_MapsWinningPolicyName()
    {
        var result = _service.Evaluate(
            CreateRequest(
                "Developer",
                "DeleteProductionDatabase",
                "prod"));

        Assert.Equal("deny", result.Decision);
        Assert.Equal(
            "Developers cannot delete production databases",
            result.PolicyMatched);
    }

    [Fact]
    public void Evaluate_MapsRequiresApprovalDecision()
    {
        var result = _service.Evaluate(
            CreateRequest(
                "SupportAgent",
                "ReadSecret",
                "prod"));

        Assert.Equal("requires_approval", result.Decision);
        Assert.Equal(
            "Support secret reads require approval",
            result.PolicyMatched);
    }

    [Fact]
    public void Evaluate_ProjectsNonAllowDecisionAsLoggedOnly()
    {
        var result = _service.Evaluate(
            CreateRequest(
                "Developer",
                "DeleteProductionDatabase",
                "prod"));

        Assert.Equal("deny", result.Decision);
        Assert.Equal("logged_only", result.EffectiveAction);
        Assert.Equal("LogOnly", result.Mode);
    }

    private static ApiDecisionRequest CreateRequest(
        string identity,
        string capability,
        string environment)
    {
        return new ApiDecisionRequest
        {
            Identity = identity,
            Capability = capability,
            Context = new Dictionary<string, string>
            {
                ["environment"] = environment,
                ["resource"] = "contract-test-resource"
            }
        };
    }
}
