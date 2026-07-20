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
        AppendInvestigationNavigation(html, filter);
        AppendInsights(html, events);
        AppendFilterForm(html, filter);

        if (events.Count == 0)
        {
            html.AppendLine("            <section class=\"notice\">");
            html.AppendLine("                <h2>No audit events yet</h2>");
            html.AppendLine("                <p class=\"muted\">Audit events are created automatically when decisions are evaluated.</p>");
            html.AppendLine("                <p class=\"muted\">Users do not manually create audit events. Run a policy evaluation to populate the audit trail.</p>");
            html.AppendLine("                <p class=\"muted\">Try: <span class=\"code\">seneschal evaluate payment-agent azure.keyvault.secret.read production</span></p>");
            html.AppendLine("            </section>");
        }
        else
        {
            AppendTimeline(html, events);

            html.AppendLine("            <section class=\"panel\">");
            html.AppendLine("                <h2>Recent Audit Events</h2>");
            html.AppendLine("                <div class=\"table-scroll\" tabindex=\"0\" role=\"region\" aria-label=\"Recent audit events\">");
            html.AppendLine("                <table class=\"audit-table\">");
            html.AppendLine("                    <thead>");
            html.AppendLine("                        <tr>");
            html.AppendLine("                            <th>Timestamp</th>");
            html.AppendLine("                            <th>Identity</th>");
            html.AppendLine("                            <th>Capability</th>");
            html.AppendLine("                            <th>Decision</th>");
        html.AppendLine("                            <th>Matched Policy</th>");
        html.AppendLine("                            <th>Reason</th>");
        html.AppendLine("                            <th>Governance Window</th>");
        html.AppendLine("                            <th>Trace</th>");
        html.AppendLine("                        </tr>");
            html.AppendLine("                    </thead>");
            html.AppendLine("                    <tbody>");

            foreach (var auditEvent in events)
            {
                AppendRow(html, auditEvent);
            }

            html.AppendLine("                    </tbody>");
            html.AppendLine("                </table>");
            html.AppendLine("                </div>");
            html.AppendLine("            </section>");
        }

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

        html.AppendLine("            <nav class=\"trace-navigation\" aria-label=\"Continue investigation\">");
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
        html.AppendLine("            </nav>");
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

    private static void AppendInsights(
        StringBuilder html,
        IReadOnlyCollection<AuditEvent> events)
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
        html.AppendLine("                <h2>Audit Insights</h2>");
        html.AppendLine("                <div class=\"audit-insights-grid\">");
        AppendInsightCard(html, "Total decisions", totalDecisions.ToString());
        AppendInsightCard(html, "Allowed", allowedCount.ToString());
        AppendInsightCard(html, "Denied", deniedCount.ToString());
        AppendInsightCard(html, "Pending approval", pendingApprovalCount.ToString());
        AppendInsightCard(html, "Most active identity", mostActiveIdentity);
        AppendInsightCard(
            html,
            "Most evaluated capability",
            mostEvaluatedCapability);
        AppendInsightCard(html, "Most matched policy", mostMatchedPolicy);
        AppendInsightCard(
            html,
            "Average evaluation duration",
            $"{averageDuration:0.##} ms");
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
        html.AppendLine("                <h2>Audit Timeline</h2>");
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

    private static void AppendRow(
        StringBuilder html,
        AuditEvent auditEvent)
    {
        html.AppendLine("                        <tr>");
        AppendCell(html, auditEvent.TimestampUtc.ToString("u"));
        AppendCell(html, auditEvent.IdentityId);
        AppendCell(html, auditEvent.CapabilityId);
        html.Append("                            <td><span class=\"badge decision-badge ")
            .Append(DecisionClass(auditEvent.Decision))
            .Append("\">")
            .Append(DecisionLabel(auditEvent.Decision))
            .AppendLine("</span></td>");
        AppendCell(
            html,
            auditEvent.MatchedPolicies.FirstOrDefault() ?? "none");
        AppendCell(html, auditEvent.Reason);
        AppendCell(
            html,
            auditEvent.GovernanceWindowMessage ?? "—");
        html.Append("                            <td><a class=\"table-link\" href=\"/audit/")
            .Append(Uri.EscapeDataString(auditEvent.Id))
            .AppendLine("\">View Decision Trace</a></td>");
        html.AppendLine("                        </tr>");
    }

    private static void AppendCell(
        StringBuilder html,
        string value)
    {
        html.Append("                            <td>")
            .Append(WebUtility.HtmlEncode(value))
            .AppendLine("</td>");
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
