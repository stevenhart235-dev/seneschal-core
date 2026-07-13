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

        Assert.Equal(11, policies.Count);
        Assert.Equal("Developers can deploy to dev", policies[0].Name);
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
            [11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1],
            projectedPolicies.Select(policy => policy.Priority));
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
}
