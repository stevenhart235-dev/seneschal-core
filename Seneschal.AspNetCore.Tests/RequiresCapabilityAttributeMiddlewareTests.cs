using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Seneschal.Client;
using Seneschal.Client.Models;
using Xunit;

namespace Seneschal.AspNetCore.Tests;

public sealed class RequiresCapabilityAttributeMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_AllowsAttributedEndpointWhenDecisionAllows()
    {
        var nextCalled = false;
        var client = new FakeSeneschalClient(new DecisionResult
        {
            Decision = "allow",
            Mode = "Enforce",
            ExecutionGuidance = "Proceed",
            Reason = "Allowed"
        });
        var middleware = CreateMiddleware(
            client,
            context =>
            {
                nextCalled = true;
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            });
        var context = CreateContext(new RequiresCapabilityAttribute("app.deploy")
        {
            Environment = "dev",
            ResourceId = "deployment-api"
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Assert.Equal("app.deploy", client.Requests.Single().Capability);
        Assert.Equal("Developer", client.Requests.Single().Identity);
        Assert.Equal("dev", client.Requests.Single().Context["environment"]);
        Assert.Equal("deployment-api", client.Requests.Single().Context["resource"]);
    }

    [Fact]
    public async Task InvokeAsync_BlocksAttributedEndpointWhenDecisionDenies()
    {
        var nextCalled = false;
        var client = new FakeSeneschalClient(new DecisionResult
        {
            Decision = "deny",
            Mode = "Enforce",
            ExecutionGuidance = "Block",
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
        var context = CreateContext(new RequiresCapabilityAttribute("app.deploy"));

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);

        var body = await ReadBodyAsync(context);
        Assert.Contains("deny", body);
        Assert.Contains("Denied", body);
        Assert.Contains("deny-policy", body);
    }

    [Fact]
    public async Task InvokeAsync_BlocksAttributedEndpointWhenDecisionRequiresApproval()
    {
        var nextCalled = false;
        var client = new FakeSeneschalClient(new DecisionResult
        {
            Decision = "requires_approval",
            Mode = "Enforce",
            ExecutionGuidance = "Pause",
            Reason = "Needs approval",
            PolicyMatched = "approval-policy",
            Obligations = ["ticket-required"]
        });
        var middleware = CreateMiddleware(
            client,
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });
        var context = CreateContext(new RequiresCapabilityAttribute("app.deploy"));

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);

        var body = await ReadBodyAsync(context);
        Assert.Contains("requires_approval", body);
        Assert.Contains("Needs approval", body);
        Assert.Contains("approval-policy", body);
        Assert.DoesNotContain("ticket-required", body);
    }

    [Fact]
    public async Task InvokeAsync_ContinuesNormallyWhenEndpointHasNoAttribute()
    {
        var nextCalled = false;
        var client = new FakeSeneschalClient(new DecisionResult
        {
            Decision = "deny",
            Mode = "Enforce",
            Reason = "Denied"
        });
        var middleware = CreateMiddleware(
            client,
            context =>
            {
                nextCalled = true;
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            });
        var context = CreateContext(attribute: null);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Empty(client.Requests);
        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_AllowsAttributedEndpointWhenRuntimeModeIsMonitor()
    {
        var nextCalled = false;
        var client = new FakeSeneschalClient(new DecisionResult
        {
            Decision = "deny",
            Mode = "LogOnly",
            ExecutionGuidance = "ContinueLogOnly",
            Reason = "Would deny"
        });
        var middleware = CreateMiddleware(
            client,
            context =>
            {
                nextCalled = true;
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            });
        var context = CreateContext(new RequiresCapabilityAttribute("app.deploy"));

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Single(client.Requests);
        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_FailsClosedForUnknownGuidanceDespiteAllowDecision()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(
            new FakeSeneschalClient(new DecisionResult
            {
                Decision = "allow",
                Mode = "LogOnly",
                ExecutionGuidance = "FutureValue",
                Reason = "Allowed by policy"
            }),
            _ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateContext(new RequiresCapabilityAttribute("app.deploy"));

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status502BadGateway, context.Response.StatusCode);
    }

    private static SeneschalCapabilityAttributeMiddleware CreateMiddleware(
        ISeneschalClient client,
        RequestDelegate next)
    {
        return new SeneschalCapabilityAttributeMiddleware(
            next,
            client,
            SeneschalEnforcementBehavior.HonorDecisionMode);
    }

    private static DefaultHttpContext CreateContext(
        RequiresCapabilityAttribute? attribute)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/deploy";
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "Developer")],
            authenticationType: "test"));

        var metadata = attribute is null
            ? new EndpointMetadataCollection()
            : new EndpointMetadataCollection(attribute);

        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            metadata,
            "test endpoint"));

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
