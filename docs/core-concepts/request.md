# Request

**Status:** Draft

**Version:** 0.1

**Last Updated:** 2026-07-03

---

# Definition

A Request represents an Identity asking to perform a specific Capability against a target Resource within a given Context.

Requests are the primary unit of evaluation within the Seneschal platform.

Every authorization decision originates from a Request.

---

# Purpose

The Request model provides a consistent and auditable representation of an attempted action.

Rather than evaluating Identities or Capabilities independently, Seneschal evaluates Requests that combine both into a complete authorization context.

---

# Responsibilities

A Request:

- Identifies who is making the request
- Identifies which Capability is being requested
- Identifies the target Resource
- Provides contextual information for policy evaluation
- Produces exactly one Decision
- Produces one or more Audit Events

---

# Required Properties

| Property | Description |
|-----------|-------------|
| Id | Globally unique identifier |
| Identity | The requesting Identity |
| Capability | The requested Capability |
| Timestamp | Request creation time |

---

# Optional Properties

| Property | Description |
|-----------|-------------|
| Resource | Target of the requested action |
| Environment | Dev, Test, Production, etc. |
| Metadata | Additional request context |
| CorrelationId | Trace identifier |
| ParentRequestId | Parent request for chained operations |
| RequestedBy | Original requester when acting on behalf of another identity |

---

# Lifecycle

1. Request Submitted
2. Context Collected
3. Policy Evaluation Begins
4. Decision Produced
5. Audit Events Recorded
6. Response Returned

---

# Invariants

A Request MUST:

- Have exactly one Identity.
- Reference exactly one Capability.
- Produce exactly one Decision.
- Have a unique identifier.
- Include a timestamp.
- Become immutable after submission.

A Request MAY:

- Reference one Resource.
- Produce multiple Audit Events.
- Participate in distributed traces.
- Be correlated with other Requests.

---

# Relationships

Identity

↓

submits

↓

Request

↓

references

↓

Capability

↓

evaluated by

↓

Policy

↓

produces

↓

Decision

↓

recorded as

↓

Audit Event

---

# Design Principles

A Request should:

- Be immutable.
- Be independently auditable.
- Be traceable across distributed systems.
- Contain sufficient context for policy evaluation.
- Remain independent of transport protocols.

---

# Examples

Example Request:

Identity:
Payment Agent

Capability:
payments.refund

Resource:
payment/12345

Environment:
Production

---

Example Decision:

Approved

Reason:
Matched Finance Refund Policy

Audit Id:
9d8f0b...

---

# Future Considerations

Future versions may support:

- Request replay
- Request simulation
- Batch requests
- Scheduled requests
- Request expiration
- Risk scoring
- Multi-stage approval workflows

These capabilities are outside the scope of Version 1.

---

# Non-Goals

A Request is not:

- A Policy
- A Capability
- A Permission
- An Identity
- A Decision

A Request answers the question:

"What action is being requested?"
