# Policy

**Status:** Draft

**Version:** 0.1

**Last Updated:** 2026-07-03

---

# Definition

A Policy defines the organizational rules used to evaluate Requests within the Seneschal platform.

Policies determine whether a Request should be allowed, denied, logged, or require approval.

Policies are implementation-independent and describe organizational intent rather than application behavior.

---

# Purpose

The Policy model enables organizations to express governance requirements in a consistent, declarative manner.

Policies provide centralized authorization logic that may be applied across multiple Identities, Capabilities, and environments.

---

# Responsibilities

A Policy:

- Evaluates Requests
- References one or more Capabilities
- May evaluate Request Context
- Produces an authorization Effect
- Explains why a Decision was made
- Supports auditing and compliance

---

# Required Properties

| Property | Description |
|-----------|-------------|
| Id | Globally unique identifier |
| Name | Human-readable name |
| Effect | Authorization outcome |
| Status | Lifecycle state |

---

# Optional Properties

| Property | Description |
|-----------|-------------|
| Description | Purpose of the Policy |
| Priority | Evaluation priority |
| Conditions | Rules evaluated against the Request |
| Capability Filters | Capabilities governed by this Policy |
| Identity Filters | Optional Identity restrictions |
| Labels | Searchable tags |
| Metadata | Arbitrary key/value data |
| Version | Policy version |

---

# Effects

A Policy may produce one of the following Effects.

- Allow
- Deny
- LogOnly
- RequireApproval

Effects describe the intended authorization outcome.

The Decision Engine determines the final Decision.

---

# Conditions

Policies may evaluate contextual information including:

- Environment
- Resource
- Time
- Risk Level
- Labels
- Request Metadata
- Identity Type

Policies should evaluate Request attributes rather than implementation-specific details.

---

# Relationships

Policy

↑

evaluates

↑

Request

↓

produces

↓

Decision

↓

recorded as

↓

Audit Event

---

# Evaluation

Policies should be deterministic.

The same Request evaluated against the same Policies should always produce the same Decision.

Policies should not depend on mutable external state unless explicitly configured.

---

# Design Principles

A Policy should:

- Be human readable
- Be declarative
- Be deterministic
- Be versionable
- Be independently testable
- Explain its outcome

---

# Invariants

A Policy MUST:

- Produce exactly one Effect.
- Have a unique identifier.
- Be deterministic.
- Be independently evaluatable.

A Policy MAY:

- Apply to multiple Capabilities.
- Apply to multiple Identities.
- Evaluate Request Context.
- Be versioned.

---

# Examples

## Authoring workflow

Move a policy through validate -> simulate -> observe -> enforce:

1. Run `seneschal policy validate .\Policies\policies.yaml` to check YAML,
   required fields, identifiers, decisions, and catalog references.
2. Run `seneschal policy simulate` for representative identities,
   capabilities, environments, and resources.
3. Observe decisions under the existing non-enforcing runtime mode.
4. Enable enforcement only after the observed results match the intended
   policy behavior.

The validator first applies the versioned
[Policy Schema v1](../../integrations/contracts/policy/README.md), then uses the
existing loader and semantic validator. It reads `identities.yaml` and
`capabilities.yaml` beside the supplied policy file. Schema-valid does not mean
operationally valid: unique IDs and configured identity and capability
references are separate checks. Warnings do not fail validation; errors do.

## Current YAML authoring shape

Policy Schema v1 requires a non-empty root `policies` array. Each policy
requires a non-blank `name`, `reason`, and `decision`, plus at least one value
from each scalar-or-plural target family:

- `identity` or `identities`
- `capability` or `capabilities`
- `environment` or `environments`

Supported decisions are `allow`, `deny`, `warn`, `log_only`, and
`requires_approval`. Optional string metadata is `displayName`, `description`,
`owner`, `severity`, and `rationale`. Unknown properties and malformed types
are rejected. The current authoring contract does not expose arbitrary
conditions, operators, actions, resources, environment catalogs, or time
windows.

See [Policy Schema Contract](../../integrations/contracts/policy/README.md) for
versioning and editor association.

Valid:

    policies:
      - name: production-deployment
        identity: deployment-worker
        capability: production.deployment.execute
        environment: production
        decision: allow
        reason: Approved release automation may deploy to production

Invalid because the capability is not present in `capabilities.yaml`:

    policies:
      - name: invalid-production-deployment
        identity: deployment-worker
        capability: production.deployment.missing
        environment: production
        decision: allow
        reason: Production deployment

Finance Refund Policy

Capability:

payments.refund

Condition:

Environment == Production

Effect:

RequireApproval

---

Production Secret Access

Capability:

secrets.read

Condition:

Identity.Type == Agent

Effect:

Allow

---

Delete Production Storage

Capability:

storage.delete

Condition:

Environment == Production

Effect:

Deny

---

# Future Considerations

Future versions may support:

- Policy composition
- Policy inheritance
- Policy simulation
- Policy testing
- Policy version comparison
- Policy templates
- Compliance policy packs

These features are outside the scope of Version 1.

---

# Non-Goals

A Policy is not:

- An Identity
- A Request
- A Capability
- A Decision
- An Audit Event

A Policy answers the question:

"How should this Request be evaluated?"
