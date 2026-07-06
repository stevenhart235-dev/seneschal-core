using ApiDecisionRequest = Seneschal.Api.Models.DecisionRequest;
using CoreDecisionResult = Seneschal.Core.Models.DecisionResult;
using CoreEnforcementMode = Seneschal.Core.Enums.EnforcementMode;
using Seneschal.Api.Mappers;
using Seneschal.Core.Enums;
using Seneschal.Core.Models;
using Xunit;

namespace Seneschal.Api.Tests.Mappers;

public sealed class DtoMapperTests
{
    [Fact]
    public void DecisionRequestMapper_MapsCurrentApiShapeToCore()
    {
        var timestamp = new DateTimeOffset(
            2026,
            7,
            6,
            12,
            0,
            0,
            TimeSpan.Zero);
        var request = new ApiDecisionRequest
        {
            Identity = "SupportAgent",
            Capability = "azure.keyvault.secret.read",
            Context = new Dictionary<string, string>
            {
                ["environment"] = "prod",
                ["resource"] = "payment-secret",
                ["source"] = "contract-test"
            }
        };

        var result = DecisionRequestMapper.ToCore(
            request,
            "request-1",
            timestamp);

        Assert.Equal("request-1", result.RequestId);
        Assert.Equal(timestamp, result.Timestamp);
        Assert.Equal("SupportAgent", result.Identity.Id);
        Assert.Equal(IdentityType.Agent, result.Identity.Type);
        Assert.Equal("prod", result.Identity.Environment);
        Assert.Equal("azure.keyvault.secret.read", result.Capability.Id);
        Assert.Equal("azure.keyvault.secret.read", result.Capability.Name);
        Assert.Equal("api", result.Capability.Provider);
        Assert.Equal(RiskLevel.Low, result.Capability.RiskLevel);
        Assert.Equal("azure.keyvault.secret.read", result.Intent.Action);
        Assert.Equal("payment-secret", result.Resource.Id);
        Assert.Equal("prod", result.Resource.Environment);
        Assert.Equal("contract-test", result.Context["source"]);
        Assert.NotSame(request.Context, result.Context);
    }

    [Theory]
    [InlineData(DecisionType.Allow, "allow")]
    [InlineData(DecisionType.Deny, "deny")]
    [InlineData(DecisionType.Warn, "warn")]
    [InlineData(DecisionType.LogOnly, "log_only")]
    [InlineData(DecisionType.RequireApproval, "requires_approval")]
    public void DecisionTypeMapper_ConvertsBothDirections(
        DecisionType coreDecision,
        string apiDecision)
    {
        Assert.Equal(apiDecision, DecisionTypeMapper.ToApi(coreDecision));
        Assert.Equal(
            coreDecision,
            DecisionTypeMapper.ToCore(apiDecision.ToUpperInvariant()));
    }

    [Fact]
    public void DecisionTypeMapper_RejectsUnmappableDecision()
    {
        Assert.Throws<ArgumentException>(
            () => DecisionTypeMapper.ToCore("unsupported"));
    }

    [Theory]
    [InlineData(CoreEnforcementMode.LogOnly, "LogOnly")]
    [InlineData(CoreEnforcementMode.Enforce, "Enforce")]
    public void EnforcementModeMapper_ConvertsToApiFormat(
        CoreEnforcementMode coreMode,
        string apiMode)
    {
        Assert.Equal(apiMode, EnforcementModeMapper.ToApi(coreMode));
    }

    [Fact]
    public void DecisionResultMapper_MapsCurrentApiShapeAndWinningPolicy()
    {
        var result = CreateDecisionResult(
            DecisionType.RequireApproval,
            CoreEnforcementMode.LogOnly);

        var apiResult = DecisionResultMapper.ToApi(result, 17);

        Assert.Equal("requires_approval", apiResult.Decision);
        Assert.Equal("Approval is required.", apiResult.Reason);
        Assert.Equal("Protect secrets", apiResult.PolicyMatched);
        Assert.Equal(17, apiResult.DurationMs);
        Assert.Equal("logged_only", apiResult.EffectiveAction);
        Assert.Equal("LogOnly", apiResult.Mode);
    }

    [Fact]
    public void DecisionResultMapper_LeavesAllowedDecisionEffective()
    {
        var result = CreateDecisionResult(
            DecisionType.Allow,
            CoreEnforcementMode.LogOnly);

        var apiResult = DecisionResultMapper.ToApi(result, 3);

        Assert.Equal("allow", apiResult.Decision);
        Assert.Equal("allow", apiResult.EffectiveAction);
    }

    private static CoreDecisionResult CreateDecisionResult(
        DecisionType decision,
        CoreEnforcementMode mode)
    {
        var winningPolicy = new PolicyMatch
        {
            PolicyId = "protect-secrets",
            PolicyName = "Protect secrets",
            Priority = 100,
            Effect = decision,
            Reason = "Approval is required."
        };

        return new CoreDecisionResult
        {
            DecisionId = "decision-1",
            RequestId = "request-1",
            Timestamp = DateTimeOffset.UtcNow,
            Decision = decision,
            Mode = mode,
            Reason = winningPolicy.Reason,
            WinningPolicy = winningPolicy
        };
    }
}
