using System.Diagnostics;
using Seneschal.Core.Enums;
using Seneschal.Core.Models;

namespace Seneschal.Core.Services;

public sealed class DecisionEngine
{
    public DecisionResult Evaluate(DecisionRequest request)
    {
        var stopwatch = Stopwatch.StartNew();

        stopwatch.Stop();

        return new DecisionResult
        {
            DecisionId = Guid.NewGuid().ToString("N"),
            RequestId = request.RequestId,
            Timestamp = DateTimeOffset.UtcNow,
            Decision = DecisionType.Allow,
            Mode = EnforcementMode.LogOnly,
            Reason = "No policies evaluated. Defaulting to log-only allow.",
            MatchedPolicies = [],
            Obligations = ["audit"],
            LatencyMs = (int)stopwatch.ElapsedMilliseconds
        };
    }
}