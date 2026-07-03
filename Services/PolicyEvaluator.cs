using System.Diagnostics;
using Seneschal.Api.Models;

namespace Seneschal.Api.Services;

public class PolicyEvaluator
{
    private readonly PolicyLoader _policyLoader;
    private readonly RuntimeSettings _settings;

    public PolicyEvaluator(
        PolicyLoader policyLoader,
        RuntimeSettings settings)
    {
        _policyLoader = policyLoader;
        _settings = settings;
    }

    public DecisionResult Evaluate(DecisionRequest request)
    {
        var stopwatch = Stopwatch.StartNew();

        var environment = request.Context.GetValueOrDefault("environment", "");

        var matchedPolicy = _policyLoader.GetPolicies()
            .FirstOrDefault(policy =>
                policy.Identity.Equals(request.Identity, StringComparison.OrdinalIgnoreCase) &&
                policy.Capability.Equals(request.Capability, StringComparison.OrdinalIgnoreCase) &&
                policy.Environment.Equals(environment, StringComparison.OrdinalIgnoreCase));

        stopwatch.Stop();

        DecisionResult result;

        if (matchedPolicy is null)
        {
            result = new DecisionResult
            {
                Decision = "deny",
                Reason = "No matching allow policy found",
                PolicyMatched = "default-deny",
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        else
        {
            result = new DecisionResult
            {
                Decision = matchedPolicy.Decision,
                Reason = matchedPolicy.Reason,
                PolicyMatched = matchedPolicy.Name,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }

        result.Mode = _settings.Mode.ToString();

        result.EffectiveAction =
            _settings.Mode == EnforcementMode.LogOnly && result.Decision != "allow"
                ? "logged_only"
                : result.Decision;

        return result;
    }
}