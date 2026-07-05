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
            Timestamp = result.Timestamp,
            DecisionId = result.DecisionId,
            RequestId = result.RequestId,

            Identity = request.Identity,
            Capability = request.Capability,
            Intent = request.Intent,
            Resource = request.Resource,

            Decision = result.Decision,
            Mode = result.Mode,

            MatchedPolicies = result.MatchedPolicies,

            LatencyMs = result.LatencyMs
        };

        await _auditSink.WriteAsync(auditEvent, cancellationToken);

        return result;
    }
}