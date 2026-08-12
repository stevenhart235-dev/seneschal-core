using Xunit;

namespace Seneschal.Api.Tests;

public sealed class PolicyValidationCommandTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "seneschal-policy-validation-" + Guid.NewGuid().ToString("N"));

    public PolicyValidationCommandTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task ValidConfiguration_PassesWithZeroExitCode()
    {
        var result = await RunAsync(ValidPolicy());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Policy validation: PASSED", result.Output);
        Assert.Contains("0 errors, 0 warnings", result.Output);
    }

    [Fact]
    public async Task MalformedYaml_FailsWithoutEchoingSourceContent()
    {
        var result = await RunAsync("policies:\n  - name: [unterminated\n    apiKey: do-not-print");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Malformed or unsupported YAML", result.Output);
        Assert.DoesNotContain("do-not-print", result.Output);
    }

    [Fact]
    public async Task DuplicatePolicyId_Fails()
    {
        var result = await RunAsync(ValidPolicy() + ValidPolicyItem());

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Duplicate policy id 'policy-1'", result.Output);
    }

    [Fact]
    public async Task UnknownIdentity_Fails()
    {
        var result = await RunAsync(ValidPolicy(identity: "missing-identity"));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("unknown identity 'missing-identity'", result.Output);
    }

    [Fact]
    public async Task UnknownCapability_Fails()
    {
        var result = await RunAsync(ValidPolicy(capability: "missing-capability"));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("unknown capability 'missing-capability'", result.Output);
    }

    [Fact]
    public async Task MalformedCondition_FailsAsUnsupportedYaml()
    {
        var result = await RunAsync(ValidPolicy(extra: "    conditions: identity =="));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Malformed or unsupported YAML", result.Output);
    }

    [Theory]
    [InlineData("    operator: regex")]
    [InlineData("    values: [prod, dev]")]
    public async Task UnsupportedOperatorOrValue_Fails(string unsupportedField)
    {
        var result = await RunAsync(ValidPolicy(extra: unsupportedField));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Malformed or unsupported YAML", result.Output);
    }

    [Fact]
    public async Task WarningOnlyConfiguration_Passes()
    {
        var result = await RunAsync(
            ValidPolicy(),
            identities: "identities:\n  - name: known-identity\n    type: Agent\n    displayName: '   '\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("WARNING", result.Output);
        Assert.Contains("0 errors, 1 warning", result.Output);
    }

    [Fact]
    public async Task MultipleSemanticErrors_AreReportedTogether()
    {
        var policy = """
            policies:
              - name: broken-policy
                identity: missing-identity
                capability: missing-capability
                environment: dev
                decision: explode
            """;

        var result = await RunAsync(policy);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("missing required field 'reason'", result.Output);
        Assert.Contains("unknown identity", result.Output);
        Assert.Contains("unknown capability", result.Output);
        Assert.Contains("invalid decision", result.Output);
        Assert.Contains("4 errors", result.Output);
    }

    [Fact]
    public async Task MissingReferencedConfigurationFiles_Fail()
    {
        File.WriteAllText(Path.Combine(_directory, "policies.yaml"), ValidPolicy());
        var output = new StringWriter();

        var exitCode = await PolicyValidationCommand.RunAsync(
            [Path.Combine(_directory, "policies.yaml")], output);

        Assert.Equal(2, exitCode);
        Assert.Contains("identities.yaml", output.ToString());
        Assert.Contains("capabilities.yaml", output.ToString());
    }

    private async Task<(int ExitCode, string Output)> RunAsync(
        string policies,
        string? identities = null)
    {
        File.WriteAllText(Path.Combine(_directory, "policies.yaml"), policies);
        File.WriteAllText(
            Path.Combine(_directory, "identities.yaml"),
            identities ?? "identities:\n  - name: known-identity\n    type: Agent\n");
        File.WriteAllText(
            Path.Combine(_directory, "capabilities.yaml"),
            "capabilities:\n  - name: known-capability\n");

        var output = new StringWriter();
        var exitCode = await PolicyValidationCommand.RunAsync(
            [Path.Combine(_directory, "policies.yaml")], output);
        return (exitCode, output.ToString());
    }

    private static string ValidPolicy(
        string identity = "known-identity",
        string capability = "known-capability",
        string? extra = null) =>
        "policies:\n" + ValidPolicyItem(identity, capability, extra);

    private static string ValidPolicyItem(
        string identity = "known-identity",
        string capability = "known-capability",
        string? extra = null) =>
        $"  - name: policy-1\n" +
        $"    identity: {identity}\n" +
        $"    capability: {capability}\n" +
        "    environment: dev\n" +
        "    decision: allow\n" +
        "    reason: test policy\n" +
        (extra is null ? "" : extra + "\n");

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }
}
