using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Services;

public sealed class DecisionEngine
{
    private readonly IPolicyRepository _policyRepository;
    private readonly IPolicyEvaluator _policyEvaluator;
    private readonly IAuditSink _auditSink;

    public DecisionEngine(
        IPolicyRepository policyRepository,
        IPolicyEvaluator policyEvaluator,
        IAuditSink auditSink)
    {
        _policyRepository = policyRepository;
        _policyEvaluator = policyEvaluator;
        _auditSink = auditSink;
    }

    public async Task<DecisionResult> EvaluateAsync(
        DecisionRequest request,
        EnforcementMode mode = EnforcementMode.Enforce,
        CancellationToken cancellationToken = default)
    {
        var policies =
            await _policyRepository.GetPoliciesAsync(cancellationToken);

        var result =
            _policyEvaluator.Evaluate(request, policies, mode);

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

        await _auditSink.WriteAsync(auditEvent, cancellationToken);

        return result;
    }
}
