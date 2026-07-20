using System.Net;
using System.Text;
using Seneschal.Api.Models;

namespace Seneschal.Api.Services;

public static class AuditTrailPageRenderer
{
    public static string Render(
        IReadOnlyCollection<AuditEvent> events,
        AuditEventFilter filter)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(filter);

        var html = new StringBuilder();

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"utf-8\" />");
        html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        html.AppendLine("    <title>Audit Trail</title>");
        html.AppendLine("    <link rel=\"stylesheet\" href=\"/styles.css\" />");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("    <div class=\"app-shell\">");
        html.Append(PortalSidebarRenderer.Render("audit"));
        html.AppendLine("        <main class=\"container explorer-page\">");
        html.AppendLine("            <header class=\"page-header\">");
        html.AppendLine("                <nav class=\"breadcrumb\" aria-label=\"Breadcrumb\"><a href=\"/monitor\">Live Monitor</a> / Audit Trail</nav>");
        html.AppendLine("                <h1>Audit Trail</h1>");
        html.AppendLine("                <p class=\"subtitle\">Recent completed policy evaluations.</p>");
        html.AppendLine("            </header>");
        AppendInvestigationContext(html, filter);
        AppendSummary(html, events, filter);

        if (events.Count == 0)
        {
            html.AppendLine("            <section class=\"notice\">");
            if (HasActiveFilters(filter))
            {
                html.AppendLine("                <h2>No matching evidence</h2>");
                html.AppendLine("                <p class=\"muted\">No audit events match the active filters. Review or clear the filters to broaden this investigation.</p>");
                html.AppendLine("                <a class=\"table-link\" href=\"/audit\">Clear active filters</a>");
            }
            else
            {
                html.AppendLine("                <h2>No audit events recorded</h2>");
                html.AppendLine("                <p class=\"muted\">Completed policy evaluations will appear here when audit evidence is recorded.</p>");
            }
            html.AppendLine("            </section>");
        }
        else
        {
            AppendTimeline(html, events);
        }

        AppendInvestigationNavigation(html, filter);
        AppendFilterForm(html, filter);

        html.AppendLine("            <footer class=\"app-footer\">Seneschal v0.2.1-alpha</footer>");
        html.AppendLine("        </main>");
        html.AppendLine("    </div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    private static void AppendFilterForm(
        StringBuilder html,
        AuditEventFilter filter)
    {
        var isOpen = HasActiveFilters(filter) ? " open" : string.Empty;

        html.Append("            <details class=\"panel filter-panel\"")
            .Append(isOpen)
            .AppendLine(">");
        html.AppendLine("                <summary>Filter Audit Events</summary>");
        html.AppendLine("                <p class=\"muted filter-help\">Narrow recent decisions by identity, capability, environment, policy, decision, or mode.</p>");
        html.AppendLine("                <form class=\"filter-form\" method=\"get\" action=\"/audit\">");
        AppendFilterInput(
            html,
            "identityId",
            "Identity ID",
            filter.IdentityId,
            "payment-agent");
        AppendFilterInput(
            html,
            "capabilityId",
            "Capability ID",
            filter.CapabilityId,
            "azure.keyvault.secret.read");
        AppendFilterSelect(
            html,
            "decision",
            "Decision",
            filter.Decision,
            [
                ("", "All"),
                ("allow", "Allow"),
                ("deny", "Deny"),
                ("requires_approval", "Pending Approval")
            ]);
        AppendFilterSelect(
            html,
            "enforcementMode",
            "Runtime mode",
            filter.EnforcementMode,
            [
                ("", "All"),
                ("LogOnly", "LogOnly"),
                ("Enforce", "Enforce")
            ]);
        AppendFilterInput(
            html,
            "environment",
            "Environment",
            filter.Environment,
            "production");
        AppendFilterInput(
            html,
            "matchedPolicy",
            "Matched Policy",
            filter.MatchedPolicy,
            "prod-secret-read");
        html.AppendLine("                    <div class=\"filter-actions\">");
        html.AppendLine("                        <button type=\"submit\">Apply Filters</button>");
        html.AppendLine("                        <a class=\"table-link\" href=\"/audit\">Clear</a>");
        html.AppendLine("                    </div>");
        html.AppendLine("                </form>");
        html.AppendLine("            </details>");
    }

    private static void AppendInvestigationNavigation(
        StringBuilder html,
        AuditEventFilter filter)
    {
        if (string.IsNullOrWhiteSpace(filter.CapabilityId) &&
            string.IsNullOrWhiteSpace(filter.IdentityId))
            return;

        html.AppendLine("            <section class=\"panel\" aria-labelledby=\"investigation-actions-heading\">");
        html.AppendLine("                <h2 id=\"investigation-actions-heading\">Continue investigation</h2>");
        html.AppendLine("                <nav class=\"trace-navigation\" aria-label=\"Investigation actions\">");
        if (!string.IsNullOrWhiteSpace(filter.CapabilityId))
        {
            var parameters = new List<string>
            {
                $"capabilityId={Uri.EscapeDataString(filter.CapabilityId)}"
            };
            Add("identity", filter.IdentityId);
            Add("environment", filter.Environment);
            Add("runtimeMode", filter.EnforcementMode);
            var decision = CapabilityActivityDecision(filter.Decision);
            Add("decision", decision);
            AppendInvestigationLink(html,
                $"/capability-activity?{string.Join("&", parameters)}",
                "Investigate Capability Activity");
            AppendInvestigationLink(html,
                $"/capability-explorer?capabilityId={Uri.EscapeDataString(filter.CapabilityId)}",
                "View capability profile");

            void Add(string name, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    parameters.Add($"{name}={Uri.EscapeDataString(value)}");
            }
        }
        if (!string.IsNullOrWhiteSpace(filter.IdentityId))
        {
            AppendInvestigationLink(html,
                $"/identity-activity?identityId={Uri.EscapeDataString(filter.IdentityId)}",
                "View Identity Activity");
        }
        html.AppendLine("                </nav>");
        html.AppendLine("            </section>");
    }

    private static string? CapabilityActivityDecision(string? decision) =>
        decision?.ToLowerInvariant() switch
        {
            "allow" => "Allow",
            "deny" => "Deny",
            "requires_approval" => "PendingApproval",
            _ => null
        };

    private static void AppendInvestigationLink(
        StringBuilder html, string href, string label)
    {
        html.Append("                <a href=\"")
            .Append(Encode(href)).Append("\">")
            .Append(Encode(label)).AppendLine("</a>");
    }

    private static void AppendInvestigationContext(
        StringBuilder html,
        AuditEventFilter filter)
    {
        html.AppendLine("            <section class=\"panel\" aria-labelledby=\"investigation-context-heading\">");
        html.AppendLine("                <h2 id=\"investigation-context-heading\">Investigation context</h2>");
        if (!HasActiveFilters(filter))
        {
            html.AppendLine("                <p class=\"muted\">Showing all recent audit evidence. No filters are active.</p>");
        }
        else
        {
            html.AppendLine("                <p class=\"muted\">Showing audit evidence that matches all active filters.</p>");
            html.AppendLine("                <dl class=\"timeline-meta\">");
            AppendActiveFilter(html, "Capability", filter.CapabilityId);
            AppendActiveFilter(html, "Identity", filter.IdentityId);
            AppendActiveFilter(html, "Environment", filter.Environment);
            AppendActiveFilter(html, "Runtime mode", filter.EnforcementMode);
            AppendActiveFilter(html, "Decision", FilterDecisionLabel(filter.Decision));
            AppendActiveFilter(html, "Matched policy", filter.MatchedPolicy);
            html.AppendLine("                </dl>");
        }
        html.AppendLine("            </section>");
    }

    private static void AppendActiveFilter(StringBuilder html, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        AppendTimelineField(html, label, value);
    }

    private static string? FilterDecisionLabel(string? decision) =>
        decision?.ToLowerInvariant() switch
        {
            "allow" => "Allow",
            "deny" => "Deny",
            "requires_approval" => "Pending Approval",
            _ => decision
        };

    private static void AppendSummary(
        StringBuilder html,
        IReadOnlyCollection<AuditEvent> events,
        AuditEventFilter filter)
    {
        var totalDecisions = events.Count;
        var allowedCount = CountDecision(events, "allow");
        var deniedCount = CountDecision(events, "deny");
        var pendingApprovalCount = CountDecision(events, "requires_approval");
        var mostActiveIdentity = MostCommon(
            events.Select(auditEvent => auditEvent.IdentityId));
        var mostEvaluatedCapability = MostCommon(
            events.Select(auditEvent => auditEvent.CapabilityId));
        var mostMatchedPolicy = MostCommon(
            events.SelectMany(auditEvent => auditEvent.MatchedPolicies));
        var averageDuration = events.Count == 0
            ? 0
            : events.Average(auditEvent => auditEvent.EvaluationDurationMs);

        html.AppendLine("            <section class=\"panel\">");
        html.AppendLine("                <h2>Investigation summary</h2>");
        html.AppendLine("                <div class=\"audit-insights-grid\">");
        AppendInsightCard(html, "Matching events", totalDecisions.ToString());
        AppendInsightCard(html, "Allowed", allowedCount.ToString());
        AppendInsightCard(html, "Denied", deniedCount.ToString());
        AppendInsightCard(html, "Pending approval", pendingApprovalCount.ToString());
        if (string.IsNullOrWhiteSpace(filter.IdentityId))
            AppendInsightCard(html, "Most active identity", mostActiveIdentity);
        if (string.IsNullOrWhiteSpace(filter.CapabilityId))
            AppendInsightCard(html, "Most evaluated capability", mostEvaluatedCapability);
        if (string.IsNullOrWhiteSpace(filter.MatchedPolicy))
            AppendInsightCard(html, "Most matched policy", mostMatchedPolicy);
        AppendInsightCard(html, "Average evaluation duration", $"{averageDuration:0.##} ms");
        html.AppendLine("                </div>");
        html.AppendLine("            </section>");
    }

    private static int CountDecision(
        IReadOnlyCollection<AuditEvent> events,
        string decision)
    {
        return events.Count(auditEvent => string.Equals(
            auditEvent.Decision,
            decision,
            StringComparison.OrdinalIgnoreCase));
    }

    private static string MostCommon(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Key)
            .FirstOrDefault() ?? "none";
    }

    private static void AppendInsightCard(
        StringBuilder html,
        string label,
        string value)
    {
        html.AppendLine("                    <article class=\"audit-insight-card\">");
        html.Append("                        <strong>")
            .Append(Encode(value))
            .AppendLine("</strong>");
        html.Append("                        <span>")
            .Append(Encode(label))
            .AppendLine("</span>");
        html.AppendLine("                    </article>");
    }

    private static void AppendTimeline(
        StringBuilder html,
        IReadOnlyCollection<AuditEvent> events)
    {
        html.AppendLine("            <section class=\"panel\">");
        html.AppendLine("                <h2>Evidence</h2>");
        html.AppendLine("                <ol class=\"audit-timeline\">");

        foreach (var auditEvent in events.OrderByDescending(
            auditEvent => auditEvent.TimestampUtc))
        {
            html.AppendLine("                    <li class=\"timeline-item\">");
            html.AppendLine("                        <div class=\"timeline-marker\" aria-hidden=\"true\"></div>");
            html.AppendLine("                        <article class=\"timeline-content\">");
            html.AppendLine("                            <div class=\"timeline-header\">");
            html.Append("                                <time datetime=\"")
                .Append(Encode(auditEvent.TimestampUtc.ToString("O")))
                .Append("\">")
                .Append(Encode(auditEvent.TimestampUtc.ToString("u")))
                .AppendLine("</time>");
            html.Append("                                <span class=\"badge decision-badge ")
                .Append(DecisionClass(auditEvent.Decision))
                .Append("\">")
                .Append(DecisionLabel(auditEvent.Decision))
                .AppendLine("</span>");
            html.AppendLine("                            </div>");
            html.AppendLine("                            <dl class=\"timeline-meta\">");
            AppendTimelineField(html, "Identity", auditEvent.IdentityId);
            AppendTimelineField(html, "Capability", auditEvent.CapabilityId);
            AppendTimelineField(html, "Environment", auditEvent.Environment);
            AppendTimelineField(html, "Runtime mode", auditEvent.EnforcementMode);
            AppendTimelineField(html, "Operation", auditEvent.ApprovalOperationId ?? "Not recorded");
            AppendTimelineField(html, "Policy", auditEvent.MatchedPolicies.FirstOrDefault() ?? "No matched policy recorded");
            AppendTimelineField(html, "Approval", auditEvent.ApprovalStatus ?? "Not involved or not recorded");
            if (!string.IsNullOrWhiteSpace(auditEvent.GovernanceWindowName))
            {
                AppendTimelineField(
                    html,
                    "Governance Window",
                    $"{auditEvent.GovernanceWindowName} ({auditEvent.GovernanceWindowMode})");
            }
            html.AppendLine("                            </dl>");
            html.Append("                            <p class=\"timeline-reason\">")
                .Append(Encode(auditEvent.Reason))
                .AppendLine("</p>");
            html.Append("                            <a class=\"table-link\" href=\"/audit/")
                .Append(Uri.EscapeDataString(auditEvent.Id))
                .AppendLine("\">View Decision Trace</a>");
            html.AppendLine("                        </article>");
            html.AppendLine("                    </li>");
        }

        html.AppendLine("                </ol>");
        html.AppendLine("            </section>");
    }

    private static void AppendTimelineField(
        StringBuilder html,
        string label,
        string value)
    {
        html.AppendLine("                                <div>");
        html.Append("                                    <dt>")
            .Append(Encode(label))
            .AppendLine("</dt>");
        html.Append("                                    <dd>")
            .Append(Encode(value))
            .AppendLine("</dd>");
        html.AppendLine("                                </div>");
    }

    private static void AppendFilterInput(
        StringBuilder html,
        string name,
        string label,
        string? value,
        string placeholder)
    {
        html.AppendLine("                    <label>");
        html.Append("                        <span>")
            .Append(Encode(label))
            .AppendLine("</span>");
        html.Append("                        <input name=\"")
            .Append(Encode(name))
            .Append("\" placeholder=\"")
            .Append(Encode(placeholder))
            .Append("\" value=\"")
            .Append(Encode(value ?? string.Empty))
            .AppendLine("\" />");
        html.AppendLine("                    </label>");
    }

    private static void AppendFilterSelect(
        StringBuilder html,
        string name,
        string label,
        string? selectedValue,
        IReadOnlyCollection<(string Value, string Label)> options)
    {
        html.AppendLine("                    <label>");
        html.Append("                        <span>")
            .Append(Encode(label))
            .AppendLine("</span>");
        html.Append("                        <select name=\"")
            .Append(Encode(name))
            .AppendLine("\">");

        foreach (var option in options)
        {
            var selected = IsSelectedFilterValue(
                selectedValue,
                option.Value,
                option.Label)
                ? " selected"
                : string.Empty;

            html.Append("                            <option value=\"")
                .Append(Encode(option.Value))
                .Append("\"")
                .Append(selected)
                .Append(">")
                .Append(Encode(option.Label))
                .AppendLine("</option>");
        }

        html.AppendLine("                        </select>");
        html.AppendLine("                    </label>");
    }

    private static bool HasActiveFilters(AuditEventFilter filter)
    {
        return !string.IsNullOrWhiteSpace(filter.IdentityId) ||
            !string.IsNullOrWhiteSpace(filter.CapabilityId) ||
            !string.IsNullOrWhiteSpace(filter.Decision) ||
            !string.IsNullOrWhiteSpace(filter.EnforcementMode) ||
            !string.IsNullOrWhiteSpace(filter.Environment) ||
            !string.IsNullOrWhiteSpace(filter.MatchedPolicy);
    }

    private static bool IsSelectedFilterValue(
        string? selectedValue,
        string optionValue,
        string optionLabel)
    {
        if (string.IsNullOrWhiteSpace(selectedValue))
        {
            return string.IsNullOrWhiteSpace(optionValue);
        }

        return string.Equals(
                selectedValue,
                optionValue,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                selectedValue,
                optionLabel,
                StringComparison.OrdinalIgnoreCase);
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private static string DecisionClass(string decision)
    {
        return decision switch
        {
            "allow" => "decision-allow",
            "deny" => "decision-deny",
            "requires_approval" => "decision-pending",
            _ => "decision-log-only"
        };
    }

    private static string DecisionLabel(string decision)
    {
        return decision switch
        {
            "requires_approval" => "Pending Approval",
            _ => decision
        };
    }
}
