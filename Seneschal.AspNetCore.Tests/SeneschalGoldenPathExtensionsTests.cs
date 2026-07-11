using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Seneschal.AspNetCore;
using Seneschal.Client;
using Seneschal.Client.Models;
using Xunit;

namespace Seneschal.AspNetCore.Tests;

public sealed class SeneschalGoldenPathExtensionsTests
{
    [Fact]
    public void AddSeneschal_RegistersRequiredServices()
    {
        var services = new ServiceCollection();

        services.AddSeneschal(options =>
        {
            options.BaseUrl = new Uri("http://localhost:5000");
            options.ApiKey = "test-key";
        });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<ISeneschalClient>());
        Assert.NotNull(provider.GetService<IOptions<SeneschalOptions>>());
        Assert.NotNull(provider.GetService<IOptions<SeneschalClientOptions>>());
    }

    [Fact]
    public void AddSeneschal_AppliesBaseUrlAndApiKeyToClientOptions()
    {
        var services = new ServiceCollection();
        var baseUrl = new Uri("https://seneschal.example.test/");

        services.AddSeneschal(options =>
        {
            options.BaseUrl = baseUrl;
            options.ApiKey = "configured-key";
        });

        using var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptions<SeneschalClientOptions>>()
            .Value;

        Assert.Equal(baseUrl, options.BaseUrl);
        Assert.Equal("configured-key", options.ApiKey);
        Assert.Equal("/evaluate", options.EvaluatePath);
        Assert.Equal("X-Seneschal-Api-Key", options.ApiKeyHeaderName);
    }

    [Fact]
    public async Task Middleware_UsesConfiguredIdentityResolver()
    {
        var client = new RecordingClient();
        var middleware = CreateGoldenPathMiddleware(
            client,
            options =>
            {
                options.IdentityResolver = _ => "resolved-identity";
            });
        var context = CreateProtectedContext("orders.refund");

        await middleware.InvokeAsync(context);

        Assert.Equal("resolved-identity", client.Requests.Single().Identity);
    }

    [Fact]
    public async Task Middleware_UsesDefaultEnvironment()
    {
        var client = new RecordingClient();
        var middleware = CreateGoldenPathMiddleware(
            client,
            options =>
            {
                options.DefaultEnvironment = "dev";
            });
        var context = CreateProtectedContext("orders.refund");

        await middleware.InvokeAsync(context);

        Assert.Equal("dev", client.Requests.Single().Context["environment"]);
    }

    [Fact]
    public async Task UseSeneschal_EnablesAttributeEvaluation()
    {
        var client = new RecordingClient();
        var services = new ServiceCollection();
        services.AddSeneschal(options =>
        {
            options.BaseUrl = new Uri("http://localhost:5000");
            options.ApiKey = "test-key";
            options.IdentityResolver = _ => "golden-path-user";
            options.DefaultEnvironment = "production";
        });
        services.AddSingleton<ISeneschalClient>(client);

        using var provider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(provider);
        var nextCalled = false;
        app.UseSeneschal();
        app.Run(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var pipeline = app.Build();
        var context = CreateProtectedContext("ProductionDeployment");

        await pipeline(context);

        Assert.True(nextCalled);
        var request = Assert.Single(client.Requests);
        Assert.Equal("ProductionDeployment", request.Capability);
        Assert.Equal("golden-path-user", request.Identity);
        Assert.Equal("production", request.Context["environment"]);
    }

    [Fact]
    public async Task ExistingLowerLevelMiddlewareRegistration_StillWorks()
    {
        var client = new RecordingClient();
        var services = new ServiceCollection();
        services.AddSingleton<ISeneschalClient>(client);

        using var provider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(provider);
        app.UseSeneschalCapabilityAttributes(
            SeneschalEnforcementBehavior.HonorDecisionMode);
        app.Run(_ => Task.CompletedTask);
        var pipeline = app.Build();
        var context = CreateProtectedContext("legacy.capability");

        await pipeline(context);

        Assert.Equal("legacy.capability", client.Requests.Single().Capability);
    }

    private static SeneschalCapabilityAttributeMiddleware
        CreateGoldenPathMiddleware(
            ISeneschalClient client,
            Action<SeneschalOptions> configure)
    {
        var options = new SeneschalOptions
        {
            BaseUrl = new Uri("http://localhost:5000")
        };
        configure(options);

        return new SeneschalCapabilityAttributeMiddleware(
            _ => Task.CompletedTask,
            client,
            Options.Create(options));
    }

    private static DefaultHttpContext CreateProtectedContext(
        string capabilityId)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/protected";
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(
                new RequiresCapabilityAttribute(capabilityId)),
            "protected"));

        return context;
    }

    private sealed class RecordingClient : ISeneschalClient
    {
        public List<DecisionRequest> Requests { get; } = [];

        public Task<DecisionResult> EvaluateAsync(
            DecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            return Task.FromResult(new DecisionResult
            {
                Decision = "allow",
                Mode = "Enforce",
                Reason = "Allowed by test."
            });
        }
    }
}
