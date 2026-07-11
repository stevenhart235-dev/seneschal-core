using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Seneschal.Client.Models;

namespace Seneschal.Client;

/// <summary>
/// HTTP implementation of <see cref="ISeneschalClient"/>.
/// </summary>
public sealed class SeneschalClient : ISeneschalClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private const string ClientUserAgent = "Seneschal.Client/0.1.0-alpha.1";

    private readonly HttpClient _httpClient;
    private readonly SeneschalClientOptions _options;
    private readonly Uri _baseUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeneschalClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to call Seneschal.</param>
    /// <param name="options">Client configuration options.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="httpClient"/> or <paramref name="options"/>
    /// is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when no base URL is provided by options or the HTTP client.
    /// </exception>
    public SeneschalClient(
        HttpClient httpClient,
        IOptions<SeneschalClientOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        var value = options.Value;

        _httpClient = httpClient;
        _options = value;
        _baseUrl = value.BaseUrl ?? httpClient.BaseAddress ??
            throw new ArgumentException(
                "A Seneschal base URL must be provided.",
                nameof(options));
    }

    /// <summary>
    /// Creates a client from explicit connection settings.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to call Seneschal.</param>
    /// <param name="baseUrl">The base URL of the Seneschal API.</param>
    /// <param name="apiKey">
    /// Optional API key placeholder for future authenticated deployments.
    /// </param>
    /// <returns>A configured Seneschal client.</returns>
    public static SeneschalClient Create(
        HttpClient httpClient,
        Uri baseUrl,
        string? apiKey = null)
    {
        return new SeneschalClient(
            httpClient,
            Options.Create(new SeneschalClientOptions
            {
                BaseUrl = baseUrl,
                ApiKey = apiKey
            }));
    }

    /// <inheritdoc />
    public async Task<DecisionResult> EvaluateAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            BuildEvaluationUri());
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);
        httpRequest.Headers.UserAgent.ParseAdd(ClientUserAgent);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            httpRequest.Headers.TryAddWithoutValidation(
                _options.ApiKeyHeaderName,
                _options.ApiKey);
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException)
        {
            throw new SeneschalClientException(
                exception is TaskCanceledException
                    ? "The Seneschal evaluation request timed out."
                    : "Unable to reach the Seneschal runtime.",
                exception);
        }

        using var _ = response;

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(
                cancellationToken);

            throw new SeneschalClientException(
                $"Seneschal returned HTTP {(int)response.StatusCode} " +
                $"{response.StatusCode}.",
                response.StatusCode,
                responseBody);
        }

        try
        {
            var decision = await response.Content.ReadFromJsonAsync<
                DecisionResult>(
                JsonOptions,
                cancellationToken);

            return decision ?? throw new SeneschalClientException(
                "Seneschal returned an empty decision response.");
        }
        catch (JsonException exception)
        {
            throw new SeneschalClientException(
                "Seneschal returned an invalid decision response.",
                exception);
        }
    }

    private Uri BuildEvaluationUri()
    {
        return new Uri(_baseUrl, _options.EvaluatePath);
    }
}
