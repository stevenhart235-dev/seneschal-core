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
        AppendShellStart(html, "Seneschal Decision Trace");
        html.AppendLine("            <header class=\"page-header\">");
        html.AppendLine("                <div class=\"breadcrumb\">Operations / Audit / Trace</div>");
        html.AppendLine("                <h1>Decision Trace</h1>");
        html.AppendLine("                <p class=\"subtitle\">Audit Event Detail — how Seneschal reached the final outcome for this evaluation.</p>");
        html.AppendLine("                <a href=\"/audit\">Back to Audit Trail</a>");
        html.AppendLine("            </header>");

        AppendRequestContext(html, auditEvent);
        AppendPolicyEvaluation(html, auditEvent);
        if (HasApproval(auditEvent))
        {
            AppendApproval(html, auditEvent);
        }
        if (HasWindow(auditEvent))
        {
            AppendGovernanceWindow(html, auditEvent);
        }
        AppendDecisionResolution(html, auditEvent);
        AppendFinalOutcome(html, auditEvent);
        AppendRawFields(html, auditEvent);
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

    private static void AppendRequestContext(StringBuilder html, AuditEvent auditEvent)
    {
        html.AppendLine("            <section class=\"panel decision-trace-section\">");
        html.AppendLine("                <span class=\"trace-section-number\">1</span><h2>Request Context</h2>");
        html.AppendLine("                <dl class=\"trace-context-grid\">");
        AppendMetadata(html, "Identity", auditEvent.IdentityId);
        AppendMetadata(html, "Capability", auditEvent.CapabilityId);
        AppendMetadata(html, "Environment", auditEvent.Environment);
        AppendMetadata(html, "Resource", auditEvent.ResourceId);
        AppendMetadata(html, "Timestamp", auditEvent.TimestampUtc.ToString("u"));
        AppendMetadata(html, "Request ID", auditEvent.RequestId);
        AppendMetadata(html, "Decision ID", auditEvent.Id);
        html.AppendLine("                </dl>");
        html.AppendLine("            </section>");
    }

    private static void AppendPolicyEvaluation(StringBuilder html, AuditEvent auditEvent)
    {
        var winningPolicyId = auditEvent.MatchedPolicies.FirstOrDefault();
        var winningPolicy = auditEvent.PolicyEvaluations.FirstOrDefault(policy =>
            policy.PolicyId.Equals(winningPolicyId, StringComparison.OrdinalIgnoreCase));

        html.AppendLine("            <section class=\"panel decision-trace-section\">");
        html.AppendLine("                <span class=\"trace-section-number\">2</span><h2>Policy Evaluation</h2>");
        html.AppendLine("                <dl class=\"trace-context-grid\">");
        AppendMetadata(html, "Winning policy", winningPolicy?.PolicyName ?? winningPolicyId ?? "none");
        AppendMetadata(html, "Policy decision", DisplayDecision(auditEvent.PolicyDecision));
        AppendMetadata(html, "Reason", auditEvent.PolicyReason);
        html.AppendLine("                </dl>");

        html.AppendLine("                <h3>Condition evaluation</h3>");
        html.AppendLine("                <div class=\"why-timeline\">");
        if (winningPolicy is not null)
        {
            AppendPolicyConditions(html, winningPolicy, "Winning policy");
        }
        var otherPolicies = auditEvent.PolicyEvaluations
            .Where(policy => !ReferenceEquals(policy, winningPolicy))
            .ToList();
        if (otherPolicies.Count > 0)
        {
            html.Append("                    <details class=\"other-policy-conditions\"><summary>Other evaluated policies (")
                .Append(otherPolicies.Count)
                .AppendLine(")</summary>");
            foreach (var policy in otherPolicies)
            {
                AppendPolicyConditions(html, policy, null);
            }
            html.AppendLine("                    </details>");
        }
        if (auditEvent.PolicyEvaluations.Count == 0)
        {
            html.AppendLine("                    <p class=\"muted\">Condition-level evidence was not recorded for this evaluation.</p>");
        }
        html.AppendLine("                </div>");

        html.AppendLine("                <h3>Policy Matches / Matched Policies</h3>");
        html.AppendLine("                <ul class=\"policy-evaluation-list\">");
        var visiblePolicies = auditEvent.PolicyEvaluations
            .Where(policy => policy.Matched)
            .Concat(auditEvent.PolicyEvaluations.Where(policy => !policy.Matched).Take(2))
            .Distinct()
            .ToList();
        foreach (var policy in visiblePolicies)
        {
            AppendPolicyOutcome(html, policy);
        }
        if (auditEvent.PolicyEvaluations.Count == 0)
        {
            html.AppendLine("                    <li class=\"muted\">Policy evaluation evidence is unavailable.</li>");
        }
        html.AppendLine("                </ul>");
        var remainingPolicies = auditEvent.PolicyEvaluations
            .Except(visiblePolicies)
            .ToList();
        if (remainingPolicies.Count > 0)
        {
            html.Append("                <details class=\"other-policy-matches\"><summary>Show remaining policy results (")
                .Append(remainingPolicies.Count)
                .AppendLine(")</summary><ul class=\"policy-evaluation-list\">");
            foreach (var policy in remainingPolicies)
            {
                AppendPolicyOutcome(html, policy);
            }
            html.AppendLine("                </ul></details>");
        }
        html.AppendLine("            </section>");
    }

    private static void AppendPolicyConditions(
        StringBuilder html,
        AuditPolicyEvaluation policy,
        string? contextLabel)
    {
        html.Append("                    <section class=\"why-policy\"><h4>")
            .Append(Encode(policy.PolicyName));
        if (!string.IsNullOrWhiteSpace(contextLabel))
        {
            html.Append(" <span>").Append(Encode(contextLabel)).Append("</span>");
        }
        html.AppendLine("</h4>");
        if (policy.Conditions.Count == 0)
        {
            html.AppendLine("                        <p class=\"muted\">No conditions were recorded for this policy.</p>");
        }
        foreach (var condition in policy.Conditions)
        {
            AppendCondition(html, condition);
        }
        html.AppendLine("                    </section>");
    }

    private static void AppendGovernanceWindow(StringBuilder html, AuditEvent auditEvent)
    {
        var changedDecision = !SameDecision(auditEvent.PolicyDecision, auditEvent.Decision);
        var effect = changedDecision
            ? $"Changed {DisplayDecision(auditEvent.PolicyDecision)} to {DisplayDecision(auditEvent.Decision)}"
            : "Matched; policy result unchanged";

        html.AppendLine("            <section class=\"panel decision-trace-section trace-window\">");
        html.AppendLine("                <span class=\"trace-section-number\">3</span><h2>Governance Window</h2>");
        html.AppendLine("                <dl class=\"trace-context-grid\">");
        AppendMetadata(html, "Window name", auditEvent.GovernanceWindowName ?? string.Empty);
        AppendMetadata(html, "Window mode", auditEvent.GovernanceWindowMode ?? string.Empty);
        AppendMetadata(html, "Matched", "Yes");
        AppendMetadata(html, "Effect on policy result", effect);
        AppendMetadata(html, "Window reason", auditEvent.GovernanceWindowReason ?? auditEvent.GovernanceWindowMessage ?? string.Empty);
        html.AppendLine("                </dl>");
        html.AppendLine("                <div class=\"window-result-flow\">");
        AppendFlowValue(html, "Policy result", DisplayDecision(auditEvent.PolicyDecision), "trace-passed");
        AppendFlowValue(html, auditEvent.GovernanceWindowName ?? "Governance Window", auditEvent.GovernanceWindowMode ?? "Matched", changedDecision ? "trace-overridden" : "trace-continued");
        AppendFlowValue(html, "Window result", DisplayDecision(auditEvent.Decision), changedDecision ? "trace-overridden" : "trace-passed");
        html.AppendLine("                </div>");
        html.AppendLine("            </section>");
    }

    private static void AppendApproval(StringBuilder html, AuditEvent auditEvent)
    {
        var changed = !SameDecision(auditEvent.PolicyDecision, auditEvent.Decision) &&
            !HasWindow(auditEvent);
        html.AppendLine("            <section class=\"panel decision-trace-section trace-approval\">");
        html.AppendLine("                <h2>Human Approval</h2><dl class=\"trace-context-grid\">");
        AppendMetadata(html, "Approval ID", auditEvent.ApprovalId ?? string.Empty);
        AppendMetadata(html, "Action", auditEvent.ApprovalAction ?? string.Empty);
        AppendMetadata(html, "Resolution status", auditEvent.ApprovalStatus ?? string.Empty);
        AppendMetadata(html, "Request reason", auditEvent.ApprovalRequestReason ?? string.Empty);
        AppendMetadata(html, "Resolved at", auditEvent.ApprovalResolvedAt?.ToString("u") ?? string.Empty);
        AppendMetadata(html, "Resolved by", auditEvent.ApprovalResolvedBy ?? string.Empty);
        AppendMetadata(html, "Effect", changed
            ? $"Changed Pending Approval to {DisplayDecision(auditEvent.Decision)}"
            : "Approval remains pending");
        html.AppendLine("                </dl></section>");
    }

    private static void AppendDecisionResolution(StringBuilder html, AuditEvent auditEvent)
    {
        var effective = GetEffectiveResult(auditEvent);
        var changedDecision = HasWindow(auditEvent) &&
            !SameDecision(auditEvent.PolicyDecision, auditEvent.Decision);
        var windowText = !HasWindow(auditEvent)
            ? "No window override"
            : changedDecision
                ? $"{auditEvent.GovernanceWindowName} changed result to {DisplayDecision(auditEvent.Decision)}"
                : $"{auditEvent.GovernanceWindowName} matched; no decision change";

        html.AppendLine("            <section class=\"panel decision-trace-section\">");
        html.Append("                <span class=\"trace-section-number\">")
            .Append(HasWindow(auditEvent) ? "4" : "3")
            .AppendLine("</span><h2>Decision Resolution</h2>");
        html.AppendLine("                <ol class=\"resolution-flow\">");
        AppendResolutionStep(html, "Policy Decision", DisplayDecision(auditEvent.PolicyDecision), "trace-passed");
        if (HasApproval(auditEvent))
        {
            AppendResolutionStep(html, "Human Approval",
                $"{auditEvent.ApprovalStatus}: {DisplayDecision(auditEvent.Decision)}",
                auditEvent.ApprovalStatus == "Pending" ? "trace-continued" : "trace-overridden");
        }
        AppendResolutionStep(html, "Governance Window", windowText, changedDecision ? "trace-overridden" : "trace-continued");
        AppendResolutionStep(html, "Runtime Governance", auditEvent.EnforcementMode, "trace-mode");
        AppendResolutionStep(html, "Effective application result", effective.Text, effective.CssClass);
        html.AppendLine("                </ol>");
        html.AppendLine("            </section>");
    }

    private static void AppendFinalOutcome(StringBuilder html, AuditEvent auditEvent)
    {
        var effective = GetEffectiveResult(auditEvent);
        html.Append("            <section class=\"panel final-outcome ")
            .Append(effective.CssClass)
            .AppendLine("\">");
        html.Append("                <span class=\"trace-section-number\">")
            .Append(HasWindow(auditEvent) ? "5" : "4")
            .AppendLine("</span><h2>Final Outcome</h2>");
        html.Append("                <div class=\"final-outcome-primary\"><strong>")
            .Append(Encode(DisplayDecision(auditEvent.Decision)))
            .Append("</strong><span>")
            .Append(Encode(effective.Text))
            .AppendLine("</span></div>");
        html.AppendLine("                <dl class=\"trace-context-grid\">");
        AppendMetadata(html, "Final reason", auditEvent.Reason);
        AppendMetadata(html, "Runtime mode", auditEvent.EnforcementMode);
        AppendMetadata(html, "Evaluation latency", $"{auditEvent.EvaluationDurationMs} ms");
        if (auditEvent.Obligations.Count > 0)
        {
            AppendMetadata(html, "Obligations", FormatList(auditEvent.Obligations));
        }
        html.AppendLine("                </dl>");
        html.AppendLine("            </section>");
    }

    private static void AppendRawFields(StringBuilder html, AuditEvent auditEvent)
    {
        html.AppendLine("            <details class=\"panel raw-trace-fields\">");
        html.AppendLine("                <summary>Raw Fields</summary>");
        html.AppendLine("                <dl class=\"metadata-grid\">");
        AppendMetadata(html, "Id", auditEvent.Id);
        AppendMetadata(html, "RequestId", auditEvent.RequestId);
        AppendMetadata(html, "TimestampUtc", auditEvent.TimestampUtc.ToString("u"));
        AppendMetadata(html, "IdentityId", auditEvent.IdentityId);
        AppendMetadata(html, "CapabilityId", auditEvent.CapabilityId);
        AppendMetadata(html, "ResourceId", auditEvent.ResourceId);
        AppendMetadata(html, "Environment", auditEvent.Environment);
        AppendMetadata(html, "PolicyDecision", auditEvent.PolicyDecision);
        AppendMetadata(html, "PolicyReason", auditEvent.PolicyReason);
        AppendMetadata(html, "Decision", auditEvent.Decision);
        AppendMetadata(html, "EnforcementMode", auditEvent.EnforcementMode);
        AppendMetadata(html, "MatchedPolicies", FormatList(auditEvent.MatchedPolicies));
        AppendMetadata(html, "Obligations", FormatList(auditEvent.Obligations));
        AppendMetadata(html, "Reason", auditEvent.Reason);
        AppendMetadata(html, "GovernanceWindowName", auditEvent.GovernanceWindowName ?? string.Empty);
        AppendMetadata(html, "GovernanceWindowMode", auditEvent.GovernanceWindowMode ?? string.Empty);
        AppendMetadata(html, "GovernanceWindowMessage", auditEvent.GovernanceWindowMessage ?? string.Empty);
        AppendMetadata(html, "GovernanceWindowReason", auditEvent.GovernanceWindowReason ?? string.Empty);
        AppendMetadata(html, "ApprovalId", auditEvent.ApprovalId ?? string.Empty);
        AppendMetadata(html, "ApprovalStatus", auditEvent.ApprovalStatus ?? string.Empty);
        AppendMetadata(html, "ApprovalAction", auditEvent.ApprovalAction ?? string.Empty);
        AppendMetadata(html, "ApprovalResolvedBy", auditEvent.ApprovalResolvedBy ?? string.Empty);
        AppendMetadata(html, "EvaluationDurationMs", auditEvent.EvaluationDurationMs.ToString());
        html.AppendLine("                </dl>");
        html.AppendLine("            </details>");
    }

    private static void AppendCondition(StringBuilder html, AuditConditionEvaluation condition)
    {
        var actual = condition.Actual == "<null>" ? "missing" : condition.Actual;
        html.Append("                        <div class=\"condition-result ")
            .Append(condition.Passed ? "condition-pass" : "condition-fail")
            .Append("\"><span class=\"condition-mark\" aria-label=\"")
            .Append(condition.Passed ? "Passed" : "Failed")
            .Append("\">").Append(condition.Passed ? "&#10003;" : "&#10007;")
            .Append("</span><code>").Append(Encode(condition.Condition))
            .Append(" == ").Append(Encode(condition.Expected))
            .Append("</code><dl><dt>Expected</dt><dd>").Append(Encode(condition.Expected))
            .Append("</dd><dt>Actual</dt><dd>").Append(Encode(actual))
            .Append("</dd><dt>Result</dt><dd>").Append(condition.Passed ? "Pass" : "Fail")
            .AppendLine("</dd></dl></div>");
    }

    private static void AppendPolicyOutcome(StringBuilder html, AuditPolicyEvaluation policy)
    {
        var failed = policy.Conditions.FirstOrDefault(condition => !condition.Passed);
        var outcome = policy.Matched ? "matched" : failed is null ? "did not match" :
            failed.Actual == "<null>" ? $"{failed.Condition} missing" : $"{failed.Condition} mismatch";
        html.Append("                    <li class=\"")
            .Append(policy.Matched ? "policy-matched" : "policy-unmatched")
            .Append("\"><span aria-label=\"").Append(policy.Matched ? "Matched" : "Not matched")
            .Append("\">").Append(policy.Matched ? "&#10003;" : "&#10007;")
            .Append("</span><strong>").Append(Encode(policy.PolicyName))
            .Append("</strong><span>(").Append(Encode(outcome)).AppendLine(")</span></li>");
    }

    private static void AppendResolutionStep(StringBuilder html, string label, string value, string cssClass)
    {
        html.Append("                    <li class=\"").Append(cssClass)
            .Append("\"><span>").Append(Encode(label)).Append("</span><strong>")
            .Append(Encode(value)).AppendLine("</strong></li>");
    }

    private static void AppendFlowValue(StringBuilder html, string label, string value, string cssClass)
    {
        html.Append("                    <div class=\"").Append(cssClass)
            .Append("\"><span>").Append(Encode(label)).Append("</span><strong>")
            .Append(Encode(value)).AppendLine("</strong></div>");
    }

    private static (string Text, string CssClass) GetEffectiveResult(AuditEvent auditEvent)
    {
        if (SameDecision(auditEvent.Decision, "allow"))
        {
            return ("Executed", "trace-passed");
        }
        if (auditEvent.EnforcementMode.Equals("LogOnly", StringComparison.OrdinalIgnoreCase))
        {
            return ("Recorded; operation continues", "trace-continued");
        }
        if (IsPending(auditEvent.Decision))
        {
            return ("Blocked pending approval", "trace-blocked");
        }
        return ("Blocked", "trace-blocked");
    }

    private static bool HasWindow(AuditEvent auditEvent) =>
        !string.IsNullOrWhiteSpace(auditEvent.GovernanceWindowName);

    private static bool HasApproval(AuditEvent auditEvent) =>
        !string.IsNullOrWhiteSpace(auditEvent.ApprovalId);

    private static bool IsPending(string decision) =>
        decision.Equals("requires_approval", StringComparison.OrdinalIgnoreCase) ||
        decision.Equals("pendingapproval", StringComparison.OrdinalIgnoreCase);

    private static bool SameDecision(string left, string right) =>
        left.Equals(right, StringComparison.OrdinalIgnoreCase);

    private static string DisplayDecision(string decision) => decision.ToLowerInvariant() switch
    {
        "allow" => "Allow",
        "deny" => "Deny",
        "requires_approval" or "pendingapproval" => "Pending Approval",
        _ => decision
    };

    private static void AppendShellStart(StringBuilder html, string title)
    {
        html.AppendLine("<!DOCTYPE html><html lang=\"en\"><head>");
        html.AppendLine("    <meta charset=\"utf-8\" /><meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        html.Append("    <title>").Append(Encode(title)).AppendLine("</title><link rel=\"stylesheet\" href=\"/styles.css\" />");
        html.AppendLine("</head><body><div class=\"app-shell\">");
        html.Append(PortalSidebarRenderer.Render("audit"));
        html.AppendLine("        <main class=\"container explorer-page decision-trace-page\">");
    }

    private static void AppendShellEnd(StringBuilder html)
    {
        html.AppendLine("            <footer class=\"app-footer\">Seneschal v0.2.1-alpha</footer>");
        html.AppendLine("        </main></div></body></html>");
    }

    private static void AppendMetadata(StringBuilder html, string label, string value)
    {
        html.Append("                    <dt>").Append(Encode(label)).Append("</dt><dd>")
            .Append(Encode(string.IsNullOrWhiteSpace(value) ? "none" : value)).AppendLine("</dd>");
    }

    private static string FormatList(IReadOnlyCollection<string> values) =>
        values.Count == 0 ? "none" : string.Join(", ", values);

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
