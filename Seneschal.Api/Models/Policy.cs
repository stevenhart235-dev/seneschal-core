using System.Text.Json.Serialization;

namespace Seneschal.Api.Models;

public class Policy
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Rationale { get; set; } = "";
    public string Identity { get; set; } = "";
    public List<string> Identities { get; set; } = new();
    public string Capability { get; set; } = "";
    public List<string> Capabilities { get; set; } = new();
    public string Environment { get; set; } = "";
    public List<string> Environments { get; set; } = new();
    public string Decision { get; set; } = "";
    public string Reason { get; set; } = "";

    [JsonIgnore]
    public IReadOnlyList<string> EffectiveIdentities =>
        MergeTargets(Identity, Identities);

    [JsonIgnore]
    public IReadOnlyList<string> EffectiveCapabilities =>
        MergeTargets(Capability, Capabilities);

    [JsonIgnore]
    public IReadOnlyList<string> EffectiveEnvironments =>
        MergeTargets(Environment, Environments);

    private static IReadOnlyList<string> MergeTargets(
        string legacyTarget,
        IEnumerable<string>? targets)
    {
        return new[] { legacyTarget }
            .Concat(targets ?? [])
            .Where(target => !string.IsNullOrWhiteSpace(target))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
