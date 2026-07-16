using Seneschal.Api.Models;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class ExecutionGuidanceTests
{
    [Theory]
    [InlineData("Developer", "DeployApplication", "dev", EnforcementMode.LogOnly, "Proceed")]
    [InlineData("Developer", "DeployApplication", "dev", EnforcementMode.Enforce, "Proceed")]
    [InlineData("Developer", "DeleteProductionDatabase", "prod", EnforcementMode.LogOnly, "ContinueLogOnly")]
    [InlineData("Developer", "DeleteProductionDatabase", "prod", EnforcementMode.Enforce, "Block")]
    [InlineData("SupportAgent", "azure.keyvault.secret.read", "prod", EnforcementMode.LogOnly, "ContinueLogOnly")]
    [InlineData("SupportAgent", "azure.keyvault.secret.read", "prod", EnforcementMode.Enforce, "Pause")]
    public void EvaluateMapsDecisionAndModeToCallerGuidance(
        string identity, string capability, string environment,
        EnforcementMode mode, string expected)
    {
        var service = new CoreDecisionService(
            new PolicyLoader(), new Seneschal.Core.Services.PolicyEvaluator(),
            new InMemoryGovernanceModeStore(new RuntimeSettings { Mode = mode }),
            approvalStore: new InMemoryApprovalStore());

        var result = service.Evaluate(new DecisionRequest
        {
            Identity = identity,
            Capability = capability,
            Context = new() { ["environment"] = environment, ["resource"] = "resource" }
        });

        Assert.Equal(expected, result.ExecutionGuidance);
        Assert.False(string.IsNullOrWhiteSpace(result.Decision));
        Assert.False(string.IsNullOrWhiteSpace(result.EffectiveAction));
        if (result.Decision == "requires_approval")
        {
            Assert.False(string.IsNullOrWhiteSpace(result.ApprovalId));
            Assert.Equal("Pending", result.ApprovalStatus);
            Assert.Contains("Approval is required", result.Message);
            Assert.Contains("Retry", result.RetryGuidance);
        }
    }
}
