using Seneschal.Api.Models;

namespace Seneschal.Api.Services;

public sealed class IntegrationApiKeyAuthorizer
{
    public const string HeaderName = "X-Seneschal-Api-Key";
    private const string UnauthorizedReason =
        "A valid Seneschal API key is required.";
    private const string ForbiddenReason =
        "The Seneschal API key is not authorized.";

    private readonly IntegrationApiKeyLoader _keyLoader;

    public IntegrationApiKeyAuthorizer(IntegrationApiKeyLoader keyLoader)
    {
        _keyLoader = keyLoader;
    }

    public IntegrationApiKeyAuthorizationResult Authorize(
        HttpRequest httpRequest,
        DecisionRequest decisionRequest)
    {
        if (!httpRequest.Headers.TryGetValue(HeaderName, out var headerValues))
        {
            return IntegrationApiKeyAuthorizationResult.Unauthorized(
                UnauthorizedReason);
        }

        var presentedKey = headerValues.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(presentedKey))
        {
            return IntegrationApiKeyAuthorizationResult.Unauthorized(
                UnauthorizedReason);
        }

        var integrationKey = _keyLoader.GetKeys().FirstOrDefault(key =>
            string.Equals(key.Key, presentedKey, StringComparison.Ordinal));

        if (integrationKey is null)
        {
            return IntegrationApiKeyAuthorizationResult.Unauthorized(
                UnauthorizedReason);
        }

        if (!integrationKey.Enabled)
        {
            return IntegrationApiKeyAuthorizationResult.Forbidden(
                ForbiddenReason);
        }

        if (!MatchesScope(integrationKey.AllowedIdentities, decisionRequest.Identity))
        {
            return IntegrationApiKeyAuthorizationResult.Forbidden(
                ForbiddenReason);
        }

        if (!MatchesScope(integrationKey.AllowedCapabilities, decisionRequest.Capability))
        {
            return IntegrationApiKeyAuthorizationResult.Forbidden(
                ForbiddenReason);
        }

        if (!string.IsNullOrWhiteSpace(integrationKey.Environment))
        {
            var requestEnvironment = decisionRequest.Context.TryGetValue(
                "environment",
                out var environment)
                ? environment
                : string.Empty;

            if (!string.Equals(
                    integrationKey.Environment,
                    requestEnvironment,
                    StringComparison.OrdinalIgnoreCase))
            {
                return IntegrationApiKeyAuthorizationResult.Forbidden(
                    ForbiddenReason);
            }
        }

        return IntegrationApiKeyAuthorizationResult.Allowed(integrationKey);
    }

    private static bool MatchesScope(
        IReadOnlyCollection<string> allowedValues,
        string requestedValue)
    {
        return allowedValues.Any(value =>
            string.Equals(value, "*", StringComparison.Ordinal) ||
            string.Equals(
                value,
                requestedValue,
                StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record IntegrationApiKeyAuthorizationResult
{
    private IntegrationApiKeyAuthorizationResult(
        bool isAllowed,
        int? statusCode,
        string reason,
        IntegrationApiKey? integrationKey)
    {
        IsAllowed = isAllowed;
        StatusCode = statusCode;
        Reason = reason;
        IntegrationKey = integrationKey;
    }

    public bool IsAllowed { get; }

    public int? StatusCode { get; }

    public string Reason { get; }

    public IntegrationApiKey? IntegrationKey { get; }

    public static IntegrationApiKeyAuthorizationResult Allowed(
        IntegrationApiKey integrationKey)
    {
        return new IntegrationApiKeyAuthorizationResult(
            true,
            null,
            string.Empty,
            integrationKey);
    }

    public static IntegrationApiKeyAuthorizationResult Unauthorized(
        string reason)
    {
        return new IntegrationApiKeyAuthorizationResult(
            false,
            StatusCodes.Status401Unauthorized,
            reason,
            null);
    }

    public static IntegrationApiKeyAuthorizationResult Forbidden(
        string reason)
    {
        return new IntegrationApiKeyAuthorizationResult(
            false,
            StatusCodes.Status403Forbidden,
            reason,
            null);
    }
}
