using System.Net.Http.Json;
using System.Text.Json;
using Seneschal.Client;
using Seneschal.Client.Models;

public static class PreflightCommand
{
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter? output = null,
        CancellationToken cancellationToken = default)
    {
        output ??= Console.Out;
        if (!PreflightOptions.TryParse(args, out var options, out var error))
        {
            await output.WriteLineAsync(error);
            return 1;
        }

        using var httpClient = new HttpClient();
        var transport = new HttpPreflightTransport(httpClient, options!);
        var report = await PreflightRunner.RunAsync(
            options!,
            transport,
            cancellationToken);
        await report.WriteAsync(output);
        return report.ExitCode;
    }
}

public sealed record PreflightOptions(
    Uri BaseUrl,
    string ApiKey,
    string Identity,
    string Capability,
    string? Environment,
    string Resource)
{
    public static bool TryParse(
        string[] args,
        out PreflightOptions? options,
        out string error)
    {
        options = null;
        error = "";
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                error = "Preflight arguments must use --name value pairs.";
                return false;
            }
            values[args[index][2..]] = args[index + 1];
        }

        if (!values.TryGetValue("url", out var url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var baseUrl) ||
            !values.TryGetValue("api-key", out var apiKey) || string.IsNullOrWhiteSpace(apiKey) ||
            !values.TryGetValue("identity", out var identity) || string.IsNullOrWhiteSpace(identity) ||
            !values.TryGetValue("capability", out var capability) || string.IsNullOrWhiteSpace(capability))
        {
            error = "Required: --url, --api-key, --identity, and --capability.";
            return false;
        }

        values.TryGetValue("environment", out var environment);
        var resource = values.GetValueOrDefault("resource", "preflight");
        options = new PreflightOptions(
            baseUrl,
            apiKey,
            identity,
            capability,
            environment,
            resource);
        return true;
    }
}

public enum PreflightFailureCategory
{
    None,
    EndpointUnavailable,
    ServiceNotReady,
    AuthenticationFailure,
    InvalidIdentity,
    InvalidCapability,
    ScopeMismatch,
    EvaluationDenied,
    ApprovalRequired,
    MalformedGuidance
}

public sealed record ServiceProbe(bool IsSuccessful, string Status);

public interface IPreflightTransport
{
    Task<ServiceProbe> CheckHealthAsync(CancellationToken cancellationToken);
    Task<ServiceProbe> CheckReadinessAsync(CancellationToken cancellationToken);
    Task<DecisionResult> EvaluateAsync(
        PreflightOptions options,
        CancellationToken cancellationToken);
}

public sealed class PreflightTransportException(
    PreflightFailureCategory category,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public PreflightFailureCategory Category { get; } = category;
}

public sealed record PreflightReport(
    IReadOnlyList<string> Lines,
    PreflightFailureCategory Category,
    int ExitCode)
{
    public async Task WriteAsync(TextWriter output)
    {
        foreach (var line in Lines)
            await output.WriteLineAsync(line);
    }
}

public static class PreflightRunner
{
    public static async Task<PreflightReport> RunAsync(
        PreflightOptions options,
        IPreflightTransport transport,
        CancellationToken cancellationToken = default)
    {
        var lines = new List<string>();
        try
        {
            var health = await transport.CheckHealthAsync(cancellationToken);
            lines.Add("Endpoint:       OK");
            lines.Add($"Health:         {health.Status}");
            if (!health.IsSuccessful)
                return Failed(lines, PreflightFailureCategory.ServiceNotReady, "Health check failed");

            var readiness = await transport.CheckReadinessAsync(cancellationToken);
            lines.Add($"Readiness:      {readiness.Status}");
            if (!readiness.IsSuccessful)
                return Failed(lines, PreflightFailureCategory.ServiceNotReady, "Service not ready");

            var result = await transport.EvaluateAsync(options, cancellationToken);
            lines.Add("Authentication: OK");
            lines.Add($"Identity:       {options.Identity}");
            lines.Add($"Capability:     {options.Capability}");
            lines.Add($"Decision:       {DisplayDecision(result.Decision)}");
            lines.Add($"Guidance:       {result.RawExecutionGuidance}");

            if (result.Guidance == ExecutionGuidanceKind.Unknown)
                return Failed(lines, PreflightFailureCategory.MalformedGuidance, "Invalid execution guidance");

            var category = IsApproval(result.Decision)
                ? PreflightFailureCategory.ApprovalRequired
                : IsDeny(result.Decision)
                    ? PreflightFailureCategory.EvaluationDenied
                    : PreflightFailureCategory.None;
            if (category == PreflightFailureCategory.ApprovalRequired)
                lines.Add("Governance:     Approval required");
            else if (category == PreflightFailureCategory.EvaluationDenied)
                lines.Add("Governance:     Denied");
            lines.Add($"Execution:      {(result.ShouldProceed ? "Proceed" : "Stop")}");
            lines.Add("Integration:    Ready");
            return new PreflightReport(lines, category, 0);
        }
        catch (PreflightTransportException exception)
        {
            if (exception.Category == PreflightFailureCategory.EndpointUnavailable)
                lines.Add("Endpoint:       Unavailable");
            else if (exception.Category == PreflightFailureCategory.AuthenticationFailure)
                lines.Add("Authentication: Failed");
            else if (exception.Category is PreflightFailureCategory.InvalidIdentity or
                     PreflightFailureCategory.InvalidCapability or
                     PreflightFailureCategory.ScopeMismatch)
            {
                lines.Add("Authentication: OK");
                lines.Add($"Identity:       {options.Identity}");
                lines.Add($"Capability:     {options.Capability}");
            }
            return Failed(lines, exception.Category, exception.Message);
        }
    }

