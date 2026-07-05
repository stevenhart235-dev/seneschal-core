# Policy

## Definition

A Policy defines the rules that determine whether a requested capability invocation should be allowed, denied, logged, warned, or require approval.

Policies express an organization's governance requirements independently of the applications, infrastructure, or AI systems enforcing them.

## Purpose

Policies allow Seneschal to answer the question:

> **Should this request be permitted under the organization's governance rules?**

Policies evaluate the facts contained within a `DecisionRequest` and produce a `DecisionResult`. By externalizing governance into policies, organizations can modify decision logic without changing application code.

## Required Fields

- `id`
- `name`
- `effect`
- `conditions`

## Policy Effects

- `allow`
- `deny`
- `warn`
- `log_only`
- `require_approval`

## Conceptual Shape

```yaml
id: prod-secret-read

name: Production Secret Access

effect: require_approval

conditions:
  environment: production
  capability: azure.keyvault.secret.read
  identity.type: agent

reason: Production secrets require human approval before access.
```

## Design Notes

- Policies should be declarative rather than procedural.
- Policies evaluate facts; they do not execute business logic.
- Policies should remain independent of transport protocols, programming languages, and infrastructure platforms.
- Multiple policies may contribute to a single decision.
- Policies should be versioned and auditable over time.
- Policy evaluation should produce deterministic results for the same input.
- Organizations should be able to operate in progressive enforcement modes, such as log-only before full enforcement.