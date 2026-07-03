using System.Net.Http.Json;

if (args.Length < 3)
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  seneschal evaluate <identity> <capability> <environment>");
    return;
}

var command = args[0];

if (!command.Equals("evaluate", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine($"Unknown command: {command}");
    return;
}

var identity = args[1];
var capability = args[2];
var environment = args[3];

var request = new
{
    identity,
    capability,
    context = new Dictionary<string, string>
    {
        ["environment"] = environment,
        ["source"] = "cli"
    }
};

var client = new HttpClient
{
    BaseAddress = new Uri("http://localhost:5077")
};

var result = await client.PostAsJsonAsync("/evaluate", request);
var body = await result.Content.ReadFromJsonAsync<DecisionResult>();

if (body is null)
{
    Console.WriteLine("No response returned.");
    return;
}

Console.WriteLine();
Console.WriteLine("Seneschal Decision");
Console.WriteLine("------------------");
Console.WriteLine($"Identity:         {identity}");
Console.WriteLine($"Capability:       {capability}");
Console.WriteLine($"Environment:      {environment}");
Console.WriteLine($"Decision:         {body.Decision}");
Console.WriteLine($"Effective Action: {body.EffectiveAction}");
Console.WriteLine($"Mode:             {body.Mode}");
Console.WriteLine($"Policy Matched:   {body.PolicyMatched}");
Console.WriteLine($"Reason:           {body.Reason}");
Console.WriteLine($"Duration:         {body.DurationMs} ms");

public class DecisionResult
{
    public string Decision { get; set; } = "";
    public string Reason { get; set; } = "";
    public string PolicyMatched { get; set; } = "";
    public long DurationMs { get; set; }
    public string EffectiveAction { get; set; } = "";
    public string Mode { get; set; } = "";
}