using Seneschal.Api.Mappers;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Seneschal.Core.Services;
using Xunit;

namespace Seneschal.Api.Tests.Services;

public sealed class BuiltInGitHubActionsCapabilityPackTests : IDisposable
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", ".."));
    private static readonly string PackPath = Path.Combine(RepositoryRoot,
        "capability-packs", "github-actions",
        "github-actions.capability-pack.yaml");
    private static readonly string LocalCatalogPath = Path.Combine(RepositoryRoot,
        "Seneschal.Api", "Policies", "capabilities.yaml");
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(),
        "seneschal-github-actions-pack-" + Guid.NewGuid().ToString("N"));

    private static readonly string[] ExpectedIds =
    [
        "github.workflow.read",
        "github.workflow.runs.read",
        "github.workflow.dispatch",
        "github.workflow.modify",
        "github.workflow.cancel",
        "github.deployment.create",
        "github.deployment.approve",
        "github.environment.read",
        "github.environment.modify",
        "github.artifact.read",
        "github.artifact.delete",
        "github.secret.read",
        "github.secret.modify",
        "github.repository.read",
        "github.repository.modify",
        "github.branch.protection.modify",
        "github.branch.delete"
    ];

    public BuiltInGitHubActionsCapabilityPackTests() =>
        Directory.CreateDirectory(_tempDirectory);

    [Fact]
    public async Task BuiltInPack_ConformsToV1AndPassesRealCliValidation()
    {
        Assert.Empty(CapabilityPackSchemaValidator.Validate(
            File.ReadAllText(PackPath)));
        var output = new StringWriter();
        var exitCode = await CapabilityPackValidationCommand.RunAsync(
            [PackPath], output);

        Assert.Equal(0, exitCode);
        Assert.Contains("Capability pack validation: VALID", output.ToString());
        Assert.Contains("Pack:    github-actions", output.ToString());
        Assert.Contains("Version: 1.0.0", output.ToString());
    }

    [Fact]
    public void BuiltInPack_HasExpectedOrderingAndCompleteMetadata()
    {
        var pack = CapabilityLoader.LoadPack(PackPath);

        Assert.Equal("github-actions", pack.Pack.Id);
        Assert.Equal("1.0.0", pack.Pack.Version);
        Assert.Equal("Seneschal", pack.Pack.Provider);
        Assert.Equal(ExpectedIds, pack.Capabilities.Select(item => item.Name));
        Assert.All(pack.Capabilities, capability =>
        {
            Assert.False(string.IsNullOrWhiteSpace(capability.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(capability.Description));
            Assert.False(string.IsNullOrWhiteSpace(capability.Category));
            Assert.Equal("github-actions", capability.Technology);
            Assert.False(string.IsNullOrWhiteSpace(capability.Owner));
            Assert.NotEmpty(capability.Tags);
            Assert.True(CapabilityMapper.TryParseRiskLevel(
                capability.Risk, out _));
        });
    }

    [Fact]
    public void CanonicalLocalCatalogAndPack_MergeWithExpectedProvenance()
    {
        var definitions = new CapabilityLoader(LocalCatalogPath, PackPath)
            .GetCatalogDefinitions();

        foreach (var id in ExpectedIds)
        {
            var definition = Assert.Single(definitions,
                item => item.Capability.Name == id);
            Assert.Contains(definition.Sources, source =>
                source.Kind == "CapabilityPack" &&
                source.PackId == "github-actions" &&
                source.PackVersion == "1.0.0");
        }

        Assert.Equal(["LocalCatalog", "CapabilityPack"],
            Assert.Single(definitions,
                item => item.Capability.Name == "github.workflow.dispatch")
                .Sources.Select(source => source.Kind));
    }

    [Fact]
    public void ConflictingLocalDefinition_FailsPredictably()
    {
        var localPath = Path.Combine(_tempDirectory, "capabilities.yaml");
        File.WriteAllText(localPath, """
            capabilities:
              - name: github.workflow.read
                displayName: Conflicting Workflow Read
                risk: Critical
            """);

        var exception = Assert.Throws<InvalidDataException>(() =>
            new CapabilityLoader(localPath, PackPath));
        Assert.Contains("Conflicting capability id 'github.workflow.read'",
            exception.Message);
    }

    [Fact]
    public async Task CatalogPreservesGitHubActionsPackProvenance()
    {
        var loader = new CapabilityLoader(LocalCatalogPath, PackPath);
        var catalog = InMemoryCapabilityCatalog.FromEntries(
            loader.GetCatalogDefinitions().Select(definition =>
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
                }));

        var entry = await catalog.GetByIdAsync("github.deployment.create");
        var provenance = Assert.Single(entry!.Provenance);
        Assert.Equal("CapabilityPack", provenance.Kind);
        Assert.Equal("github-actions", provenance.PackId);
        Assert.Equal("1.0.0", provenance.PackVersion);
    }

    [Fact]
    public void PolicyEvaluationForPackedCapabilityId_RemainsUnchanged()
    {
        var packed = CapabilityLoader.LoadPack(PackPath).Capabilities.Single(
            item => item.Name == "github.deployment.create");
        var request = new DecisionRequest
        {
            RequestId = "github-actions-pack-evaluation",
            Timestamp = DateTimeOffset.UtcNow,
            Identity = new Identity
            {
                Id = "release-workflow",
                Type = IdentityType.Pipeline,
                Owner = "Release Engineering",
                Environment = "production"
            },
            Capability = CapabilityMapper.ToCore(packed),
            Intent = new Intent { Action = "deploy", Reason = "test" },
            Resource = new Resource
            {
                Id = "checkout-api",
                Type = "github-deployment",
                Environment = "production"
            }
        };
        var policy = new Policy
        {
            Id = "github-deploy",
            Name = "GitHub deployment",
            Effect = DecisionType.Allow,
            Reason = "test",
            Conditions =
            {
                ["capability.id"] = "github.deployment.create"
            }
        };

        var result = new PolicyEvaluator().Evaluate(
            request, [policy], EnforcementMode.Enforce);

        Assert.Equal(DecisionType.Allow, result.Decision);
        Assert.Equal("github-deploy", Assert.Single(result.MatchedPolicies));
    }

    [Fact]
    public void TechnologyAlias_MapsGitHubActionsMetadataToGitHubExplorer()
    {
        var packed = CapabilityLoader.LoadPack(PackPath).Capabilities.Single(
            item => item.Name == "github.workflow.read");

        var technology = new TechnologyClassifier().Classify(
            packed.Name, CapabilityMapper.ToCore(packed));

        Assert.Equal("github", technology.Key);
        Assert.Equal("GitHub", technology.DisplayName);
    }

    public void Dispose() => Directory.Delete(_tempDirectory, recursive: true);
}
