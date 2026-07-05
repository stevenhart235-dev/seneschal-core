# DecisionRequest

## Definition

A DecisionRequest represents a request to evaluate whether an identity should be permitted to use a capability against a resource for a specific intent.

It is the primary input to the Seneschal decision engine and serves as the common language between applications, agents, SDKs, sidecars, gateways, and other integrations.

Every request entering Seneschal should be normalized into a DecisionRequest before policy evaluation begins.

## Purpose

DecisionRequest allows Seneschal to answer the question:

> **What decision is being requested?**

By providing a consistent, transport-independent model, DecisionRequests enable the same governance logic to be applied regardless of where or how a request originated.

## Required Fields

- `requestId`
- `timestamp`
- `identity`
- `capability`
- `intent`
- `resource`
- `context`

## Conceptual Shape

```yaml
requestId: req-123

timestamp: 2026-07-05T22:15:00Z

identity:
  id: payment-agent
  type: agent
  owner: platform-team

capability:
  id: azure.keyvault.secret.read
  provider: azure
  category: secret-management
  risk: high

intent:
  action: retrieve-secret
  reason: Retrieve the SQL connection string for application startup.

resource:
  type: keyvault-secret
  id: prod/payment-api/sql-password

context:
  application: payment-api
  namespace: payments
  environment: production
  source: sdk
  traceId: trace-abc123
```

## Design Notes

- DecisionRequest is the canonical input to the Seneschal decision engine.
- Every integration should produce the same conceptual DecisionRequest regardless of transport or protocol.
- DecisionRequests describe facts about a request rather than policy outcomes.
- DecisionRequests should remain immutable during evaluation.
- Additional contextual metadata may be included without changing the core decision model.
- DecisionRequests should be expressive enough to support governance across humans, applications, AI agents, pipelines, and future capability providers.