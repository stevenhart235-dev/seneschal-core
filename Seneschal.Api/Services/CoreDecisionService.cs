using System.Diagnostics;
using Seneschal.Api.Mappers;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using ApiDecisionRequest = Seneschal.Api.Models.DecisionRequest;
using ApiDecisionResult = Seneschal.Api.Models.DecisionResult;
using CorePolicyEvaluator = Seneschal.Core.Interfaces.IPolicyEvaluator;

namespace Seneschal.Api.Services;

public sealed class CoreDecisionService
{
    public const string DecisionActivitySourceName = "Seneschal.Decisions";
    private static readonly ActivitySource DecisionActivitySource =
        new(DecisionActivitySourceName);

    private readonly PolicyLoader _policyLoader;
    private readonly CorePolicyEvaluator _policyEvaluator;
    private readonly IGovernanceModeStore _governanceModeStore;
    private readonly IAuditSink? _auditSink;
    private readonly IActivityStore? _activityStore;
    private readonly IDecisionExporter? _decisionExporter;
    private readonly IDecisionMetrics? _decisionMetrics;
    private readonly IGovernanceIncidentStore? _governanceIncidentStore;

    public CoreDecisionService(
        PolicyLoader policyLoader,
        CorePolicyEvaluator policyEvaluator,
        IGovernanceModeStore governanceModeStore,
        IAuditSink? auditSink = null,
        IActivityStore? activityStore = null,
        IDecisionExporter? decisionExporter = null,
        IDecisionMetrics? decisionMetrics = null,
        IGovernanceIncidentStore? governanceIncidentStore = null)
    {
        _policyLoader = policyLoader;
        _policyEvaluator = policyEvaluator;
        _governanceModeStore = governanceModeStore;
        _auditSink = auditSink;
        _activityStore = activityStore;
        _decisionExporter = decisionExporter;
        _decisionMetrics = decisionMetrics;
        _governanceIncidentStore = governanceIncidentStore;
    }

    public ApiDecisionResult Evaluate(ApiDecisionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var coreRequest = DecisionRequestMapper.ToCore(
            request,
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow);
        var corePolicies = _policyLoader.GetCorePolicies();

        using var activity = DecisionActivitySource.StartActivity(
            "seneschal.evaluate",
            ActivityKind.Internal);
        activity?.SetTag("seneschal.identity_id", coreRequest.Identity.Id);
        activity?.SetTag("seneschal.capability_id", coreRequest.Capability.Id);
        activity?.SetTag(
            "seneschal.environment",
            coreRequest.Resource.Environment ?? string.Empty);
        activity?.SetTag("seneschal.resource_id", coreRequest.Resource.Id);

        var stopwatch = Stopwatch.StartNew();
        var coreResult = _policyEvaluator.Evaluate(
            coreRequest,
            corePolicies,
            _governanceModeStore.GetMode());
        stopwatch.Stop();

        coreResult = coreResult with
        {
            LatencyMs = (int)stopwatch.ElapsedMilliseconds
        };

        PopulateDecisionActivity(
            activity,
            coreResult);

        WriteAuditEvent(
            coreRequest,
            coreResult);

        return DecisionResultMapper.ToApi(
            coreResult,
            stopwatch.ElapsedMilliseconds);
    }

    private static void PopulateDecisionActivity(
        Activity? activity,
        DecisionResult result)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag("seneschal.decision", result.Decision.ToString());
        activity.SetTag("seneschal.enforcement_mode", result.Mode.ToString());
        activity.SetTag(
            "seneschal.matched_policy_count",
            result.MatchedPolicies.Count);
        activity.SetTag(
            "seneschal.obligation_count",
            result.Obligations.Count);
        activity.SetTag(
            "seneschal.evaluation_duration_ms",
            result.LatencyMs);

        if (result.MatchedPolicies.Count > 0)
        {
            activity.SetTag(
                "seneschal.matched_policies",
                string.Join(",", result.MatchedPolicies));
        }

        if (result.Obligations.Count > 0)
        {
            activity.SetTag(
                "seneschal.obligations",
                string.Join(",", result.Obligations));
        }

        if (result.Decision is DecisionType.Deny or DecisionType.RequireApproval)
        {
            activity.SetStatus(
                ActivityStatusCode.Error,
                result.Decision.ToString());
        }
    }

    private void WriteAuditEvent(
        DecisionRequest request,
        DecisionResult result)
    {
        if (_auditSink is null &&
            _activityStore is null &&
            _decisionExporter is null &&
            _decisionMetrics is null &&
            _governanceIncidentStore is null)
        {
            return;
        }

        var auditEvent = new AuditEvent
        {
            Id = result.DecisionId,
            TimestampUtc = result.Timestamp,
            IdentityId = request.Identity.Id,
            CapabilityId = request.Capability.Id,
            ResourceId = request.Resource.Id,
            Environment = request.Resource.Environment ?? string.Empty,
            Decision = result.Decision,
            EnforcementMode = result.Mode,
            MatchedPolicies = result.MatchedPolicies,
            Obligations = result.Obligations,
            Reason = result.Reason,
            EvaluationDurationMs = result.LatencyMs
        };

        _auditSink?.WriteAsync(auditEvent).GetAwaiter().GetResult();
        _activityStore?.RecordAsync(auditEvent).GetAwaiter().GetResult();
        TryExport(auditEvent);
        TryRecordMetrics(auditEvent);
        TryRecordIncident(auditEvent);
    }

    private void TryExport(AuditEvent auditEvent)
    {
        if (_decisionExporter is null)
        {
            return;
        }

        try
        {
            _decisionExporter.ExportAsync(auditEvent).GetAwaiter().GetResult();
        }
        catch
        {
            // Decision export is intentionally isolated from policy evaluation,
            // audit history, and runtime activity metrics.
        }
    }

    private void TryRecordMetrics(AuditEvent auditEvent)
    {
        if (_decisionMetrics is null)
        {
            return;
        }

        try
        {
            _decisionMetrics.RecordAsync(auditEvent).GetAwaiter().GetResult();
        }
        catch
        {
            // Decision metrics are intentionally isolated from policy
            // evaluation, audit history, activity metrics, export, and tracing.
        }
    }

    private void TryRecordIncident(AuditEvent auditEvent)
    {
        if (_governanceIncidentStore is null)
        {
            return;
        }

        try
        {
            _governanceIncidentStore
                .RecordAsync(auditEvent)
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            // Governance incidents are intentionally isolated from policy
            // evaluation, audit history, activity metrics, export, metrics,
            // and tracing.
        }
    }
}
