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
    /// Gets or sets behavior when Seneschal cannot return a valid decision.
    /// </summary>
    public SeneschalFailureBehavior FailureBehavior { get; set; } =
        SeneschalFailureBehavior.FailClosed;

    /// <summary>
    /// Gets or sets the timeout for calls to the Seneschal API.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets how middleware applies decisions returned by Seneschal.
    /// </summary>
    public SeneschalEnforcementBehavior EnforcementBehavior { get; set; } =
        SeneschalEnforcementBehavior.HonorDecisionMode;

    internal void Validate()
    {
        if (BaseUrl is null)
        {
            throw new InvalidOperationException(
                "Seneschal configuration is invalid: BaseUrl is required.");
        }

        if (!BaseUrl.IsAbsoluteUri)
        {
            throw new InvalidOperationException(
                "Seneschal configuration is invalid: BaseUrl must be an absolute URI.");
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException(
                "Seneschal configuration is invalid: ApiKey is required.");
        }

        if (DefaultEnvironment is not null &&
            (string.IsNullOrWhiteSpace(DefaultEnvironment) ||
             DefaultEnvironment.Any(character =>
                 !char.IsLetterOrDigit(character) &&
                 character is not '.' and not '_' and not '-')))
        {
            throw new InvalidOperationException(
                "Seneschal configuration is invalid: DefaultEnvironment may contain only letters, numbers, '.', '_' or '-'.");
        }

        if (Timeout <= TimeSpan.Zero &&
            Timeout != System.Threading.Timeout.InfiniteTimeSpan)
        {
            throw new InvalidOperationException(
                "Seneschal configuration is invalid: Timeout must be positive or infinite.");
        }

        if (IdentityResolver is null)
        {
            throw new InvalidOperationException(
                "Seneschal requires an IdentityResolver.");
        }
    }
}
