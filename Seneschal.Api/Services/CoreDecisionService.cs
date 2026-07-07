using System.Diagnostics;
using Seneschal.Api.Mappers;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using ApiDecisionRequest = Seneschal.Api.Models.DecisionRequest;
using ApiDecisionResult = Seneschal.Api.Models.DecisionResult;
using CorePolicyEvaluator = Seneschal.Core.Interfaces.IPolicyEvaluator;

namespace Seneschal.Api.Services;

public sealed class CoreDecisionService
{
    private readonly PolicyLoader _policyLoader;
    private readonly CorePolicyEvaluator _policyEvaluator;
    private readonly RuntimeSettings _settings;
    private readonly IAuditSink? _auditSink;
    private readonly IActivityStore? _activityStore;

    public CoreDecisionService(
        PolicyLoader policyLoader,
        CorePolicyEvaluator policyEvaluator,
        RuntimeSettings settings,
        IAuditSink? auditSink = null,
        IActivityStore? activityStore = null)
    {
        _policyLoader = policyLoader;
        _policyEvaluator = policyEvaluator;
        _settings = settings;
        _auditSink = auditSink;
        _activityStore = activityStore;
    }

    public ApiDecisionResult Evaluate(ApiDecisionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var coreRequest = DecisionRequestMapper.ToCore(
            request,
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow);
        var corePolicies = _policyLoader.GetCorePolicies();

        var stopwatch = Stopwatch.StartNew();
        var coreResult = _policyEvaluator.Evaluate(
            coreRequest,
            corePolicies,
            _settings.Mode);
        stopwatch.Stop();

        coreResult = coreResult with
        {
            LatencyMs = (int)stopwatch.ElapsedMilliseconds
        };

        WriteAuditEvent(
            coreRequest,
            coreResult);

        return DecisionResultMapper.ToApi(
            coreResult,
            stopwatch.ElapsedMilliseconds);
    }

    private void WriteAuditEvent(
        DecisionRequest request,
        DecisionResult result)
    {
        if (_auditSink is null && _activityStore is null)
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
    }
}
