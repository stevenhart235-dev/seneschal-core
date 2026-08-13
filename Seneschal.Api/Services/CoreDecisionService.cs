using System.Diagnostics;
using Seneschal.Api.Mappers;
using Seneschal.Core.Exceptions;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
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
    private readonly IEvaluationCommitCoordinator _evaluationCommitCoordinator;
    private readonly IActivityStore? _activityStore;
    private readonly IDecisionExporter? _decisionExporter;
    private readonly IDecisionMetrics? _decisionMetrics;
    private readonly IGovernanceIncidentStore? _governanceIncidentStore;
    private readonly IGovernanceWindowStore? _governanceWindowStore;
    private readonly IApprovalStore? _approvalStore;
    private readonly GovernanceConfigurationFingerprintService? _configurationFingerprint;

    public CoreDecisionService(
        PolicyLoader policyLoader,
        CorePolicyEvaluator policyEvaluator,
        IGovernanceModeStore governanceModeStore,
        IAuditSink? auditSink = null,
        IActivityStore? activityStore = null,
        IDecisionExporter? decisionExporter = null,
        IDecisionMetrics? decisionMetrics = null,
        IGovernanceIncidentStore? governanceIncidentStore = null,
        IGovernanceWindowStore? governanceWindowStore = null,
        IApprovalStore? approvalStore = null,
        IEvaluationCommitCoordinator? evaluationCommitCoordinator = null,
        GovernanceConfigurationFingerprintService? configurationFingerprint = null)
    {
        _policyLoader = policyLoader;
        _policyEvaluator = policyEvaluator;
        _governanceModeStore = governanceModeStore;
        _evaluationCommitCoordinator = evaluationCommitCoordinator ??
            CreateCompatibilityCommitCoordinator(auditSink, approvalStore);
        _activityStore = activityStore;
        _decisionExporter = decisionExporter;
        _decisionMetrics = decisionMetrics;
        _governanceIncidentStore = governanceIncidentStore;
        _governanceWindowStore = governanceWindowStore;
        _approvalStore = approvalStore;
        _configurationFingerprint = configurationFingerprint;
    }

    public ApiDecisionResult Evaluate(ApiDecisionRequest request)
        => Evaluate(request, commit: true);

    public ApiDecisionResult Preview(ApiDecisionRequest request)
        => Evaluate(request, commit: false);

    private ApiDecisionResult Evaluate(
        ApiDecisionRequest request,
        bool commit)
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
        var policyDecision = coreResult.Decision;
        var policyReason = coreResult.Reason;
        var approvalEvaluation = PlanApproval(
            coreRequest,
            ref coreResult,
            out var approvalMutation);
        var decisionBeforeWindow = coreResult.Decision;
        var windowEvaluation = EvaluateGovernanceWindow(
            coreRequest.Capability.Id,
            ref coreResult);
        coreResult = coreResult with
        {
            ExecutionGuidance = ResolveExecutionGuidance(
                coreResult.Decision, coreResult.Mode)
        };
        stopwatch.Stop();

        coreResult = coreResult with
        {
            LatencyMs = (int)stopwatch.ElapsedMilliseconds
        };

        PopulateDecisionActivity(
            activity,
            coreResult);

        if (commit)
        {
            WriteAuditEvent(
                coreRequest,
                coreResult,
                windowEvaluation,
                policyDecision,
                policyReason,
                approvalEvaluation,
                approvalMutation);
        }

        return DecisionResultMapper.ToApi(
            coreResult,
            stopwatch.ElapsedMilliseconds,
            windowEvaluation?.Name,
            windowEvaluation?.Mode.ToString(),
            windowEvaluation?.Reason,
            windowEvaluation is not null && decisionBeforeWindow != coreResult.Decision,
            includeSimulationExplanation: !commit);
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
        DecisionResult result,
        GovernanceWindowEvaluation? windowEvaluation,
        DecisionType policyDecision,
        string policyReason,
        ApprovalEvaluation? approvalEvaluation,
        ApprovalMutation? approvalMutation)
    {
        var auditEvent = new AuditEvent
        {
            Id = result.DecisionId,
            RequestId = request.RequestId,
            TimestampUtc = result.Timestamp,
            IdentityId = request.Identity.Id,
            CapabilityId = request.Capability.Id,
            RequestedAction = request.Intent.Action,
            RequestContext = new Dictionary<string, string>(request.Context),
            ResourceId = request.Resource.Id,
            Environment = request.Resource.Environment ?? string.Empty,
            Decision = result.Decision,
            EnforcementMode = result.Mode,
            EffectiveAction = EffectiveActionFor(result),
            MatchedPolicies = result.MatchedPolicies,
            Obligations = result.Obligations,
            Reason = result.Reason,
            EvaluationDurationMs = result.LatencyMs,
            GovernanceWindowName = windowEvaluation?.Name,
            GovernanceWindowMode = windowEvaluation?.Mode.ToString(),
            GovernanceWindowMessage = windowEvaluation?.Message,
            GovernanceWindowReason = windowEvaluation?.Reason,
            PolicyDecision = policyDecision,
            PolicyReason = policyReason,
            PolicyEvaluations = result.PolicyEvaluations
            ,ApprovalId = approvalEvaluation?.Record.Id
            ,ApprovalStatus = approvalEvaluation?.Record.Status.ToString()
            ,ApprovalAction = approvalEvaluation?.Action
            ,ApprovalRequestReason = approvalEvaluation?.Record.RequestReason
            ,ApprovalResolvedAt = approvalEvaluation?.Record.ResolvedAt
            ,ApprovalResolvedBy = approvalEvaluation?.Record.ResolvedBy
            ,ApprovalConsumedAt = approvalEvaluation?.Record.ConsumedAt
            ,ApprovalConsumedByDecisionId = approvalEvaluation?.Record.ConsumedByDecisionId
            ,ExecutionGuidance = result.ExecutionGuidance.ToString()
            ,CallerMessage = result.CallerMessage
            ,RetryGuidance = result.RetryGuidance
            ,ApprovalOperationId = approvalEvaluation?.Record.OperationId
            ,ApprovalCorrelationMode = approvalEvaluation?.Record.CorrelationMode.ToString()
            ,GovernanceConfigurationFingerprint = _configurationFingerprint?.GetCurrentFingerprint()
        };

        _evaluationCommitCoordinator.CommitAsync(new EvaluationCommit
        {
            Evidence = auditEvent,
            ApprovalMutation = approvalMutation
        }).GetAwaiter().GetResult();

        TryRecordActivity(auditEvent);
        TryExport(auditEvent);
        TryRecordMetrics(auditEvent);
        TryRecordIncident(auditEvent);
    }

    private ApprovalEvaluation? PlanApproval(
        DecisionRequest request,
        ref DecisionResult result,
        out ApprovalMutation? mutation)
    {
        mutation = null;
        if (_approvalStore is null || result.Decision != DecisionType.RequireApproval)
            return null;

        var reason = request.Context.GetValueOrDefault("reason", request.Intent.Reason);
        var operationId = string.IsNullOrWhiteSpace(request.OperationId)
            ? null
            : request.OperationId.Trim();
        var record = _approvalStore.Find(
            request.Identity.Id,
            request.Capability.Id,
            request.Resource.Environment ?? string.Empty,
            request.Resource.Id,
            operationId);
        var action = record?.Status == ApprovalStatus.Pending
            ? "Reused"
            : "Used";

        if (record is null)
        {
            record = new ApprovalRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                IdentityId = request.Identity.Id,
                CapabilityId = request.Capability.Id,
                Environment = request.Resource.Environment ?? string.Empty,
                ResourceId = request.Resource.Id,
                OperationId = operationId,
                CorrelationMode = operationId is null
                    ? ApprovalCorrelationMode.LegacyContext
                    : ApprovalCorrelationMode.Operation,
                RequestReason = reason,
                RequestedAt = request.Timestamp
            };
            mutation = new ApprovalMutation
            {
                Kind = ApprovalMutationKind.Create,
                Record = record
            };
            action = "Requested";
        }
        else if (record.Status == ApprovalStatus.Approved)
        {
            record = record with
            {
                Status = ApprovalStatus.Consumed,
                ConsumedAt = result.Timestamp,
                ConsumedByDecisionId = result.DecisionId
            };
            mutation = new ApprovalMutation
            {
                Kind = ApprovalMutationKind.Consume,
                Record = record,
                ExpectedStatus = ApprovalStatus.Approved
            };
            action = "Consumed";
            result = result with
            {
                Decision = DecisionType.Allow,
                Reason = $"Approved through single-use human approval {record.Id}."
            };
        }
        else if (record.Status == ApprovalStatus.Rejected)
        {
            result = result with
            {
                Decision = DecisionType.Deny,
                Reason = $"Rejected through human approval {record.Id}."
            };
        }

        result = result with
        {
            ApprovalId = record.Id,
            ApprovalStatus = record.Status.ToString(),
            OperationId = record.OperationId,
            ApprovalCorrelationMode = record.CorrelationMode.ToString(),
            CallerMessage = result.Decision == DecisionType.RequireApproval
                ? result.Mode == EnforcementMode.LogOnly
                    ? "Approval is required by policy; LogOnly records the decision and allows the operation to continue."
                    : "Approval is required before this operation can continue. Retry with the same operationId after approval."
                : null,
            RetryGuidance = result.Decision == DecisionType.RequireApproval
                ? record.CorrelationMode == ApprovalCorrelationMode.Operation
                    ? $"Retry after approval using operationId '{record.OperationId}'."
                    : "Retry after approval with the same legacy identity, capability, environment, and resource context. Production callers should provide operationId."
                : null
        };

        return new ApprovalEvaluation(record, action);
    }

    private static string EffectiveActionFor(DecisionResult result)
    {
        if (result.Mode == EnforcementMode.LogOnly &&
            result.Decision != DecisionType.Allow)
        {
            return "logged_only";
        }

        return DecisionTypeMapper.ToApi(result.Decision);
    }

    private void TryRecordActivity(AuditEvent auditEvent)
    {
        if (_activityStore is null)
        {
            return;
        }

        try
        {
            _activityStore.RecordAsync(auditEvent).GetAwaiter().GetResult();
        }
        catch
        {
            // Activity is a recomputable projection. Its failure cannot undo
            // committed evaluation evidence.
        }
    }

    private static IEvaluationCommitCoordinator CreateCompatibilityCommitCoordinator(
        IAuditSink? auditSink,
        IApprovalStore? approvalStore)
    {
        var inMemoryEvidence = auditSink as InMemoryAuditEventStore ??
            new InMemoryAuditEventStore();
        var inMemoryApprovals = approvalStore as InMemoryApprovalStore ??
            new InMemoryApprovalStore();

        if ((auditSink is null or InMemoryAuditEventStore) &&
            (approvalStore is null or InMemoryApprovalStore))
        {
            return new InMemoryEvaluationCommitCoordinator(
                inMemoryEvidence,
                inMemoryApprovals);
        }

        return new CompatibilityEvaluationCommitCoordinator(
            auditSink ?? inMemoryEvidence);
    }

    internal static ExecutionGuidance ResolveExecutionGuidance(
        DecisionType decision,
        EnforcementMode mode) => (decision, mode) switch
    {
        (DecisionType.Allow, _) => ExecutionGuidance.Proceed,
        (DecisionType.Deny, EnforcementMode.LogOnly) => ExecutionGuidance.ContinueLogOnly,
        (DecisionType.Deny, _) => ExecutionGuidance.Block,
        (DecisionType.RequireApproval, EnforcementMode.LogOnly) => ExecutionGuidance.ContinueLogOnly,
        (DecisionType.RequireApproval, _) => ExecutionGuidance.Pause,
        _ => ExecutionGuidance.Block
    };

    private GovernanceWindowEvaluation? EvaluateGovernanceWindow(
        string capabilityId,
        ref DecisionResult result)
    {
        if (_governanceWindowStore is null)
        {
            return null;
        }

        var window = _governanceWindowStore.GetWindow();
        if (!window.Enabled || !window.AffectedCapabilities.Contains(
                capabilityId,
                StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        var message = $"Governance Window matched: {window.Name}";
        var evaluation = result.Evaluation.ToList();
        evaluation.Add(new EvaluationStep
        {
            Property = "GovernanceWindow",
            Expected = string.Join(", ", window.AffectedCapabilities),
            Actual = capabilityId,
            Matched = true
        });

        if (window.Mode == GovernanceWindowMode.Enforce &&
            result.Decision == DecisionType.Allow)
        {
            result = result with
            {
                Decision = DecisionType.Deny,
                Reason = $"Blocked by Governance Window: {window.Name}",
                Evaluation = evaluation
            };
        }
        else
        {
            result = result with { Evaluation = evaluation };
        }

        return new GovernanceWindowEvaluation(
            window.Name,
            window.Mode,
            message,
            window.Reason);
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

internal sealed record GovernanceWindowEvaluation(
    string Name,
    GovernanceWindowMode Mode,
    string Message,
    string Reason);

internal sealed record ApprovalEvaluation(
    ApprovalRecord Record,
    string Action);
