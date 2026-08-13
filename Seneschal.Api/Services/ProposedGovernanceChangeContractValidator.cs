using System.Text.Json;
using Json.Schema;
using Seneschal.Api.Models;

namespace Seneschal.Api.Services;

public sealed class ProposedGovernanceChangeContractValidator
{
    public const string ContractVersion = "v1";
    public const int ContractRevision = 1;
    public const string SchemaFileName = "proposed-governance-change.v1.schema.json";
    private static readonly object SchemaLock = new();
    private static JsonSchema? _sharedSchema;
    private readonly JsonSchema _schema;

    public ProposedGovernanceChangeContractValidator(IHostEnvironment environment)
    {
        var path = Path.Combine(environment.ContentRootPath, "..", "integrations",
            "contracts", "proposed-governance-change", SchemaFileName);
        if (!File.Exists(path))
            path = Path.Combine(AppContext.BaseDirectory, "contracts",
                "proposed-governance-change", SchemaFileName);
        lock (SchemaLock)
            _schema = _sharedSchema ??= JsonSchema.FromText(File.ReadAllText(Path.GetFullPath(path)),
                new BuildOptions { SchemaRegistry = new SchemaRegistry() });
    }

    public IReadOnlyCollection<string> Validate(ProposedGovernanceChange proposal)
    {
        var json = JsonSerializer.SerializeToElement(proposal,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Validate(json);
    }

    public IReadOnlyCollection<string> Validate(JsonElement json)
    {
        var result = _schema.Evaluate(json, new EvaluationOptions
        { OutputFormat = OutputFormat.List });
        if (result.IsValid) return [];
        return result.Details?.Where(detail => !detail.IsValid && detail.Errors?.Count > 0)
            .SelectMany(detail => detail.Errors!.Values.Select(issue =>
                $"{detail.InstanceLocation}: {issue}"))
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)
            .ToList() ?? ["Proposal does not conform to Proposed Governance Change v1."];
    }
}
