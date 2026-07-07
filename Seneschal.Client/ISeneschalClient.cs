using Seneschal.Client.Models;

namespace Seneschal.Client;

/// <summary>
/// Provides runtime access to Seneschal capability decision evaluation.
/// </summary>
public interface ISeneschalClient
{
    /// <summary>
    /// Requests a capability decision from a running Seneschal instance.
    /// </summary>
    /// <param name="request">The capability decision request.</param>
    /// <param name="cancellationToken">
    /// A token used to cancel the HTTP request.
    /// </param>
    /// <returns>The decision returned by Seneschal.</returns>
    /// <exception cref="SeneschalClientException">
    /// Thrown when Seneschal cannot be reached, returns an unsuccessful
    /// response, or returns an invalid decision payload.
    /// </exception>
    Task<DecisionResult> EvaluateAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default);
}
