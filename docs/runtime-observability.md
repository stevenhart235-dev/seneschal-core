# Runtime Observability

## Purpose

Runtime observability complements Seneschal governance and audit.

Audit answers:

```text
Why was this decision made?
```

Observability answers:

```text
What is happening across my capability ecosystem?
```

Seneschal already records decision evidence through audit events. Runtime
observability builds on that activity to show patterns, trends, pressure points,
and operational health across capabilities, identities, policies, and runtime
integrations.

Observability should help teams understand how capabilities are actually used
before they tighten governance or enforcement.

## Event Types

Seneschal should distinguish between three related but different concepts.

### Decision Events

Decision events are individual runtime evaluations.

They represent one request from one identity to use one capability against a
resource or environment. They include the decision, enforcement mode, matched
policy, obligations, reason, and evaluation duration.

Decision events are the raw runtime signal.

### Activity Metrics

Activity metrics are aggregated usage views derived from decision events.

They answer questions such as:

- Which capabilities are used most often?
- Which identities are most active?
- Which capabilities are denied most frequently?
- Which policies match most often?
- Is evaluation latency healthy?

Activity metrics are optimized for dashboards and exploration, not evidence.

### Audit History

Audit history is immutable decision evidence.

It preserves what happened, when it happened, who requested it, what policy
matched, what decision was returned, and why.

Audit history should remain reviewable even if aggregation logic changes later.

## Metrics to Collect

### Per Capability

For each capability, Seneschal should collect:

- Total requests.
- Allowed count.
- Denied count.
- Pending approval count.
- Last used timestamp.
- Average evaluation time.

These metrics help identify high-use capabilities, risky capabilities, and
capabilities that are frequently blocked or awaiting approval.

### Per Identity

For each identity, Seneschal should collect:

- Total requests.
- Distinct capabilities used.
- Denied count.
- Pending approval count.

These metrics help identify active automation, unusual usage patterns, and
identities that repeatedly hit governance boundaries.

### Per Policy

For each policy, Seneschal should collect:

- Match count.
- Last matched timestamp.

These metrics help show which policies are actively governing runtime behavior
and which policies may be stale, redundant, or overly broad.

## Aggregation Strategy

Sprint 8 should start with in-memory aggregation that is simple, deterministic,
and easy to replace later.

### Real-Time Counters

Real-time counters should update as decisions are evaluated.

The first implementation can use an in-memory activity store that receives
decision activity from the same runtime path that writes audit events.

This keeps observability local and lightweight while preserving the current
architecture.

### Rolling Windows

Seneschal should eventually support rolling windows such as:

- 1 hour.
- 24 hours.
- 7 days.

Rolling windows make dashboards more useful than lifetime totals alone. They
help teams distinguish current activity from historical accumulation.

The first implementation may store timestamps with activity samples or maintain
simple in-memory buckets. The model should avoid assuming a database too early.

### Historical Summaries

Historical summaries should be derived from stored decision activity or audit
events once persistence exists.

Examples include:

- Daily capability request totals.
- Weekly denied-request trends.
- Policy match trends over time.
- Identity activity baselines.

Historical summaries should be separate from immutable audit records.

### Future Persistence Considerations

In-memory aggregation is appropriate for early product development, but runtime
observability will eventually need persistence.

Future persistence should support:

- Efficient time-window queries.
- Retention policies.
- Rebuilding summaries from audit history when needed.
- Export to external observability platforms.
- Multi-instance deployments.

Persistence should be introduced behind activity store interfaces so runtime
evaluation does not become coupled to a database implementation.

## Dashboard Concepts

Runtime observability should add operational views to the Seneschal dashboard.

Useful dashboard concepts include:

- Top capabilities.
- Most denied capabilities.
- Most active identities.
- Recently active capabilities.
- Policy match frequency.
- Runtime health summary.

The runtime health summary should include signals such as:

- Total evaluations.
- Average evaluation duration.
- Recent deny rate.
- Pending approval rate.
- Last observed runtime activity.

These views should make it easier to understand capability usage before opening
individual audit traces.

## Relationship to Audit

Audit records and activity metrics are intentionally different.

Audit records are evidence. They should be complete, reviewable, and stable.
They explain why a specific decision was made.

Activity metrics are summaries. They can be aggregated, rolled up, expired,
recomputed, or exported. They explain what is happening across the system.

Seneschal should not replace audit history with metrics. Metrics should be
derived from runtime activity and, where appropriate, traceable back to audit
events.

This separation keeps investigation and observability from competing with each
other:

- Audit supports accountability.
- Observability supports understanding.
- Governance uses both.

## Future Integrations

Seneschal runtime observability should eventually integrate with existing
operations ecosystems.

Potential integrations include:

- Prometheus for metric scraping.
- OpenTelemetry for traces, metrics, and structured events.
- Grafana for dashboards.
- SIEM platforms for security analytics.
- Export APIs for external reporting and data pipelines.

These integrations should consume Seneschal activity models rather than
reimplementing decision evaluation or governance logic.

## Sprint 8 Implementation Plan

### Commit 1: Activity Models and In-Memory Activity Store

Introduce Core activity models and an in-memory activity store.

The store should capture runtime decision activity in a form suitable for
aggregation without replacing audit events.

### Commit 2: Runtime Activity Aggregation

Update the runtime evaluation flow so completed evaluations update activity
metrics.

Aggregation should preserve existing decision behavior and audit behavior.

### Commit 3: Dashboard Activity Widgets

Add dashboard widgets for high-value runtime signals:

- Total evaluations.
- Top capabilities.
- Most denied capabilities.
- Most active identities.
- Average evaluation duration.

Widgets should use the activity store rather than scanning UI state.

### Commit 4: Identity Activity Explorer

Add an identity-focused activity view.

The first version should show:

- Total requests by identity.
- Distinct capabilities used.
- Denied count.
- Pending approval count.
- Recent activity.

### Commit 5: Capability Activity Timeline

Add a capability-focused activity timeline.

The first version should show recent runtime activity for a selected capability,
including decisions, identities, environments, matched policies, and timestamps.

This timeline should complement audit details without replacing the audit trail.
