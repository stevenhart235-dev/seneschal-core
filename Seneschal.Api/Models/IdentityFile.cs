namespace Seneschal.Api.Models;

public class IdentityFile
{
    public List<IdentityDefinition> Identities { get; set; } = new();
}