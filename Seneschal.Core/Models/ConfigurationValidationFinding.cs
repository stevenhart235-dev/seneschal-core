namespace Seneschal.Core.Models;

public sealed record ConfigurationValidationFinding
{
    public required string Severity { get; init; }

    public required string Category { get; init; }

    public required string Message { get; init; }

    public string? RelatedObjectId { get; init; }
}
