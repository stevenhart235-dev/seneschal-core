using Seneschal.Core.Enums;
using Seneschal.Core.Models;
using Seneschal.Core.Services;
using Xunit;

namespace Seneschal.Core.Tests.Services;

public sealed class DecisionResolverTests
{
    private readonly DecisionResolver _resolver = new();
    private readonly DecisionRequest _request = CreateRequest();

    [Fact]
    public void Resolve_HighestPriorityPolicyWins()
    {
        var matches = new[]
        {
            CreateMatch("lower-deny", 10, DecisionType.Deny),
            CreateMatch("higher-allow", 20, DecisionType.Allow)
        };

        var result = _resolver.Resolve(_request, matches, EnforcementMode.Enforce);

        Assert.Equal(DecisionType.Allow, result.Decision);
        Assert.Equal("Reason for higher-allow", result.Reason);
    }

    [Theory]
    [InlineData(DecisionType.Deny, DecisionType.RequireApproval)]
    [InlineData(DecisionType.RequireApproval, DecisionType.Warn)]
    [InlineData(DecisionType.Warn, DecisionType.Allow)]
    [InlineData(DecisionType.Allow, DecisionType.LogOnly)]
    public void Resolve_SeverityBreaksPriorityTies(
        DecisionType expectedWinner,
        DecisionType lowerSeverity)
    {
        var matches = new[]
        {
            CreateMatch("lower-severity", 100, lowerSeverity),
            CreateMatch("expected-winner", 100, expectedWinner)
        };

        var result = _resolver.Resolve(_request, matches, EnforcementMode.Enforce);

        Assert.Equal(expectedWinner, result.Decision);
        Assert.Equal("Reason for expected-winner", result.Reason);
    }

    [Fact]
    public void Resolve_MergesMatchedPolicyObligationsDistinctly()
    {
        var matches = new[]
        {
            CreateMatch("first", 20, DecisionType.Allow, ["audit", "approval"]),
            CreateMatch("second", 10, DecisionType.Warn, ["AUDIT", "notify"])
        };

        var result = _resolver.Resolve(_request, matches, EnforcementMode.Enforce);

        Assert.Equal(["audit", "approval", "notify"], result.Obligations);
    }

    [Fact]
    public void Resolve_NoMatchesPreservesCurrentDefaultBehavior()
    {
        var result = _resolver.Resolve(
            _request,
            Array.Empty<PolicyMatch>(),
            EnforcementMode.LogOnly);

        Assert.Equal(DecisionType.Allow, result.Decision);
        Assert.Equal(EnforcementMode.LogOnly, result.Mode);
        Assert.Equal("No matching policy found.", result.Reason);
        Assert.Empty(result.MatchedPolicies);
        Assert.Empty(result.MatchedPolicyDetails);
        Assert.Equal(["audit"], result.Obligations);
        Assert.Empty(result.Evaluation);
        Assert.Equal(0, result.LatencyMs);
    }

    [Fact]
    public void Resolve_OrdersMatchedPoliciesByDescendingPriority()
    {
        var matches = new[]
        {
            CreateMatch("low", 10, DecisionType.Allow),
            CreateMatch("high", 30, DecisionType.Allow),
            CreateMatch("middle", 20, DecisionType.Allow)
        };

        var result = _resolver.Resolve(_request, matches, EnforcementMode.Enforce);

        Assert.Equal(["high", "middle", "low"], result.MatchedPolicies);
        Assert.Equal(
            ["high", "middle", "low"],
            result.MatchedPolicyDetails.Select(match => match.PolicyId));
    }

    private static PolicyMatch CreateMatch(
        string policyId,
        int priority,
        DecisionType effect,
        List<string>? obligations = null)
    {
        return new PolicyMatch
        {
            PolicyId = policyId,
            PolicyName = policyId,
            Priority = priority,
            Effect = effect,
            Reason = $"Reason for {policyId}",
            Obligations = obligations ?? []
        };
    }

    private static DecisionRequest CreateRequest()
    {
        return new DecisionRequest
        {
            RequestId = "test-request",
            Timestamp = DateTimeOffset.UtcNow,
            Identity = new Identity
            {
                Id = "test-identity",
                Type = IdentityType.Agent,
                Owner = "platform",
                Environment = "test"
            },
            Capability = new Capability
            {
                Id = "test-capability",
                Provider = "test",
                Category = "test",
                Risk = RiskLevel.Low,
                Description = "Test capability"
            },
            Intent = new Intent
            {
                Action = "test",
                Reason = "Unit test"
            },
            Resource = new Resource
            {
                Type = "test-resource",
                Id = "test-resource"
            }
        };
    }
}
