using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Seneschal.Core.Services;

if (args.Length == 0 || IsHelp(args[0]))
{
    WriteUsage();
    return;
}

var command = args[0];

if (command.Equals("preflight", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length == 2 && IsHelp(args[1]))
    {
        WritePreflightUsage();
        return;
    }

    Environment.ExitCode = await PreflightCommand.RunAsync(args[1..]);
    return;
}

if (command.Equals("policy", StringComparison.OrdinalIgnoreCase) &&
    args.Length > 1 &&
    args[1].Equals("init", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length == 3 && IsHelp(args[2]))
    {
        WritePolicyInitUsage();
        return;
    }

    Environment.ExitCode = await PolicyInitCommand.RunAsync(args[2..]);
    return;
}

if (command.Equals("policy", StringComparison.OrdinalIgnoreCase) &&
    args.Length > 1 &&
    args[1].Equals("validate", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length == 3 && IsHelp(args[2]))
    {
        WritePolicyValidationUsage();
        return;
    }

    Environment.ExitCode = await PolicyValidationCommand.RunAsync(args[2..]);
    return;
}

if (command.Equals("policy", StringComparison.OrdinalIgnoreCase) &&
    args.Length > 1 &&
    args[1].Equals("simulate", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length == 3 && IsHelp(args[2]))
    {
        WritePolicySimulationUsage();
        return;
    }

    Environment.ExitCode = await PolicySimulationCommand.RunAsync(args[2..]);
    return;
}

if (command.Equals("evaluate", StringComparison.OrdinalIgnoreCase))
{
    await EvaluateAsync(args);
    return;
}

if (command.Equals("capability", StringComparison.OrdinalIgnoreCase))
{
    await HandleCapabilityAsync(args);
    return;
}

Console.WriteLine($"Unknown command: {command}");

static async Task EvaluateAsync(string[] args)
{
    if (args.Length < 4)
    {
        WriteUsage();
        return;
    }

    var identityArg = args[1];
    var capabilityArg = args[2];
    var environmentArg = args[3];

    var repository = new InMemoryPolicyRepository(CreatePolicies());
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
            Owner = "platform",
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
    Console.WriteLine();
    Console.WriteLine("Policy Evaluation");
    Console.WriteLine("-----------------");

    if (result.Evaluation.Count == 0)
    {
        Console.WriteLine("No policy conditions evaluated.");
    }
    else
    {
        foreach (var step in result.Evaluation)
        {
            var symbol = step.Matched ? "✓" : "✗";

            Console.WriteLine($"{symbol} {step.Property}");
            Console.WriteLine($"  Expected: {step.Expected}");
            Console.WriteLine($"  Actual:   {step.Actual}");
        }
    }

    Console.WriteLine($"Audit Events:     {auditSink.Events.Count}");
    Console.WriteLine($"Duration:         {result.LatencyMs} ms");
}

static async Task HandleCapabilityAsync(string[] args)
{
    if (args.Length < 3 ||
        !args[1].Equals("show", StringComparison.OrdinalIgnoreCase))
    {
        WriteUsage();
        return;
    }

    var explorer = CreateCapabilityExplorer();
    var overview = await explorer.GetOverviewAsync(
        new CapabilityExplorerQuery
        {
            CapabilityId = args[2]
        });

    if (overview is null)
    {
        Console.WriteLine($"Capability not found: {args[2]}");
        return;
    }

    WriteCapabilityOverview(overview);
}

static ICapabilityExplorer CreateCapabilityExplorer()
{
    var catalog = new InMemoryCapabilityCatalog(CreateCapabilities());
    var graph = new InMemoryGovernanceGraph(CreateGovernanceRelationships());

    return new CapabilityExplorer(catalog, graph);
}

static IReadOnlyCollection<Capability> CreateCapabilities()
{
    return
    [
        new Capability
        {
            Id = "azure.keyvault.secret.read",
            Name = "Read Azure Key Vault Secret",
            Provider = "azure",
            Category = "secret-management",
            RiskLevel = RiskLevel.High,
            Owner = "platform",
            Version = "1.0",
            Description = "Read a secret value from Azure Key Vault.",
            Tags =
            [
                "azure",
                "keyvault",
                "secret"
            ]
        }
    ];
}

