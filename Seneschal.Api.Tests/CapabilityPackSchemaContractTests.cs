using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class CapabilityPackSchemaContractTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", ".."));
    private static readonly string ContractDirectory = Path.Combine(
        RepositoryRoot, "integrations", "contracts", "capability-pack");
    private static readonly string SchemaPath = Path.Combine(
        ContractDirectory, CapabilityPackSchemaValidator.SchemaFileName);

    [Fact]
    public void Manifest_AgreesWithSchemaConstantsAndChecksum()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            ContractDirectory, "capability-pack-contract.json")));
        var root = manifest.RootElement;
        Assert.Equal("seneschal.capability-pack", root.GetProperty("contract").GetString());
        Assert.Equal(CapabilityPackSchemaValidator.ContractVersion,
            root.GetProperty("contractVersion").GetString());
        Assert.Equal(CapabilityPackSchemaValidator.ContractRevision,
            root.GetProperty("schemaRevision").GetInt32());
        Assert.Equal(root.GetProperty("schemaSha256").GetString(),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(SchemaPath)))
                .ToLowerInvariant());
    }

    [Fact]
    public void ValidPackConformsAndUnknownFieldsAreRejected()
    {
        const string valid = """
            pack:
              id: postgres
              version: 1.0.0
            capabilities:
              - name: postgres.table.read
                risk: Low
            """;
        Assert.Empty(CapabilityPackSchemaValidator.Validate(valid, SchemaPath));
        Assert.NotEmpty(CapabilityPackSchemaValidator.Validate(
            valid.Replace("    risk: Low", "    risk: Low\n    score: 99"), SchemaPath));
    }
}
