using System.Text.Json;
using Json.Schema;
using YamlDotNet.Serialization;

public sealed record PolicySchemaFinding(string Path, string Issue);

public static class PolicySchemaValidator
{
    private static readonly object SchemaLock = new();
    private static JsonSchema? _schema;
    public const string ContractVersion = "v1";
    public const int ContractRevision = 1;
    public const string SchemaFileName = "policy-schema.v1.json";

    public static IReadOnlyList<PolicySchemaFinding> Validate(
        string yaml,
        string? schemaPath = null)
    {
        var deserializer = new DeserializerBuilder()
            .WithAttemptingUnquotedStringTypeDeserialization()
            .Build();
        var yamlValue = deserializer.Deserialize<object?>(yaml);
        using var instanceDocument = JsonDocument.Parse(
            JsonSerializer.Serialize(yamlValue));
        var schema = GetSchema(schemaPath);
        var result = schema.Evaluate(instanceDocument.RootElement, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        });

        if (result.IsValid) return [];

        return result.Details?
            .Where(detail => !detail.IsValid && detail.Errors?.Count > 0)
            .SelectMany(detail => detail.Errors!.Values.Select(issue =>
                new PolicySchemaFinding(
                    detail.InstanceLocation.ToString(),
                    issue)))
            .Distinct()
            .ToList()
            ?? [new PolicySchemaFinding("/", "Document does not conform to Policy Schema v1.")];
    }

    private static JsonSchema GetSchema(string? schemaPath)
    {
        if (_schema is not null) return _schema;

        lock (SchemaLock)
        {
            return _schema ??= JsonSchema.FromText(File.ReadAllText(
                Path.GetFullPath(schemaPath ?? ResolveSchemaPath())));
        }
    }

    public static string ResolveSchemaPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "contracts", "policy", SchemaFileName),
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..",
                "integrations", "contracts", "policy", SchemaFileName))
        };

        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException(
                $"Policy Schema {ContractVersion} could not be loaded.");
    }
}
