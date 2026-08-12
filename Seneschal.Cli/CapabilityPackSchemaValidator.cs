using System.Text.Json;
using Json.Schema;
using YamlDotNet.Serialization;

public static class CapabilityPackSchemaValidator
{
    private static readonly object SchemaLock = new();
    private static JsonSchema? _schema;
    public const string ContractVersion = "v1";
    public const int ContractRevision = 1;
    public const string SchemaFileName = "capability-pack.v1.schema.json";

    public static IReadOnlyList<PolicySchemaFinding> Validate(
        string yaml, string? schemaPath = null)
    {
        var yamlValue = new DeserializerBuilder()
            .WithAttemptingUnquotedStringTypeDeserialization()
            .Build().Deserialize<object?>(yaml);
        using var instance = JsonDocument.Parse(JsonSerializer.Serialize(yamlValue));
        var schema = GetSchema(schemaPath);
        var result = schema.Evaluate(instance.RootElement,
            new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (result.IsValid) return [];
        return result.Details?
            .Where(detail => !detail.IsValid && detail.Errors?.Count > 0)
            .SelectMany(detail => detail.Errors!.Values.Select(issue =>
                new PolicySchemaFinding(detail.InstanceLocation.ToString(), issue)))
            .Distinct().ToList()
            ?? [new PolicySchemaFinding("/", "Document does not conform to Capability Pack v1.")];
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
            Path.Combine(AppContext.BaseDirectory, "contracts", "capability-pack", SchemaFileName),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                "integrations", "contracts", "capability-pack", SchemaFileName))
        };
        return candidates.FirstOrDefault(File.Exists) ?? throw new FileNotFoundException(
            $"Capability Pack {ContractVersion} schema could not be loaded.");
    }
}
