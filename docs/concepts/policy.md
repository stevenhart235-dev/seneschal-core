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
