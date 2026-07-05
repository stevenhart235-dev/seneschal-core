# DecisionResult

## Definition

A DecisionResult is the canonical response produced by the Seneschal decision engine after evaluating a DecisionRequest.

It communicates the outcome of policy evaluation, explains why the decision was reached, and provides any obligations or actions that must accompany the result.

Every DecisionRequest should produce exactly one DecisionResult.

## Purpose

DecisionResult allows Seneschal to answer the question:

> **What decision was made, and why?**

It provides a consistent response model for applications, agents, SDKs, gateways, sidecars, and audit systems regardless of how the request entered Seneschal.

## Required Fields

- `decisionId`
- `requestId`
- `timestamp`
- `decision`
- `mode`
- `reason`
- `matchedPolicies`
- `obligations`

## Decision Types

- `allow`
- `deny`
- `warn`
- `log_only`
- `require_approval`

## Conceptual Shape

```yaml
decisionId: dec-456

requestId: req-123

timestamp: 2026-07-05T22:15:01Z

decision: allow

mode: enforce

reason: Identity is authorized to retrieve production secrets.

matchedPolicies:
  - prod-secret-read

obligations:
  - log-access
  - redact-secret-value
  - include-trace-id

latencyMs: 8
```

## Design Notes

- Every DecisionRequest should produce exactly one DecisionResult.
- DecisionResults should be deterministic for identical inputs evaluated against the same policy set.
- DecisionResults explain why a decision was made rather than simply returning an allow or deny response.
- Obligations communicate additional actions required by the caller, such as logging, masking sensitive data, or requesting approval.
- DecisionResults should remain transport-independent so they can be consumed consistently across APIs, SDKs, sidecars, agents, and gateways.
- DecisionResults should contain sufficient information to support enforcement, troubleshooting, and the creation of an AuditEvent.