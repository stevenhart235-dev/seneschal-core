using Seneschal.Api.Models;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class PolicyInitCommandTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "seneschal-policy-init-" + Guid.NewGuid().ToString("N"));

    public PolicyInitCommandTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task CreatesSchemaValidLoadableDocument()
    {
        var path = Path.Combine(_directory, "policies.yaml");
        var output = new StringWriter();

        var exitCode = await PolicyInitCommand.RunAsync([path], output);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(path));
        Assert.Empty(PolicySchemaValidator.Validate(File.ReadAllText(path)));
        var policy = Assert.Single(new PolicyLoader(path).GetPolicies());
        Assert.Equal("example-policy", policy.Name);
        Assert.Contains("Schema:   v1", output.ToString());
        Assert.Contains("Revision: 1", output.ToString());
        Assert.Contains("Status:   Valid", output.ToString());
    }

    [Fact]
    public async Task GeneratedDocumentPassesReferentialValidation()
    {
        var path = Path.Combine(_directory, "policies.yaml");
        Assert.Equal(0, await PolicyInitCommand.RunAsync([path], new StringWriter()));
        var policies = new PolicyLoader(path).GetPolicies();

        var result = ConfigurationValidator.Validate(
            [new Capability { Name = "DeployApplication" }],
            [new IdentityDefinition { Name = "Developer", Type = "Human" }],
            policies,
            new RuntimeSettings { Mode = EnforcementMode.LogOnly });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ExistingDestinationIsNotOverwritten()
    {
        var path = Path.Combine(_directory, "policies.yaml");
        File.WriteAllText(path, "existing content");
        var output = new StringWriter();

        var exitCode = await PolicyInitCommand.RunAsync([path], output);

        Assert.Equal(2, exitCode);
        Assert.Equal("existing content", File.ReadAllText(path));
        Assert.Contains("Policy file already exists", output.ToString());
        Assert.Contains("No changes were made", output.ToString());
    }

    [Fact]
    public async Task ForceExplicitlyOverwritesExistingDestination()
    {
        var path = Path.Combine(_directory, "policies.yaml");
        File.WriteAllText(path, "existing content");

        var exitCode = await PolicyInitCommand.RunAsync(
            [path, "--force"], new StringWriter());

        Assert.Equal(0, exitCode);
        Assert.Equal(
            PolicyInitCommand.GeneratedDocument + Environment.NewLine,
            File.ReadAllText(path));
    }

    [Fact]
    public async Task CreatesNestedDestinationDirectory()
    {
        var path = Path.Combine(_directory, "nested", "Policies", "policies.yaml");

        var exitCode = await PolicyInitCommand.RunAsync([path], new StringWriter());

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task UnwritableDestinationReturnsNonZero()
    {
        var parentFile = Path.Combine(_directory, "not-a-directory");
        File.WriteAllText(parentFile, "content");
        var output = new StringWriter();

        var exitCode = await PolicyInitCommand.RunAsync(
            [Path.Combine(parentFile, "policies.yaml")], output);

        Assert.Equal(2, exitCode);
        Assert.Contains("could not be created", output.ToString());
    }

    [Theory]
    [InlineData]
    [InlineData("--force")]
    [InlineData("one.yaml", "two.yaml")]
    [InlineData("one.yaml", "--unknown")]
    public async Task InvalidArgumentsReturnUsageAndNonZero(params string[] args)
    {
        var output = new StringWriter();

        var exitCode = await PolicyInitCommand.RunAsync(args, output);

        Assert.Equal(1, exitCode);
        Assert.Contains("Usage: seneschal policy init", output.ToString());
    }

    [Fact]
    public void GeneratedDocumentContainsOnlySchemaSupportedFieldsAndNoSecrets()
    {
        Assert.Empty(PolicySchemaValidator.Validate(
            PolicyInitCommand.GeneratedDocument));
        Assert.DoesNotContain("apiKey", PolicyInitCommand.GeneratedDocument,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", PolicyInitCommand.GeneratedDocument,
            StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }
}
