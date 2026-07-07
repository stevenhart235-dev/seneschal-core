using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Seneschal.Client;
using Seneschal.Client.Models;
using Xunit;

namespace Seneschal.AspNetCore.Tests;

public sealed class SeneschalCapabilityMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_AllowsRequestWhenDecisionAllows()
    {
        var nextCalled = false;
        var client = new FakeSeneschalClient(new DecisionResult
        {
            Decision = "allow",
            Mode = "Enforce",
            Reason = "Allowed"
        });
        var middleware = CreateMiddleware(
            client,
            _ =>
            {
                nextCalled = true;
                _.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            });
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Assert.Equal("DeployApplication", client.Requests.Single().Capability);
        Assert.Equal("Developer", client.Requests.Single().Identity);
        Assert.Equal("dev", client.Requests.Single().Context["environment"]);
        Assert.Equal("sample-api", client.Requests.Single().Context["resource"]);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsForbiddenWhenDecisionDenies()
    {
        var nextCalled = false;
        var client = new FakeSeneschalClient(new DecisionResult
        {
            Decision = "deny",
            Mode = "Enforce",
            Reason = "Denied",
            PolicyMatched = "deny-policy"
        });
        var middleware = CreateMiddleware(
            client,
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);

        var body = await ReadBodyAsync(context);
        Assert.Contains("deny", body);
        Assert.Contains("Denied", body);
        Assert.Contains("deny-policy", body);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsConflictWhenDecisionRequiresApproval()
    {
        var nextCalled = false;
        var client = new FakeSeneschalClient(new DecisionResult
        {
            Decision = "requires_approval",
            Mode = "Enforce",
            Reason = "Needs approval",
            Obligations = ["ticket-required"]
        });
        var middleware = CreateMiddleware(
            client,
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);

        var body = await ReadBodyAsync(context);
        Assert.Contains("requires_approval", body);
        Assert.Contains("Needs approval", body);
        Assert.Contains("ticket-required", body);
    }

    [Fact]
    public async Task InvokeAsync_AllowsRequestWhenRuntimeModeIsMonitor()
    {
        var nextCalled = false;
        var client = new FakeSeneschalClient(new DecisionResult
        {
            Decision = "deny",
            Mode = "LogOnly",
            Reason = "Would deny"
        });
        var middleware = CreateMiddleware(
            client,
            _ =>
            {
                nextCalled = true;
                _.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            });
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }

    private static SeneschalCapabilityMiddleware CreateMiddleware(
        ISeneschalClient client,
        RequestDelegate next)
    {
        return new SeneschalCapabilityMiddleware(
            next,
            client,
            Options.Create(new SeneschalCapabilityOptions
            {
                CapabilityId = "DeployApplication",
                IdentityId = "Developer",
                Environment = "dev",
                ResourceId = "sample-api"
            }));
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/deploy";
        context.Response.Body = new MemoryStream();

        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;

        using var reader = new StreamReader(context.Response.Body);

        return await reader.ReadToEndAsync();
    }

    private sealed class FakeSeneschalClient : ISeneschalClient
    {
        private readonly DecisionResult _result;

        public FakeSeneschalClient(DecisionResult result)
        {
            _result = result;
        }

        public List<DecisionRequest> Requests { get; } = new();

        public Task<DecisionResult> EvaluateAsync(
            DecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            return Task.FromResult(_result);
        }
    }
}
