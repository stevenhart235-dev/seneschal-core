using Seneschal.Api.Mappers;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Seneschal.Core.Services;
using Xunit;

namespace Seneschal.Api.Tests.Services;

public sealed class BuiltInPostgresCapabilityPackTests : IDisposable
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", ".."));
    private static readonly string PackPath = Path.Combine(RepositoryRoot,
        "capability-packs", "postgres", "postgres.capability-pack.yaml");
    private static readonly string LocalCatalogPath = Path.Combine(RepositoryRoot,
        "Seneschal.Api", "Policies", "capabilities.yaml");
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(),
        "seneschal-postgres-pack-" + Guid.NewGuid().ToString("N"));

    private static readonly string[] ExpectedIds =
    [
        "postgres.database.connect",
        "postgres.database.read",
        "postgres.schema.read",
        "postgres.schema.modify",
        "postgres.table.read",
        "postgres.table.write",
        "postgres.table.truncate",
        "postgres.table.drop",
        "postgres.role.read",
        "postgres.role.modify",
        "postgres.extension.manage",
        "postgres.backup.create",
        "postgres.restore.execute"
    ];

    public BuiltInPostgresCapabilityPackTests() =>
        Directory.CreateDirectory(_tempDirectory);

    [Fact]
    public async Task BuiltInPack_ConformsToV1AndPassesRealCliValidation()
    {
        var yaml = File.ReadAllText(PackPath);
        Assert.Empty(CapabilityPackSchemaValidator.Validate(yaml));

        var output = new StringWriter();
        var exitCode = await CapabilityPackValidationCommand.RunAsync(
            [PackPath], output);

        Assert.Equal(0, exitCode);
        Assert.Contains("Capability pack validation: VALID", output.ToString());
        Assert.Contains("Pack:    postgres", output.ToString());
        Assert.Contains("Version: 1.0.0", output.ToString());
    }

    [Fact]
    public void BuiltInPack_HasExpectedOrderedCapabilitiesAndCompleteMetadata()
    {
        var pack = CapabilityLoader.LoadPack(PackPath);

        Assert.Equal("postgres", pack.Pack.Id);
        Assert.Equal("1.0.0", pack.Pack.Version);
        Assert.Equal("Seneschal", pack.Pack.Provider);
        Assert.Equal(ExpectedIds, pack.Capabilities.Select(item => item.Name));
        Assert.All(pack.Capabilities, capability =>
        {
            Assert.False(string.IsNullOrWhiteSpace(capability.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(capability.Description));
            Assert.False(string.IsNullOrWhiteSpace(capability.Category));
            Assert.Equal("postgresql", capability.Technology);
            Assert.Equal("Data Platform", capability.Owner);
            Assert.NotEmpty(capability.Tags);
            Assert.True(CapabilityMapper.TryParseRiskLevel(
                capability.Risk, out _));
        });
    }

    [Fact]
    public void CanonicalLocalCatalogAndBuiltInPack_MergeWithPackProvenance()
    {
        var loader = new CapabilityLoader(LocalCatalogPath, PackPath);
        var definitions = loader.GetCatalogDefinitions();

        foreach (var id in ExpectedIds)
        {
            var definition = Assert.Single(definitions,
                item => item.Capability.Name == id);
            Assert.Contains(definition.Sources, source =>
                source.Kind == "CapabilityPack" &&
                source.PackId == "postgres" &&
                source.PackVersion == "1.0.0");
        }

        Assert.Equal(["LocalCatalog", "CapabilityPack"],
            Assert.Single(definitions,
                item => item.Capability.Name == "postgres.table.drop")
                .Sources.Select(source => source.Kind));
        Assert.Equal(["LocalCatalog", "CapabilityPack"],
            Assert.Single(definitions,
                item => item.Capability.Name == "postgres.backup.create")
                .Sources.Select(source => source.Kind));
    }

    [Fact]
    public void ConflictingLocalDefinition_FailsPredictably()
    {
        var localPath = Path.Combine(_tempDirectory, "capabilities.yaml");
        File.WriteAllText(localPath, """
            capabilities:
              - name: postgres.table.read
                displayName: Conflicting Table Read
                risk: Critical
            """);

        var exception = Assert.Throws<InvalidDataException>(() =>
            new CapabilityLoader(localPath, PackPath));

        Assert.Contains("Conflicting capability id 'postgres.table.read'",
            exception.Message);
    }

    [Fact]
    public async Task CatalogPreservesPostgresPackProvenance()
    {
        var loader = new CapabilityLoader(LocalCatalogPath, PackPath);
        var entries = loader.GetCatalogDefinitions().Select(definition =>
            new CapabilityCatalogEntry
            {
                Capability = CapabilityMapper.ToCore(definition.Capability),
                Provenance = definition.Sources.Select(source =>
                    new CapabilityProvenance
                    {
                        Kind = source.Kind,
                        PackId = source.PackId,
                        PackVersion = source.PackVersion,
                        Path = source.Path
                    }).ToList()
            });
        var catalog = InMemoryCapabilityCatalog.FromEntries(entries);

        var entry = await catalog.GetByIdAsync("postgres.restore.execute");
        var provenance = Assert.Single(entry!.Provenance);
        Assert.Equal("CapabilityPack", provenance.Kind);
        Assert.Equal("postgres", provenance.PackId);
        Assert.Equal("1.0.0", provenance.PackVersion);
    }

    [Fact]
    public void PolicyEvaluationForPackedCapabilityId_RemainsUnchanged()
    {
        var packed = CapabilityLoader.LoadPack(PackPath).Capabilities.Single(
            item => item.Name == "postgres.table.write");
        var request = new DecisionRequest
        {
            RequestId = "postgres-pack-evaluation",
            Timestamp = DateTimeOffset.UtcNow,
            Identity = new Identity
            {
                Id = "migration-worker",
                Type = IdentityType.Application,
                Owner = "Data Platform",
                Environment = "production"
            },
            Capability = CapabilityMapper.ToCore(packed),
            Intent = new Intent { Action = "write", Reason = "test" },
            Resource = new Resource
            {
                Id = "orders",
                Type = "postgres-table",
                Environment = "production"
            }
        };
        var policy = new Policy
        {
            Id = "postgres-write",
            Name = "PostgreSQL write",
            Effect = DecisionType.Allow,
            Reason = "test",
            Conditions =
            {
                ["capability.id"] = "postgres.table.write"
            }
        };

        var result = new PolicyEvaluator().Evaluate(
            request, [policy], EnforcementMode.Enforce);

        Assert.Equal(DecisionType.Allow, result.Decision);
        Assert.Equal("postgres-write", Assert.Single(result.MatchedPolicies));
    }

    public void Dispose() => Directory.Delete(_tempDirectory, recursive: true);
}
