using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Xunit;

namespace Seneschal.Api.Tests.Services;

public sealed class PolicyLoaderTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly PolicyLoader _loader;

    public PolicyLoaderTests(ApiApplicationFactory factory)
    {
        _ = factory;
        _loader = new PolicyLoader();
    }

    [Fact]
    public void GetPolicies_PreservesExistingApiDtos()
    {
        var policies = _loader.GetPolicies();

        Assert.Equal(15, policies.Count);
        Assert.Equal("Developers can deploy to dev", policies[0].Name);
        Assert.Equal("Development Deployment Access", policies[0].DisplayName);
        Assert.Equal("Release Engineering", policies[0].Owner);
        Assert.Equal("allow", policies[0].Decision);
    }

    [Fact]
    public void GetCorePolicies_ProjectsYamlFieldsToCoreConditions()
    {
        var policy = _loader.GetCorePolicies()[0];

        Assert.Equal("Developers can deploy to dev", policy.Id);
        Assert.Equal("Developers can deploy to dev", policy.Name);
        Assert.Equal(DecisionType.Allow, policy.Effect);
        Assert.Equal(
            "Developer",
            policy.Conditions["identity.id"]);
        Assert.Equal(
            "DeployApplication",
            policy.Conditions["capability.id"]);
        Assert.Equal(
            "dev",
            policy.Conditions["resource.environment"]);
    }

    [Fact]
    public void GetCorePolicies_GeneratesStrictlyDescendingPriorities()
    {
        var projectedPolicies = _loader
            .GetCorePolicies()
            .Where(policy => policy.Id != "default-deny")
            .ToList();

        Assert.Equal(
            [15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1],
            projectedPolicies
                .Select(policy => policy.Priority)
                .Distinct()
                .OrderDescending());
    }

    [Fact]
    public void GetPolicies_LoadsBalancedEnterpriseCatalog()
    {
        var policies = _loader.GetPolicies();

        Assert.Equal(15, policies.Count);
        Assert.All(policies, policy =>
        {
            Assert.False(string.IsNullOrWhiteSpace(policy.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(policy.Description));
            Assert.False(string.IsNullOrWhiteSpace(policy.Owner));
            Assert.False(string.IsNullOrWhiteSpace(policy.Severity));
            Assert.False(string.IsNullOrWhiteSpace(policy.Rationale));
            Assert.NotEmpty(policy.EffectiveIdentities);
            Assert.NotEmpty(policy.EffectiveCapabilities);
            Assert.NotEmpty(policy.EffectiveEnvironments);
        });

        Assert.Equal(
            ["allow", "deny", "log_only", "requires_approval"],
            policies
                .Select(policy => policy.Decision)
                .Distinct()
                .Order(StringComparer.Ordinal));
        Assert.Equal(
            ["AI Platform", "Data Platform", "Platform Engineering",
                "Release Engineering", "Security Engineering"],
            policies
                .Select(policy => policy.Owner)
                .Distinct()
                .Order(StringComparer.Ordinal));
        Assert.Equal(
            ["critical", "high", "low", "medium"],
            policies
                .Select(policy => policy.Severity)
                .Distinct()
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public void GetPolicies_RetainsLegacyPolicyIds()
    {
        var policyIds = _loader.GetPolicies()
            .Select(policy => policy.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("Developers can deploy to dev", policyIds);
        Assert.Contains("Developers cannot delete production databases", policyIds);
        Assert.Contains("Support secret reads require approval", policyIds);
        Assert.Contains("Platform engineers can deploy to dev", policyIds);
        Assert.Contains("Platform agent can apply production infrastructure", policyIds);
        Assert.Contains("Platform agent cannot destroy production infrastructure", policyIds);
        Assert.Contains("Deployment worker can deploy to production", policyIds);
        Assert.Contains("Migration worker cannot migrate production database", policyIds);
        Assert.Contains("Refund worker can create production refunds", policyIds);
        Assert.Contains("Release approval worker requires production approval", policyIds);
        Assert.Contains("GitHub Actions can deploy checkout API to production", policyIds);
        Assert.Contains("Terraform can apply production infrastructure", policyIds);
    }

    [Fact]
    public void GetCorePolicies_ExpandsEnterpriseTargetListsToExactConditions()
    {
        var policies = _loader.GetCorePolicies()
            .Where(policy => policy.Id == "Northwind privileged AI operations")
            .ToList();

        Assert.Equal(4, policies.Count);
        Assert.All(policies, policy =>
            Assert.Equal(DecisionType.Deny, policy.Effect));
        Assert.Contains(policies, policy =>
            policy.Conditions["identity.id"] == "support-ai" &&
            policy.Conditions["capability.id"] == "openai.production-secret.read" &&
            policy.Conditions["resource.environment"] == "production");
    }

    [Fact]
    public void GetCorePolicies_AppendsCompatibilityDefaultDenyOnlyToCore()
    {
        var apiPolicies = _loader.GetPolicies();
        var corePolicies = _loader.GetCorePolicies();

        Assert.DoesNotContain(
            apiPolicies,
            policy => policy.Name == "default-deny");

        var defaultDeny = Assert.Single(
            corePolicies,
            policy => policy.Id == "default-deny");
        Assert.Equal(DecisionType.Deny, defaultDeny.Effect);
        Assert.Equal(int.MinValue, defaultDeny.Priority);
        Assert.Equal(
            "No matching allow policy found",
            defaultDeny.Reason);
        Assert.Empty(defaultDeny.Conditions);
    }

    [Fact]
    public void ProductionFreezeProfile_PrependsDenialsAndPreservesWorkerPolicies()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            ".."));
        var loader = new PolicyLoader(Path.Combine(
            repositoryRoot,
            "Seneschal.Api",
            "Policies",
            "policies.production-freeze.yaml"));

        var policies = loader.GetPolicies();
        var corePolicies = loader.GetCorePolicies();

        Assert.Equal(14, policies.Count);
        Assert.Collection(
            policies.Take(2),
            policy =>
            {
                Assert.Equal(
                    "Production freeze blocks GitHub Actions deployments",
                    policy.Name);
                Assert.Equal("deny", policy.Decision);
                Assert.Equal("Production freeze is active.", policy.Reason);
            },
            policy =>
            {
                Assert.Equal(
                    "Production freeze blocks Terraform applies",
                    policy.Name);
                Assert.Equal("deny", policy.Decision);
                Assert.Equal("Production freeze is active.", policy.Reason);
            });
        Assert.Equal(14, corePolicies[0].Priority);
        Assert.Equal(13, corePolicies[1].Priority);
        Assert.Contains(
            policies,
            policy => policy.Name == "Deployment worker can deploy to production" &&
                policy.Decision == "allow");
        Assert.Contains(
            policies,
            policy => policy.Name == "Migration worker cannot migrate production database" &&
                policy.Decision == "deny");
        Assert.Contains(
            policies,
            policy => policy.Name == "Refund worker can create production refunds" &&
                policy.Decision == "allow");
        Assert.Contains(
            policies,
            policy => policy.Name == "Release approval worker requires production approval" &&
                policy.Decision == "requires_approval");
    }
}
