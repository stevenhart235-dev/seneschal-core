using System.Net;
using System.Text;

namespace Seneschal.Api.Services;

public static class PortalSidebarRenderer
{
    public static string Render(string activeItem)
    {
        var html = new StringBuilder();
        html.AppendLine("        <aside class=\"sidebar\">");
        html.AppendLine("            <div class=\"sidebar-brand\"><span>Seneschal</span><small>Capability control plane</small></div>");
        html.AppendLine("            <nav class=\"sidebar-nav\" aria-label=\"Primary navigation\">");
        AppendGroup(html, "Overview",
            ("dashboard", "/dashboard", "Dashboard"),
            ("monitor", "/monitor", "Monitor"));
        AppendGroup(html, "Governance",
            ("governance", "/governance", "Runtime Governance"),
            ("policies", "/policies", "Policies"),
            ("capabilities", "/capability-explorer", "Capabilities"),
            ("identities", "/identity-explorer", "Identities"),
            ("resources", "/resources", "Resources"));
        AppendGroup(html, "Operations",
            ("capability-activity", "/capability-activity", "Capability Activity"),
            ("identity-activity", "/identity-activity", "Identity Activity"),
            ("audit", "/audit", "Audit Trail"),
            ("incidents", "/incidents", "Incidents"));
        AppendGroup(html, "System",
            ("graph", "/graph-view", "Relationship Graph"),
            ("diagnostics", "/diagnostics-view", "Diagnostics"));
        html.AppendLine("            </nav>");
        html.AppendLine("        </aside>");
        return html.ToString();

        void AppendGroup(
            StringBuilder builder,
            string title,
            params (string Key, string Href, string Label)[] links)
        {
            builder.AppendLine("                <div class=\"nav-section\">");
            builder.Append("                    <div class=\"nav-section-title\">")
                .Append(WebUtility.HtmlEncode(title))
                .AppendLine("</div>");
            foreach (var link in links)
            {
                var isActive = string.Equals(activeItem, link.Key, StringComparison.Ordinal);
                builder.Append("                    <a");
                if (isActive)
                {
                    builder.Append(" class=\"active\"");
                }
                builder.Append(" href=\"")
                    .Append(WebUtility.HtmlEncode(link.Href));
                if (isActive)
                {
                    builder.Append("\" aria-current=\"page");
                }
                builder.Append("\"><span>")
                    .Append(WebUtility.HtmlEncode(link.Label))
                    .AppendLine("</span></a>");
            }
            builder.AppendLine("                </div>");
        }
    }
}
