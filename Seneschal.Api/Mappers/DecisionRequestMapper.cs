using ApiDecisionRequest = Seneschal.Api.Models.DecisionRequest;
using CoreDecisionRequest = Seneschal.Core.Models.DecisionRequest;
using Seneschal.Core.Enums;
using Seneschal.Core.Models;

namespace Seneschal.Api.Mappers;

public static class DecisionRequestMapper
{
    public static CoreDecisionRequest ToCore(
        ApiDecisionRequest request,
        string requestId,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        var environment = request.Context.GetValueOrDefault(
            "environment",
            string.Empty);
        var resourceId = request.Context.GetValueOrDefault(
            "resource",
            string.Empty);

        return new CoreDecisionRequest
        {
            RequestId = requestId,
            Timestamp = timestamp,
            OperationId = request.OperationId,
            Identity = new Identity
            {
                Id = request.Identity,
                Type = IdentityType.Agent,
                Owner = string.Empty,
                Environment = environment
            },
            Capability = new Capability
            {
                Id = request.Capability,
                Name = request.Capability,
                Provider = "api",
                Category = "unspecified",
                Description = request.Capability,
                RiskLevel = RiskLevel.Low
            },
            Intent = new Intent
            {
                Action = request.Capability,
                Reason = "API evaluation request."
            },
            Resource = new Resource
            {
                Type = "resource",
                Id = resourceId,
                Environment = environment
            },
            Context = new Dictionary<string, string>(request.Context)
        };
    }
}
