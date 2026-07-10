using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Seneschal.Api.Services;
using Xunit;

namespace Seneschal.Api.Tests.Services;

[Collection("CurrentDirectoryIsolation")]
public sealed class YamlConfigurationLoaderPathTests
{
    [Fact]
    public void RelativePaths_ResolveFromApplicationContentRoot()
    {
        using var files = TestConfigurationFiles.Create();
        var configuration = BuildConfiguration(
            files.GetRelativePaths("Configuration"));
        var environment = CreateEnvironment(files.ContentRootPath);

        AssertConfigurationLoads(environment, configuration);
    }

    [Fact]
    public void AbsolutePaths_RemainSupported()
    {
        using var files = TestConfigurationFiles.Create();
        var configuration = BuildConfiguration(files.GetAbsolutePaths());
        var environment = CreateEnvironment(
            Path.Combine(files.ContentRootPath, "unused-content-root"));

        AssertConfigurationLoads(environment, configuration);
    }

    [Fact]
    public void StartupLoading_DoesNotDependOnCurrentDirectory()
    {
        using var files = TestConfigurationFiles.Create();
        var unrelatedDirectory = Path.Combine(
            files.ContentRootPath,
            "unrelated-current-directory");
        Directory.CreateDirectory(unrelatedDirectory);
        var originalCurrentDirectory = Environment.CurrentDirectory;

        try
        {
            Environment.CurrentDirectory = unrelatedDirectory;

            var configuration = BuildConfiguration(
                files.GetRelativePaths("Configuration"));
            var environment = CreateEnvironment(files.ContentRootPath);

            AssertConfigurationLoads(environment, configuration);
        }
        finally
        {
            Environment.CurrentDirectory = originalCurrentDirectory;
        }
    }

    private static void AssertConfigurationLoads(
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        var capability = Assert.Single(
            new CapabilityLoader(environment, configuration)
                .GetCapabilities());
        var identity = Assert.Single(
            new IdentityLoader(environment, configuration)
                .GetIdentities());
        var policy = Assert.Single(
            new PolicyLoader(environment, configuration)
                .GetPolicies());
        var key = Assert.Single(
            new IntegrationApiKeyLoader(environment, configuration)
                .GetKeys());

        Assert.Equal("test.capability", capability.Name);
        Assert.Equal("test-identity", identity.Name);
        Assert.Equal("test-policy", policy.Name);
        Assert.Equal("test-key", key.Key);
    }

    private static IConfiguration BuildConfiguration(
        IReadOnlyDictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static IHostEnvironment CreateEnvironment(string contentRootPath)
    {
        return new TestHostEnvironment
        {
            ContentRootPath = contentRootPath
        };
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Seneschal.Api.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }

    private sealed class TestConfigurationFiles : IDisposable
    {
        private TestConfigurationFiles(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
        }

        public string ContentRootPath { get; }

        public static TestConfigurationFiles Create()
        {
            var contentRoot = Path.Combine(
                Path.GetTempPath(),
                $"seneschal-loader-tests-{Guid.NewGuid():N}");
            var configurationDirectory = Path.Combine(
                contentRoot,
                "Configuration");
            Directory.CreateDirectory(configurationDirectory);

            File.WriteAllText(
                Path.Combine(configurationDirectory, "capabilities.yaml"),
                """
                capabilities:
                  - name: test.capability
                    description: Test capability
                    risk: Low
                    category: Test
                """);
            File.WriteAllText(
                Path.Combine(configurationDirectory, "identities.yaml"),
                """
                identities:
                  - name: test-identity
                    description: Test identity
                    type: Service
                """);
            File.WriteAllText(
                Path.Combine(configurationDirectory, "policies.yaml"),
                """
                policies:
                  - name: test-policy
                    identity: test-identity
                    capability: test.capability
                    environment: test
                    decision: allow
                    reason: Test policy
                """);
            File.WriteAllText(
                Path.Combine(configurationDirectory, "integration-keys.yaml"),
                """
                integrationKeys:
                  - name: test-integration
                    key: test-key
                    enabled: true
                    allowedIdentities:
                      - test-identity
                    allowedCapabilities:
                      - test.capability
                """);

            return new TestConfigurationFiles(contentRoot);
        }

        public IReadOnlyDictionary<string, string?> GetRelativePaths(
            string directory)
        {
            return Paths(fileName => Path.Combine(directory, fileName));
        }

        public IReadOnlyDictionary<string, string?> GetAbsolutePaths()
        {
            return Paths(fileName => Path.Combine(
                ContentRootPath,
                "Configuration",
                fileName));
        }

        public void Dispose()
        {
            Directory.Delete(ContentRootPath, recursive: true);
        }

        private static IReadOnlyDictionary<string, string?> Paths(
            Func<string, string> path)
        {
            return new Dictionary<string, string?>
            {
                ["Seneschal:Configuration:CapabilitiesPath"] =
                    path("capabilities.yaml"),
                ["Seneschal:Configuration:IdentitiesPath"] =
                    path("identities.yaml"),
                ["Seneschal:Configuration:PoliciesPath"] =
                    path("policies.yaml"),
                ["Seneschal:Configuration:IntegrationKeysPath"] =
                    path("integration-keys.yaml")
            };
        }
    }
}

[CollectionDefinition("CurrentDirectoryIsolation", DisableParallelization = true)]
public sealed class CurrentDirectoryIsolationCollection;
