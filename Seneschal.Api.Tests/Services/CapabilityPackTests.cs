using Seneschal.Api.Mappers;
using Seneschal.Api.Services;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests.Services;

public sealed class CapabilityPackTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "seneschal-pack-" + Guid.NewGuid().ToString("N"));
    private readonly string _localPath;
    private readonly string _packsPath;

    public CapabilityPackTests()
    {
        Directory.CreateDirectory(_directory);
        _packsPath = Path.Combine(_directory, "packs");
        Directory.CreateDirectory(_packsPath);
        _localPath = Write("capabilities.yaml", """
            capabilities:
              - name: local.deploy
                displayName: Local Deploy
                risk: Low
            """);
    }

    [Fact]
    public void LegacyLocalCatalogOnly_RemainsSupported()
    {
        var loader = new CapabilityLoader(_localPath);
        var capability = Assert.Single(loader.GetCapabilities());
        Assert.Equal("local.deploy", capability.Name);
        Assert.Equal("LocalCatalog", Assert.Single(
            loader.GetCatalogDefinitions()).Sources.Single().Kind);
    }

    [Fact]
    public async Task LocalCatalogAndMultiplePacks_LoadInDeterministicOrderWithProvenance()
    {
        WritePack("b.yaml", "beta", "2.0.0", "beta.read");
        WritePack("a.yaml", "alpha", "1.0.0", "alpha.read");

        var loader = new CapabilityLoader(_localPath, _packsPath);
        Assert.Equal(["local.deploy", "alpha.read", "beta.read"],
            loader.GetCapabilities().Select(item => item.Name));

        var entries = loader.GetCatalogDefinitions().Select(item =>
            new CapabilityCatalogEntry
            {
                Capability = CapabilityMapper.ToCore(item.Capability),
                Provenance = item.Sources.Select(source =>
                    new CapabilityProvenance
                    {
                        Kind = source.Kind,
                        PackId = source.PackId,
                        PackVersion = source.PackVersion
                    }).ToList()
            });
        var catalog = InMemoryCapabilityCatalog.FromEntries(entries);
        var packed = await catalog.GetByIdAsync("alpha.read");
        var source = Assert.Single(packed!.Provenance);
        Assert.Equal("CapabilityPack", source.Kind);
        Assert.Equal("alpha", source.PackId);
        Assert.Equal("1.0.0", source.PackVersion);
    }

    [Fact]
    public void IdenticalCrossSourceDefinition_DeduplicatesAndPreservesBothSources()
    {
        Write("same.yaml", """
            pack:
              id: local-extension
              version: 1.0.0
            capabilities:
              - name: local.deploy
                displayName: Local Deploy
                risk: Low
            """, _packsPath);

        var definition = Assert.Single(
            new CapabilityLoader(_localPath, _packsPath).GetCatalogDefinitions());
        Assert.Equal(2, definition.Sources.Count);
        Assert.Equal(["LocalCatalog", "CapabilityPack"],
            definition.Sources.Select(source => source.Kind));
    }

    [Fact]
    public void ConflictingCrossSourceDefinition_FailsWithoutLastFileWins()
    {
        WritePack("conflict.yaml", "conflict", "1.0.0", "local.deploy",
            displayName: "Different Deploy");
        var exception = Assert.Throws<InvalidDataException>(() =>
            new CapabilityLoader(_localPath, _packsPath));
        Assert.Contains("Conflicting capability id 'local.deploy'", exception.Message);
    }

    [Theory]
    [InlineData("", "1.0.0", "invalid pack id")]
    [InlineData("Postgres Pack", "1.0.0", "invalid pack id")]
    [InlineData("postgres", "v1", "invalid version")]
    public void MissingOrInvalidPackMetadata_Fails(
        string id, string version, string expected)
    {
        var path = Write("invalid.yaml", $"""
            pack:
              id: {id}
              version: {version}
            capabilities:
              - name: postgres.read
            """);
        var exception = Assert.Throws<InvalidDataException>(() =>
            CapabilityLoader.LoadPack(path));
        Assert.Contains(expected, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidCapabilityMetadata_IsReportedByExistingValidator()
    {
        var path = WritePack("invalid-risk.yaml", "postgres", "1.0.0",
            "postgres.read", risk: "Severe");
        var pack = CapabilityLoader.LoadPack(path);
        var result = ConfigurationValidator.ValidateCapabilities(pack.Capabilities);
        Assert.False(result.IsValid);
        Assert.Contains(result.Findings, finding =>
            finding.Message.Contains("invalid risk level 'Severe'"));
    }

    [Fact]
    public async Task ProvenanceDoesNotAlterCapabilityUsedByEvaluation()
    {
        var capability = CapabilityMapper.ToCore(
            new CapabilityLoader(_localPath).GetCapabilities().Single());
        var catalog = InMemoryCapabilityCatalog.FromEntries(
        [
            new CapabilityCatalogEntry
            {
                Capability = capability,
                Provenance =
                [
                    new CapabilityProvenance
                    {
                        Kind = "CapabilityPack",
                        PackId = "test",
                        PackVersion = "1.0.0"
                    }
                ]
            }
        ]);

        var loaded = await catalog.GetByIdAsync("local.deploy");
        Assert.Same(capability, loaded!.Capability);
        Assert.Equal("local.deploy", loaded.Capability.Id);
        Assert.Equal("test", Assert.Single(loaded.Provenance).PackId);
    }

    [Fact]
    public void FilePackPath_LoadsWithoutDirectoryOrNetworkResolution()
    {
        var packPath = WritePack("postgres.yaml", "postgres", "1.0.0",
            "postgres.table.read");
        var loader = new CapabilityLoader(_localPath, packPath);
        Assert.Equal(2, loader.GetCapabilities().Count);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string WritePack(string fileName, string id, string version,
        string capability, string displayName = "Read table", string risk = "Low") =>
        Write(fileName, $"""
            pack:
              id: {id}
              version: {version}
              description: Test pack
              provider: Test
            capabilities:
              - name: {capability}
                displayName: {displayName}
                category: data
                technology: postgresql
                risk: {risk}
                tags: [database, test]
            """, _packsPath);

    private string Write(string fileName, string content, string? directory = null)
    {
        var path = Path.Combine(directory ?? _directory, fileName);
        File.WriteAllText(path, content);
        return path;
    }
    [Fact]
    public void PackDirectory_RecursivelyLoadsNestedPacksInDeterministicOrder()
    {
        var nested = Path.Combine(_packsPath, "built-in", "postgres");
        Directory.CreateDirectory(nested);
        Write("postgres.yaml", """
            pack:
              id: postgres
              version: 1.0.0
            capabilities:
              - name: postgres.table.read
                technology: postgresql
                risk: Low
            """, nested);

        var definitions = new CapabilityLoader(_localPath, _packsPath)
            .GetCatalogDefinitions();

        Assert.Equal(["local.deploy", "postgres.table.read"],
            definitions.Select(item => item.Capability.Name));
        var source = Assert.Single(definitions[1].Sources);
        Assert.Equal("postgres", source.PackId);
    }
}
