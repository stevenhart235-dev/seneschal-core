using System.Net;
using System.Text.Json;
using Seneschal.Api.Models;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class ConfigurationValidationTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly HttpClient _client;

    public ConfigurationValidationTests(ApiApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ConfigValidate_ReturnsValidationResultJson()
    {
        using var response = await _client.GetAsync("/config/validate");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.True(root.GetProperty("isValid").GetBoolean());
        Assert.Equal(0, root.GetProperty("errorCount").GetInt32());
        Assert.True(root.GetProperty("warningCount").GetInt32() >= 0);
        Assert.True(root.GetProperty("infoCount").GetInt32() >= 0);
        Assert.Equal(JsonValueKind.Array, root.GetProperty("findings").ValueKind);
    }

    [Fact]
    public async Task ValidSampleConfiguration_ReturnsNoErrors()
    {
        using var response = await _client.GetAsync("/config/validate");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);

        Assert.True(document.RootElement.GetProperty("isValid").GetBoolean());
        Assert.Equal(0, document.RootElement.GetProperty("errorCount").GetInt32());
    }

    [Fact]
    public void UnknownCapabilityReference_ProducesErrorFinding()
    {
        var result = Validate(
            capabilities: [Capability("known-capability")],
            identities: [Identity("known-identity")],
            policies: [Policy("policy-1", "known-identity", "missing-capability")]);

        var finding = Assert.Single(result.Findings, finding =>
            finding.Category == "PolicyReference" &&
            finding.Message.Contains("unknown capability"));

        Assert.False(result.IsValid);
        Assert.Equal("Error", finding.Severity);
        Assert.Equal("PolicyReference", finding.Category);
        Assert.Equal("policy-1", finding.RelatedObjectId);
        Assert.Contains("unknown capability", finding.Message);
    }

    [Fact]
    public void UnknownIdentityReference_ProducesErrorFinding()
    {
        var result = Validate(
            capabilities: [Capability("known-capability")],
            identities: [Identity("known-identity")],
            policies: [Policy("policy-1", "missing-identity", "known-capability")]);

        var finding = Assert.Single(result.Findings, finding =>
            finding.Category == "PolicyReference" &&
            finding.Message.Contains("unknown identity"));

        Assert.False(result.IsValid);
        Assert.Equal("Error", finding.Severity);
        Assert.Equal("PolicyReference", finding.Category);
        Assert.Equal("policy-1", finding.RelatedObjectId);
        Assert.Contains("unknown identity", finding.Message);
    }

    [Fact]
    public void OrphanedCapability_ProducesInfoFinding()
    {
        var result = Validate(
            capabilities:
            [
                Capability("used-capability"),
                Capability("unused-capability")
            ],
            identities: [Identity("known-identity")],
            policies: [Policy("policy-1", "known-identity", "used-capability")]);

        var finding = Assert.Single(result.Findings);

        Assert.True(result.IsValid);
        Assert.Equal("Info", finding.Severity);
        Assert.Equal("OrphanedCapability", finding.Category);
        Assert.Equal("unused-capability", finding.RelatedObjectId);
    }

    [Fact]
    public void DuplicatePolicyIds_ProduceErrorFinding()
    {
        var result = Validate(
            capabilities: [Capability("known-capability")],
            identities: [Identity("known-identity")],
            policies:
            [
                Policy("policy-1", "known-identity", "known-capability"),
                Policy("policy-1", "known-identity", "known-capability")
            ]);

        var finding = Assert.Single(result.Findings);

        Assert.False(result.IsValid);
        Assert.Equal("Error", finding.Severity);
        Assert.Equal("PolicyIdentity", finding.Category);
        Assert.Equal("policy-1", finding.RelatedObjectId);
    }

    [Fact]
    public async Task Ready_IncludesValidationSummary()
    {
        using var response = await _client.GetAsync("/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.True(root.GetProperty("configValid").GetBoolean());
        Assert.Equal(0, root.GetProperty("validationErrors").GetInt32());
        Assert.True(root.GetProperty("validationWarnings").GetInt32() >= 0);
    }

    private static Seneschal.Core.Models.ConfigurationValidationResult Validate(
        IReadOnlyCollection<Capability> capabilities,
        IReadOnlyCollection<IdentityDefinition> identities,
        IReadOnlyCollection<Policy> policies)
    {
        return ConfigurationValidator.Validate(
            capabilities,
            identities,
            policies,
            new RuntimeSettings
            {
                Mode = EnforcementMode.LogOnly
            });
    }

    private static Capability Capability(string name)
    {
        return new Capability
        {
            Name = name,
            Description = $"{name} description",
            Risk = "Low",
            Category = "Test"
        };
    }

    private static IdentityDefinition Identity(string name)
    {
        return new IdentityDefinition
        {
            Name = name,
            Description = $"{name} description",
            Type = "Agent"
        };
    }

    private static Policy Policy(
        string name,
        string identity,
        string capability)
    {
        return new Policy
        {
            Name = name,
            Identity = identity,
            Capability = capability,
            Environment = "dev",
            Decision = "allow",
            Reason = "test policy"
        };
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
