using Seneschal.Client;
using Seneschal.Client.Models;

public static class PolicySimulationCommand
{
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter? output = null,
        CancellationToken cancellationToken = default)
    {
        output ??= Console.Out;
        if (!PreflightOptions.TryParse(args, out var options, out var error, "simulation"))
        {
            await output.WriteLineAsync(error);
            return 1;
        }

        using var httpClient = new HttpClient();
        var report = await PolicySimulationRunner.RunAsync(
            options!,
            new HttpPreflightTransport(httpClient, options!),
            cancellationToken);
        await report.WriteAsync(output);
        return report.ExitCode;
    }
}

public static class PolicySimulationRunner
{
    public static async Task<PreflightReport> RunAsync(
        PreflightOptions options,
        IPreflightTransport transport,
        CancellationToken cancellationToken = default)
    {
        var lines = new List<string>();
        try
        {
            var result = await transport.EvaluateAsync(options, cancellationToken);
            lines.Add($"Identity:                    {options.Identity}");
            lines.Add($"Capability:                  {options.Capability}");
            lines.Add($"Environment:                 {options.Environment ?? "Not specified"}");
            lines.Add($"Resource:                    {options.Resource}");
            lines.Add($"Decision:                    {DisplayDecision(result.Decision)}");
            lines.Add($"Effective action:            {result.EffectiveAction}");
            lines.Add($"Execution Guidance:          {result.RawExecutionGuidance}");

            if (result.Guidance == ExecutionGuidanceKind.Unknown)
                return Failed(lines, PreflightFailureCategory.MalformedGuidance, "Invalid execution guidance; Would execute: No (fail closed)");

            lines.Add($"ShouldProceed/Would execute: {(result.ShouldProceed ? "Yes" : "No")}");
            lines.Add("Matched policies:");
            if (result.MatchedPolicies.Count == 0)
                lines.Add("  (none)");
            else
                lines.AddRange(result.MatchedPolicies.Select(policy => $"  - {policy}"));
            lines.Add($"Reason:                      {result.Reason}");
            lines.Add($"Approval status:             {result.ApprovalStatus ?? "Not applicable"}");
            lines.Add("Governance window:");
            if (string.IsNullOrWhiteSpace(result.GovernanceWindowName))
                lines.Add("  No governance window matched or influenced the result.");
            else
            {
                lines.Add($"  Name: {result.GovernanceWindowName}");
                lines.Add($"  Mode: {result.GovernanceWindowMode ?? "Not specified"}");
                lines.Add($"  Reason: {result.GovernanceWindowReason ?? "Not specified"}");
                lines.Add($"  Influenced result: {(result.GovernanceWindowInfluencedResult ? "Yes" : "No")}");
            }

            var category = IsApproval(result.Decision)
                ? PreflightFailureCategory.ApprovalRequired
                : IsDeny(result.Decision)
                    ? PreflightFailureCategory.EvaluationDenied
                    : PreflightFailureCategory.None;
            return new PreflightReport(lines, category, 0);
        }
        catch (PreflightTransportException exception)
        {
            return Failed(lines, exception.Category, exception.Message);
        }
    }

    private static PreflightReport Failed(
        List<string> lines,
        PreflightFailureCategory category,
        string message)
    {
        lines.Add($"Failure:                     {category}: {message}");
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
