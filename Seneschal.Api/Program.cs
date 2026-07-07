using Seneschal.Api.Mappers;
using Seneschal.Api.Models;
using Seneschal.Api.Services;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Repositories;
using Seneschal.Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSingleton<PolicyLoader>();
builder.Services.AddSingleton<
    Seneschal.Core.Interfaces.IPolicyEvaluator,
    Seneschal.Core.Services.PolicyEvaluator>();
builder.Services.AddSingleton<CoreDecisionService>();
builder.Services.AddSingleton<PolicyValidator>();
builder.Services.AddSingleton<IAuditEventStore, InMemoryAuditEventStore>();
builder.Services.AddSingleton<IAuditSink>(
    services => services.GetRequiredService<IAuditEventStore>());
builder.Services.AddSingleton(new RuntimeSettings
{
    Mode = Seneschal.Core.Enums.EnforcementMode.LogOnly
});
builder.Services.AddSingleton<CapabilityLoader>();
builder.Services.AddSingleton<IdentityLoader>();
builder.Services.AddSingleton<PolicyProjector>();
builder.Services.AddSingleton<ICapabilityCatalog>(services =>
    new InMemoryCapabilityCatalog(
        services
            .GetRequiredService<CapabilityLoader>()
            .GetCapabilities()
            .Select(CapabilityMapper.ToCore)));
builder.Services.AddSingleton<IGovernanceGraph>(services =>
    new InMemoryGovernanceGraph(
        services
            .GetRequiredService<PolicyProjector>()
            .Project(
                services
                    .GetRequiredService<PolicyLoader>()
                    .GetPolicies())));
builder.Services.AddSingleton<ICapabilityExplorer, CapabilityExplorer>();
builder.Services.AddSingleton<GraphBuilder>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.Services.GetRequiredService<PolicyValidator>();

app.MapPost("/evaluate", (DecisionRequest request, CoreDecisionService decisionService) =>
{
    var result = decisionService.Evaluate(request);

    return Results.Ok(result);
});

app.MapGet("/audit", async (
    HttpRequest request,
    string? identityId,
    string? capabilityId,
    string? decision,
    string? enforcementMode,
    string? environment,
    string? matchedPolicy,
    IAuditEventStore auditEventStore,
    CancellationToken cancellationToken) =>
{
    var filter = new AuditEventFilter
    {
        IdentityId = identityId,
        CapabilityId = capabilityId,
        Decision = decision,
        EnforcementMode = enforcementMode,
        Environment = environment,
        MatchedPolicy = matchedPolicy
    };
    var events = AuditEventFilterService.Apply(
        (await auditEventStore.GetRecentAsync(
            cancellationToken: cancellationToken))
            .Select(AuditEventMapper.ToApi),
        filter);

    if (AcceptsHtml(request))
    {
        return Results.Content(
            AuditTrailPageRenderer.Render(events, filter),
            "text/html; charset=utf-8");
    }

    return Results.Ok(events);
});

app.MapGet("/audit/{auditEventId}", async (
    string auditEventId,
    IAuditEventStore auditEventStore,
    CancellationToken cancellationToken) =>
{
    var auditEvent = await auditEventStore.GetByIdAsync(
        auditEventId,
        cancellationToken);

    if (auditEvent is null)
    {
        return Results.Content(
            AuditEventDetailPageRenderer.RenderNotFound(auditEventId),
            "text/html; charset=utf-8",
            statusCode: StatusCodes.Status404NotFound);
    }

    return Results.Content(
        AuditEventDetailPageRenderer.Render(
            AuditEventMapper.ToApi(auditEvent)),
        "text/html; charset=utf-8");
});

app.MapGet("/policies", async (
    HttpRequest request,
    PolicyLoader policyLoader,
    IGovernanceGraph governanceGraph,
    CancellationToken cancellationToken) =>
{
    if (AcceptsHtml(request))
    {
        return Results.Content(
            await PolicyExplorerPageRenderer.RenderAsync(
                policyLoader.GetPolicies(),
                governanceGraph,
                cancellationToken),
            "text/html; charset=utf-8");
    }

    return Results.Ok(policyLoader.GetPolicies());
});

app.MapGet("/capabilities",
    (CapabilityLoader loader) =>
{
    return Results.Ok(loader.GetCapabilities());
});

app.MapGet(
    "/capabilities/{capabilityId}/overview",
    async (
        string capabilityId,
        ICapabilityExplorer explorer,
        CancellationToken cancellationToken) =>
    {
        var overview = await explorer.GetOverviewAsync(
            new Seneschal.Core.Models.CapabilityExplorerQuery
            {
                CapabilityId = capabilityId
            },
            cancellationToken);

        return overview is null
            ? Results.NotFound()
            : Results.Ok(overview);
    });

app.MapGet("/identities", (IdentityLoader loader) =>
{
    return Results.Ok(loader.GetIdentities());
});

app.MapGet(
    "/graph",
    async (
        GraphBuilder graphBuilder,
        CapabilityLoader capabilityLoader,
        IdentityLoader identityLoader,
        PolicyLoader policyLoader,
        IGovernanceGraph governanceGraph,
        CancellationToken cancellationToken) =>
    {
        var apiPolicies = policyLoader.GetPolicies();
        var graph = await graphBuilder.BuildAsync(
            capabilityLoader
                .GetCapabilities()
                .Select(CapabilityMapper.ToCore),
            identityLoader
                .GetIdentities()
                .Select(ToCoreIdentity),
            policyLoader
                .GetCorePolicies()
                .Where(policy => !string.Equals(
                    policy.Id,
                    "default-deny",
                    StringComparison.OrdinalIgnoreCase)),
            apiPolicies
                .Where(policy => !string.IsNullOrWhiteSpace(policy.Environment))
                .Select(policy => policy.Environment)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(environment => new Seneschal.Core.Models.Resource
                {
                    Type = "environment",
                    Id = environment,
                    Environment = environment
                }),
            governanceGraph,
            cancellationToken);

        return Results.Ok(graph);
    });

app.MapRazorPages();

app.Run();

static bool AcceptsHtml(HttpRequest request)
{
    return request.Headers.Accept.ToString().Contains(
        "text/html",
        StringComparison.OrdinalIgnoreCase);
}

static Seneschal.Core.Models.Identity ToCoreIdentity(
    IdentityDefinition identity)
{
    if (!Enum.TryParse<Seneschal.Core.Enums.IdentityType>(
            identity.Type,
            ignoreCase: true,
            out var identityType))
    {
        identityType = Seneschal.Core.Enums.IdentityType.Agent;
    }

    return new Seneschal.Core.Models.Identity
    {
        Id = identity.Name,
        Type = identityType,
        Owner = identity.Description,
        Environment = string.Empty
    };
}

public partial class Program;
