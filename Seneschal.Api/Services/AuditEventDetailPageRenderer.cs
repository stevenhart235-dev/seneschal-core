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
        html.AppendLine("                <nav class=\"breadcrumb\" aria-label=\"Breadcrumb\"><a href=\"/audit\">Audit Trail</a> / Decision Trace</nav>");
        html.AppendLine("                <h1>Decision Trace</h1>");
        html.AppendLine("                <p class=\"subtitle\">Audit Event Detail — why Seneschal returned this decision and what the caller should do next.</p>");
        html.AppendLine("            </header>");

        AppendOutcomeHeader(html, auditEvent);
        AppendPlainEnglishExplanation(html, auditEvent);
        AppendTraceNavigation(html, auditEvent);
        AppendRequestContext(html, auditEvent);
        AppendPolicyEvaluation(html, auditEvent);
        AppendDecisionResolution(html, auditEvent);
        if (HasApproval(auditEvent))
        {
            AppendApproval(html, auditEvent);
        }
        if (HasWindow(auditEvent))
        {
            AppendGovernanceWindow(html, auditEvent);
        }
        else
        {
            AppendNoGovernanceWindow(html);
        }
        AppendExecutionGuidance(html, auditEvent);
        AppendTraceSequence(html, auditEvent);
        AppendRawFields(html, auditEvent);
        AppendShellEnd(html);
        return html.ToString();
    }

    private static void AppendOutcomeHeader(StringBuilder html, AuditEvent auditEvent)
    {
        var effective = GetEffectiveResult(auditEvent);
        var decision = DisplayDecision(auditEvent.Decision);
        html.Append("            <section class=\"trace-outcome-hero ")
            .Append(effective.CssClass).Append(" decision-")
            .Append(Encode(decision.Replace(" ", "-").ToLowerInvariant()))
            .AppendLine("\" aria-labelledby=\"trace-outcome-title\">");
        html.AppendLine("                <div class=\"trace-outcome-primary\"><span>Final Outcome</span>");
        html.Append("                    <h2 id=\"trace-outcome-title\">")
            .Append(Encode(OutcomeHeadline(auditEvent))).AppendLine("</h2>");
        html.Append("                    <p>").Append(Encode(auditEvent.Reason)).AppendLine("</p></div>");
        html.AppendLine("                <dl class=\"trace-outcome-facts\">");
        AppendMetadata(html, "Final decision", decision);
        AppendMetadata(html, "Effective action", effective.Text);
        AppendMetadata(html, "Runtime mode", auditEvent.EnforcementMode);
        AppendMetadata(html, "Execution guidance", GetExecutionGuidance(auditEvent));
        AppendMetadata(html, "Identity", auditEvent.IdentityId);
        AppendMetadata(html, "Capability", auditEvent.CapabilityId);
        AppendMetadata(html, "Resource", auditEvent.ResourceId);
        AppendMetadata(html, "Operation ID", auditEvent.ApprovalOperationId ?? "Not provided");
        html.AppendLine("                </dl></section>");
    }

    private static void AppendPlainEnglishExplanation(StringBuilder html, AuditEvent auditEvent)
    {
        html.AppendLine("            <section class=\"trace-explanation\" aria-labelledby=\"trace-explanation-title\">");
        html.AppendLine("                <span id=\"trace-explanation-title\">Why this happened</span>");
        html.Append("                <p>").Append(Encode(BuildExplanation(auditEvent))).AppendLine("</p></section>");
    }

    private static void AppendTraceNavigation(StringBuilder html, AuditEvent auditEvent)
    {
        html.AppendLine("            <nav class=\"trace-navigation\" aria-label=\"Related investigation links\">");
        AppendNavLink(html, BuildCapabilityActivityLink(auditEvent), "Investigate Capability Activity");
        AppendNavLink(html, $"/capability-explorer?capabilityId={Uri.EscapeDataString(auditEvent.CapabilityId)}", "View capability profile");
        AppendNavLink(html, $"/identity-activity?identityId={Uri.EscapeDataString(auditEvent.IdentityId)}", "View Identity Activity");
        AppendNavLink(html, BuildFilteredAuditLink(auditEvent), "Open Filtered Audit Trail");
        if (HasApproval(auditEvent)) AppendNavLink(html, "/approvals", "View related approval");
        var policy = auditEvent.MatchedPolicies.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(policy)) AppendNavLink(html, $"/policies?policyId={Uri.EscapeDataString(policy)}", "View related policy");
        html.AppendLine("            </nav>");
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
        AppendLinkedMetadata(html, "Identity", auditEvent.IdentityId,
            $"/identity-activity?identityId={Uri.EscapeDataString(auditEvent.IdentityId)}");
        AppendLinkedMetadata(html, "Capability", auditEvent.CapabilityId,
            $"/capability-explorer?capabilityId={Uri.EscapeDataString(auditEvent.CapabilityId)}");
        AppendMetadata(html, "Environment", auditEvent.Environment);
        AppendMetadata(html, "Resource", auditEvent.ResourceId);
        AppendMetadata(html, "Operation ID", auditEvent.ApprovalOperationId ?? "Not provided");
        AppendMetadata(html, "Correlation mode", auditEvent.ApprovalCorrelationMode ?? "Legacy or not applicable");
        AppendMetadata(html, "Caller / API key context", "Not recorded in this audit event");
        AppendMetadata(html, "Timestamp", auditEvent.TimestampUtc.ToString("u"));
        AppendMetadata(html, "Evaluation latency", $"{auditEvent.EvaluationDurationMs} ms");
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
        html.Append("                <p class=\"policy-resolution-summary\">")
            .Append(Encode(BuildPolicyResolution(auditEvent))).AppendLine("</p>");

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
            AppendPolicyOutcome(html, policy, winningPolicyId);
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
                AppendPolicyOutcome(html, policy, winningPolicyId);
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
        var resultBeforeWindow = ResultBeforeWindow(auditEvent);
        var changedDecision = !SameDecision(resultBeforeWindow, auditEvent.Decision);
        var effect = changedDecision
            ? $"Changed {DisplayDecision(resultBeforeWindow)} to {DisplayDecision(auditEvent.Decision)}"
            : "Matched; policy result unchanged";

        html.AppendLine("            <section class=\"panel decision-trace-section trace-window\">");
        html.AppendLine("                <span class=\"trace-section-number\">3</span><h2>Governance Window</h2>");
        html.AppendLine("                <dl class=\"trace-context-grid\">");
        AppendMetadata(html, "Window name", auditEvent.GovernanceWindowName ?? string.Empty);
        AppendMetadata(html, "Window mode", auditEvent.GovernanceWindowMode ?? string.Empty);
        AppendMetadata(html, "Status", "Participated");
        AppendMetadata(html, "Matched", "Yes");
        AppendMetadata(html, "Scope", $"Capability {auditEvent.CapabilityId} in {Fallback(auditEvent.Environment, "an unspecified environment")}");
        AppendMetadata(html, "Effect on policy result", effect);
        AppendMetadata(html, "Window reason", auditEvent.GovernanceWindowReason ?? auditEvent.GovernanceWindowMessage ?? string.Empty);
        html.AppendLine("                </dl>");
        html.AppendLine("                <div class=\"window-result-flow\">");
        AppendFlowValue(html, "Policy result before window", DisplayDecision(resultBeforeWindow), "trace-passed");
        AppendFlowValue(html, auditEvent.GovernanceWindowName ?? "Governance Window", auditEvent.GovernanceWindowMode ?? "Matched", changedDecision ? "trace-overridden" : "trace-continued");
        AppendFlowValue(html, "Window result", DisplayDecision(auditEvent.Decision), changedDecision ? "trace-overridden" : "trace-passed");
        html.AppendLine("                </div>");
        html.AppendLine("            </section>");
    }

    private static void AppendNoGovernanceWindow(StringBuilder html)
    {
        html.AppendLine("            <aside class=\"trace-compact-state\"><strong>No Governance Window participated</strong><span>The decision was resolved without Governance Window context or override.</span></aside>");
    }

    private static void AppendApproval(StringBuilder html, AuditEvent auditEvent)
    {
        var effect = auditEvent.ApprovalStatus switch
        {
            "Consumed" => "Changed Pending Approval to Allow",
            "Rejected" => "Changed Pending Approval to Deny",
            "Approved" => "Approval resolved; awaiting a matching retry",
            _ => "Approval remains pending"
        };
        html.AppendLine("            <section class=\"panel decision-trace-section trace-approval\">");
        html.AppendLine("                <h2>Human Approval</h2><dl class=\"trace-context-grid\">");
        AppendMetadata(html, "Approval ID", auditEvent.ApprovalId ?? string.Empty);
        AppendMetadata(html, "Application operation", auditEvent.ApprovalOperationId ?? "Not provided");
        AppendMetadata(html, "Approval scope",
            auditEvent.ApprovalCorrelationMode == "Operation" ? "Exact operation" : "Legacy context matching");
        AppendMetadata(html, "Action", auditEvent.ApprovalAction ?? string.Empty);
        AppendMetadata(html, "Approval status", auditEvent.ApprovalStatus ?? "Not recorded");
        AppendMetadata(html, "Approval usage",
            auditEvent.ApprovalStatus == "Consumed"
                ? auditEvent.ApprovalCorrelationMode == "Operation"
                    ? "Consumed by this operation"
                    : "Consumed by this legacy-context evaluation"
                : auditEvent.ApprovalAction ?? string.Empty);
        AppendMetadata(html, "Request reason", auditEvent.ApprovalRequestReason ?? string.Empty);
        AppendMetadata(html, "Requested at", auditEvent.ApprovalAction is "Requested" or "Reused"
            ? auditEvent.TimestampUtc.ToString("u") : "Not recorded on this audit event");
        AppendMetadata(html, "Resolved at", auditEvent.ApprovalResolvedAt?.ToString("u") ?? string.Empty);
        AppendMetadata(html, "Resolved by", auditEvent.ApprovalResolvedBy ?? string.Empty);
        AppendMetadata(html, "Resolution reason", auditEvent.ApprovalStatus == "Rejected"
            ? auditEvent.Reason : "No separate resolution reason recorded");
        AppendMetadata(html, "Consumed at", auditEvent.ApprovalConsumedAt?.ToString("u") ?? string.Empty);
        AppendMetadata(html, "Consuming decision ID", auditEvent.ApprovalConsumedByDecisionId ?? string.Empty);
        AppendMetadata(html, "Effect", effect);
        html.AppendLine("                </dl></section>");
    }

    private static void AppendDecisionResolution(StringBuilder html, AuditEvent auditEvent)
    {
        var effective = GetEffectiveResult(auditEvent);
        var resultBeforeWindow = ResultBeforeWindow(auditEvent);
        var changedDecision = HasWindow(auditEvent) &&
            !SameDecision(resultBeforeWindow, auditEvent.Decision);
        var windowText = !HasWindow(auditEvent)
            ? "No window override"
            : changedDecision
                ? $"{auditEvent.GovernanceWindowName} changed result to {DisplayDecision(auditEvent.Decision)} (from {DisplayDecision(resultBeforeWindow)})"
                : $"{auditEvent.GovernanceWindowName} matched; no decision change";

        html.AppendLine("            <section class=\"panel decision-trace-section\">");
        html.Append("                <span class=\"trace-section-number\">")
            .Append(HasWindow(auditEvent) ? "4" : "3")
            .AppendLine("</span><h2>Decision Resolution</h2>");
        html.AppendLine("                <ol class=\"resolution-flow\">");
        html.Append("                    <li class=\"resolution-summary-step\"><span>Resolution summary</span><strong>")
            .Append(Encode(BuildPolicyResolution(auditEvent))).AppendLine("</strong></li>");
        AppendResolutionStep(html, "Policy Decision", DisplayDecision(auditEvent.PolicyDecision), "trace-passed");
        if (HasApproval(auditEvent))
        {
            AppendResolutionStep(html, "Human Approval",
                auditEvent.ApprovalStatus == "Consumed" ? "Approved" : auditEvent.ApprovalStatus ?? "Pending",
                auditEvent.ApprovalStatus == "Pending" ? "trace-continued" : "trace-overridden");
            if (auditEvent.ApprovalStatus == "Consumed")
            {
                AppendResolutionStep(html, "Approval usage",
                    auditEvent.ApprovalCorrelationMode == "Operation"
                        ? "Consumed by this operation"
                        : "Consumed by this legacy-context evaluation",
                    "trace-overridden");
            }
        }
        AppendResolutionStep(html, "Governance Window", windowText, changedDecision ? "trace-overridden" : "trace-continued");
        AppendResolutionStep(html, "Runtime Governance", auditEvent.EnforcementMode, "trace-mode");
        AppendResolutionStep(html, "Execution guidance", GetExecutionGuidance(auditEvent), "trace-mode");
        AppendResolutionStep(html, "Effective action", effective.Text, effective.CssClass);
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
        AppendMetadata(html, "Execution guidance", GetExecutionGuidance(auditEvent));
        if (!string.IsNullOrWhiteSpace(auditEvent.CallerMessage))
            AppendMetadata(html, "Caller message", auditEvent.CallerMessage);
        if (!string.IsNullOrWhiteSpace(auditEvent.RetryGuidance))
            AppendMetadata(html, "Retry guidance", auditEvent.RetryGuidance);
        AppendMetadata(html, "Evaluation latency", $"{auditEvent.EvaluationDurationMs} ms");
        if (auditEvent.Obligations.Count > 0)
        {
            AppendMetadata(html, "Obligations", FormatList(auditEvent.Obligations));
        }
        html.AppendLine("                </dl>");
        html.AppendLine("            </section>");
    }

    private static void AppendExecutionGuidance(StringBuilder html, AuditEvent auditEvent)
    {
        var guidance = GetExecutionGuidance(auditEvent);
        html.AppendLine("            <section class=\"trace-guidance\" aria-labelledby=\"execution-guidance-title\">");
        html.AppendLine("                <div><span>Caller action</span><h2 id=\"execution-guidance-title\">Execution Guidance</h2></div>");
        html.Append("                <strong>").Append(Encode(guidance)).AppendLine("</strong>");
        html.Append("                <p>").Append(Encode(GuidanceText(guidance, auditEvent))).AppendLine("</p>");
        html.Append("                <small>Advisory guidance for the integrated caller. Seneschal does not execute, pause, queue, or retry the external operation. Runtime mode: ")
            .Append(Encode(auditEvent.EnforcementMode)).AppendLine(".</small></section>");
    }

    private static void AppendTraceSequence(StringBuilder html, AuditEvent auditEvent)
    {
        html.AppendLine("            <section class=\"panel trace-sequence\" aria-labelledby=\"trace-sequence-title\">");
        html.AppendLine("                <h2 id=\"trace-sequence-title\">Evaluation Sequence</h2><ol>");
        AppendSequenceItem(html, "Request received", auditEvent.TimestampUtc.ToString("u"));
        AppendSequenceItem(html, "Policy evaluation", auditEvent.PolicyEvaluations.Count > 0
            ? $"{auditEvent.PolicyEvaluations.Count} policy result(s) recorded" : "Condition-level evidence unavailable");
        if (HasWindow(auditEvent)) AppendSequenceItem(html, "Governance Window evaluation",
            $"{auditEvent.GovernanceWindowName} · {auditEvent.GovernanceWindowMode}");
        if (HasApproval(auditEvent)) AppendSequenceItem(html,
            $"Approval {Fallback(auditEvent.ApprovalAction, "evaluated").ToLowerInvariant()}",
            auditEvent.ApprovalResolvedAt?.ToString("u") ?? auditEvent.ApprovalConsumedAt?.ToString("u") ?? "Recorded during this evaluation");
        AppendSequenceItem(html, "Decision resolved", DisplayDecision(auditEvent.Decision));
        AppendSequenceItem(html, "Audit recorded", $"Decision ID {auditEvent.Id}");
        AppendSequenceItem(html, "Caller guidance returned", GetExecutionGuidance(auditEvent));
        html.AppendLine("                </ol></section>");
    }

    private static void AppendRawFields(StringBuilder html, AuditEvent auditEvent)
    {
        html.AppendLine("            <section class=\"trace-raw-disclosures\" aria-label=\"Raw and diagnostic details\">");
        html.AppendLine("            <details class=\"panel raw-trace-fields\"><summary>Raw decision payload</summary><p class=\"muted\">The original request payload is not retained. Request identifiers and recorded context are available in the raw audit record below.</p></details>");
        html.AppendLine("            <details class=\"panel raw-trace-fields\"><summary>Matched policy identifiers</summary>");
        html.Append("                <p>").Append(Encode(FormatList(auditEvent.MatchedPolicies))).AppendLine("</p></details>");
        html.AppendLine("            <details class=\"panel raw-trace-fields\"><summary>Diagnostic metadata</summary><dl class=\"metadata-grid\">");
        AppendMetadata(html, "Decision ID", auditEvent.Id);
        AppendMetadata(html, "Request ID", auditEvent.RequestId);
        AppendMetadata(html, "Evaluation duration", $"{auditEvent.EvaluationDurationMs} ms");
        html.AppendLine("                </dl></details>");
        html.AppendLine("            <details class=\"panel raw-trace-fields\">");
        html.AppendLine("                <summary>Raw Fields / Raw audit record</summary>");
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
        AppendMetadata(html, "ApprovalConsumedAt", auditEvent.ApprovalConsumedAt?.ToString("u") ?? string.Empty);
        AppendMetadata(html, "ApprovalConsumedByDecisionId", auditEvent.ApprovalConsumedByDecisionId ?? string.Empty);
        AppendMetadata(html, "ApprovalOperationId", auditEvent.ApprovalOperationId ?? string.Empty);
        AppendMetadata(html, "ApprovalCorrelationMode", auditEvent.ApprovalCorrelationMode ?? string.Empty);
        AppendMetadata(html, "ExecutionGuidance", auditEvent.ExecutionGuidance);
        AppendMetadata(html, "CallerMessage", auditEvent.CallerMessage ?? string.Empty);
        AppendMetadata(html, "RetryGuidance", auditEvent.RetryGuidance ?? string.Empty);
        AppendMetadata(html, "EvaluationDurationMs", auditEvent.EvaluationDurationMs.ToString());
        html.AppendLine("                </dl>");
        html.AppendLine("            </details>");
        html.AppendLine("            </section>");
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

    private static void AppendPolicyOutcome(StringBuilder html,
        AuditPolicyEvaluation policy, string? winningPolicyId)
    {
        var failed = policy.Conditions.FirstOrDefault(condition => !condition.Passed);
        var outcome = policy.Matched ? "matched" : failed is null ? "did not match" :
            failed.Actual == "<null>" ? $"{failed.Condition} missing" : $"{failed.Condition} mismatch";
        html.Append("                    <li class=\"")
            .Append(policy.Matched ? "policy-matched" : "policy-unmatched")
            .Append("\"><span aria-label=\"").Append(policy.Matched ? "Matched" : "Not matched")
            .Append("\">").Append(policy.Matched ? "&#10003;" : "&#10007;")
            .Append("</span><strong>").Append(Encode(policy.PolicyName))
            .Append("</strong><span>(").Append(Encode(outcome)).Append(")</span><dl>")
            .Append("<dt>Match result</dt><dd>").Append(policy.Matched ? "Matched" : "Not matched")
            .Append("</dd><dt>Effect</dt><dd>Not recorded per policy</dd>")
            .Append("<dt>Priority</dt><dd>Not recorded</dd><dt>Contribution</dt><dd>")
            .Append(Encode(string.Equals(policy.PolicyId, winningPolicyId,
                    StringComparison.OrdinalIgnoreCase)
                ? "Recorded winning policy"
                : policy.Matched
                    ? "Matched; exact precedence unavailable"
                    : "Did not contribute to the outcome"))
            .AppendLine("</dd></dl></li>");
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

    private static void AppendSequenceItem(StringBuilder html, string label, string evidence)
    {
        html.Append("                    <li><span>").Append(Encode(label))
            .Append("</span><strong>").Append(Encode(evidence)).AppendLine("</strong></li>");
    }

    private static string BuildCapabilityActivityLink(AuditEvent auditEvent)
    {
        var parameters = new List<string>();
        Add("capabilityId", auditEvent.CapabilityId);
        Add("identity", auditEvent.IdentityId);
        Add("environment", auditEvent.Environment);
        Add("operationId", auditEvent.ApprovalOperationId);
        Add("runtimeMode", auditEvent.EnforcementMode);
        return $"/capability-activity?{string.Join("&", parameters)}";

        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                parameters.Add($"{name}={Uri.EscapeDataString(value)}");
        }
    }

    private static string BuildFilteredAuditLink(AuditEvent auditEvent)
    {
        var parameters = new List<string>();
        Add("capabilityId", auditEvent.CapabilityId);
        Add("identityId", auditEvent.IdentityId);
        Add("environment", auditEvent.Environment);
        Add("enforcementMode", auditEvent.EnforcementMode);
        Add("matchedPolicy", auditEvent.MatchedPolicies.FirstOrDefault());
        return $"/audit?{string.Join("&", parameters)}";

        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                parameters.Add($"{name}={Uri.EscapeDataString(value)}");
        }
    }

    private static string OutcomeHeadline(AuditEvent auditEvent)
    {
        if (SameDecision(auditEvent.Decision, "allow"))
        {
            return auditEvent.ApprovalStatus == "Consumed"
                ? "Approved for this operation; approval consumed"
                : "Allowed; caller may proceed";
        }
        if (IsPending(auditEvent.Decision))
        {
            return auditEvent.EnforcementMode.Equals("LogOnly", StringComparison.OrdinalIgnoreCase)
                ? "Approval required, recorded, and allowed to continue"
                : "Approval required. Caller should pause and retry";
        }
        return auditEvent.EnforcementMode.Equals("LogOnly", StringComparison.OrdinalIgnoreCase)
            ? "Denied, recorded, and allowed to continue"
            : "Denied and blocked";
    }

    private static string BuildExplanation(AuditEvent auditEvent)
    {
        var decision = DisplayDecision(auditEvent.Decision);
        var winningPolicy = auditEvent.PolicyEvaluations.FirstOrDefault(item => item.Matched)
            ?.PolicyName ?? auditEvent.MatchedPolicies.FirstOrDefault();
        var basis = string.IsNullOrWhiteSpace(winningPolicy)
            ? SameDecision(auditEvent.Decision, "deny")
                ? "No matching policy identifier was recorded, so the audit evidence reflects the default Deny outcome."
                : $"No matching policy identifier was recorded for the {decision} outcome."
            : $"This request returned {decision} after {winningPolicy} matched. Recorded reason: {Fallback(auditEvent.PolicyReason, auditEvent.Reason)}.";

        if (IsPending(auditEvent.Decision))
        {
            basis += auditEvent.EnforcementMode.Equals("LogOnly", StringComparison.OrdinalIgnoreCase)
                ? " Human approval is required, but LogOnly records the result and allows the caller to continue."
                : $" The caller should pause and retry{OperationRetryText(auditEvent)} after the approval is resolved.";
        }
        else if (SameDecision(auditEvent.Decision, "deny"))
        {
            basis += auditEvent.EnforcementMode.Equals("LogOnly", StringComparison.OrdinalIgnoreCase)
                ? " Seneschal was operating in LogOnly mode, so the denial was recorded and the caller was allowed to continue."
                : " Runtime enforcement was active, so the caller was instructed not to execute the operation.";
        }
        else
        {
            basis += " The caller was instructed to proceed.";
        }

        if (HasWindow(auditEvent))
            basis += $" Governance Window {auditEvent.GovernanceWindowName} participated in {auditEvent.GovernanceWindowMode} mode.";
        return basis;
    }

    private static string BuildPolicyResolution(AuditEvent auditEvent)
    {
        var matched = auditEvent.PolicyEvaluations.Count(item => item.Matched);
        if (matched == 0 && auditEvent.MatchedPolicies.Count == 0 &&
            SameDecision(auditEvent.PolicyDecision, "deny"))
            return "No configured policy match was recorded, so the default Deny result was used.";
        if (IsPending(auditEvent.PolicyDecision))
            return "The recorded matching policy required human approval.";
        if (matched > 0)
            return $"{matched} evaluated {(matched == 1 ? "policy matched" : "policies matched")}; the recorded policy result was {DisplayDecision(auditEvent.PolicyDecision)}. Exact policy priority or precedence was not recorded.";
        if (auditEvent.MatchedPolicies.Count > 0)
            return $"The recorded matched policy produced {DisplayDecision(auditEvent.PolicyDecision)}. Condition-level precedence data is unavailable.";
        return $"The audit record contains a {DisplayDecision(auditEvent.PolicyDecision)} policy result, but policy match details are unavailable.";
    }

    private static string GuidanceText(string guidance, AuditEvent auditEvent) =>
        guidance.ToLowerInvariant() switch
        {
            "proceed" => "Continue with the requested operation.",
            "block" => "Do not execute the requested operation.",
            "pause" => $"Pause this operation and retry{OperationRetryText(auditEvent)} after approval is resolved.",
            "continuelogonly" => "The decision was recorded, but Seneschal is not enforcing it.",
            "retry" => "Retry the same operation after the blocking condition changes.",
            "queue" => "Queue the operation for later processing.",
            _ => "Follow the recorded execution guidance before continuing."
        };

    private static string OperationRetryText(AuditEvent auditEvent) =>
        string.IsNullOrWhiteSpace(auditEvent.ApprovalOperationId)
            ? " with the same request context (no Operation ID was recorded)"
            : $" using the same Operation ID ({auditEvent.ApprovalOperationId})";

    private static string Fallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static (string Text, string CssClass) GetEffectiveResult(AuditEvent auditEvent)
    {
        if (SameDecision(auditEvent.Decision, "allow"))
        {
            return ("Caller may proceed", "trace-passed");
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

    private static string ResultBeforeWindow(AuditEvent auditEvent) =>
        auditEvent.ApprovalStatus switch
        {
            "Consumed" => "allow",
            "Rejected" => "deny",
            _ => auditEvent.PolicyDecision
        };

    private static string GetExecutionGuidance(AuditEvent auditEvent)
    {
        if (!string.IsNullOrWhiteSpace(auditEvent.ExecutionGuidance))
            return auditEvent.ExecutionGuidance;
        if (SameDecision(auditEvent.Decision, "allow")) return "Proceed";
        if (auditEvent.EnforcementMode.Equals("LogOnly", StringComparison.OrdinalIgnoreCase))
            return "ContinueLogOnly";
        return IsPending(auditEvent.Decision) ? "Pause" : "Block";
    }

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

    private static void AppendLinkedMetadata(StringBuilder html, string label,
        string value, string href)
    {
        html.Append("                    <dt>").Append(Encode(label))
            .Append("</dt><dd><a href=\"").Append(Encode(href)).Append("\">")
            .Append(Encode(Fallback(value, "none"))).AppendLine("</a></dd>");
    }

    private static void AppendNavLink(StringBuilder html, string href, string label)
    {
        html.Append("                <a href=\"").Append(Encode(href)).Append("\">")
            .Append(Encode(label)).AppendLine("</a>");
    }

    private static string FormatList(IReadOnlyCollection<string> values) =>
        values.Count == 0 ? "none" : string.Join(", ", values);

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