static IReadOnlyCollection<GovernanceRelationship> CreateGovernanceRelationships()
{
    return
    [
        new GovernanceRelationship
        {
            Id = "cli-policy-0-policy-capability",
            From = Entity(GovernanceEntityType.Policy, "platform-secret-access"),
            To = Entity(GovernanceEntityType.Capability, "azure.keyvault.secret.read"),
            Type = GovernanceRelationshipType.PolicyAppliesToCapability,
            Origin = GovernanceRelationshipOrigin.Declared,
            SourceSystem = "CliSeed"
        },
        new GovernanceRelationship
        {
            Id = "cli-policy-0-policy-identity",
            From = Entity(GovernanceEntityType.Policy, "platform-secret-access"),
            To = Entity(GovernanceEntityType.Identity, "platform"),
            Type = GovernanceRelationshipType.PolicyAppliesToIdentity,
            Origin = GovernanceRelationshipOrigin.Declared,
            SourceSystem = "CliSeed"
        },
        new GovernanceRelationship
        {
            Id = "cli-policy-0-identity-capability",
            From = Entity(GovernanceEntityType.Identity, "platform"),
            To = Entity(GovernanceEntityType.Capability, "azure.keyvault.secret.read"),
            Type = GovernanceRelationshipType.IdentityAssignedCapability,
            Origin = GovernanceRelationshipOrigin.Declared,
            SourceSystem = "CliSeed"
        },
        new GovernanceRelationship
        {
            Id = "cli-policy-0-policy-resource",
            From = Entity(GovernanceEntityType.Policy, "platform-secret-access"),
            To = Entity(GovernanceEntityType.Resource, "production", "environment"),
            Type = GovernanceRelationshipType.PolicyAppliesToResource,
            Origin = GovernanceRelationshipOrigin.Declared,
            SourceSystem = "CliSeed"
        },
        new GovernanceRelationship
        {
            Id = "cli-policy-1-policy-capability",
            From = Entity(GovernanceEntityType.Policy, "prod-secret-read"),
            To = Entity(GovernanceEntityType.Capability, "azure.keyvault.secret.read"),
            Type = GovernanceRelationshipType.PolicyAppliesToCapability,
            Origin = GovernanceRelationshipOrigin.Declared,
            SourceSystem = "CliSeed"
        },
        new GovernanceRelationship
        {
            Id = "cli-policy-1-policy-resource",
            From = Entity(GovernanceEntityType.Policy, "prod-secret-read"),
            To = Entity(GovernanceEntityType.Resource, "production", "environment"),
            Type = GovernanceRelationshipType.PolicyAppliesToResource,
            Origin = GovernanceRelationshipOrigin.Declared,
            SourceSystem = "CliSeed"
        }
    ];
}

