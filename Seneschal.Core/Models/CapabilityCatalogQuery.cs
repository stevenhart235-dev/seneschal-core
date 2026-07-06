using Seneschal.Core.Enums;

namespace Seneschal.Core.Models;

public sealed record CapabilityCatalogQuery
{
    public string? Owner { get; init; }
    public IReadOnlyCollection<RiskLevel> RiskLevels { get; init; } = [];
}
