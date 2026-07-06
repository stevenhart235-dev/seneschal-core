using System.Text.Json;
using Seneschal.Api.Mappers;
using Seneschal.Api.Models;
using Seneschal.Api.Services;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Repositories;
using Seneschal.Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<PolicyLoader>();
builder.Services.AddSingleton<
    Seneschal.Core.Interfaces.IPolicyEvaluator,
    Seneschal.Core.Services.PolicyEvaluator>();
builder.Services.AddSingleton<CoreDecisionService>();
builder.Services.AddSingleton<PolicyValidator>();
builder.Services.AddSingleton<AuditLogger>();
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

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.Services.GetRequiredService<PolicyValidator>();

app.MapPost("/evaluate", (DecisionRequest request, CoreDecisionService decisionService, AuditLogger auditLogger) =>
{
    var result = decisionService.Evaluate(request);
    auditLogger.Log(request, result);

    return Results.Ok(result);
});

app.MapGet("/audit", () =>
{
    var auditFile = Path.Combine(AppContext.BaseDirectory, "Audit", "audit.jsonl");

    if (!File.Exists(auditFile))
        return Results.Ok(new List<AuditEvent>());

    var events = File.ReadLines(auditFile)
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .Select(line => JsonSerializer.Deserialize<AuditEvent>(line)!)
        .ToList();

    return Results.Ok(events);
});

app.MapGet("/policies", (PolicyLoader policyLoader) =>
{
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

app.Run();

public partial class Program;