static IReadOnlyCollection<Policy> CreatePolicies()
{
    return
    [
        new Policy
        {
            Id = "platform-secret-access",
            Name = "Platform Team Secret Access",
            Effect = DecisionType.Allow,
            Priority = 100,
            Reason = "Platform-owned identities are allowed to access production secrets.",

            Conditions = new Dictionary<string, string>
            {
                ["identity.owner"] = "platform",
                ["capability.id"] = "azure.keyvault.secret.read",
                ["resource.environment"] = "production"
            },

            Obligations =
            [
                "audit"
            ]
        },

        new Policy
        {
            Id = "prod-secret-read",
            Name = "Production Secret Access",
            Effect = DecisionType.RequireApproval,
            Priority = 50,
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
    ];
}

static GovernanceEntityReference Entity(
    GovernanceEntityType type,
    string id,
    string? scope = null)
{
    return new GovernanceEntityReference
    {
        Type = type,
        Id = id,
        Scope = scope
    };
}

static void WriteCapabilityOverview(CapabilityOverview overview)
{
    var capability = overview.CatalogEntry.Capability;

    Console.WriteLine();
    Console.WriteLine("Seneschal Capability");
    Console.WriteLine("--------------------");
    Console.WriteLine($"Id:                 {capability.Id}");
    Console.WriteLine($"Name:               {capability.Name}");
    Console.WriteLine($"Provider:           {capability.Provider}");
    Console.WriteLine($"Category:           {capability.Category}");
    Console.WriteLine($"Risk:               {capability.RiskLevel}");
    Console.WriteLine($"Owner:              {capability.Owner}");
    Console.WriteLine($"Version:            {capability.Version}");
    Console.WriteLine($"Description:        {capability.Description}");
    Console.WriteLine($"Tags:               {string.Join(", ", capability.Tags)}");
    Console.WriteLine();

    Console.WriteLine("Governance Summary");
    Console.WriteLine("------------------");
    Console.WriteLine($"Assigned Identities: {overview.Summary.AssignedIdentityCount}");
    Console.WriteLine($"Observed Identities: {overview.Summary.ObservedIdentityCount}");
    Console.WriteLine($"Resources:           {overview.Summary.ResourceCount}");
    Console.WriteLine($"Governing Policies:  {overview.Summary.GoverningPolicyCount}");
    Console.WriteLine($"Origins:             {string.Join(", ", overview.Summary.Origins)}");
    Console.WriteLine();

    Console.WriteLine("Relationships");
    Console.WriteLine("-------------");

    if (overview.Relationships.Count == 0)
    {
        Console.WriteLine("No relationships found.");
        return;
    }

    foreach (var group in overview.Relationships
        .GroupBy(GetRelationshipGroupLabel)
        .OrderBy(group => GetRelationshipGroupOrder(group.Key))
        .ThenBy(group => group.Key))
    {
        Console.WriteLine(group.Key);

        foreach (var relationship in group.OrderBy(relationship => relationship.Id))
        {
            Console.WriteLine($"  {FormatRelationshipItem(relationship)}");
            Console.WriteLine(
                $"    Origin: {relationship.Origin}; Source: {relationship.SourceSystem ?? "unknown"}");
        }
    }
}

static string GetRelationshipGroupLabel(GovernanceRelationship relationship)
{
    return relationship.Type switch
    {
        GovernanceRelationshipType.IdentityAssignedCapability =>
            "Assigned Identities",
        GovernanceRelationshipType.IdentityInvokedCapability =>
            "Observed Identities",
        GovernanceRelationshipType.PolicyAppliesToCapability =>
            "Governing Policies",
        GovernanceRelationshipType.CapabilityTargetsResource or
            GovernanceRelationshipType.PolicyAppliesToResource =>
            "Resources",
        _ => relationship.Type.ToString()
    };
}

static int GetRelationshipGroupOrder(string groupLabel)
{
    return groupLabel switch
    {
        "Assigned Identities" => 0,
        "Observed Identities" => 1,
        "Governing Policies" => 2,
        "Resources" => 3,
        _ => 4
    };
}

static string FormatRelationshipItem(GovernanceRelationship relationship)
{
    return relationship.Type switch
    {
        GovernanceRelationshipType.IdentityAssignedCapability or
            GovernanceRelationshipType.IdentityInvokedCapability =>
            FormatRelatedEntity(relationship, GovernanceEntityType.Identity),
        GovernanceRelationshipType.PolicyAppliesToCapability =>
            FormatRelatedEntity(relationship, GovernanceEntityType.Policy),
        GovernanceRelationshipType.CapabilityTargetsResource or
            GovernanceRelationshipType.PolicyAppliesToResource =>
            FormatRelatedEntity(relationship, GovernanceEntityType.Resource),
        _ => $"{FormatEntity(relationship.From)} -> {FormatEntity(relationship.To)}"
    };
}

static string FormatRelatedEntity(
    GovernanceRelationship relationship,
    GovernanceEntityType entityType)
{
    var entity = relationship.From.Type == entityType
        ? relationship.From
        : relationship.To;

    return FormatEntity(entity);
}

static string FormatEntity(GovernanceEntityReference entity)
{
    var scope = string.IsNullOrWhiteSpace(entity.Scope)
        ? string.Empty
        : $"[{entity.Scope}]";

    return $"{entity.Type}{scope}:{entity.Id}";
}

static void WriteUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  seneschal preflight --url <url> --api-key <key> --identity <id> --capability <id> [--environment <name>]");
    Console.WriteLine("  seneschal policy init <path> [--force]");
    Console.WriteLine("  seneschal policy validate <path>");
    Console.WriteLine("  seneschal policy simulate --url <url> --api-key <key> --identity <id> --capability <id> [--environment <name>] [--resource <id>]");
    Console.WriteLine("  seneschal evaluate <identity> <capability> <environment>");
    Console.WriteLine("  seneschal capability show <capabilityId>");
}

static void WritePolicyInitUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  seneschal policy init <path> [--force]");
    Console.WriteLine();
    Console.WriteLine("Creates a minimal Policy Schema v1 document. Existing files require explicit --force.");
}

static void WritePolicyValidationUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  seneschal policy validate <path>");
    Console.WriteLine();
    Console.WriteLine("Validates policy YAML against sibling identities.yaml and capabilities.yaml files without changing runtime state.");
}

static void WritePolicySimulationUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  seneschal policy simulate --url <url> --api-key <key> --identity <id> --capability <id> [--environment <name>] [--resource <id>]");
    Console.WriteLine();
    Console.WriteLine("Simulates policy through the non-mutating /preflight endpoint and presents the complete explanation.");
}

static void WritePreflightUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  seneschal preflight --url <url> --api-key <key> --identity <id> --capability <id> [--environment <name>] [--resource <id>]");
    Console.WriteLine();
    Console.WriteLine("Validates connectivity, credentials, scope, and Execution Guidance without executing the governed action.");
}

static bool IsHelp(string value) =>
    value.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
    value.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
    value.Equals("help", StringComparison.OrdinalIgnoreCase);
