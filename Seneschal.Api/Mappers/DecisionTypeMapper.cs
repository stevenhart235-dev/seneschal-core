using Seneschal.Core.Enums;

namespace Seneschal.Api.Mappers;

public static class DecisionTypeMapper
{
    public static string ToApi(DecisionType decision)
    {
        return decision switch
        {
            DecisionType.Allow => "allow",
            DecisionType.Deny => "deny",
            DecisionType.Warn => "warn",
            DecisionType.LogOnly => "log_only",
            DecisionType.RequireApproval => "requires_approval",
            _ => throw new ArgumentOutOfRangeException(
                nameof(decision),
                decision,
                "Unsupported decision type.")
        };
    }

    public static DecisionType ToCore(string decision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(decision);

        return decision.ToLowerInvariant() switch
        {
            "allow" => DecisionType.Allow,
            "deny" => DecisionType.Deny,
            "warn" => DecisionType.Warn,
            "log_only" => DecisionType.LogOnly,
            "requires_approval" => DecisionType.RequireApproval,
            _ => throw new ArgumentException(
                $"Unsupported decision value '{decision}'.",
                nameof(decision))
        };
    }
}