    private static PreflightReport Failed(
        List<string> lines,
        PreflightFailureCategory category,
        string message)
    {
        lines.Add($"Failure:        {category}: {message}");
        lines.Add("Integration:    Not ready");
        return new PreflightReport(lines, category, 2);
    }

    private static bool IsDeny(string decision) =>
        string.Equals(decision, "deny", StringComparison.OrdinalIgnoreCase);

    private static bool IsApproval(string decision) =>
        string.Equals(decision, "requires_approval", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(decision, "PendingApproval", StringComparison.OrdinalIgnoreCase);

    private static string DisplayDecision(string decision) =>
        decision.ToLowerInvariant() switch
        {
            "allow" => "Allow",
            "deny" => "Deny",
            "requires_approval" or "pendingapproval" => "RequireApproval",
            _ when string.IsNullOrWhiteSpace(decision) => "Unknown",
            _ => decision
        };
}

internal sealed class HttpPreflightTransport : IPreflightTransport
{
    private readonly HttpClient _httpClient;
    private readonly SeneschalClient _client;
    private readonly Uri _baseUrl;

    public HttpPreflightTransport(HttpClient httpClient, PreflightOptions options)
    {
        _httpClient = httpClient;
        _baseUrl = options.BaseUrl;
        _client = SeneschalClient.Create(httpClient, options.BaseUrl, options.ApiKey);
    }

    public Task<ServiceProbe> CheckHealthAsync(CancellationToken cancellationToken) =>
        ProbeAsync("/health", "healthy", cancellationToken);

    public Task<ServiceProbe> CheckReadinessAsync(CancellationToken cancellationToken) =>
        ProbeAsync("/ready", "ready", cancellationToken);

    public async Task<DecisionResult> EvaluateAsync(
        PreflightOptions options,
        CancellationToken cancellationToken)
    {
        var request = new DecisionRequest
        {
            Identity = options.Identity,
            Capability = options.Capability,
            Context = new Dictionary<string, string>
            {
                ["resource"] = options.Resource
            }
        };
        if (!string.IsNullOrWhiteSpace(options.Environment))
            request.Context["environment"] = options.Environment;

        try
        {
            return await _client.PreflightAsync(request, cancellationToken);
        }
        catch (SeneschalClientException exception)
        {
            var code = ParseErrorCode(exception.ResponseBody);
            var category = code switch
            {
                "authentication_failure" => PreflightFailureCategory.AuthenticationFailure,
                "invalid_identity" => PreflightFailureCategory.InvalidIdentity,
                "invalid_capability" => PreflightFailureCategory.InvalidCapability,
                "scope_mismatch" => PreflightFailureCategory.ScopeMismatch,
                _ => PreflightFailureCategory.ServiceNotReady
            };
            throw new PreflightTransportException(
                category,
                exception.Message,
                exception);
        }
    }

    private async Task<ServiceProbe> ProbeAsync(
        string path,
        string expectedStatus,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                new Uri(_baseUrl, path),
                cancellationToken);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            var status = body.TryGetProperty("status", out var value)
                ? value.GetString() ?? "unknown"
                : "unknown";
            var displayStatus = string.IsNullOrWhiteSpace(status)
                ? "Unknown"
                : char.ToUpperInvariant(status[0]) + status[1..];
            return new ServiceProbe(
                response.IsSuccessStatusCode && string.Equals(
                    status,
                    expectedStatus,
                    StringComparison.OrdinalIgnoreCase),
                displayStatus);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            throw new PreflightTransportException(
                PreflightFailureCategory.EndpointUnavailable,
                "Seneschal endpoint is unavailable.",
                exception);
        }
    }

    private static string? ParseErrorCode(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return null;
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return document.RootElement.TryGetProperty("code", out var code)
                ? code.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
