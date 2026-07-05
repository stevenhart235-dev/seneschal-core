namespace Seneschal.Core.Models;

public sealed record Intent
{
    public required string Action { get; init; }
    public required string Reason { get; init; }
}