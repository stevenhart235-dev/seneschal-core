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
builder.Services.AddSingleton<IActivityStore, InMemoryActivityStore>();
builder.Services.AddSingleton<IDecisionExporter, NullDecisionExporter>();
builder.Services.AddSingleton<IDecisionMetrics, InMemoryDecisionMetrics>();
builder.Services.AddSingleton(new RuntimeSettings
{
    Mode = Seneschal.Core.Enums.EnforcementMode.LogOnly
});
builder.Services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();
builder.Services.AddSingleton<IntegrationApiKeyLoader>();
builder.Services.AddSingleton<IntegrationApiKeyAuthorizer>();
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

app.MapPost("/evaluate", (
    DecisionRequest request,
    HttpRequest httpRequest,
    IntegrationApiKeyAuthorizer apiKeyAuthorizer,
    CoreDecisionService decisionService) =>
{
    var authorization = apiKeyAuthorizer.Authorize(
        httpRequest,
        request);

    if (!authorization.IsAllowed)
    {
        return Results.Json(
            new
            {
                reason = authorization.Reason
            },
            statusCode: authorization.StatusCode);
    }

    var result = decisionService.Evaluate(request);

    return Results.Ok(result);
});

app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "healthy",
        timestampUtc = DateTimeOffset.UtcNow
    });
});

app.MapGet("/live", () =>
{
    return Results.Ok(new
    {
        status = "live",
        timestampUtc = DateTimeOffset.UtcNow
    });
});

app.MapGet("/ready", (
    CapabilityLoader capabilityLoader,
    IdentityLoader identityLoader,
    PolicyLoader policyLoader,
    RuntimeSettings runtimeSettings,
    IConfigurationValidator configurationValidator) =>
{
    var capabilityCount = capabilityLoader.GetCapabilities().Count;
    var identityCount = identityLoader.GetIdentities().Count;
    var policyCount = policyLoader.GetPolicies().Count;
    var runtimeSettingsLoaded = runtimeSettings is not null;
    var validationResult = configurationValidator.Validate();
    var ready = capabilityCount > 0
        && identityCount > 0
        && policyCount > 0
        && runtimeSettingsLoaded;

    return Results.Ok(new
    {
        status = ready ? "ready" : "not_ready",
        timestampUtc = DateTimeOffset.UtcNow,
        capabilitiesLoaded = capabilityCount > 0,
        identitiesLoaded = identityCount > 0,
        policiesLoaded = policyCount > 0,
        runtimeSettingsLoaded,
        configValid = validationResult.IsValid,
        validationErrors = validationResult.ErrorCount,
        validationWarnings = validationResult.WarningCount
    });
});

app.MapGet("/config/validate", (
    IConfigurationValidator configurationValidator) =>
{
    return Results.Ok(configurationValidator.Validate());
});

app.MapGet("/diagnostics", async (
    RuntimeSettings runtimeSettings,
    CapabilityLoader capabilityLoader,
    IdentityLoader identityLoader,
    PolicyLoader policyLoader,
    IAuditEventStore auditEventStore,
    IActivityStore activityStore,
    IDecisionExporter decisionExporter,
    IDecisionMetrics decisionMetrics,
    CancellationToken cancellationToken) =>
{
    var auditEvents = await auditEventStore.GetRecentAsync(
        count: int.MaxValue,
        cancellationToken: cancellationToken);
    var activitySnapshot = await activityStore.GetSnapshotAsync(
        cancellationToken);

    return Results.Ok(new
    {
        currentRuntimeMode = runtimeSettings.Mode.ToString(),
        capabilityCount = capabilityLoader.GetCapabilities().Count,
        identityCount = identityLoader.GetIdentities().Count,
        policyCount = policyLoader.GetPolicies().Count,
        auditEventCount = auditEvents.Count,
        activityCapabilityCount = activitySnapshot.Capabilities.Count,
        activityIdentityCount = activitySnapshot.Identities.Count,
        activityPolicyCount = activitySnapshot.Policies.Count,
        exporterType = decisionExporter.GetType().Name,
        metricsType = decisionMetrics.GetType().Name,
        timestampUtc = DateTimeOffset.UtcNow
    });
});

app.MapGet("/activity", async (
    IActivityStore activityStore,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await activityStore.GetSnapshotAsync(cancellationToken));
});

app.MapGet("/exports", async (
    IDecisionExporter exporter,
    CancellationToken cancellationToken) =>
{
    if (exporter is InMemoryDecisionExporter inMemoryExporter)
    {
        return Results.Ok(await inMemoryExporter.GetExportsAsync(cancellationToken));
    }

    return Results.Ok(Array.Empty<Seneschal.Core.Models.DecisionExportRecord>());
});

app.MapGet("/metrics", (IDecisionMetrics metrics) =>
{
    if (metrics is InMemoryDecisionMetrics inMemoryMetrics)
    {
        return Results.Text(
            inMemoryMetrics.RenderPrometheus(),
            "text/plain; version=0.0.4; charset=utf-8");
    }

    return Results.Text(
        string.Empty,
        "text/plain; version=0.0.4; charset=utf-8");
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
    IActivityStore activityStore,
    IAuditEventStore auditEventStore,
    CancellationToken cancellationToken) =>
{
    if (AcceptsHtml(request))
    {
        return Results.Content(
            await PolicyExplorerPageRenderer.RenderAsync(
                policyLoader.GetPolicies(),
                governanceGraph,
                activityStore,
                auditEventStore,
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
