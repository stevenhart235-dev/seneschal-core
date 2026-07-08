using System.Net;
using System.Text;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using ApiPolicy = Seneschal.Api.Models.Policy;

namespace Seneschal.Api.Services;

public static class PolicyExplorerPageRenderer
{
    public static async Task<string> RenderAsync(
        IReadOnlyList<ApiPolicy> policies,
        IGovernanceGraph governanceGraph,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(governanceGraph);

        var relationships = await governanceGraph.QueryAsync(
            new GovernanceRelationshipQuery(),
            cancellationToken);

        var cards = policies
            .Select((policy, index) => CreateCard(
                policy,
                policies.Count - index,
                relationships))
            .ToList();

        var html = new StringBuilder();

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"utf-8\" />");
        html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        html.AppendLine("    <title>Seneschal Policy Explorer</title>");
        html.AppendLine("    <link rel=\"stylesheet\" href=\"/styles.css\" />");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("    <div class=\"app-shell\">");
        html.AppendLine("        <aside class=\"sidebar\">");
        html.AppendLine("            <div class=\"sidebar-brand\">Seneschal</div>");
        html.AppendLine("            <nav class=\"sidebar-nav\" aria-label=\"Primary navigation\">");
        html.AppendLine("                <a href=\"/dashboard\">Dashboard</a>");
        html.AppendLine("                <a href=\"/monitor\">Monitor</a>");
        html.AppendLine("                <a href=\"/capability-explorer\">Capabilities</a>");
        html.AppendLine("                <a class=\"active\" href=\"/policies\">Policies</a>");
        html.AppendLine("                <a href=\"/identities\">Identities</a>");
        html.AppendLine("                <a href=\"#\">Resources</a>");
        html.AppendLine("                <a href=\"/audit\">Audit</a>");
        html.AppendLine("            </nav>");
        html.AppendLine("        </aside>");
        html.AppendLine("        <main class=\"container explorer-page\">");
        html.AppendLine("            <header class=\"page-header\">");
        html.AppendLine("                <h1>Seneschal Policy Explorer</h1>");
        html.AppendLine("                <p class=\"subtitle\">Review configured policies and their projected governance relationships.</p>");
        html.AppendLine("            </header>");
        html.AppendLine("            <section class=\"policy-grid\">");

        foreach (var card in cards)
        {
            AppendPolicyCard(html, card);
        }

        html.AppendLine("            </section>");
        html.AppendLine("            <footer class=\"app-footer\">Seneschal v0.2.1-alpha</footer>");
        html.AppendLine("        </main>");
        html.AppendLine("    </div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    private static PolicyCard CreateCard(
        ApiPolicy policy,
        int priority,
        IReadOnlyCollection<GovernanceRelationship> relationships)
    {
        var policyRelationships = relationships
            .Where(relationship =>
                relationship.From.Type == GovernanceEntityType.Policy &&
                string.Equals(
                    relationship.From.Id,
                    policy.Name,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new PolicyCard(
            policy,
            priority,
            RelatedEntityIds(
                policyRelationships,
                GovernanceRelationshipType.PolicyAppliesToIdentity,
                GovernanceEntityType.Identity),
            RelatedEntityIds(
                policyRelationships,
                GovernanceRelationshipType.PolicyAppliesToCapability,
                GovernanceEntityType.Capability),
            RelatedEntityIds(
                policyRelationships,
                GovernanceRelationshipType.PolicyAppliesToResource,
                GovernanceEntityType.Resource));
    }

    private static IReadOnlyCollection<string> RelatedEntityIds(
        IEnumerable<GovernanceRelationship> relationships,
        GovernanceRelationshipType relationshipType,
        GovernanceEntityType entityType)
    {
        return relationships
            .Where(relationship => relationship.Type == relationshipType)
            .SelectMany(relationship => new[]
            {
                relationship.From,
                relationship.To
            })
            .Where(entity => entity.Type == entityType)
            .Select(entity => string.IsNullOrWhiteSpace(entity.Scope)
                ? entity.Id
                : $"{entity.Scope}:{entity.Id}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AppendPolicyCard(
        StringBuilder html,
        PolicyCard card)
    {
        html.AppendLine("                <article class=\"policy-card\">");
        html.AppendLine("                    <div class=\"policy-card-header\">");
        html.Append("                        <h2>")
            .Append(Encode(card.Policy.Name))
            .AppendLine("</h2>");
        html.Append("                        <span class=\"badge decision-badge decision-")
            .Append(Encode(CssClass(card.Policy.Decision)))
            .Append("\">")
            .Append(Encode(card.Policy.Decision))
            .AppendLine("</span>");
        html.AppendLine("                    </div>");
        html.Append("                    <p class=\"policy-reason\">")
            .Append(Encode(card.Policy.Reason))
            .AppendLine("</p>");
        html.Append("                    <p class=\"muted\">Priority: ")
            .Append(card.Priority)
            .AppendLine("</p>");
        html.AppendLine("                    <div class=\"policy-relations\">");
        AppendRelationGroup(html, "Related identities", card.Identities);
        AppendRelationGroup(html, "Related capabilities", card.Capabilities);
        AppendRelationGroup(html, "Related resources", card.Resources);
        html.AppendLine("                    </div>");
        html.AppendLine("                </article>");
    }

    private static void AppendRelationGroup(
        StringBuilder html,
        string label,
        IReadOnlyCollection<string> values)
    {
        html.AppendLine("                        <div>");
        html.Append("                            <h3>")
            .Append(Encode(label))
            .AppendLine("</h3>");

        if (values.Count == 0)
        {
            html.AppendLine("                            <p class=\"muted\">None</p>");
        }
        else
        {
            html.AppendLine("                            <ul>");
            foreach (var value in values)
            {
                html.Append("                                <li>")
                    .Append(Encode(value))
                    .AppendLine("</li>");
            }
            html.AppendLine("                            </ul>");
        }

        html.AppendLine("                        </div>");
    }

    private static string CssClass(string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace("_", "-", StringComparison.Ordinal);
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private sealed record PolicyCard(
        ApiPolicy Policy,
        int Priority,
        IReadOnlyCollection<string> Identities,
        IReadOnlyCollection<string> Capabilities,
        IReadOnlyCollection<string> Resources);
}
