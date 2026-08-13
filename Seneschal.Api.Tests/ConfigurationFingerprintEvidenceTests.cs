using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Seneschal.Core.Interfaces;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class ConfigurationFingerprintEvidenceTests(
    ApiApplicationFactory factory) : IClassFixture<ApiApplicationFactory>
{
    [Fact]
    public async Task Evaluate_CommitsSemanticConfigurationFingerprint()
    {
        var id = $"fingerprint-{Guid.NewGuid():N}";
        using var response = await factory.CreateClient().PostAsJsonAsync("/evaluate", new
        {
            identity = "Developer", capability = "DeployApplication",
            context = new { environment = "dev", resource = id }
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var audit = factory.Services.GetRequiredService<IAuditEventStore>();
        var evidence = Assert.Single(await audit.GetRecentAsync(int.MaxValue),
            item => item.ResourceId == id);
        Assert.StartsWith("sha256:", evidence.GovernanceConfigurationFingerprint);
        Assert.Equal(71, evidence.GovernanceConfigurationFingerprint!.Length);
    }
}