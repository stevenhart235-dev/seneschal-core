using Seneschal.Api.Models;
using Seneschal.Api.Services;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class AuditDecisionExplanationTests
{
    [Fact]
    public void Render_ApprovalEvidenceIsHumanReadableAndEncoded()
    {
        var auditEvent = CreateEvent("allow", "Enforce", "requires_approval");
        auditEvent.ApprovalId = "approval-1";
        auditEvent.ApprovalStatus = "Consumed";
        auditEvent.ApprovalAction = "Consumed";
        auditEvent.ApprovalRequestReason = "<request>";
        auditEvent.ApprovalResolvedBy = "<reviewer>";
        auditEvent.ApprovalConsumedAt = DateTimeOffset.UtcNow;
        auditEvent.ApprovalConsumedByDecisionId = "decision-1";
        auditEvent.ApprovalOperationId = "<release-001>";
        auditEvent.ApprovalCorrelationMode = "Operation";
        var html = AuditEventDetailPageRenderer.Render(auditEvent);
        Assert.Contains("Human Approval", html);
        Assert.Contains("Changed Pending Approval to Allow", html);
        Assert.Contains("Consumed by this operation", html);
        Assert.Contains("decision-1", html);
        Assert.Contains("Exact operation", html);
        Assert.Contains("&lt;release-001&gt;", html);
        Assert.Contains("&lt;request&gt;", html);
        Assert.Contains("&lt;reviewer&gt;", html);
        Assert.DoesNotContain("<reviewer>", html);
    }

    [Fact]
    public void Render_ShowsEncodedExecutionGuidanceNearOutcome()
    {
        var auditEvent = CreateEvent(
            "requires_approval", "Enforce", "requires_approval");
        auditEvent.ExecutionGuidance = "Pause";
        auditEvent.CallerMessage = "<stop current work>";
        auditEvent.RetryGuidance = "Retry after approval";

        var html = AuditEventDetailPageRenderer.Render(auditEvent);

        Assert.Contains("Execution guidance", html);
        Assert.Contains("Pause", html);
        Assert.Contains("Blocked pending approval", html);
        Assert.Contains("&lt;stop current work&gt;", html);
        Assert.DoesNotContain("<stop current work>", html);
    }
    [Fact]
    public void Render_PlainAllowShowsExecutedFlow()
    {
        var html = RenderEvent("allow", "Enforce", "allow");

        AssertSections(html, hasWindow: false);
        Assert.Contains("Policy Decision", html);
        Assert.Contains("Allow", html);
        Assert.Contains("No window override", html);
        Assert.Contains("Effective action", html);
        Assert.Contains("Caller may proceed", html);
    }

    [Fact]
    public void Render_PlainDenyShowsBlockedFlow()
    {
        var html = RenderEvent("deny", "Enforce", "deny");

        Assert.Contains("Final Outcome", html);
        Assert.Contains("Blocked", html);
        Assert.DoesNotContain("Governance Window</h2>", html);
    }

    [Fact]
    public void Render_PendingApprovalShowsBlockedPendingApproval()
    {
        var html = RenderEvent(
            "requires_approval",
            "Enforce",
            "requires_approval");

        Assert.Contains("Pending Approval", html);
        Assert.Contains("Blocked pending approval", html);
    }

    [Fact]
    public void Render_ObserveWindowShowsMatchWithoutOverride()
    {
        var html = RenderEvent(
            "allow",
            "LogOnly",
            "allow",
            windowMode: "Observe");

        AssertSections(html, hasWindow: true);
        Assert.Contains("Matched; policy result unchanged", html);
        Assert.Contains("Weekend production freeze.", html);
        Assert.Contains("Caller may proceed", html);
    }

    [Fact]
    public void Render_EnforceWindowInLogOnlyShowsRecordedAndContinued()
    {
        var html = RenderEvent(
            "deny",
            "LogOnly",
            "allow",
            windowMode: "Enforce");

        Assert.Contains("Changed Allow to Deny", html);
        Assert.Contains("Production Freeze changed result to Deny", html);
        Assert.Contains("Recorded; operation continues", html);
        Assert.Contains("trace-continued", html);
    }

    [Fact]
    public void Render_EnforceWindowAndRuntimeEnforceShowsBlocked()
    {
        var html = RenderEvent(
            "deny",
            "Enforce",
            "allow",
            windowMode: "Enforce");

        Assert.Contains("Policy result", html);
        Assert.Contains("Window result", html);
        Assert.Contains("Production Freeze changed result to Deny", html);
        Assert.Contains("Blocked", html);
        Assert.Contains("trace-blocked", html);
    }

    [Fact]
    public void Render_MissingOptionalFieldsUsesSafeFallbacks()
    {
        var auditEvent = CreateEvent("allow", "LogOnly", "allow");
        auditEvent.RequestId = "";
        auditEvent.ResourceId = "";
        auditEvent.Obligations = [];
        auditEvent.PolicyEvaluations = [];
        auditEvent.MatchedPolicies = [];

        var html = AuditEventDetailPageRenderer.Render(auditEvent);

        Assert.Contains("Request ID", html);
        Assert.Contains("none", html);
        Assert.Contains("Condition-level evidence was not recorded", html);
        Assert.DoesNotContain("Governance Window</h2>", html);
    }

    [Fact]
    public void Render_HtmlEncodesEveryDisplayedAuditValue()
    {
        const string unsafeValue = "<script>alert(&quot;x&quot;)</script>";
        var auditEvent = CreateEvent("deny", "Enforce", "allow", "Enforce");
        auditEvent.Id = unsafeValue;
        auditEvent.RequestId = unsafeValue;
        auditEvent.IdentityId = unsafeValue;
        auditEvent.CapabilityId = unsafeValue;
        auditEvent.Environment = unsafeValue;
        auditEvent.ResourceId = unsafeValue;
        auditEvent.Reason = unsafeValue;
        auditEvent.PolicyReason = unsafeValue;
        auditEvent.GovernanceWindowName = unsafeValue;
        auditEvent.GovernanceWindowReason = unsafeValue;
        auditEvent.Obligations = [unsafeValue];
        auditEvent.MatchedPolicies = [unsafeValue];
        auditEvent.PolicyEvaluations =
        [
            new AuditPolicyEvaluation
            {
                PolicyId = unsafeValue,
                PolicyName = unsafeValue,
                Matched = false,
                Conditions =
                [
                    new AuditConditionEvaluation
                    {
                        Condition = unsafeValue,
                        Expected = unsafeValue,
                        Actual = unsafeValue,
                        Passed = false
                    }
                ]
            }
        ];

        var html = AuditEventDetailPageRenderer.Render(auditEvent);

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("&amp;quot;", html);
    }

    [Fact]
    public void Render_ShowsRecordedConditionEvidenceAndPolicyOutcomes()
    {
        var auditEvent = CreateEvent("allow", "LogOnly", "allow");
        auditEvent.PolicyEvaluations =
        [
            new AuditPolicyEvaluation
            {
                PolicyId = "policy-a",
                PolicyName = "Policy A",
                Matched = true,
                Conditions = [Condition("identity.owner", "platform", "platform", true)]
            },
            new AuditPolicyEvaluation
            {
                PolicyId = "policy-b",
                PolicyName = "Policy B",
                Matched = false,
                Conditions = [Condition("request.changeTicket", "required", "<null>", false)]
            }
        ];

        var html = AuditEventDetailPageRenderer.Render(auditEvent);

        Assert.Contains("identity.owner == platform", html);
        Assert.Contains("request.changeTicket == required", html);
        Assert.Contains("Expected", html);
        Assert.Contains("Actual", html);
        Assert.Contains("Pass", html);
        Assert.Contains("Fail", html);
        Assert.Contains("Policy A", html);
        Assert.Contains("(matched)", html);
        Assert.Contains("(request.changeTicket missing)", html);
    }

    [Theory]
    [InlineData("deny", "Enforce", "Denied and blocked", "Blocked")]
    [InlineData("deny", "LogOnly", "Denied, recorded, and allowed to continue", "ContinueLogOnly")]
    [InlineData("requires_approval", "Enforce", "Approval required. Caller should pause and retry", "Pause")]
    [InlineData("allow", "LogOnly", "Allowed; caller may proceed", "Proceed")]
    public void Render_OutcomeHeaderExplainsDecisionModeAndGuidance(
        string decision, string mode, string headline, string guidance)
    {
        var auditEvent = CreateEvent(decision, mode, decision);
        auditEvent.ExecutionGuidance = guidance;

        var html = AuditEventDetailPageRenderer.Render(auditEvent);

        Assert.Contains("Final Outcome", html);
        Assert.Contains(headline, html);
        Assert.Contains("Effective action", html);
        Assert.Contains("Runtime mode", html);
        Assert.Contains("Execution Guidance", html);
        Assert.Contains("Seneschal does not execute, pause, queue, or retry", html);
    }

    [Fact]
    public void Render_RequestContextAndRelatedNavigationPreserveInvestigationScope()
    {
        var auditEvent = CreateEvent("requires_approval", "Enforce", "requires_approval");
        auditEvent.ApprovalId = "approval-7";
        auditEvent.ApprovalStatus = "Pending";
        auditEvent.ApprovalAction = "Requested";
        auditEvent.ApprovalOperationId = "release-007";
        auditEvent.ApprovalCorrelationMode = "Operation";

        var html = AuditEventDetailPageRenderer.Render(auditEvent);

        Assert.Contains("Operation ID", html);
        Assert.Contains("release-007", html);
        Assert.Contains("Correlation mode", html);
        Assert.Contains("Caller / API key context", html);
        Assert.Contains("Not recorded in this audit event", html);
        Assert.Contains("/capability-activity?capabilityId=production.deployment.execute", html);
        Assert.Contains("/capability-explorer?capabilityId=production.deployment.execute", html);
        Assert.Contains("/identity-activity?identityId=github-actions-production", html);
        Assert.Contains("/audit?capabilityId=production.deployment.execute", html);
        Assert.Contains("/approvals", html);
        Assert.Contains("/policies?policyId=policy-a", html);
        Assert.Contains("Investigate Capability Activity", html);
        Assert.Contains("View capability profile", html);
        Assert.Contains("View Identity Activity", html);
        Assert.Contains("Open Filtered Audit Trail", html);
        Assert.Contains("View related approval", html);
        Assert.Contains("View related policy", html);
        Assert.Contains(
            "/capability-activity?capabilityId=production.deployment.execute&amp;identity=github-actions-production&amp;environment=production&amp;operationId=release-007&amp;runtimeMode=Enforce",
            html);
        Assert.Contains(
            "/audit?capabilityId=production.deployment.execute&amp;identityId=github-actions-production&amp;environment=production&amp;enforcementMode=Enforce&amp;matchedPolicy=policy-a",
            html);
        Assert.Contains("href=\"/audit\">Audit Trail</a> / Decision Trace", html);
        Assert.Equal(1, html.Split("View related approval").Length - 1);
    }

    [Fact]
    public void Render_DefaultDenyAndPartialEvidenceAreExplicit()
    {
        var auditEvent = CreateEvent("deny", "Enforce", "deny");
        auditEvent.MatchedPolicies = [];
        auditEvent.PolicyEvaluations = [];
        auditEvent.ResourceId = "";
        auditEvent.ApprovalOperationId = null;

        var html = AuditEventDetailPageRenderer.Render(auditEvent);

        Assert.Contains("default Deny result was used", html);
        Assert.Contains("Condition-level evidence was not recorded", html);
        Assert.Contains("Policy evaluation evidence is unavailable", html);
        Assert.Contains("Not provided", html);
        Assert.Contains("Legacy or not applicable", html);
        Assert.Contains("No Governance Window participated", html);
    }

    [Theory]
    [InlineData("Pending", "Requested", "Approval remains pending")]
    [InlineData("Approved", "Approved", "Approval resolved; awaiting a matching retry")]
    [InlineData("Rejected", "Rejected", "Changed Pending Approval to Deny")]
    [InlineData("Consumed", "Consumed", "Changed Pending Approval to Allow")]
    public void Render_ApprovalLifecycleStateIsVisible(
        string status, string action, string effect)
    {
        var auditEvent = CreateEvent(
            status == "Rejected" ? "deny" : status == "Consumed" ? "allow" : "requires_approval",
            "Enforce", "requires_approval");
        auditEvent.ApprovalId = "approval-state";
        auditEvent.ApprovalStatus = status;
        auditEvent.ApprovalAction = action;
        auditEvent.ApprovalOperationId = "operation-state";
        auditEvent.ApprovalCorrelationMode = "Operation";

        var html = AuditEventDetailPageRenderer.Render(auditEvent);

        Assert.Contains("Human Approval", html);
        Assert.Contains("Approval status", html);
        Assert.Contains(status, html);
        Assert.Contains(effect, html);
        Assert.Contains("Requested at", html);
        Assert.Contains("Resolution reason", html);
        Assert.Contains("View related approval", html);
    }

    [Fact]
    public void Render_RawEvidenceUsesCollapsedDisclosures()
    {
        var html = RenderEvent("allow", "Enforce", "allow");

        Assert.Contains("<details class=\"panel raw-trace-fields\"><summary>Raw decision payload", html);
        Assert.Contains("Matched policy identifiers", html);
        Assert.Contains("Diagnostic metadata", html);
        Assert.Contains("Raw Fields / Raw audit record", html);
        Assert.Contains("original request payload is not retained", html);
        Assert.Contains("Evaluation Sequence", html);
    }

    [Fact]
    public void RenderNotFound_ExplainsMissingDecisionAndLinksToAuditTrail()
    {
        var html = AuditEventDetailPageRenderer.RenderNotFound("missing-<decision>");

        Assert.Contains("Audit event not found", html);
        Assert.Contains("missing-&lt;decision&gt;", html);
        Assert.Contains("href=\"/audit\"", html);
        Assert.Contains("Back to Audit Trail", html);
        Assert.DoesNotContain("missing-<decision>", html);
    }

    private static string RenderEvent(
        string decision,
        string mode,
        string policyDecision,
        string? windowMode = null) =>
        AuditEventDetailPageRenderer.Render(
            CreateEvent(decision, mode, policyDecision, windowMode));

    private static AuditEvent CreateEvent(
        string decision,
        string mode,
        string policyDecision,
        string? windowMode = null)
    {
        return new AuditEvent
        {
            Id = "decision-1",
            RequestId = "request-1",
            TimestampUtc = DateTimeOffset.UtcNow,
            IdentityId = "github-actions-production",
            CapabilityId = "production.deployment.execute",
            ResourceId = "checkout-api",
            Environment = "production",
            Decision = decision,
            EnforcementMode = mode,
            MatchedPolicies = ["policy-a"],
            Obligations = ["audit"],
            Reason = windowMode is null ? "Policy reason" : "Blocked by Governance Window: Production Freeze",
            PolicyDecision = policyDecision,
            PolicyReason = "GitHub deployment is allowed by policy",
            EvaluationDurationMs = 4,
            GovernanceWindowName = windowMode is null ? null : "Production Freeze",
            GovernanceWindowMode = windowMode,
            GovernanceWindowMessage = windowMode is null ? null : "Governance Window matched: Production Freeze",
            GovernanceWindowReason = windowMode is null ? null : "Weekend production freeze.",
            PolicyEvaluations =
            [
                new AuditPolicyEvaluation
                {
                    PolicyId = "policy-a",
                    PolicyName = "GitHub production deployment",
                    Matched = true,
                    Conditions = [Condition("identity.id", "github-actions-production", "github-actions-production", true)]
                }
            ]
        };
    }

    private static AuditConditionEvaluation Condition(
        string condition,
        string expected,
        string actual,
        bool passed) => new()
        {
            Condition = condition,
            Expected = expected,
            Actual = actual,
            Passed = passed
        };

    private static void AssertSections(string html, bool hasWindow)
    {
        Assert.Contains("Request Context", html);
        Assert.Contains("Policy Evaluation", html);
        Assert.Contains("Decision Resolution", html);
        Assert.Contains("Final Outcome", html);
        Assert.Equal(hasWindow, html.Contains("Governance Window</h2>"));
    }
}
