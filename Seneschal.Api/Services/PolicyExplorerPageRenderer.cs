using System.Net;
using System.Text;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using ApiPolicy = Seneschal.Api.Models.Policy;

namespace Seneschal.Api.Services;

public static class PolicyExplorerPageRenderer
{
    public static async Task<string> RenderAsync(
        IReadOnlyList<ApiPolicy> policies,
        IGovernanceGraph governanceGraph,
        IActivityStore activityStore,
        IAuditEventStore auditEventStore,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(governanceGraph);
        ArgumentNullException.ThrowIfNull(activityStore);
        ArgumentNullException.ThrowIfNull(auditEventStore);

        var relationships = await governanceGraph.QueryAsync(
            new GovernanceRelationshipQuery(),
            cancellationToken);
        var activity = await activityStore.GetSnapshotAsync(cancellationToken);
        var auditEvents = await auditEventStore.GetRecentAsync(
            cancellationToken: cancellationToken);

        var cards = policies
            .Select((policy, index) => CreateCard(
                policy,
                policies.Count - index,
                relationships,
                activity,
                auditEvents))
            .ToList();

        var html = new StringBuilder();

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"utf-8\" />");
        html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        html.AppendLine("    <title>Policy Explorer</title>");
        html.AppendLine("    <link rel=\"stylesheet\" href=\"/styles.css\" />");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("    <div class=\"app-shell\">");
        html.Append(PortalSidebarRenderer.Render("policies"));
        html.AppendLine("        <main class=\"container explorer-page\">");
        html.AppendLine("            <header class=\"page-header\">");
        html.AppendLine("                <nav class=\"breadcrumb\" aria-label=\"Breadcrumb\"><a href=\"/dashboard\">Dashboard</a> / Policies</nav>");
        html.AppendLine("                <h1>Policy Explorer</h1>");
        html.AppendLine("                <p class=\"subtitle\">Review configured policies and their projected governance relationships.</p>");
        html.AppendLine("            </header>");
        html.AppendLine("            <section class=\"policy-grid\">");

        foreach (var card in cards)
        {
            AppendPolicyCard(html, card);
        }

