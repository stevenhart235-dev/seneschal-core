using System.Diagnostics;
using Seneschal.Api.Mappers;
using ApiDecisionRequest = Seneschal.Api.Models.DecisionRequest;
using ApiDecisionResult = Seneschal.Api.Models.DecisionResult;
using CorePolicyEvaluator = Seneschal.Core.Interfaces.IPolicyEvaluator;

namespace Seneschal.Api.Services;

public sealed class CoreDecisionService
{
    private readonly PolicyLoader _policyLoader;
    private readonly CorePolicyEvaluator _policyEvaluator;
    private readonly RuntimeSettings _settings;

    public CoreDecisionService(
        PolicyLoader policyLoader,
        CorePolicyEvaluator policyEvaluator,
        RuntimeSettings settings)
    {
        _policyLoader = policyLoader;
        _policyEvaluator = policyEvaluator;
        _settings = settings;
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

        return DecisionResultMapper.ToApi(
            coreResult,
            stopwatch.ElapsedMilliseconds);
    }
}
