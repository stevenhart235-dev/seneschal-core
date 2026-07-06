namespace Seneschal.Core.Models;

public sealed record CapabilityOverview
{
    public required CapabilityCatalogEntry CatalogEntry { get; init; }
    public IReadOnlyCollection<GovernanceRelationship> Relationships
        { get; init; } = [];
    public required CapabilityOverviewSummary Summary { get; init; }
}
