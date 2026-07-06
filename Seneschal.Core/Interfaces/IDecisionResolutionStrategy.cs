using Seneschal.Core.Models;

namespace Seneschal.Core.Interfaces;

public interface IDecisionResolutionStrategy
{
    PolicyMatch SelectWinner(IReadOnlyCollection<PolicyMatch> matches);
}
