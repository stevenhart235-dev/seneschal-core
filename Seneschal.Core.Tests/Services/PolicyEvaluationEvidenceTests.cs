using Seneschal.Core.Enums;
using Seneschal.Core.Models;
using Seneschal.Core.Services;
using Xunit;

namespace Seneschal.Core.Tests.Services;

public sealed class PolicyEvaluationEvidenceTests
{
    [Fact]
    public void Evaluate_RetainsConditionEvidenceForMatchedAndUnmatchedPolicies()
    {
        var request = new DecisionRequest
        {
            RequestId = "request-1",
            Timestamp = DateTimeOffset.UtcNow,
            Identity = new Identity
            {
                Id = "deployment-worker",
                Type = IdentityType.ServiceAccount,
                Owner = "platform",
                Environment = "production"
            },
            Capability = new Capability
            {
                Id = "production.deployment.execute",
                Provider = "test",
                Category = "deployment",
                Description = "test"
            },
            Intent = new Intent { Action = "execute", Reason = "test" },
            Resource = new Resource
            {
                Type = "application",
                Id = "checkout-api",
                Environment = "production"
            }
        };
        var policies = new[]
        {
            new Policy
            {
                Id = "matching-policy",
                Name = "Matching Policy",
                Effect = DecisionType.Allow,
                Reason = "allowed",
                Conditions = new Dictionary<string, string>
                {
                    ["identity.owner"] = "platform",
                    ["resource.environment"] = "production"
                }
            },
            new Policy
            {
                Id = "nonmatching-policy",
                Name = "Nonmatching Policy",
                Effect = DecisionType.Deny,
                Reason = "denied",
                Conditions = new Dictionary<string, string>
                {
                    ["identity.owner"] = "security"
                }
            }
        };

        var result = new PolicyEvaluator().Evaluate(
            request,
            policies,
            EnforcementMode.LogOnly);

        Assert.Equal(2, result.PolicyEvaluations.Count);
        var matched = Assert.Single(result.PolicyEvaluations, item => item.Matched);
        Assert.Equal("Matching Policy", matched.Policy.Name);
        Assert.All(matched.Conditions, condition => Assert.True(condition.Matched));
        var unmatched = Assert.Single(result.PolicyEvaluations, item => !item.Matched);
        Assert.Equal("Nonmatching Policy", unmatched.Policy.Name);
        Assert.False(Assert.Single(unmatched.Conditions).Matched);
    }
}
