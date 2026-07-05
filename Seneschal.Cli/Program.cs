using Seneschal.Core.Enums;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Seneschal.Core.Services;

if (args.Length < 4)
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  seneschal evaluate <identity> <capability> <environment>");
    return;
}

var command = args[0];

if (!command.Equals("evaluate", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine($"Unknown command: {command}");
    return;
}

var identityArg = args[1];
var capabilityArg = args[2];
var environmentArg = args[3];

var policies = new[]
{
    new Policy
    {
        Id = "prod-secret-read",
        Name = "Production Secret Access",
        Effect = DecisionType.RequireApproval,
        Reason = "Production secrets require approval.",

        Conditions = new Dictionary<string, string>
        {
            ["capability.id"] = "azure.keyvault.secret.read",
            ["resource.environment"] = "production"
        },

        Obligations =
        [
            "audit",
            "approval"
        ]
    }
};

var repository = new InMemoryPolicyRepository(policies);
var evaluator = new PolicyEvaluator();
var auditSink = new InMemoryAuditSink();

var engine = new DecisionEngine(
    repository,
    evaluator,
    auditSink);

var request = new DecisionRequest
{
    RequestId = Guid.NewGuid().ToString("N"),
    Timestamp = DateTimeOffset.UtcNow,

    Identity = new Identity
    {
        Id = identityArg,
        Type = IdentityType.Agent,
        Owner = "cli",
        Environment = environmentArg
    },

    Capability = new Capability
    {
        Id = capabilityArg,
        Provider = "azure",
        Category = "secret-management",
        Risk = RiskLevel.High,
        Description = "Read a secret value from Azure Key Vault."
    },

    Intent = new Intent
    {
        Action = "retrieve-secret",
        Reason = "CLI evaluation request."
    },

    Resource = new Resource
    {
        Type = "keyvault-secret",
        Id = "prod/payment-api/sql-password",
        Environment = environmentArg
    },

    Context = new Dictionary<string, string>
    {
        ["source"] = "cli",
        ["environment"] = environmentArg
    }
};

var result = await engine.EvaluateAsync(request);

Console.WriteLine();
Console.WriteLine("Seneschal Decision");
Console.WriteLine("------------------");
Console.WriteLine($"Identity:         {request.Identity.Id}");
Console.WriteLine($"Capability:       {request.Capability.Id}");
Console.WriteLine($"Environment:      {environmentArg}");
Console.WriteLine($"Decision:         {result.Decision}");
Console.WriteLine($"Mode:             {result.Mode}");
Console.WriteLine($"Policy Matched:   {string.Join(", ", result.MatchedPolicies)}");
Console.WriteLine($"Reason:           {result.Reason}");
Console.WriteLine($"Obligations:      {string.Join(", ", result.Obligations)}");
Console.WriteLine($"Audit Events:     {auditSink.Events.Count}");
Console.WriteLine($"Duration:         {result.LatencyMs} ms");