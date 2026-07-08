namespace Seneschal.Core.Models;

public sealed record ConfigurationValidationResult
{
    public IReadOnlyCollection<ConfigurationValidationFinding> Findings { get; init; } =
        Array.Empty<ConfigurationValidationFinding>();

    public bool IsValid => ErrorCount == 0;

    public int ErrorCount => CountBySeverity("Error");

    public int WarningCount => CountBySeverity("Warning");

    public int InfoCount => CountBySeverity("Info");

    private int CountBySeverity(string severity)
    {
        return Findings.Count(finding => string.Equals(
            finding.Severity,
            severity,
            StringComparison.OrdinalIgnoreCase));
    }
}
