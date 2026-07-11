using Seneschal.Client;
using Seneschal.Client.Models;

const string identity = "migration-worker";
const string capability = "database.migration.execute";
const string environment = "production";
const string resource = "customer-db";
const string apiKey = "dev-migration-worker-key";

await RunAsync(
    identity,
    capability,
    environment,
    resource,
    apiKey,
    defaultIntervalSeconds: 7);

static async Task RunAsync(
    string identity,
    string capability,
    string environment,
    string resource,
    string apiKey,
    int defaultIntervalSeconds)
{
    var baseUrl = Environment.GetEnvironmentVariable("SENESCHAL_URL") ??
        "http://localhost:5000";
    var intervalSeconds = ReadPositiveInteger(
        "LAB_INTERVAL_SECONDS",
        defaultIntervalSeconds);
    var maxIterations = ReadPositiveInteger("LAB_MAX_ITERATIONS", int.MaxValue);
    using var httpClient = new HttpClient();
    var client = SeneschalClient.Create(httpClient, new Uri(baseUrl), apiKey);

    Console.WriteLine(
        $"DatabaseMigrationWorker started; interval={intervalSeconds}s; Seneschal={baseUrl}");

    for (var iteration = 1; iteration <= maxIterations; iteration++)
    {
        await EvaluateAndExecuteAsync(
            client,
            identity,
            capability,
            environment,
            resource);

        if (iteration < maxIterations)
        {
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
        }
    }
}

static async Task EvaluateAndExecuteAsync(
    ISeneschalClient client,
    string identity,
    string capability,
    string environment,
    string resource)
{
    var timestamp = DateTimeOffset.Now;

    try
    {
        var result = await client.EvaluateAsync(new DecisionRequest
        {
            Identity = identity,
            Capability = capability,
            Context = new Dictionary<string, string>
            {
                ["environment"] = environment,
                ["resource"] = resource
            }
        });
        var blocked = IsEnforce(result.Mode) && !IsAllow(result.Decision);
        var outcome = blocked ? "BLOCKED" : "EXECUTED";

        Console.WriteLine(
            $"[{timestamp:O}] identity={identity} capability={capability} " +
            $"decision={result.Decision} mode={result.Mode} " +
            $"effectiveAction={result.EffectiveAction} " +
            $"policy={Display(result.PolicyMatched)} " +
            $"reason=\"{result.Reason}\" operation={outcome}");
    }
    catch (SeneschalClientException exception)
    {
        Console.WriteLine(
            $"[{timestamp:O}] identity={identity} capability={capability} " +
            $"decision=unavailable mode=unknown effectiveAction=blocked " +
            $"policy=n/a reason=\"{exception.Message}\" operation=BLOCKED");
    }
}

static bool IsAllow(string decision) =>
    string.Equals(decision, "allow", StringComparison.OrdinalIgnoreCase);

static bool IsEnforce(string mode) =>
    string.Equals(mode, "Enforce", StringComparison.OrdinalIgnoreCase);

static string Display(string value) =>
    string.IsNullOrWhiteSpace(value) ? "n/a" : value;

static int ReadPositiveInteger(string name, int defaultValue) =>
    int.TryParse(Environment.GetEnvironmentVariable(name), out var value) &&
    value > 0
        ? value
        : defaultValue;
