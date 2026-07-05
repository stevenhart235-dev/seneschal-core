using Seneschal.Core.Enums;
using Seneschal.Core.Models;

namespace Seneschal.Core.Interfaces;

public interface IPolicyEvaluator
{
    DecisionResult Evaluate(
        DecisionRequest request,
        IEnumerable<Policy> policies,
        EnforcementMode mode);
}