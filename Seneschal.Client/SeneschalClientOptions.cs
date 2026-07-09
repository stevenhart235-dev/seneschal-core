namespace Seneschal.Client;

/// <summary>
/// Configuration options for <see cref="SeneschalClient"/>.
/// </summary>
public sealed record SeneschalClientOptions
{
    /// <summary>
    /// Gets the base URL of the Seneschal API.
    /// </summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>
    /// Gets the relative runtime evaluation endpoint path.
    /// </summary>
    public string EvaluatePath { get; set; } = "/evaluate";

    /// <summary>
    /// Gets an optional API key placeholder for future authenticated
    /// deployments.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets the request header name used when <see cref="ApiKey"/> is set.
    /// </summary>
    public string ApiKeyHeaderName { get; set; } = "X-Seneschal-Api-Key";
}
