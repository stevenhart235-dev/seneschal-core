using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Seneschal.Api.Models;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class IdentityMetadataTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public IdentityMetadataTests(ApiApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void LegacyIdentityYamlLoadsWithOptionalMetadataAbsent()
    {
        var path = WriteIdentityFile(
            """
            identities:
              - name: legacy-worker
                description: Existing worker configuration
                type: Service
            """);

        try
        {
            var identity = Assert.Single(
                new IdentityLoader(path).GetIdentities());

            Assert.Equal("legacy-worker", identity.Name);
            Assert.Equal("Existing worker configuration", identity.Description);
            Assert.Equal("Service", identity.Type);
            Assert.Null(identity.DisplayName);
            Assert.Null(identity.Owner);
            Assert.Null(identity.Application);
            Assert.Null(identity.Environment);
            Assert.Null(identity.Technology);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExtendedIdentityYamlLoadsDescriptiveMetadata()
    {
        var path = WriteIdentityFile(
            """
            identities:
              - name: github-release-worker
                displayName: GitHub Release Worker
                owner: Release Engineering
                application: Release Pipeline
                environment: Production
                technology: GitHub
                description: Automated production deployment workflow.
                type: Service
            """);

        try
        {
            var identity = Assert.Single(
                new IdentityLoader(path).GetIdentities());

            Assert.Equal("GitHub Release Worker", identity.DisplayName);
            Assert.Equal("Release Engineering", identity.Owner);
            Assert.Equal("Release Pipeline", identity.Application);
            Assert.Equal("Production", identity.Environment);
            Assert.Equal("GitHub", identity.Technology);
            Assert.Equal(
                "Automated production deployment workflow.",
                identity.Description);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task IdentityApiExposesMetadataAndOmitsMissingOptionalValues()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/identities");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        var enriched = document.RootElement.EnumerateArray().Single(item =>
            item.GetProperty("name").GetString() == "github-release-worker");

        Assert.Equal(
            "GitHub Release Worker",
            enriched.GetProperty("displayName").GetString());
        Assert.Equal(
            "Release Engineering",
            enriched.GetProperty("owner").GetString());
        Assert.Equal(
            "Release Pipeline",
            enriched.GetProperty("application").GetString());
        Assert.Equal(
            "Shared",
            enriched.GetProperty("environment").GetString());
        Assert.Equal(
            "GitHub",
            enriched.GetProperty("technology").GetString());
        Assert.All(
            document.RootElement.EnumerateArray(),
            identity =>
            {
                Assert.True(identity.TryGetProperty("displayName", out _));
                Assert.True(identity.TryGetProperty("application", out _));
                Assert.True(identity.TryGetProperty("technology", out _));
            });
    }

    [Fact]
    public async Task IdentityViewsRenderMetadataWithoutChangingInvestigationLinks()
    {
        using var client = _factory.CreateClient();
        var explorer = await client.GetStringAsync("/identity-explorer");

        Assert.Contains("GitHub Release Worker", explorer);
        Assert.Contains("github-release-worker", explorer);
        Assert.Contains("Owner:</strong> Release Engineering", explorer);
        Assert.Contains("Application:</strong> Release Pipeline", explorer);
        Assert.Contains("Environment:</strong> Shared", explorer);
        Assert.Contains("Technology:</strong> GitHub", explorer);
        Assert.Contains(
            "/identity-activity?identityId=github-release-worker",
            explorer);

        using var evaluation = await client.PostAsJsonAsync("/evaluate", new
        {
            identity = "github-release-worker",
            capability = "production.deployment.execute",
            context = new
            {
                environment = "production",
                resource = "checkout-api"
            }
        });
        Assert.Equal(HttpStatusCode.OK, evaluation.StatusCode);

        var activity = await client.GetStringAsync(
            "/identity-activity?identityId=github-release-worker");
        Assert.Contains("<h2>GitHub Release Worker</h2>", activity);
        Assert.Contains("<dt>Owner</dt><dd>Release Engineering</dd>", activity);
        Assert.Contains("<dt>Application</dt><dd>Release Pipeline</dd>", activity);
        Assert.Contains("<dt>Environment</dt><dd>Shared</dd>", activity);
        Assert.Contains("<dt>Technology</dt><dd>GitHub</dd>", activity);
        Assert.Contains(
            "/audit?identityId=github-release-worker",
            activity);
    }

    [Fact]
    public async Task GovernanceGraphCarriesIdentityMetadata()
    {
        using var client = _factory.CreateClient();
        using var document = JsonDocument.Parse(
            await client.GetStringAsync("/graph"));
        var node = document.RootElement.GetProperty("nodes")
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() ==
                "identity:github-release-worker");
        var metadata = node.GetProperty("metadata");

        Assert.Equal(
            "GitHub Release Worker",
            node.GetProperty("label").GetString());
        Assert.Equal(
            "Release Engineering",
            metadata.GetProperty("owner").GetString());
        Assert.Equal(
            "Release Pipeline",
            metadata.GetProperty("application").GetString());
        Assert.Equal(
            "Shared",
            metadata.GetProperty("environment").GetString());
        Assert.Equal(
            "GitHub",
            metadata.GetProperty("technology").GetString());
    }

    [Fact]
    public void ExplicitlyBlankOptionalMetadataProducesWarning()
    {
        var result = ConfigurationValidator.Validate(
            [
                new Capability
                {
                    Name = "test.capability",
                    Description = "Test",
                    Risk = "Low",
                    Category = "Test"
                }
            ],
            [
                new IdentityDefinition
                {
                    Name = "test-identity",
                    Type = "Service",
                    Owner = " "
                }
            ],
            [
                new Policy
                {
                    Name = "test-policy",
                    Identity = "test-identity",
                    Capability = "test.capability",
                    Environment = "test",
                    Decision = "allow",
                    Reason = "Test"
                }
            ],
            new RuntimeSettings { Mode = EnforcementMode.LogOnly });

        var finding = Assert.Single(result.Findings);
        Assert.Equal("Warning", finding.Severity);
        Assert.Equal("IdentityMetadata", finding.Category);
        Assert.Equal("test-identity", finding.RelatedObjectId);
        Assert.Contains("'owner'", finding.Message);
        Assert.True(result.IsValid);
    }

    private static string WriteIdentityFile(string yaml)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"seneschal-identities-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        return path;
    }
}
