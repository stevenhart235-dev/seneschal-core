using Xunit;

namespace Seneschal.Api.Tests;

public sealed class CapabilityPackValidationCommandTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "seneschal-pack-cli-" + Guid.NewGuid().ToString("N"));

    public CapabilityPackValidationCommandTests() =>
        Directory.CreateDirectory(_directory);

    [Fact]
    public async Task ValidPack_ReturnsZeroAndReportsIdentity()
    {
        var result = await RunAsync("""
            pack:
              id: postgres
              version: 1.0.0
            capabilities:
              - name: postgres.table.read
                risk: Low
            """);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Capability pack validation: VALID", result.Output);
        Assert.Contains("postgres", result.Output);
    }

    [Fact]
    public async Task InvalidCapabilityAndMalformedYaml_ReturnNonZero()
    {
        var invalid = await RunAsync("""
            pack:
              id: postgres
              version: 1.0.0
            capabilities:
              - name: postgres.table.read
                risk: Severe
            """);
        Assert.Equal(1, invalid.ExitCode);
        Assert.Contains("Capability Pack v1 violation", invalid.Output);

        var malformed = await RunAsync("pack: [unterminated");
        Assert.Equal(1, malformed.ExitCode);
        Assert.Contains("FAILED", malformed.Output);
    }

    private async Task<(int ExitCode, string Output)> RunAsync(string yaml)
    {
        var path = Path.Combine(_directory, Guid.NewGuid() + ".yaml");
        File.WriteAllText(path, yaml);
        var output = new StringWriter();
        var exitCode = await CapabilityPackValidationCommand.RunAsync([path], output);
        return (exitCode, output.ToString());
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
