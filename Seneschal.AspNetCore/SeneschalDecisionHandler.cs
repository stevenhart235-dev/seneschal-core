using System.Text.Json;
using System.Net;
using Microsoft.AspNetCore.Http;
using Seneschal.Client;
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
            SeneschalEnforcementBehavior.Enforce =>
                decision.ShouldProceed &&
                !string.Equals(
                    decision.ExecutionGuidance,
                    ExecutionGuidanceContract.ContinueLogOnly,
                    StringComparison.OrdinalIgnoreCase),
            _ => decision.ShouldProceed
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
                    policyMatched = decision.PolicyMatched
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
                policyMatched = decision.PolicyMatched
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

    public static async Task WriteFailureResponseAsync(
        HttpContext context,
        SeneschalClientException exception)
    {
        var (statusCode, decision, reason) = exception.StatusCode switch
        {
            HttpStatusCode.Unauthorized => (
                StatusCodes.Status401Unauthorized,
                "authentication_failed",
                "Seneschal rejected the integration API key."),
            HttpStatusCode.Forbidden => (
                StatusCodes.Status403Forbidden,
                "integration_forbidden",
                "The Seneschal integration key is not authorized for this capability request."),
            _ when exception.InnerException is JsonException => (
                StatusCodes.Status502BadGateway,
                "invalid_response",
                "Seneschal returned an invalid decision response."),
            _ => (
                StatusCodes.Status503ServiceUnavailable,
                "unavailable",
                "Seneschal is unavailable or did not return a decision in time.")
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            new
            {
                decision,
                reason,
                policyMatched = (string?)null
            },
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

}
