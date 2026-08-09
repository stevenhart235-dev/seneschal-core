using Seneschal.Client;
using Seneschal.Client.Models;

var baseUrl = args.Length > 0
    ? new Uri(args[0])
    : new Uri(
        Environment.GetEnvironmentVariable("SENESCHAL_URL")
        ?? "http://localhost:5000");

Console.WriteLine("Seneschal CapabilityControlDemo");
Console.WriteLine($"Runtime: {baseUrl}");
Console.WriteLine();

await RunScenarioAsync(
    "1. Allowed request with valid scoped API key and matching policy",
    apiKey: "dev-capability-control-key",
    request: BuildRequest("infrastructure.production.apply"),
    expected: "Decision: Allow");

await RunScenarioAsync(
    "2. Denied request when policy does not allow it",
    apiKey: "dev-capability-control-key",
    request: BuildRequest("infrastructure.production.destroy"),
    expected: "Decision: Deny, Effective Action: logged_only");

await RunScenarioAsync(
    "3. Rejected request when integration API key is missing",
    apiKey: null,
    request: BuildRequest("infrastructure.production.apply"),
    expected: "HTTP 401");

await RunScenarioAsync(
    "3b. Rejected request when integration API key is invalid",
    apiKey: "not-a-real-key",
    request: BuildRequest("infrastructure.production.apply"),
    expected: "HTTP 401");

await RunScenarioAsync(
    "4. Rejected request when API key is valid but not scoped for capability",
    apiKey: "dev-capability-control-limited-key",
    request: BuildRequest("infrastructure.production.apply"),
    expected: "HTTP 403");

static DecisionRequest BuildRequest(string capability)
{
    return new DecisionRequest
    {
        Identity = "platform-agent",
        Capability = capability,
        Context = new Dictionary<string, string>
        {
            ["environment"] = "production",
            ["resource"] = "prod-subscription"
        }
    };
}

async Task RunScenarioAsync(
    string title,
    string? apiKey,
    DecisionRequest request,
    string expected)
{
    Console.WriteLine(title);
    Console.WriteLine($"  Expected: {expected}");
    Console.WriteLine($"  Identity: {request.Identity}");
    Console.WriteLine($"  Capability: {request.Capability}");
    Console.WriteLine($"  Environment: {request.Context["environment"]}");
    Console.WriteLine($"  Resource: {request.Context["resource"]}");

    var client = SeneschalClient.Create(
        new HttpClient(),
        baseUrl,
        apiKey);

    try
    {
        var decision = await client.EvaluateAsync(request);

        Console.WriteLine(
            $"  Result: Decision: {FormatDecision(decision.Decision)}");
        Console.WriteLine($"  Enforcement Mode: {decision.Mode}");
        Console.WriteLine($"  Effective Action: {decision.EffectiveAction}");
        Console.WriteLine($"  Policy: {Fallback(decision.PolicyMatched)}");
        Console.WriteLine($"  Reason: {decision.Reason}");
        Console.WriteLine(
            "  Application Behavior: " + ToApplicationBehavior(decision));
    }
    catch (SeneschalClientException exception) when (exception.StatusCode is not null)
    {
        Console.WriteLine(
            $"  Result: HTTP {(int)exception.StatusCode.Value} {exception.StatusCode}");
        Console.WriteLine($"  Body: {Fallback(exception.ResponseBody)}");
        Console.WriteLine(
            "  Application Behavior: Request rejected before policy evaluation; no action executed.");
    }
    catch (SeneschalClientException exception)
    {
        Console.WriteLine($"  Result: Client error: {exception.Message}");
    }

    Console.WriteLine();
}

static string Fallback(string? value)
{
    return string.IsNullOrWhiteSpace(value)
        ? "n/a"
        : value;
}

static string ToApplicationBehavior(DecisionResult decision)
{
    if (decision.ShouldProceed &&
        !string.Equals(decision.Decision, "Allow", StringComparison.OrdinalIgnoreCase))
    {
        return "Monitor mode records the denial, but the simulated integration would proceed.";
    }

    return decision.ShouldProceed
        ? "Allowed; would apply infrastructure changes."
        : "Blocked; would not execute infrastructure changes.";
}

static string FormatDecision(string decision)
{
    return decision.ToLowerInvariant() switch
    {
        "allow" => "Allow",
        "deny" => "Deny",
        "pendingapproval" or "requires_approval" => "PendingApproval",
        _ => decision
    };
}
