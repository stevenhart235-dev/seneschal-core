using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Seneschal.AspNetCore;
using Seneschal.Client;
using Seneschal.Client.Models;
using Xunit;

namespace Seneschal.AspNetCore.Tests;

public sealed class SeneschalCustomerExperienceHardeningTests
{
    [Fact]
    public void AddSeneschal_MissingBaseUrlFailsClearly()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddSeneschal(options =>
            {
                options.ApiKey = "test-key";
            }));

        Assert.Equal(
            "Seneschal configuration is invalid: BaseUrl is required.",
            exception.Message);
    }

    [Fact]
    public void AddSeneschal_RelativeBaseUrlFailsClearly()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddSeneschal(options =>
            {
                options.BaseUrl = new Uri("relative", UriKind.Relative);
                options.ApiKey = "test-key";
            }));

        Assert.Equal(
            "Seneschal configuration is invalid: BaseUrl must be an absolute URI.",
            exception.Message);
    }

    [Fact]
    public void AddSeneschal_MissingApiKeyFailsWithoutExposingValues()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddSeneschal(options =>
            {
                options.BaseUrl = new Uri("http://localhost:5000");
                options.ApiKey = "   ";
            }));

        Assert.Equal(
            "Seneschal configuration is invalid: ApiKey is required.",
            exception.Message);
    }

    [Fact]
    public void AddSeneschal_BindsAppsettingsStyleConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seneschal:BaseUrl"] = "http://localhost:5000",
                ["Seneschal:ApiKey"] = "bound-key",
                ["Seneschal:DefaultEnvironment"] = "dev",
                ["Seneschal:FailureBehavior"] = "FailOpen",
                ["Seneschal:Timeout"] = "00:00:03"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddSeneschal(configuration.GetSection("Seneschal"));

        using var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptions<SeneschalOptions>>()
            .Value;
        var clientOptions = provider
            .GetRequiredService<IOptions<SeneschalClientOptions>>()
            .Value;

        Assert.Equal(new Uri("http://localhost:5000"), options.BaseUrl);
        Assert.Equal("bound-key", options.ApiKey);
        Assert.Equal("dev", options.DefaultEnvironment);
        Assert.Equal(SeneschalFailureBehavior.FailOpen, options.FailureBehavior);
        Assert.Equal(TimeSpan.FromSeconds(3), options.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(3), clientOptions.Timeout);
    }

    [Fact]
    public async Task FailClosed_ReturnsSafeUnavailableResponse()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(
            new ThrowingClient(new SeneschalClientException(
                "Unable to reach the Seneschal runtime.",
                new HttpRequestException("connection refused"))),
            SeneschalFailureBehavior.FailClosed,
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        var json = await ReadJsonAsync(context);
        Assert.Equal("unavailable", json.RootElement.GetProperty("decision").GetString());
        Assert.True(json.RootElement.TryGetProperty("policyMatched", out _));
        Assert.Equal(3, json.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public async Task FailOpen_ContinuesWhenRuntimeIsUnavailable()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(
            new ThrowingClient(new SeneschalClientException(
                "Unable to reach the Seneschal runtime.")),
            SeneschalFailureBehavior.FailOpen,
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

        await middleware.InvokeAsync(CreateContext());

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task MalformedDecision_ReturnsBadGateway()
    {
        var middleware = CreateMiddleware(
            new ThrowingClient(new SeneschalClientException(
                "Seneschal returned an invalid decision response.",
                new JsonException("invalid"))),
            SeneschalFailureBehavior.FailClosed,
            _ => Task.CompletedTask);
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status502BadGateway, context.Response.StatusCode);
        var json = await ReadJsonAsync(context);
        Assert.Equal("invalid_response", json.RootElement.GetProperty("decision").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, 401, "authentication_failed")]
    [InlineData(HttpStatusCode.Forbidden, 403, "integration_forbidden")]
    public async Task IntegrationRejection_ReturnsSafeDistinctResponse(
        HttpStatusCode upstreamStatus,
        int expectedStatus,
        string expectedDecision)
    {
        var middleware = CreateMiddleware(
            new ThrowingClient(new SeneschalClientException(
                "upstream rejection",
                upstreamStatus,
                "sensitive upstream body")),
            SeneschalFailureBehavior.FailClosed,
            _ => Task.CompletedTask);
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        var json = await ReadJsonAsync(context);
        Assert.Equal(expectedDecision, json.RootElement.GetProperty("decision").GetString());
        Assert.DoesNotContain("sensitive", json.RootElement.GetRawText());
    }

    [Fact]
    public async Task DenyResponse_ContainsOnlySafeConsistentFields()
    {
        var middleware = CreateMiddleware(
            new FixedClient(new DecisionResult
            {
                Decision = "deny",
                Mode = "Enforce",
                ExecutionGuidance = "Block",
                Reason = "Denied by policy.",
                PolicyMatched = "deny-policy"
            }),
            SeneschalFailureBehavior.FailClosed,
            _ => Task.CompletedTask);
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        var json = await ReadJsonAsync(context);
        Assert.Equal(
            ["decision", "reason", "policyMatched"],
            json.RootElement.EnumerateObject().Select(property => property.Name));
    }

    private static SeneschalCapabilityAttributeMiddleware CreateMiddleware(
        ISeneschalClient client,
        SeneschalFailureBehavior failureBehavior,
        RequestDelegate next)
    {
        return new SeneschalCapabilityAttributeMiddleware(
            next,
            client,
            Options.Create(new SeneschalOptions
            {
                BaseUrl = new Uri("http://localhost:5000"),
                ApiKey = "test-key",
                FailureBehavior = failureBehavior
            }));
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/deploy";
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(
                new RequiresCapabilityAttribute("production.deployment.execute")),
            "deploy"));
        return context;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonDocument.ParseAsync(context.Response.Body);
    }

    private sealed class ThrowingClient(Exception exception) : ISeneschalClient
    {
        public Task<DecisionResult> EvaluateAsync(
            DecisionRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromException<DecisionResult>(exception);
    }

    private sealed class FixedClient(DecisionResult decision) : ISeneschalClient
    {
        public Task<DecisionResult> EvaluateAsync(
            DecisionRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(decision);
    }
}
