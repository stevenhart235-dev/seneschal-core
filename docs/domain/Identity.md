# Identity

## Definition

An Identity represents the actor requesting the use of a capability.

Identities may represent humans, applications, AI agents, service accounts, workloads, pipelines, or other autonomous systems. Seneschal treats identities consistently regardless of their origin or authentication mechanism.

## Purpose

Identity allows Seneschal to answer the question:

> **Who is attempting to use this capability?**

Policies evaluate identities to determine whether a requested action should be allowed, denied, logged, or require approval.

By separating identity from authentication, Seneschal can apply consistent governance across diverse environments and identity providers.

## Required Fields

- `id`
- `type`
- `owner`
- `environment`

## Conceptual Shape

```yaml
id: payment-agent

type: agent

owner: platform-team

environment: production

attributes:
  application: payment-api
  department: Payments
  service: Payment Processing
```

## Design Notes

- Identity describes the actor, not how the actor authenticated.
- Authentication proves identity but is not part of the identity model.
- Authorization decisions are made by policy, not by the identity itself.
- Identity should remain independent of any specific identity provider or authentication technology.
- Identity should be stable over time so audit events remain meaningful and traceable.