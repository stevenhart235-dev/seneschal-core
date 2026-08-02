using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Api.Services;

public sealed class TechnologyClassifier
{
    private static readonly IReadOnlyDictionary<string, TechnologyDefinition> Definitions =
        new Dictionary<string, TechnologyDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["azure"] = Define("azure", "Azure", "Azure capabilities and their runtime governance evidence."),
            ["github"] = Define("github", "GitHub", "GitHub delivery and automation activity under governance."),
            ["terraform"] = Define("terraform", "Terraform", "Terraform and OpenTofu infrastructure activity under governance."),
            ["kubernetes"] = Define("kubernetes", "Kubernetes", "Kubernetes workload and cluster operations under governance."),
            ["openai"] = Define("openai", "OpenAI", "OpenAI model and platform capabilities under governance."),
            ["aws"] = Define("aws", "AWS", "AWS platform capabilities and their runtime governance evidence."),
            ["postgresql"] = Define("postgresql", "PostgreSQL", "PostgreSQL data operations under governance."),
            ["slack"] = Define("slack", "Slack", "Slack collaboration and operational notification capabilities under governance."),
            ["m365"] = Define("m365", "Microsoft 365", "Microsoft 365 communication and document capabilities under governance."),
            ["custom"] = Define("custom", "Custom", "Customer-specific and internal platform capabilities under governance."),
            ["unclassified"] = Define("unclassified", "Unclassified", "Capabilities without explicit technology metadata.")
        };

    public TechnologyDefinition Classify(string capabilityId, Capability? capability = null)
    {
        var id = capabilityId.Trim();
        var provider = capability?.Provider ?? string.Empty;
        var tags = capability?.Tags ?? [];
        var documentation = capability?.DocumentationUrl ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(capability?.Technology))
        {
            var explicitKey = capability.Technology.Trim().ToLowerInvariant();
            return Definitions.TryGetValue(explicitKey, out var explicitDefinition)
                ? explicitDefinition
                : Definitions["unclassified"];
        }

        if (Matches(id, provider, tags, "azure")) return Definitions["azure"];
        if (Matches(id, provider, tags, "github") || documentation.Contains("/github-actions/", StringComparison.OrdinalIgnoreCase)) return Definitions["github"];
        if (Matches(id, provider, tags, "terraform") || tags.Contains("opentofu", StringComparer.OrdinalIgnoreCase) || documentation.Contains("/terraform/", StringComparison.OrdinalIgnoreCase)) return Definitions["terraform"];
        if (Matches(id, provider, tags, "kubernetes") || StartsWith(id, "k8s.") || StartsWith(id, "aks.")) return Definitions["kubernetes"];
        if (Matches(id, provider, tags, "openai")) return Definitions["openai"];
        if (Matches(id, provider, tags, "aws")) return Definitions["aws"];
        if (StartsWith(id, "postgres.") || StartsWith(id, "postgresql.") || tags.Contains("postgres", StringComparer.OrdinalIgnoreCase) || tags.Contains("postgresql", StringComparer.OrdinalIgnoreCase)) return Definitions["postgresql"];
        return Definitions["unclassified"];
    }

    public TechnologyDefinition? Find(string key) =>
        Definitions.TryGetValue(key, out var definition) ? definition : null;

    private static bool Matches(string id, string provider, IEnumerable<string> tags, string key) =>
        StartsWith(id, $"{key}.") || string.Equals(provider, key, StringComparison.OrdinalIgnoreCase) ||
        tags.Contains(key, StringComparer.OrdinalIgnoreCase);

    private static bool StartsWith(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    private static TechnologyDefinition Define(string key, string displayName, string description) =>
        new(key, displayName, TechnologyIconCatalog.PathFor(key), description);
}

public static class TechnologyIconCatalog
{
    private const string Root = "/technology-icons/";
    private static readonly IReadOnlyDictionary<string, string> Icons =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["azure"] = "azure.svg", ["github"] = "github.svg", ["terraform"] = "terraform.svg",
            ["kubernetes"] = "kubernetes.svg", ["openai"] = "openai.svg", ["aws"] = "aws.svg",
            ["postgresql"] = "postgresql.svg", ["slack"] = "slack.svg", ["m365"] = "microsoft365.svg",
            ["custom"] = "custom.svg", ["unclassified"] = "unclassified.svg"
        };

    public static string PathFor(string technologyKey) =>
        Root + (Icons.TryGetValue(technologyKey, out var icon) ? icon : Icons["unclassified"]);
}

public sealed class TechnologyActivityService
{
    private readonly ICapabilityCatalog _catalog;
    private readonly IInvestigationActivityReader _investigationActivity;
    private readonly IAuditEventStore _auditStore;
    private readonly IGovernanceWindowStore _windowStore;
    private readonly TechnologyClassifier _classifier;

    public TechnologyActivityService(ICapabilityCatalog catalog, IInvestigationActivityReader investigationActivity,
        IAuditEventStore auditStore, IGovernanceWindowStore windowStore, TechnologyClassifier classifier)
    {
        _catalog = catalog;
        _investigationActivity = investigationActivity;
        _auditStore = auditStore;
        _windowStore = windowStore;
        _classifier = classifier;
    }

    public async Task<IReadOnlyList<TechnologyActivity>> GetTechnologiesAsync(CancellationToken cancellationToken = default)
    {
        var catalog = await _catalog.SearchAsync(new CapabilityCatalogQuery(), cancellationToken);
        var activity = await _investigationActivity.GetSnapshotAsync(cancellationToken);
        var audit = await _auditStore.GetRecentAsync(100, cancellationToken);
        var window = _windowStore.GetWindow();
        return Build(catalog, activity, audit, window);
    }

