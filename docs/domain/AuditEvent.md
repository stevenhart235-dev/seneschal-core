# AuditEvent

## Definition

An AuditEvent is the immutable record of a policy evaluation performed by Seneschal.

Every DecisionRequest evaluated by the decision engine should produce an AuditEvent, regardless of whether the request was allowed, denied, logged, warned, or required approval.

## Purpose

AuditEvents allow Seneschal to answer the questions:

> **What happened?**
>
> **Who did it?**
>
> **What capability was used?**
>
> **What resource was affected?**
>
> **Why was the decision made?**

AuditEvents provide the foundation for governance, compliance, forensic analysis, operational observability, and organizational reporting.

## Required Fields

- `timestamp`
- `decisionId`
- `requestId`
- `identity`
- `capability`
- `intent`
- `resource`
- `decision`

## Conceptual Shape

```yaml
timestamp: 2026-07-05T22:15:01Z

decisionId: dec-456

requestId: req-123

identity:
  id: payment-agent

capability:
  id: azure.keyvault.secret.read

intent:
  action: retrieve-secret

resource:
  type: keyvault-secret
  id: prod/payment-api/sql-password

decision: allow

matchedPolicies:
  - prod-secret-read

mode: enforce

latencyMs: 8
```

## Design Notes

- AuditEvents are append-only and should never be modified after creation.
- Every policy evaluation should produce an AuditEvent, even when operating in log-only mode.
- AuditEvents should contain sufficient information to reconstruct how a decision was reached.
- AuditEvents should support long-term retention for governance and compliance requirements.
- AuditEvents should be transport-independent and consistently structured regardless of how a DecisionRequest entered the system.
- AuditEvents should serve as the authoritative record for reporting, dashboards, investigations, and historical analysis.