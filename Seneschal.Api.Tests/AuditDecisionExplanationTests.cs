using Seneschal.Api.Models;
using Seneschal.Api.Services;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class AuditDecisionExplanationTests
{
    [Fact]
    public void Render_PlainAllowShowsExecutedFlow()
    {
        var html = RenderEvent("allow", "Enforce", "allow");

        AssertSections(html, hasWindow: false);
        Assert.Contains("Policy Decision", html);
        Assert.Contains("Allow", html);
        Assert.Contains("No window override", html);
        Assert.Contains("Effective application result", html);
        Assert.Contains("Executed", html);
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
        Assert.Contains("Executed", html);
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
