using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Repositories;

public sealed class InMemoryCapabilityCatalog : ICapabilityCatalog
{
    private IReadOnlyCollection<CapabilityCatalogEntry> _entries = [];
    private IReadOnlyDictionary<string, CapabilityCatalogEntry> _entriesById =
        new Dictionary<string, CapabilityCatalogEntry>();

    public InMemoryCapabilityCatalog(IEnumerable<Capability> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        var entries = capabilities
            .Select(capability => new CapabilityCatalogEntry
            {
                Capability = capability,
                Provenance =
                [
                    new CapabilityProvenance
                    {
                        Kind = "LocalCatalog"
                    }
                ]
            });
        Initialize(entries);
    }

    private InMemoryCapabilityCatalog(
        IEnumerable<CapabilityCatalogEntry> entries,
        bool preserveEntries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Initialize(entries);
    }

    public static InMemoryCapabilityCatalog FromEntries(
        IEnumerable<CapabilityCatalogEntry> entries) =>
        new(entries, preserveEntries: true);

    private void Initialize(IEnumerable<CapabilityCatalogEntry> entries)
    {
        var materialized = entries.ToList();

        _entriesById = materialized.ToDictionary(
            entry => entry.Capability.Id,
            StringComparer.OrdinalIgnoreCase);
        _entries = materialized;
    }

    public Task<CapabilityCatalogEntry?> GetByIdAsync(
        string capabilityId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityId);
        cancellationToken.ThrowIfCancellationRequested();

        _entriesById.TryGetValue(capabilityId, out var entry);
        return Task.FromResult(entry);
    }

    public Task<IReadOnlyCollection<CapabilityCatalogEntry>> SearchAsync(
        CapabilityCatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<CapabilityCatalogEntry> matches = _entries;

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            matches = matches.Where(entry =>
                MatchesSearchText(
                    entry.Capability,
                    query.SearchText));
        }

        if (!string.IsNullOrWhiteSpace(query.Owner))
        {
            matches = matches.Where(entry =>
                string.Equals(
                    entry.Capability.Owner,
                    query.Owner,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (query.RiskLevels.Count > 0)
        {
            matches = matches.Where(entry =>
                query.RiskLevels.Contains(entry.Capability.RiskLevel));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            matches = matches.Where(entry => string.Equals(
                entry.Capability.Category,
                query.Category,
                StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Lifecycle))
        {
            matches = matches.Where(entry => string.Equals(
                entry.Capability.Lifecycle,
                query.Lifecycle,
                StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IReadOnlyCollection<CapabilityCatalogEntry>>(
            matches.ToList());
    }

    private static bool MatchesSearchText(
        Capability capability,
        string searchText)
    {
        return Contains(capability.Id, searchText) ||
            Contains(capability.Name, searchText) ||
            Contains(capability.DisplayName, searchText) ||
            Contains(capability.Description, searchText) ||
            Contains(capability.Owner, searchText) ||
            Contains(capability.Category, searchText) ||
            Contains(capability.Lifecycle, searchText) ||
            capability.Tags.Any(tag => Contains(tag, searchText));
    }

    private static bool Contains(
        string value,
        string searchText)
    {
        return value.Contains(
            searchText,
            StringComparison.OrdinalIgnoreCase);
    }
}
