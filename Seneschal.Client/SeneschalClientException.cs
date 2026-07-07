using System.Net;

namespace Seneschal.Client;

/// <summary>
/// Represents an error returned or encountered by the Seneschal client.
/// </summary>
public sealed class SeneschalClientException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SeneschalClientException"/>
    /// class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public SeneschalClientException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SeneschalClientException"/>
    /// class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public SeneschalClientException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SeneschalClientException"/>
    /// class for an unsuccessful HTTP response.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="statusCode">The HTTP status code returned by Seneschal.</param>
    /// <param name="responseBody">The response body returned by Seneschal.</param>
    public SeneschalClientException(
        string message,
        HttpStatusCode statusCode,
        string? responseBody)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    /// <summary>
    /// Gets the HTTP status code returned by Seneschal, when available.
    /// </summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// Gets the response body returned by Seneschal, when available.
    /// </summary>
    public string? ResponseBody { get; }
}
