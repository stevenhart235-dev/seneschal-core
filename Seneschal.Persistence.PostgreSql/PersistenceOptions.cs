namespace Seneschal.Persistence.PostgreSql;

public sealed class PersistenceOptions
{
    public const string SectionName = "Seneschal:Persistence";

    public string Provider { get; set; } = "InMemory";
}
