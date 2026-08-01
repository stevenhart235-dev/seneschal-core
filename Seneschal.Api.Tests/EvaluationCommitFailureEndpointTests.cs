using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Core.Exceptions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class EvaluationCommitFailureEndpointTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public EvaluationCommitFailureEndpointTests(ApiApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Evaluate_CommitFailureReturnsServiceUnavailable()
    {
        using var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEvaluationCommitCoordinator>();
                services.AddSingleton<IEvaluationCommitCoordinator,
                    ThrowingEvaluationCommitCoordinator>();
            });
        }).CreateClient();

        using var response = await client.PostAsJsonAsync("/evaluate", new
        {
            identity = "Developer",
            capability = "DeployApplication",
            context = new
            {
                environment = "dev",
                resource = "commit-failure-test"
            }
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("could not be committed", body);
        Assert.DoesNotContain("allow", body, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ThrowingEvaluationCommitCoordinator :
        IEvaluationCommitCoordinator
    {
        public Task CommitAsync(
            EvaluationCommit evaluationCommit,
            CancellationToken cancellationToken = default)
        {
            throw new EvaluationCommitException("Injected commit failure.");
        }
    }
}
