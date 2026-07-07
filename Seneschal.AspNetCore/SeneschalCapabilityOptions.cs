using Microsoft.AspNetCore.Http;

namespace Seneschal.AspNetCore;

/// <summary>
/// Configures <see cref="SeneschalCapabilityMiddleware"/>.
/// </summary>
public sealed class SeneschalCapabilityOptions
{
    /// <summary>
    /// Gets or sets the required capability identifier.
    /// </summary>
    public string CapabilityId { get; set; } = "";

    /// <summary>
    /// Gets or sets the identity identifier sent to Seneschal. When omitted,
    /// the middleware uses the authenticated user name, then falls back to
    /// <c>anonymous</c>.
    /// </summary>
    public string? IdentityId { get; set; }

    /// <summary>
    /// Gets or sets the environment sent in the decision request context.
    /// </summary>
    public string? Environment { get; set; }

    /// <summary>
    /// Gets or sets the resource identifier sent in the decision request
    /// context. When omitted, the current request path is used.
    /// </summary>
    public string? ResourceId { get; set; }

    /// <summary>
    /// Gets or sets how the middleware applies decisions.
    /// </summary>
    public SeneschalEnforcementBehavior EnforcementBehavior { get; set; } =
        SeneschalEnforcementBehavior.HonorDecisionMode;

    /// <summary>
    /// Resolves the identity identifier for a request.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>The identity identifier to send to Seneschal.</returns>
    public string ResolveIdentity(HttpContext context)
    {
        return FirstNonEmpty(
            IdentityId,
            context.User.Identity?.Name,
            "anonymous");
    }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(CapabilityId))
        {
            throw new InvalidOperationException(
                "Seneschal capability middleware requires a CapabilityId.");
        }
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.First(value => !string.IsNullOrWhiteSpace(value))!;
    }
}
