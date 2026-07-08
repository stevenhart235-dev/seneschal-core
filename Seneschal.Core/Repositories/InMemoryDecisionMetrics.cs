using System.Globalization;
using System.Text;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Repositories;

public sealed class InMemoryDecisionMetrics : IDecisionMetrics
{
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _capabilityCounts =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _identityCounts =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _policyCounts =
        new(StringComparer.OrdinalIgnoreCase);

    private long _totalDecisions;
    private long _allowedDecisions;
    private long _deniedDecisions;
    private long _pendingApprovalDecisions;
    private long _totalEvaluationDurationMs;

    public Task RecordAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _totalDecisions++;
            _totalEvaluationDurationMs += auditEvent.EvaluationDurationMs;

            if (auditEvent.Decision == DecisionType.Allow)
            {
                _allowedDecisions++;
            }
            else if (auditEvent.Decision == DecisionType.Deny)
            {
                _deniedDecisions++;
            }
            else if (auditEvent.Decision == DecisionType.RequireApproval)
            {
                _pendingApprovalDecisions++;
            }

            Increment(_capabilityCounts, auditEvent.CapabilityId);
            Increment(_identityCounts, auditEvent.IdentityId);

            foreach (var policy in auditEvent.MatchedPolicies
                .Where(policy => !string.IsNullOrWhiteSpace(policy)))
            {
                Increment(_policyCounts, policy);
            }
        }

        return Task.CompletedTask;
    }

    public string RenderPrometheus()
    {
        lock (_gate)
        {
            var output = new StringBuilder();

            AppendHelp(
                output,
                "seneschal_decisions_total",
                "Total Seneschal decision evaluations.");
            AppendType(output, "seneschal_decisions_total", "counter");
            AppendMetric(output, "seneschal_decisions_total", _totalDecisions);

            AppendHelp(
                output,
                "seneschal_decisions_allowed_total",
                "Total allowed Seneschal decisions.");
            AppendType(output, "seneschal_decisions_allowed_total", "counter");
            AppendMetric(
                output,
                "seneschal_decisions_allowed_total",
                _allowedDecisions);

            AppendHelp(
                output,
                "seneschal_decisions_denied_total",
                "Total denied Seneschal decisions.");
            AppendType(output, "seneschal_decisions_denied_total", "counter");
            AppendMetric(
                output,
                "seneschal_decisions_denied_total",
                _deniedDecisions);

            AppendHelp(
                output,
                "seneschal_decisions_pending_total",
                "Total Seneschal decisions requiring approval.");
            AppendType(output, "seneschal_decisions_pending_total", "counter");
            AppendMetric(
                output,
                "seneschal_decisions_pending_total",
                _pendingApprovalDecisions);

            AppendLabelledCounter(
                output,
                "seneschal_capability_decisions_total",
                "Total Seneschal decisions by capability.",
                "capability",
                _capabilityCounts);
            AppendLabelledCounter(
                output,
                "seneschal_identity_decisions_total",
                "Total Seneschal decisions by identity.",
                "identity",
                _identityCounts);
            AppendLabelledCounter(
                output,
                "seneschal_policy_matches_total",
                "Total Seneschal policy matches by policy.",
                "policy",
                _policyCounts);

            AppendHelp(
                output,
                "seneschal_evaluation_duration_ms_avg",
                "Average Seneschal decision evaluation duration in milliseconds.");
            AppendType(output, "seneschal_evaluation_duration_ms_avg", "gauge");
            AppendMetric(
                output,
                "seneschal_evaluation_duration_ms_avg",
                _totalDecisions == 0
                    ? 0
                    : (double)_totalEvaluationDurationMs / _totalDecisions);

            return output.ToString();
        }
    }

    private static void AppendLabelledCounter(
        StringBuilder output,
        string name,
        string help,
        string labelName,
        IReadOnlyDictionary<string, long> counts)
    {
        AppendHelp(output, name, help);
        AppendType(output, name, "counter");

        foreach (var item in counts.OrderBy(
            item => item.Key,
            StringComparer.OrdinalIgnoreCase))
        {
            output
                .Append(name)
                .Append('{')
                .Append(labelName)
                .Append("=\"")
                .Append(EscapeLabelValue(item.Key))
                .Append("\"} ")
                .AppendLine(ToPrometheusValue(item.Value));
        }
    }

    private static void Increment(
        Dictionary<string, long> counts,
        string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        counts.TryGetValue(key, out var count);
        counts[key] = count + 1;
    }

    private static void AppendHelp(
        StringBuilder output,
        string name,
        string help)
    {
        output
            .Append("# HELP ")
            .Append(name)
            .Append(' ')
            .AppendLine(help);
    }

    private static void AppendType(
        StringBuilder output,
        string name,
        string type)
    {
        output
            .Append("# TYPE ")
            .Append(name)
            .Append(' ')
            .AppendLine(type);
    }

    private static void AppendMetric(
        StringBuilder output,
        string name,
        double value)
    {
        output
            .Append(name)
            .Append(' ')
            .AppendLine(ToPrometheusValue(value));
    }

    private static string ToPrometheusValue(double value)
    {
        return value.ToString("0.########", CultureInfo.InvariantCulture);
    }

    private static string EscapeLabelValue(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
