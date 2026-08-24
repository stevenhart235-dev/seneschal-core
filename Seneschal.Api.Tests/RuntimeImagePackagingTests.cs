using System.Net;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class RuntimeImagePackagingTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(),
        $"seneschal-packaged-runtime-{Guid.NewGuid():N}");

    [Fact]
    public void ApiPublishAndContainerUseAuthoritativeRuntimeContracts()
    {
        var root = RepositoryRoot();
        var contracts = new[]
        {
            new
            {
                Include = @"..\integrations\contracts\policy\policy-schema.v1.json",
                Link = @"contracts\policy\policy-schema.v1.json",
                DockerPath = "integrations/contracts/policy/policy-schema.v1.json"
            },
            new
            {
                Include = @"..\integrations\contracts\proposed-governance-change\proposed-governance-change.v1.schema.json",
                Link = @"contracts\proposed-governance-change\proposed-governance-change.v1.schema.json",
                DockerPath = "integrations/contracts/proposed-governance-change/proposed-governance-change.v1.schema.json"
            }
        };

        var project = XDocument.Load(Path.Combine(root, "Seneschal.Api",
            "Seneschal.Api.csproj"));
        var dockerfile = File.ReadAllText(Path.Combine(root, "Dockerfile"));
        var dockerignore = File.ReadAllText(Path.Combine(root, ".dockerignore"));

        foreach (var contract in contracts)
        {
            var authoritative = Path.GetFullPath(Path.Combine(root,
                "Seneschal.Api", contract.Include));
            var packaged = Path.Combine(AppContext.BaseDirectory,
                contract.Link.Replace('\\', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(packaged),
                $"Packaged contract not found: {packaged}");
            Assert.Equal(File.ReadAllBytes(authoritative), File.ReadAllBytes(packaged));

            var projectItem = Assert.Single(project.Descendants(), element =>
                element.Name.LocalName == "None" &&
                string.Equals((string?)element.Attribute("Include"),
                    contract.Include, StringComparison.Ordinal));
            Assert.Equal(contract.Link, (string?)projectItem.Attribute("Link"));
            Assert.Equal("PreserveNewest",
                (string?)projectItem.Attribute("CopyToOutputDirectory"));
            Assert.Equal("PreserveNewest",
                (string?)projectItem.Attribute("CopyToPublishDirectory"));

            var copy = $"COPY {contract.DockerPath} " +
                contract.DockerPath[..(contract.DockerPath.LastIndexOf('/') + 1)];
            Assert.Contains(copy, dockerfile);
            Assert.True(dockerfile.IndexOf(copy, StringComparison.Ordinal) <
                dockerfile.IndexOf("RUN dotnet publish", StringComparison.Ordinal));
            Assert.Contains($"!{contract.DockerPath}", dockerignore);
        }
    }

    [Fact]
    public async Task PackagedRuntimeSchemaSupportsOperatorProposedChangePath()
    {
        Directory.CreateDirectory(_contentRoot);
        var policiesPath = Path.Combine(_contentRoot, "policies.yaml");
        await File.WriteAllTextAsync(policiesPath, """
            policies:
              - name: developer-production-operations
                identity: Developer
                capabilities:
                  - DeleteProductionDatabase
                  - DeployApplication
                environment: prod
                decision: allow
                reason: Packaged schema resolution test.
            """);

        await using var factory = new ApiApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseContentRoot(_contentRoot);
                builder.UseSetting("Seneschal:Configuration:PoliciesPath",
                    policiesPath);
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IAuditEventStore>();
                    services.AddSingleton<IAuditEventStore>(
                        new InMemoryAuditEventStore(
                            completeSinceUtc: DateTimeOffset.UtcNow.AddDays(-60)));
                });
            });
        using var client = factory.CreateClient();

        using var identity = await client.GetAsync(
            "/identity-activity?identityId=Developer");
        Assert.Equal(HttpStatusCode.OK, identity.StatusCode);

        using var review = await client.GetAsync(
            "/proposed-change-review?identityId=Developer&capabilityId=DeleteProductionDatabase&days=30");
        var html = await review.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        Assert.Contains("Proposed Governance Change Review", html);
        Assert.Contains("RemoveCapabilityFromPolicy", html);
        Assert.Contains("PROPOSED — NOT APPLIED", html);
    }

    private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", ".."));

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
            Directory.Delete(_contentRoot, recursive: true);
    }
}
