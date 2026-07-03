using System.Text.Json;
using Seneschal.Api.Models;
using Seneschal.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<PolicyLoader>();
builder.Services.AddSingleton<PolicyEvaluator>();
builder.Services.AddSingleton<PolicyValidator>();
builder.Services.AddSingleton<AuditLogger>();
builder.Services.AddSingleton(new RuntimeSettings
{
    Mode = EnforcementMode.LogOnly
});
builder.Services.AddSingleton<CapabilityLoader>();
builder.Services.AddSingleton<IdentityLoader>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.Services.GetRequiredService<PolicyValidator>();

app.MapPost("/evaluate", (DecisionRequest request, PolicyEvaluator evaluator, AuditLogger auditLogger) =>
{
    var result = evaluator.Evaluate(request);
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

app.MapGet("/identities", (IdentityLoader loader) =>
{
    return Results.Ok(loader.GetIdentities());
});

app.Run();