using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Seneschal.Core.Models;
using YamlDotNet.Core;

public static class PolicyValidationCommand
{
    public static async Task<int> RunAsync(string[] args, TextWriter? output = null)
    {
        output ??= Console.Out;
        if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
        {
            await output.WriteLineAsync("Usage: seneschal policy validate <path>");
            return 1;
        }

        var policyPath = Path.GetFullPath(args[0]);
        var directory = Path.GetDirectoryName(policyPath) ?? Directory.GetCurrentDirectory();
        var identityPath = Path.Combine(directory, "identities.yaml");
        var capabilityPath = Path.Combine(directory, "capabilities.yaml");
        var loadFindings = new List<(string File, string Issue)>();

        var policies = TryLoad(policyPath, () => new PolicyLoader(policyPath, rejectUnmatchedProperties: true).GetPolicies(), loadFindings);
        var identities = TryLoad(identityPath, () => new IdentityLoader(identityPath).GetIdentities(), loadFindings);
        var capabilities = TryLoad(capabilityPath, () => new CapabilityLoader(capabilityPath).GetCapabilities(), loadFindings);

        ConfigurationValidationResult? result = null;
        if (loadFindings.Count == 0)
        {
            result = ConfigurationValidator.Validate(
                capabilities!, identities!, policies!,
                new RuntimeSettings { Mode = EnforcementMode.LogOnly });
        }

        var errorCount = loadFindings.Count + (result?.ErrorCount ?? 0);
        var warningCount = result?.WarningCount ?? 0;
        await output.WriteLineAsync($"Policy validation: {(errorCount == 0 ? "PASSED" : "FAILED")}");
        await output.WriteLineAsync();

        foreach (var finding in loadFindings)
            await WriteFindingAsync(output, "ERROR", finding.File, null, finding.Issue);

        if (result is not null)
        {
            foreach (var finding in result.Findings.Where(f =>
                f.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase) ||
                f.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase)))
            {
                var file = finding.Category.StartsWith("Identity", StringComparison.Ordinal)
                    ? identityPath
                    : policyPath;
                await WriteFindingAsync(output, finding.Severity.ToUpperInvariant(), file,
                    finding.RelatedObjectId, finding.Message);
            }
        }

        await output.WriteLineAsync($"{errorCount} {Plural(errorCount, "error")}, {warningCount} {Plural(warningCount, "warning")}");
        return errorCount == 0 ? 0 : 2;
    }

    private static T? TryLoad<T>(string path, Func<T> load, ICollection<(string File, string Issue)> findings)
        where T : class
    {
        try
        {
            if (!File.Exists(path))
            {
                findings.Add((path, "Configuration file could not be loaded because it does not exist."));
                return null;
            }
            return load();
        }
        catch (YamlException exception)
        {
            findings.Add((path, $"Malformed or unsupported YAML at line {exception.Start.Line}, column {exception.Start.Column}."));
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or NullReferenceException)
        {
            findings.Add((path, "Configuration file could not be loaded."));
            return null;
        }
    }

    private static async Task WriteFindingAsync(TextWriter output, string severity, string file, string? policyId, string issue)
    {
        await output.WriteLineAsync($"{severity}  {file}");
        if (!string.IsNullOrWhiteSpace(policyId))
            await output.WriteLineAsync($"       Policy: {policyId}");
        await output.WriteLineAsync($"       {issue}");
        await output.WriteLineAsync();
    }

    private static string Plural(int count, string word) => count == 1 ? word : word + "s";
}
