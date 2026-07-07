using Seneschal.Client;
using Seneschal.Client.Models;
using Seneschal.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var seneschalBaseUrl = builder.Configuration["Seneschal:BaseUrl"] ??
    "http://localhost:5000";

if (!Uri.TryCreate(
        seneschalBaseUrl,
        UriKind.Absolute,
        out var seneschalUri))
{
    throw new InvalidOperationException(
        "Seneschal:BaseUrl must be an absolute URL.");
}

builder.Services.AddSingleton(new SeneschalClientOptions
{
    BaseUrl = seneschalUri,
    ApiKey = builder.Configuration["Seneschal:ApiKey"]
});
builder.Services.AddHttpClient<ISeneschalClient, SeneschalClient>();

var app = builder.Build();

app.UseRouting();
app.UseSeneschalCapabilityAttributes();

app.MapPost(
    "/deploy",
    async (
        DeployRequest request,
        ISeneschalClient seneschalClient,
        CancellationToken cancellationToken) =>
    {
        var decision = await seneschalClient.EvaluateAsync(
            new DecisionRequest
            {
                Identity = request.Identity,
                Capability = "DeployApplication",
                Context = new Dictionary<string, string>
                {
                    ["environment"] = request.Environment,
                    ["resource"] = request.Resource
                }
            },
            cancellationToken);

        if (IsAllow(decision.Decision))
        {
            return Results.Ok(new DeployAcceptedResponse(
                "Deployment started.",
                decision.Decision,
                decision.Reason,
                decision.PolicyMatched));
        }

        if (IsDeny(decision.Decision))
        {
            return Results.Json(
                new DeployRejectedResponse(
                    decision.Decision,
                    decision.Reason,
                    decision.PolicyMatched),
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (IsPendingApproval(decision.Decision))
        {
            return Results.Json(
                new DeployApprovalRequiredResponse(
                    decision.Decision,
                    decision.Reason,
                    decision.Obligations),
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Json(
            new DeployRejectedResponse(
                decision.Decision,
                $"Unsupported Seneschal decision '{decision.Decision}'.",
                decision.PolicyMatched),
            statusCode: StatusCodes.Status502BadGateway);
    });

app.MapPost(
    "/deploy/attribute",
    AttributeProtectedDeploy);

app.Map(
    "/deploy/middleware",
    middlewareApp =>
    {
        middlewareApp.UseSeneschalCapability(options =>
        {
            options.CapabilityId = "DeployApplication";
            options.IdentityId = "Developer";
            options.Environment = "dev";
            options.ResourceId = "sample-api";
        });

        middlewareApp.Run(async context =>
        {
            if (!HttpMethods.IsPost(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return;
            }

            await Results.Ok(new DeployAcceptedResponse(
                    "Deployment started by middleware-protected endpoint.",
                    "Allow",
                    "Seneschal middleware allowed the request.",
                    "n/a"))
                .ExecuteAsync(context);
        });
    });

app.Run();

[RequiresCapability(
    "DeployApplication",
    Environment = "dev",
    ResourceId = "sample-api")]
static IResult AttributeProtectedDeploy()
{
    return Results.Ok(new DeployAcceptedResponse(
        "Deployment started by attribute-protected endpoint.",
        "Allow",
        "Seneschal attribute middleware allowed the request.",
        "n/a"));
}

static bool IsAllow(string decision)
{
    return string.Equals(decision, "Allow", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(decision, "allow", StringComparison.OrdinalIgnoreCase);
}

static bool IsDeny(string decision)
{
    return string.Equals(decision, "Deny", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(decision, "deny", StringComparison.OrdinalIgnoreCase);
}

static bool IsPendingApproval(string decision)
{
    return string.Equals(
            decision,
            "PendingApproval",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            decision,
            "RequireApproval",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            decision,
            "requires_approval",
            StringComparison.OrdinalIgnoreCase);
}

public sealed record DeployRequest
{
    public string Identity { get; init; } = "Developer";

    public string Environment { get; init; } = "dev";

    public string Resource { get; init; } = "sample-api";
}

public sealed record DeployAcceptedResponse(
    string Message,
    string Decision,
    string Reason,
    string PolicyMatched);

public sealed record DeployRejectedResponse(
    string Decision,
    string Reason,
    string PolicyMatched);

public sealed record DeployApprovalRequiredResponse(
    string Decision,
    string Reason,
    IReadOnlyCollection<string> Obligations);
