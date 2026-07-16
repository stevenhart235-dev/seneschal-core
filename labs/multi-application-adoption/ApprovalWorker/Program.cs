using Seneschal.Client;
using Seneschal.Client.Models;

const string identity = "release-approval-worker";
const string capability = "production.release.approve";
const string environment = "production";
const string resource = "checkout-api";
const string apiKey = "dev-release-approval-worker-key";

var baseUrl = Environment.GetEnvironmentVariable("SENESCHAL_URL") ??
    "http://localhost:5000";
var intervalSeconds = ReadPositiveInteger("LAB_INTERVAL_SECONDS", 8);
var maxIterations = ReadPositiveInteger("LAB_MAX_ITERATIONS", int.MaxValue);
using var httpClient = new HttpClient();
var client = SeneschalClient.Create(httpClient, new Uri(baseUrl), apiKey);

Console.WriteLine(
    $"ApprovalWorker started; interval={intervalSeconds}s; Seneschal={baseUrl}");

var operationSequence = 1;
var operationId = OperationId(operationSequence);
for (var iteration = 1; iteration <= maxIterations; iteration++)
{
    var timestamp = DateTimeOffset.Now;

    try
    {
        var result = await client.EvaluateAsync(new DecisionRequest
        {
            Identity = identity,
            Capability = capability,
            OperationId = operationId,
            Context = new Dictionary<string, string>
            {
                ["environment"] = environment,
                ["resource"] = resource
            }
        });
        var pendingApproval = string.Equals(
            result.Decision,
            "requires_approval",
            StringComparison.OrdinalIgnoreCase);
        var enforce = string.Equals(
            result.Mode,
            "Enforce",
            StringComparison.OrdinalIgnoreCase);
        var allowed = string.Equals(
            result.Decision,
            "allow",
            StringComparison.OrdinalIgnoreCase);
        var blocked = enforce && !allowed;
        var projectedAction = blocked
            ? "blocked_pending_approval"
            : pendingApproval
                ? "executed_and_recorded"
                : result.EffectiveAction;

        Console.WriteLine(
            $"[{timestamp:O}] identity={identity} capability={capability} " +
            $"operationId={operationId} decision={result.Decision} mode={result.Mode} " +
            $"projectedAction={projectedAction} " +
            $"policy={Display(result.PolicyMatched)} " +
            $"reason=\"{result.Reason}\" " +
            $"operation={(blocked ? "BLOCKED" : "EXECUTED")}");

        if (allowed)
        {
            operationSequence++;
            operationId = OperationId(operationSequence);
            Console.WriteLine($"Next distinct demo operation: {operationId}");
        }
    }
    catch (SeneschalClientException exception)
    {
        Console.WriteLine(
            $"[{timestamp:O}] identity={identity} capability={capability} " +
            $"decision=unavailable mode=unknown projectedAction=blocked " +
            $"policy=n/a reason=\"{exception.Message}\" operation=BLOCKED");
    }

    if (iteration < maxIterations)
    {
        await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
    }
}

static string Display(string value) =>
    string.IsNullOrWhiteSpace(value) ? "n/a" : value;

static string OperationId(int sequence) => $"release-demo-{sequence:D4}";

static int ReadPositiveInteger(string name, int defaultValue) =>
    int.TryParse(Environment.GetEnvironmentVariable(name), out var value) &&
    value > 0
        ? value
        : defaultValue;
