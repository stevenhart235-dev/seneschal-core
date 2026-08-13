using System.Net.Http.Json;
using System.Text.Json;
using Seneschal.Api.Models;
using Seneschal.Api.Services;
using YamlDotNet.Serialization;

public static class PolicyChangeSimulationCommand
{
    public static async Task<int> RunAsync(string[] args, TextWriter? output = null,
        HttpClient? client = null, CancellationToken cancellationToken = default)
    {
        output ??= Console.Out;
        if (!TryOptions(args, out var options, out var error))
        { await output.WriteLineAsync(error); return 1; }
        try
        {
            var proposal = ReadProposal(options!.Path);
            client ??= new HttpClient();
            client.DefaultRequestHeaders.Remove("X-Seneschal-Api-Key");
            client.DefaultRequestHeaders.Add("X-Seneschal-Api-Key", options.ApiKey);
            var request = new ProposedGovernanceChangeSimulationRequest
            { Proposal=proposal, Identity=options.Identity, Capability=options.Capability,
              OperationId=options.OperationId, Context=new Dictionary<string,string>
              { ["environment"]=options.Environment, ["resource"]=options.Resource } };
            using var response = await client.PostAsJsonAsync(
                $"{options.Url.TrimEnd('/')}/policy-changes/simulate", request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            { await output.WriteLineAsync($"Proposed-change simulation: FAILED ({(int)response.StatusCode})");
              using var failure=JsonDocument.Parse(json); await output.WriteLineAsync(failure.RootElement.ToString()); return 2; }
            var result=JsonSerializer.Deserialize<ProposedChangeSimulationOutcome>(json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            await output.WriteLineAsync("Proposed-change simulation: VALID");
            await output.WriteLineAsync($"Current fingerprint:  {result.CurrentGovernanceConfigurationFingerprint}");
            await output.WriteLineAsync($"Proposed fingerprint: {result.ProposedGovernanceConfigurationFingerprint} (hypothetical)");
            await output.WriteLineAsync($"Current:  {result.Current!.Decision} / {result.Current.ExecutionGuidance} / {result.Current.WinningPolicy}");
            await output.WriteLineAsync($"Proposed: {result.Proposed!.Decision} / {result.Proposed.ExecutionGuidance} / {result.Proposed.WinningPolicy}");
            await output.WriteLineAsync("Differences:");
            foreach(var difference in result.Differences)
                await output.WriteLineAsync($"  {difference.Field}: {difference.Current} -> {difference.Proposed}");
            var impact=result.StaticGovernanceContextComparison!;
            await output.WriteLineAsync("Static governance context comparison:");
            await output.WriteLineAsync($"  Configured capabilities: {impact.CurrentConfiguredCapabilities} -> {impact.ProposedConfiguredCapabilities} ({impact.ConfiguredCapabilityDifference:+#;-#;0})");
            await output.WriteLineAsync($"  Critical: {impact.CurrentCriticalCapabilities} -> {impact.ProposedCriticalCapabilities} ({impact.CriticalDifference:+#;-#;0})");
            await output.WriteLineAsync($"  High: {impact.CurrentHighCapabilities} -> {impact.ProposedHighCapabilities} ({impact.HighDifference:+#;-#;0})");
            await output.WriteLineAsync($"  {impact.Limitation}");
            return 0;
        }
        catch(Exception exception) when (exception is IOException or JsonException or YamlDotNet.Core.YamlException)
        { await output.WriteLineAsync($"Proposed-change simulation: FAILED`n{exception.Message}"); return 2; }
    }
    private static ProposedGovernanceChange ReadProposal(string path)
    {
        var text=File.ReadAllText(Path.GetFullPath(path));
        if(Path.GetExtension(path).Equals(".json",StringComparison.OrdinalIgnoreCase))
            return JsonSerializer.Deserialize<ProposedGovernanceChange>(text,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var value=new DeserializerBuilder().WithAttemptingUnquotedStringTypeDeserialization().Build().Deserialize<object>(text);
        return JsonSerializer.Deserialize<ProposedGovernanceChange>(JsonSerializer.Serialize(value),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
    private static bool TryOptions(string[] args,out Options? option,out string error)
    {
        option=null; error="Usage: seneschal policy change simulate <proposal-path> --url <url> --api-key <key> --identity <id> --capability <id> --environment <name> [--resource <id>] [--operation-id <id>]";
        if(args.Length<1)return false; var values=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        for(var i=1;i<args.Length;i+=2){if(i+1>=args.Length)return false;values[args[i]]=args[i+1];}
        string Get(string key)=>values.GetValueOrDefault(key,"");
        if(new[]{"--url","--api-key","--identity","--capability","--environment"}.Any(k=>string.IsNullOrWhiteSpace(Get(k))))return false;
        option=new(args[0],Get("--url"),Get("--api-key"),Get("--identity"),Get("--capability"),Get("--environment"),Get("--resource") is {Length:>0} r?r:"proposed-change",Get("--operation-id") is {Length:>0} o?o:null);error="";return true;
    }
    private sealed record Options(string Path,string Url,string ApiKey,string Identity,string Capability,string Environment,string Resource,string? OperationId);
}
