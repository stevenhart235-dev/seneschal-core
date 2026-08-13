using Seneschal.Api.Mappers;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Seneschal.Core.Services;
using Xunit;

namespace Seneschal.Api.Tests.Services;

public sealed class BuiltInKubernetesCapabilityPackTests : IDisposable
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", ".."));
    private static readonly string PackPath = Path.Combine(RepositoryRoot,
        "capability-packs", "kubernetes", "kubernetes.capability-pack.yaml");
    private static readonly string LocalCatalogPath = Path.Combine(RepositoryRoot,
        "Seneschal.Api", "Policies", "capabilities.yaml");
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(),
        "seneschal-kubernetes-pack-" + Guid.NewGuid().ToString("N"));

    private static readonly string[] ExpectedIds =
    [
        "kubernetes.workload.read",
        "kubernetes.pod.logs.read",
        "kubernetes.workload.deploy",
        "kubernetes.workload.update",
        "kubernetes.workload.scale",
        "kubernetes.deployment.rollout",
        "kubernetes.workload.delete",
        "kubernetes.pod.exec",
        "kubernetes.config.read",
        "kubernetes.config.modify",
        "kubernetes.secret.read",
        "kubernetes.secret.modify",
        "kubernetes.namespace.read",
        "kubernetes.namespace.create",
        "kubernetes.namespace.delete"
    ];

    public BuiltInKubernetesCapabilityPackTests() =>
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
        Assert.Contains("Pack:    kubernetes", output.ToString());
        Assert.Contains("Version: 1.0.0", output.ToString());
    }

    [Fact]
    public void BuiltInPack_HasExpectedOrderingAndCompleteMetadata()
    {
        var pack = CapabilityLoader.LoadPack(PackPath);

        Assert.Equal("kubernetes", pack.Pack.Id);
        Assert.Equal("1.0.0", pack.Pack.Version);
        Assert.Equal("Seneschal", pack.Pack.Provider);
        Assert.Equal(ExpectedIds, pack.Capabilities.Select(item => item.Name));
        Assert.All(pack.Capabilities, capability =>
        {
            Assert.False(string.IsNullOrWhiteSpace(capability.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(capability.Description));
            Assert.False(string.IsNullOrWhiteSpace(capability.Category));
            Assert.Equal("kubernetes", capability.Technology);
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
                source.PackId == "kubernetes" &&
                source.PackVersion == "1.0.0");
        }

        foreach (var id in new[]
        {
            "kubernetes.pod.logs.read",
            "kubernetes.pod.exec",
            "kubernetes.secret.read",
            "kubernetes.namespace.create",
            "kubernetes.namespace.delete"
        })
        {
            Assert.Equal(["LocalCatalog", "CapabilityPack"],
                Assert.Single(definitions, item => item.Capability.Name == id)
                    .Sources.Select(source => source.Kind));
        }
    }

    [Fact]
    public void ConflictingLocalDefinition_FailsPredictably()
    {
        var localPath = Path.Combine(_tempDirectory, "capabilities.yaml");
        File.WriteAllText(localPath, """
            capabilities:
              - name: kubernetes.workload.read
                displayName: Conflicting Workload Read
                risk: Critical
            """);

        var exception = Assert.Throws<InvalidDataException>(() =>
            new CapabilityLoader(localPath, PackPath));
        Assert.Contains("Conflicting capability id 'kubernetes.workload.read'",
            exception.Message);
    }

    [Fact]
    public async Task CatalogPreservesKubernetesPackProvenance()
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

        var entry = await catalog.GetByIdAsync("kubernetes.workload.deploy");
        var provenance = Assert.Single(entry!.Provenance);
        Assert.Equal("CapabilityPack", provenance.Kind);
        Assert.Equal("kubernetes", provenance.PackId);
        Assert.Equal("1.0.0", provenance.PackVersion);
    }

    [Fact]
    public void PolicyEvaluationForPackedCapabilityId_RemainsUnchanged()
    {
        var packed = CapabilityLoader.LoadPack(PackPath).Capabilities.Single(
            item => item.Name == "kubernetes.workload.deploy");
        var request = new DecisionRequest
        {
            RequestId = "kubernetes-pack-evaluation",
            Timestamp = DateTimeOffset.UtcNow,
            Identity = new Identity
            {
                Id = "release-controller",
                Type = IdentityType.Application,
                Owner = "Platform Engineering",
                Environment = "production"
            },
            Capability = CapabilityMapper.ToCore(packed),
            Intent = new Intent { Action = "deploy", Reason = "test" },
            Resource = new Resource
            {
                Id = "checkout-api",
                Type = "kubernetes-workload",
                Environment = "production"
            }
        };
        var policy = new Policy
        {
            Id = "kubernetes-deploy",
            Name = "Kubernetes deploy",
            Effect = DecisionType.Allow,
            Reason = "test",
            Conditions =
            {
                ["capability.id"] = "kubernetes.workload.deploy"
            }
        };

        var result = new PolicyEvaluator().Evaluate(
            request, [policy], EnforcementMode.Enforce);

        Assert.Equal(DecisionType.Allow, result.Decision);
        Assert.Equal("kubernetes-deploy", Assert.Single(result.MatchedPolicies));
    }

    public void Dispose() => Directory.Delete(_tempDirectory, recursive: true);
}
