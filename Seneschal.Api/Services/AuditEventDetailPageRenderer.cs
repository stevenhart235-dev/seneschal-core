using System.Net;
using System.Text;
using Seneschal.Api.Models;

namespace Seneschal.Api.Services;

public static class AuditEventDetailPageRenderer
{
    public static string Render(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var html = new StringBuilder();

        AppendShellStart(html, "Seneschal Audit Event Detail");
        html.AppendLine("            <header class=\"page-header\">");
        html.AppendLine("                <div class=\"breadcrumb\">Operations / Audit / Trace</div>");
        html.AppendLine("                <h1>Audit Event Detail</h1>");
        html.AppendLine("                <p class=\"subtitle\">Full decision trace for a completed policy evaluation.</p>");
        html.AppendLine("            </header>");

        html.AppendLine("            <section class=\"panel\">");
        html.AppendLine("                <h2>Decision Trace</h2>");
        html.AppendLine("                <ol class=\"trace-list\">");
        AppendTraceStep(
            html,
            "Request",
            [
                ("TimestampUtc", auditEvent.TimestampUtc.ToString("u")),
                ("IdentityId", auditEvent.IdentityId),
                ("CapabilityId", auditEvent.CapabilityId),
                ("ResourceId", auditEvent.ResourceId),
                ("Environment", auditEvent.Environment)
            ]);
        AppendTraceStep(
            html,
            "Policy Match",
            [
                ("MatchedPolicies", FormatList(auditEvent.MatchedPolicies)),
                ("EvaluationDurationMs", $"{auditEvent.EvaluationDurationMs} ms")
            ]);
        AppendTraceStep(
            html,
            "Decision",
            [
                ("Decision", auditEvent.Decision),
                ("EnforcementMode", auditEvent.EnforcementMode)
            ]);
        if (!string.IsNullOrWhiteSpace(auditEvent.GovernanceWindowName))
        {
            AppendTraceStep(
                html,
                "Governance Window",
                [
                    ("Name", auditEvent.GovernanceWindowName),
                    ("Mode", auditEvent.GovernanceWindowMode ?? string.Empty),
                    ("Participation", auditEvent.GovernanceWindowMessage ?? string.Empty)
                ]);
        }
        AppendTraceStep(
            html,
            "Obligations",
            [
                ("Obligations", FormatList(auditEvent.Obligations))
            ]);
        AppendTraceStep(
            html,
            "Reason",
            [
                ("Reason", auditEvent.Reason)
            ]);
        html.AppendLine("                </ol>");
        html.AppendLine("            </section>");

        html.AppendLine("            <section class=\"panel\">");
        html.AppendLine("                <h2>Raw Fields</h2>");
        html.AppendLine("                <dl class=\"metadata-grid\">");
        AppendMetadata(html, "Id", auditEvent.Id);
        AppendMetadata(html, "TimestampUtc", auditEvent.TimestampUtc.ToString("u"));
        AppendMetadata(html, "IdentityId", auditEvent.IdentityId);
        AppendMetadata(html, "CapabilityId", auditEvent.CapabilityId);
        AppendMetadata(html, "ResourceId", auditEvent.ResourceId);
        AppendMetadata(html, "Environment", auditEvent.Environment);
        AppendMetadata(html, "Decision", auditEvent.Decision);
        AppendMetadata(html, "EnforcementMode", auditEvent.EnforcementMode);
        AppendMetadata(html, "MatchedPolicies", FormatList(auditEvent.MatchedPolicies));
        AppendMetadata(html, "Obligations", FormatList(auditEvent.Obligations));
        AppendMetadata(html, "Reason", auditEvent.Reason);
        AppendMetadata(html, "GovernanceWindowName", auditEvent.GovernanceWindowName ?? string.Empty);
        AppendMetadata(html, "GovernanceWindowMode", auditEvent.GovernanceWindowMode ?? string.Empty);
        AppendMetadata(html, "GovernanceWindowMessage", auditEvent.GovernanceWindowMessage ?? string.Empty);
        AppendMetadata(html, "EvaluationDurationMs", auditEvent.EvaluationDurationMs.ToString());
        html.AppendLine("                </dl>");
        html.AppendLine("            </section>");

        AppendShellEnd(html);

        return html.ToString();
    }

    public static string RenderNotFound(string auditEventId)
    {
        var html = new StringBuilder();

        AppendShellStart(html, "Audit Event Not Found");
        html.AppendLine("            <section class=\"notice\">");
        html.AppendLine("                <h1>Audit event not found</h1>");
        html.Append("                <p>No audit event was found for <span class=\"code\">")
            .Append(Encode(auditEventId))
            .AppendLine("</span>.</p>");
        html.AppendLine("                <p><a class=\"button-link\" href=\"/audit\">Back to Audit Trail</a></p>");
        html.AppendLine("            </section>");
        AppendShellEnd(html);

        return html.ToString();
    }

    private static void AppendShellStart(
        StringBuilder html,
        string title)
    {
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"utf-8\" />");
        html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        html.Append("    <title>")
            .Append(Encode(title))
            .AppendLine("</title>");
        html.AppendLine("    <link rel=\"stylesheet\" href=\"/styles.css\" />");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("    <div class=\"app-shell\">");
        html.Append(PortalSidebarRenderer.Render("audit"));
        html.AppendLine("        <main class=\"container explorer-page\">");
    }

    private static void AppendShellEnd(StringBuilder html)
    {
        html.AppendLine("            <footer class=\"app-footer\">Seneschal v0.2.1-alpha</footer>");
        html.AppendLine("        </main>");
        html.AppendLine("    </div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
    }

    private static void AppendTraceStep(
        StringBuilder html,
        string title,
        IReadOnlyCollection<(string Label, string Value)> fields)
    {
        html.AppendLine("                    <li>");
        html.Append("                        <h3>")
            .Append(Encode(title))
            .AppendLine("</h3>");
        html.AppendLine("                        <dl class=\"trace-fields\">");

        foreach (var field in fields)
        {
            AppendMetadata(html, field.Label, field.Value);
        }

        html.AppendLine("                        </dl>");
        html.AppendLine("                    </li>");
    }

    private static void AppendMetadata(
        StringBuilder html,
        string label,
        string value)
    {
        html.Append("                            <dt>")
            .Append(Encode(label))
            .AppendLine("</dt>");
        html.Append("                            <dd>")
            .Append(Encode(string.IsNullOrWhiteSpace(value) ? "none" : value))
            .AppendLine("</dd>");
    }

    private static string FormatList(IReadOnlyCollection<string> values)
    {
        return values.Count == 0
            ? "none"
            : string.Join(", ", values);
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}
