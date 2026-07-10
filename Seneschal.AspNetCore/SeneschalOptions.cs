using Microsoft.AspNetCore.Http;

namespace Seneschal.AspNetCore;

/// <summary>
/// Configures the recommended Seneschal ASP.NET Core integration.
/// </summary>
public sealed class SeneschalOptions
{
    /// <summary>
    /// Gets or sets the base URL of the Seneschal API.
    /// </summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the integration API key sent to Seneschal.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the relative runtime evaluation endpoint path.
    /// </summary>
    public string EvaluatePath { get; set; } = "/evaluate";

    /// <summary>
    /// Gets or sets the request header used for the integration API key.
    /// </summary>
    public string ApiKeyHeaderName { get; set; } = "X-Seneschal-Api-Key";

    /// <summary>
    /// Gets or sets the function used to resolve the requesting identity.
    /// </summary>
    public Func<HttpContext, string> IdentityResolver { get; set; } = context =>
        string.IsNullOrWhiteSpace(context.User.Identity?.Name)
            ? "anonymous"
            : context.User.Identity!.Name!;

    /// <summary>
    /// Gets or sets the environment used when endpoint metadata does not
    /// specify one.
    /// </summary>
    public string? DefaultEnvironment { get; set; }

    /// <summary>
    /// Gets or sets how middleware applies decisions returned by Seneschal.
    /// </summary>
    public SeneschalEnforcementBehavior EnforcementBehavior { get; set; } =
        SeneschalEnforcementBehavior.HonorDecisionMode;

    internal void Validate()
    {
        if (BaseUrl is null || !BaseUrl.IsAbsoluteUri)
        {
            throw new InvalidOperationException(
                "Seneschal requires an absolute BaseUrl.");
        }

        if (IdentityResolver is null)
        {
            throw new InvalidOperationException(
                "Seneschal requires an IdentityResolver.");
        }
    }
}
