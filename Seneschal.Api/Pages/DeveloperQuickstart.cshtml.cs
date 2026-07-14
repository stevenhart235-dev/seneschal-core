using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Api.Services;

namespace Seneschal.Api.Pages;

public sealed class DeveloperQuickstartModel : PageModel
{
    private readonly CapabilityLoader _capabilities;
    private readonly IntegrationApiKeyLoader _keys;
    private readonly PolicyLoader _policies;

    public DeveloperQuickstartModel(
        CapabilityLoader capabilities,
        IntegrationApiKeyLoader keys,
        PolicyLoader policies)
    {
        _capabilities = capabilities;
        _keys = keys;
        _policies = policies;
    }

    public string QuickstartJson { get; private set; } = "[]";

    public void OnGet()
    {
        var developmentKeys = _keys.GetKeys()
            .Where(key =>
                key.Enabled &&
                key.Key.StartsWith("dev-", StringComparison.Ordinal))
            .ToList();
        var policies = _policies.GetPolicies();

        var options = _capabilities.GetCapabilities()
            .OrderBy(capability => string.IsNullOrWhiteSpace(capability.DisplayName)
                ? capability.Name
                : capability.DisplayName)
            .Select(capability =>
            {
                var policy = policies.FirstOrDefault(item => string.Equals(
                    item.Capability,
                    capability.Name,
                    StringComparison.OrdinalIgnoreCase));
                var keys = developmentKeys
                    .Where(key => key.AllowedCapabilities.Any(allowed =>
                        allowed == "*" || string.Equals(
                            allowed,
                            capability.Name,
                            StringComparison.OrdinalIgnoreCase)))
                    .Select(key => new QuickstartKey(
                        key.Name,
                        key.Key,
                        key.AllowedIdentities.FirstOrDefault() ?? policy?.Identity ?? string.Empty,
                        key.Environment ?? policy?.Environment ?? "dev"))
                    .ToList();

                return new QuickstartCapability(
                    capability.Name,
                    string.IsNullOrWhiteSpace(capability.DisplayName)
                        ? capability.Name
                        : capability.DisplayName,
                    keys,
                    policy?.Identity ?? string.Empty,
                    policy?.Environment ?? "dev",
                    ResourceFor(capability.Name));
            })
            .ToList();

        QuickstartJson = JsonSerializer.Serialize(
            options,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static string ResourceFor(string capabilityId)
    {
        return capabilityId switch
        {
            "production.deployment.execute" => "checkout-api",
            "infrastructure.production.apply" => "prod-subscription",
            "infrastructure.production.destroy" => "prod-subscription",
            "database.migration.execute" => "customer-db",
            "payments.refund.create" => "merchant-123",
            "production.release.approve" => "checkout-api",
            "azure.keyvault.secret.read" => "prod",
            "DeployApplication" => "checkout-api",
            "DeleteProductionDatabase" => "customer-db",
            _ => string.Empty
        };
    }
}

public sealed record QuickstartCapability(
    string Id,
    string DisplayName,
    IReadOnlyCollection<QuickstartKey> Keys,
    string PolicyIdentity,
    string Environment,
    string Resource);

public sealed record QuickstartKey(
    string Name,
    string Value,
    string Identity,
    string Environment);
