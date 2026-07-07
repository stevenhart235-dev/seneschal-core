using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Seneschal.Client.Models;

namespace Seneschal.AspNetCore;

internal static class SeneschalDecisionHandler
{
    public static bool ShouldContinue(
        DecisionResult decision,
        SeneschalEnforcementBehavior enforcementBehavior)
    {
        return enforcementBehavior switch
        {
            SeneschalEnforcementBehavior.Monitor => true,
            SeneschalEnforcementBehavior.Enforce => IsAllow(decision.Decision),
            _ => IsMonitorMode(decision.Mode) || IsAllow(decision.Decision)
        };
    }

    public static IResult ToResult(DecisionResult decision)
    {
        if (IsDeny(decision.Decision))
        {
            return Results.Json(
                new
                {
                    decision = decision.Decision,
                    reason = decision.Reason,
                    policyMatched = decision.PolicyMatched
                },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (IsPendingApproval(decision.Decision))
        {
            return Results.Json(
                new
                {
                    decision = decision.Decision,
                    reason = decision.Reason,
                    obligations = decision.Obligations
                },
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Json(
            new
            {
                decision = decision.Decision,
                reason = $"Unsupported Seneschal decision '{decision.Decision}'."
            },
            statusCode: StatusCodes.Status502BadGateway);
    }

    public static async Task WriteResponseAsync(
        HttpContext context,
        DecisionResult decision)
    {
        object response;

        if (IsDeny(decision.Decision))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            response = new
            {
                decision = decision.Decision,
                reason = decision.Reason,
                policyMatched = decision.PolicyMatched
            };
        }
        else if (IsPendingApproval(decision.Decision))
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            response = new
            {
                decision = decision.Decision,
                reason = decision.Reason,
                obligations = decision.Obligations
            };
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            response = new
            {
                decision = decision.Decision,
                reason = $"Unsupported Seneschal decision '{decision.Decision}'."
            };
        }

        context.Response.ContentType = "application/json";
        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            response,
            cancellationToken: context.RequestAborted);
    }

    public static bool IsAllow(string decision)
    {
        return string.Equals(decision, "Allow", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(decision, "allow", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDeny(string decision)
    {
        return string.Equals(decision, "Deny", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(decision, "deny", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPendingApproval(string decision)
    {
        return string.Equals(
                decision,
                "PendingApproval",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                decision,
                "RequireApproval",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                decision,
                "requires_approval",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMonitorMode(string mode)
    {
        return string.Equals(mode, "Monitor", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, "LogOnly", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, "log_only", StringComparison.OrdinalIgnoreCase);
    }
}