    public async Task<TechnologyActivity?> GetTechnologyAsync(string key, CancellationToken cancellationToken = default) =>
        (await GetTechnologiesAsync(cancellationToken)).FirstOrDefault(item =>
            string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<TechnologyActivity> Build(IReadOnlyCollection<CapabilityCatalogEntry> catalog,
        ActivitySnapshot activity, IReadOnlyCollection<AuditEvent> audit, GovernanceWindow window)
    {
        var catalogById = catalog.ToDictionary(item => item.Capability.Id, item => item.Capability, StringComparer.OrdinalIgnoreCase);
        var activityById = activity.Capabilities.ToDictionary(item => item.CapabilityId, StringComparer.OrdinalIgnoreCase);
        var capabilityIds = catalogById.Keys.Concat(activityById.Keys).Concat(audit.Select(item => item.CapabilityId))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        return capabilityIds
            .GroupBy(id => _classifier.Classify(id, catalogById.GetValueOrDefault(id)))
            .Select(group => Project(group.Key, group, catalogById, activityById, audit, window))
            .OrderByDescending(item => item.DenyCount + item.PendingApprovalCount)
            .ThenByDescending(item => item.EvaluationCount)
            .ThenBy(item => item.DisplayName)
            .ToList();
    }

    private static TechnologyActivity Project(TechnologyDefinition definition, IEnumerable<string> groupedIds,
        IReadOnlyDictionary<string, Capability> catalog, IReadOnlyDictionary<string, CapabilityActivity> activity,
        IReadOnlyCollection<AuditEvent> allAudit, GovernanceWindow window)
    {
        var ids = groupedIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var events = allAudit.Where(item => ids.Contains(item.CapabilityId)).OrderByDescending(item => item.TimestampUtc).ToList();
        var capabilities = ids.Select(id =>
        {
            catalog.TryGetValue(id, out var metadata);
            activity.TryGetValue(id, out var runtime);
            var capabilityEvents = events.Where(item => string.Equals(item.CapabilityId, id, StringComparison.OrdinalIgnoreCase)).ToList();
            return new TechnologyCapability(id, metadata?.DisplayName ?? id, metadata?.Owner,
                metadata?.RiskLevel.ToString(), runtime?.TotalRequests ?? 0, runtime?.AllowedCount ?? 0,
                runtime?.DeniedCount ?? 0, runtime?.PendingApprovalCount ?? 0,
                runtime?.LastUsedUtc ?? capabilityEvents.FirstOrDefault()?.TimestampUtc);
        }).OrderByDescending(item => item.DenyCount + item.PendingApprovalCount)
          .ThenByDescending(item => item.EvaluationCount).ThenBy(item => item.Id).ToList();
        var applications = events.GroupBy(item => item.IdentityId, StringComparer.OrdinalIgnoreCase)
            .Select(group => new TechnologyApplication(group.Key, group.Count(),
                group.Count(item => item.Decision == DecisionType.Deny),
                group.Count(item => item.Decision == DecisionType.RequireApproval),
                group.Select(item => item.CapabilityId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                group.Max(item => item.TimestampUtc)))
            .OrderByDescending(item => item.DenyCount + item.PendingApprovalCount)
            .ThenByDescending(item => item.EvaluationCount).ThenBy(item => item.Name).ToList();

        return new TechnologyActivity(definition.Key, definition.DisplayName, definition.IconPath, definition.Description,
            applications.Count, capabilities.Count, capabilities.Sum(item => item.EvaluationCount),
            capabilities.Sum(item => item.AllowCount), capabilities.Sum(item => item.DenyCount),
            capabilities.Sum(item => item.PendingApprovalCount),
            capabilities.Where(item => item.LastObservedAt.HasValue).Select(item => item.LastObservedAt).Max(),
            events.Select(item => item.EnforcementMode.ToString()).Distinct().Order().ToList(), applications,
            capabilities, events.Take(20).ToList(),
            events.SelectMany(item => item.MatchedPolicies).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList(),
            window.Enabled && ids.Overlaps(window.AffectedCapabilities), window);
    }
}

public sealed record TechnologyDefinition(string Key, string DisplayName, string IconPath, string Description);
public sealed record TechnologyApplication(string Name, long EvaluationCount, long DenyCount,
    long PendingApprovalCount, int CapabilityCount, DateTimeOffset LastObservedAt);
public sealed record TechnologyCapability(string Id, string DisplayName, string? Owner, string? Risk,
    long EvaluationCount, long AllowCount, long DenyCount, long PendingApprovalCount, DateTimeOffset? LastObservedAt);
public sealed record TechnologyActivity(string Key, string DisplayName, string IconPath, string Description,
    int ApplicationCount, int CapabilityCount, long EvaluationCount, long AllowCount, long DenyCount,
    long PendingApprovalCount, DateTimeOffset? LastObservedAt, IReadOnlyList<string> RuntimeModesObserved,
    IReadOnlyList<TechnologyApplication> Applications, IReadOnlyList<TechnologyCapability> Capabilities,
    IReadOnlyList<AuditEvent> RecentDecisions, IReadOnlyList<string> MatchedPolicies,
    bool GovernanceWindowApplies, GovernanceWindow GovernanceWindow);