        html.AppendLine("            </section>");
        html.AppendLine("            <footer class=\"app-footer\">Seneschal v0.2.1-alpha</footer>");
        html.AppendLine("        </main>");
        html.AppendLine("    </div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    private static PolicyCard CreateCard(
        ApiPolicy policy,
        int priority,
        IReadOnlyCollection<GovernanceRelationship> relationships,
        ActivitySnapshot activity,
        IReadOnlyCollection<AuditEvent> auditEvents)
    {
        var policyRelationships = relationships
            .Where(relationship =>
                relationship.From.Type == GovernanceEntityType.Policy &&
                string.Equals(
                    relationship.From.Id,
                    policy.Name,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();
        var policyActivity = activity.Policies.FirstOrDefault(policyActivity =>
            string.Equals(
                policyActivity.PolicyId,
                policy.Name,
                StringComparison.OrdinalIgnoreCase));
        var matchingAuditEvents = auditEvents
            .Where(auditEvent => auditEvent.MatchedPolicies.Any(policyId =>
                string.Equals(
                    policyId,
                    policy.Name,
                    StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(auditEvent => auditEvent.TimestampUtc)
            .Take(5)
            .ToList();

        return new PolicyCard(
            policy,
            priority,
            policyActivity,
            matchingAuditEvents,
            RelatedEntityIds(
                policyRelationships,
                GovernanceRelationshipType.PolicyAppliesToIdentity,
                GovernanceEntityType.Identity),
            RelatedEntityIds(
                policyRelationships,
                GovernanceRelationshipType.PolicyAppliesToCapability,
                GovernanceEntityType.Capability),
            RelatedEntityIds(
                policyRelationships,
                GovernanceRelationshipType.PolicyAppliesToResource,
                GovernanceEntityType.Resource));
    }

    private static IReadOnlyCollection<string> RelatedEntityIds(
        IEnumerable<GovernanceRelationship> relationships,
        GovernanceRelationshipType relationshipType,
        GovernanceEntityType entityType)
    {
        return relationships
            .Where(relationship => relationship.Type == relationshipType)
            .SelectMany(relationship => new[]
            {
                relationship.From,
                relationship.To
            })
            .Where(entity => entity.Type == entityType)
            .Select(entity => string.IsNullOrWhiteSpace(entity.Scope)
                ? entity.Id
                : $"{entity.Scope}:{entity.Id}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AppendPolicyCard(
        StringBuilder html,
        PolicyCard card)
    {
        html.AppendLine("                <article class=\"policy-card\">");
        html.AppendLine("                    <div class=\"policy-card-header\">");
        html.AppendLine("                        <div>");
        html.AppendLine("                            <p class=\"muted capability-eyebrow\">Policy Profile</p>");
        html.Append("                            <h2>")
            .Append(Encode(string.IsNullOrWhiteSpace(card.Policy.DisplayName)
                ? card.Policy.Name
                : card.Policy.DisplayName))
            .AppendLine("</h2>");
        html.Append("                            <p class=\"code capability-id\">")
            .Append(Encode(card.Policy.Name))
            .AppendLine("</p>");
        html.AppendLine("                        </div>");
        html.AppendLine("                        <div class=\"badge-row\">");
        AppendEffectBadge(html, card.Policy.Decision);
        html.AppendLine("                            <span class=\"badge monitor-mode-badge\">Runtime mode: LogOnly</span>");
        html.AppendLine("                        </div>");
        html.AppendLine("                    </div>");
        html.Append("                    <p class=\"policy-reason\">")
            .Append(Encode(string.IsNullOrWhiteSpace(card.Policy.Description)
                ? card.Policy.Reason
                : card.Policy.Description))
            .AppendLine("</p>");
        if (!string.IsNullOrWhiteSpace(card.Policy.Owner) ||
            !string.IsNullOrWhiteSpace(card.Policy.Severity))
        {
            html.AppendLine("                    <dl class=\"metadata-grid compact-metadata-grid\">");
            AppendCondition(html, "Owner", card.Policy.Owner);
            AppendCondition(html, "Severity", card.Policy.Severity);
            html.AppendLine("                    </dl>");
        }
        if (!string.IsNullOrWhiteSpace(card.Policy.Rationale))
        {
            html.Append("                    <p class=\"muted\"><strong>Rationale:</strong> ")
                .Append(Encode(card.Policy.Rationale))
                .AppendLine("</p>");
        }
        html.Append("                    <p class=\"muted\">Priority: ")
            .Append(card.Priority)
            .AppendLine("</p>");

        html.AppendLine("                    <section class=\"policy-profile-section\">");
        html.AppendLine("                        <h3>Applies To</h3>");
        html.AppendLine("                        <div class=\"policy-relations compact-policy-relations\">");
        AppendRelationGroup(html, "Related identities", card.Identities, "/identity-activity?identityId=");
        AppendRelationGroup(html, "Related capabilities", card.Capabilities, "/capability-explorer?capabilityId=");
        AppendRelationGroup(html, "Related resources", card.Resources, null);
        html.AppendLine("                        </div>");
        html.AppendLine("                    </section>");

        html.AppendLine("                    <section class=\"policy-profile-section\">");
        html.AppendLine("                        <h3>Runtime Summary</h3>");
        html.AppendLine("                        <div class=\"dashboard-grid policy-runtime-grid\">");
        AppendMetricCard(html, "Match count", card.Activity?.MatchCount.ToString() ?? "0");
        AppendMetricCard(html, "Last matched", card.Activity?.LastMatchedUtc?.ToString("u") ?? "n/a");
        AppendMetricCard(html, "Related denied", card.DeniedCount.ToString());
        AppendMetricCard(html, "Related pending approval", card.PendingApprovalCount.ToString());
        html.AppendLine("                        </div>");
        html.AppendLine("                    </section>");

        html.AppendLine("                    <section class=\"policy-profile-section\">");
        html.AppendLine("                        <h3>Conditions</h3>");
        html.AppendLine("                        <dl class=\"metadata-grid compact-metadata-grid policy-condition-grid\">");
        AppendCondition(html, "Identity", string.Join(", ", card.Policy.EffectiveIdentities));
        AppendCondition(html, "Capability", string.Join(", ", card.Policy.EffectiveCapabilities));
        AppendCondition(html, "Environment", string.Join(", ", card.Policy.EffectiveEnvironments));
        AppendCondition(html, "Effect", card.Policy.Decision);
        html.AppendLine("                        </dl>");
        html.AppendLine("                    </section>");

        html.AppendLine("                    <section class=\"dashboard-columns policy-profile-columns\">");
        html.AppendLine("                        <div class=\"policy-profile-section\">");
        html.AppendLine("                            <h3>Audit Trail</h3>");
        html.Append("                            <p><a class=\"table-link\" href=\"/audit?matchedPolicy=")
            .Append(Uri.EscapeDataString(card.Policy.Name))
            .AppendLine("\">Open Filtered Audit Trail</a></p>");
        if (card.RecentAuditEvents.Count == 0)
        {
            html.AppendLine("                            <p class=\"muted empty-state\">No recent audit events matched this policy.</p>");
        }
        else
        {
            html.AppendLine("                            <ul class=\"compact-list activity-list\">");
            foreach (var auditEvent in card.RecentAuditEvents)
            {
                html.Append("                                <li><span>")
                    .Append(Encode(auditEvent.TimestampUtc.ToString("u")))
                    .Append(" · ")
                    .Append(Encode(auditEvent.IdentityId))
                    .Append("</span><span>")
                    .Append(Encode(auditEvent.Decision.ToString()))
                    .Append(" · ")
                    .Append(Encode(auditEvent.Reason))
                    .AppendLine("</span></li>");
            }

            html.AppendLine("                            </ul>");
        }

        html.AppendLine("                        </div>");
        html.AppendLine("                        <div class=\"policy-profile-section\">");
        html.AppendLine("                            <h3>Recommendations</h3>");
        html.Append("                            <p class=\"monitor-recommendation\">")
            .Append(Encode(card.Recommendation))
            .AppendLine("</p>");
        html.AppendLine("                            <p class=\"muted\">Use runtime activity and audit history to understand policy impact before changing enforcement posture.</p>");
        html.AppendLine("                        </div>");
        html.AppendLine("                    </section>");
        html.AppendLine("                </article>");
    }

    private static void AppendEffectBadge(StringBuilder html, string decision)
    {
        html.Append("                            <span class=\"badge decision-badge decision-")
            .Append(Encode(CssClass(decision)))
            .Append("\">")
            .Append(Encode(decision))
            .AppendLine("</span>");
    }

    private static void AppendMetricCard(
        StringBuilder html,
        string label,
        string value)
    {
        html.AppendLine("                            <article class=\"dashboard-card activity-card\">");
        html.Append("                                <strong>")
            .Append(Encode(value))
            .AppendLine("</strong>");
        html.Append("                                <span>")
            .Append(Encode(label))
            .AppendLine("</span>");
        html.AppendLine("                            </article>");
    }

    private static void AppendCondition(
        StringBuilder html,
        string label,
        string value)
    {
        html.Append("                            <dt>")
            .Append(Encode(label))
            .AppendLine("</dt>");
        html.Append("                            <dd>")
            .Append(Encode(string.IsNullOrWhiteSpace(value) ? "Any" : value))
            .AppendLine("</dd>");
    }

    private static void AppendRelationGroup(
        StringBuilder html,
        string label,
        IReadOnlyCollection<string> values,
        string? routePrefix)
    {
        html.AppendLine("                        <div>");
        html.Append("                            <h3>")
            .Append(Encode(label))
            .AppendLine("</h3>");

        if (values.Count == 0)
        {
            html.AppendLine("                            <p class=\"muted\">None</p>");
        }
        else
        {
            html.AppendLine("                            <ul>");
            foreach (var value in values)
            {
                html.Append("                                <li>");
                if (routePrefix is not null)
                    html.Append("<a href=\"").Append(routePrefix)
                        .Append(Uri.EscapeDataString(value)).Append("\">");
                html.Append(Encode(value));
                if (routePrefix is not null) html.Append("</a>");
                html.AppendLine("</li>");
            }
            html.AppendLine("                            </ul>");
        }

        html.AppendLine("                        </div>");
    }
    private static string CssClass(string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace("_", "-", StringComparison.Ordinal);
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private sealed record PolicyCard(
        ApiPolicy Policy,
        int Priority,
        PolicyActivity? Activity,
        IReadOnlyCollection<AuditEvent> RecentAuditEvents,
        IReadOnlyCollection<string> Identities,
        IReadOnlyCollection<string> Capabilities,
        IReadOnlyCollection<string> Resources)
    {
        public long DeniedCount => RecentAuditEvents.LongCount(auditEvent =>
            auditEvent.Decision == DecisionType.Deny);

        public long PendingApprovalCount => RecentAuditEvents.LongCount(auditEvent =>
            auditEvent.Decision == DecisionType.RequireApproval);

        public string Recommendation
        {
            get
            {
                if (Activity is null || Activity.MatchCount == 0)
                {
                    return "Policy has not been exercised yet";
                }

                if (DeniedCount > 0 || PendingApprovalCount > 0)
                {
                    return "Review runtime impact";
                }

                return "Policy appears active and stable";
            }
        }
    }
}
