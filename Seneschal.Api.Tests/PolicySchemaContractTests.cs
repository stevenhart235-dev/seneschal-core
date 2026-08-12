using System.Security.Cryptography;
using System.Text.Json;
using Seneschal.Api.Services;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class PolicySchemaContractTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", ".."));
    private static readonly string ContractDirectory = Path.Combine(
        RepositoryRoot, "integrations", "contracts", "policy");
    private static readonly string SchemaPath = Path.Combine(
        ContractDirectory, PolicySchemaValidator.SchemaFileName);

    [Fact]
    public void Manifest_AgreesWithSchemaConstantsAndChecksum()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(ContractDirectory, "policy-contract.json")));
        var root = manifest.RootElement;

        Assert.Equal("seneschal.policy-authoring", root.GetProperty("contract").GetString());
        Assert.Equal(PolicySchemaValidator.ContractVersion,
            root.GetProperty("contractVersion").GetString());
        Assert.Equal(PolicySchemaValidator.ContractRevision,
            root.GetProperty("schemaRevision").GetInt32());
        Assert.Equal(PolicySchemaValidator.SchemaFileName,
            root.GetProperty("schema").GetString());
        Assert.Equal(root.GetProperty("schemaSha256").GetString(),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(SchemaPath)))
                .ToLowerInvariant());

        using var schema = JsonDocument.Parse(File.ReadAllText(SchemaPath));
        Assert.Equal("https://json-schema.org/draft/2020-12/schema",
            schema.RootElement.GetProperty("$schema").GetString());
        Assert.EndsWith("/policy-schema.v1.json",
            schema.RootElement.GetProperty("$id").GetString());
    }

    [Theory]
    [InlineData("policies.yaml")]
    [InlineData("policies.production-freeze.yaml")]
    public void CheckedInPolicyFiles_ConformToSchemaAndLoad(string fileName)
    {
        var path = Path.Combine(RepositoryRoot, "Seneschal.Api", "Policies", fileName);

        Assert.Empty(PolicySchemaValidator.Validate(File.ReadAllText(path), SchemaPath));
        Assert.NotEmpty(new PolicyLoader(path).GetPolicies());
    }

    [Fact]
    public void ScalarAndPluralTargets_AreSupported()
    {
        const string yaml = """
            policies:
              - name: scalar
                identity: one
                capability: deploy
                environment: dev
                decision: warn
                reason: scalar targets
              - name: plural
                displayName: Plural Policy
                description: Description
                owner: Platform
                severity: medium
                rationale: Rationale
                identities: [one, two]
                capabilities: [deploy, read]
                environments: [dev, production]
                decision: log_only
                reason: plural targets
            """;

        Assert.Empty(PolicySchemaValidator.Validate(yaml, SchemaPath));
    }

    [Theory]
    [InlineData("""
        policies:
          - identity: one
            capability: deploy
            environment: dev
            decision: allow
            reason: missing name
        """)]
    [InlineData("""
        policies:
          - name: missing-target
            capability: deploy
            environment: dev
            decision: allow
            reason: missing identity
        """)]
    public void RequiredFields_AreEnforced(string yaml)
    {
        Assert.NotEmpty(PolicySchemaValidator.Validate(yaml, SchemaPath));
    }

    [Theory]
    [InlineData("allow")]
    [InlineData("deny")]
    [InlineData("warn")]
    [InlineData("log_only")]
    [InlineData("requires_approval")]
    public void SupportedDecisionValues_Conform(string decision)
    {
        Assert.Empty(PolicySchemaValidator.Validate(Policy(decision), SchemaPath));
    }

    [Fact]
    public void UnsupportedDecisionValue_IsRejected()
    {
        Assert.NotEmpty(PolicySchemaValidator.Validate(Policy("approve"), SchemaPath));
    }

    [Fact]
    public void UnknownProperty_IsRejected()
    {
        Assert.NotEmpty(PolicySchemaValidator.Validate(
            Policy("allow").Replace("    reason:", "    operator: equals\n    reason:"),
            SchemaPath));
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("identities")]
    [InlineData("decision")]
    public void MalformedTypes_AreRejected(string property)
    {
        var yaml = property switch
        {
            "identity" => Policy("allow").Replace("    identity: one", "    identity: 42"),
            "identities" => Policy("allow").Replace("    identity: one", "    identities: one"),
            "decision" => Policy("allow").Replace("    decision: allow", "    decision: true"),
            _ => throw new ArgumentOutOfRangeException(nameof(property))
        };

        Assert.NotEmpty(PolicySchemaValidator.Validate(yaml, SchemaPath));
    }

    [Fact]
    public async Task SchemaSuccess_StillRunsExistingReferentialValidation()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "policies.yaml"), Policy("allow"));
            File.WriteAllText(Path.Combine(directory, "identities.yaml"),
                "identities:\n  - name: other\n    type: Agent\n");
            File.WriteAllText(Path.Combine(directory, "capabilities.yaml"),
                "capabilities:\n  - name: deploy\n");
            var output = new StringWriter();

            var exitCode = await PolicyValidationCommand.RunAsync(
                [Path.Combine(directory, "policies.yaml")], output);

            Assert.Equal(2, exitCode);
            Assert.Contains("unknown identity 'one'", output.ToString());
            Assert.DoesNotContain("Policy Schema v1 violation", output.ToString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SchemaFailure_DoesNotPrintPropertyValue()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "policies.yaml"),
                Policy("allow").Replace(
                    "    reason: test",
                    "    integrationKey: do-not-print\n    reason: test"));
            File.WriteAllText(Path.Combine(directory, "identities.yaml"),
                "identities:\n  - name: one\n    type: Agent\n");
            File.WriteAllText(Path.Combine(directory, "capabilities.yaml"),
                "capabilities:\n  - name: deploy\n");
            var output = new StringWriter();

            var exitCode = await PolicyValidationCommand.RunAsync(
                [Path.Combine(directory, "policies.yaml")], output);

            Assert.Equal(2, exitCode);
            Assert.Contains("Policy Schema v1 violation", output.ToString());
            Assert.DoesNotContain("do-not-print", output.ToString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string Policy(string decision) => $$"""
        policies:
          - name: policy
            identity: one
            capability: deploy
            environment: dev
            decision: {{decision}}
            reason: test
        """;
}
