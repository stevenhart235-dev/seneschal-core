# Audit Event

**Status:** Draft

**Version:** 0.1

**Last Updated:** 2026-07-03

---

# Definition

An Audit Event is an immutable record of a significant action, evaluation, or state transition within the Seneschal platform.

Audit Events provide a chronological history of platform activity and support operational visibility, security investigations, and compliance requirements.

Audit Events are append-only and never modified after creation.

---

# Purpose

The Audit Event model provides a permanent, trustworthy record of governance activity.

Rather than storing only outcomes, Audit Events preserve the sequence of events that occurred throughout the lifecycle of a Request.

---

# Responsibilities

An Audit Event:

- Records significant platform activity
- References related domain objects
- Preserves chronological history
- Supports operational investigations
- Supports compliance and auditing
- Enables timeline reconstruction

---

# Required Properties

| Property | Description |
|-----------|-------------|
| Id | Globally unique identifier |
| Timestamp | Event creation time |
| EventType | Classification of event |
| RequestId | Associated Request |
| DecisionId | Associated Decision |

---

# Optional Properties

| Property | Description |
|-----------|-------------|
| IdentityId | Related Identity |
| CapabilityId | Related Capability |
| PolicyId | Related Policy |
| CorrelationId | Distributed trace identifier |
| Metadata | Additional event context |

---

# Event Types

Examples include:

- RequestSubmitted
- PolicyEvaluationStarted
- PolicyMatched
- DecisionCreated
- ApprovalRequested
- ApprovalGranted
- ApprovalDenied
- RequestCompleted

Additional event types may be introduced without changing the underlying model.

---

# Invariants

An Audit Event MUST:

- Be immutable.
- Have a timestamp.
- Have a unique identifier.
- Reference at least one domain object.

An Audit Event MAY:

- Reference multiple related objects.
- Contain additional metadata.
- Participate in distributed tracing.

Audit Events MUST NEVER be modified after creation.

Corrections should be represented by additional Audit Events.

---

# Relationships

Identity

↓

Request

↓

Decision

↓

Audit Event

Policies and Capabilities may also be referenced when relevant.

---

# Design Principles

An Audit Event should:

- Be immutable.
- Be append-only.
- Be chronologically ordered.
- Be independently understandable.
- Support timeline reconstruction.
- Support long-term retention.

---

# Examples

Example:

Timestamp:

2026-07-03T19:42:18Z

Event:

RequestSubmitted

Identity:

payment-agent

Capability:

payments.refund

---

Example:

Timestamp:

2026-07-03T19:42:18Z

Event:

DecisionCreated

Outcome:

ApprovalRequired

Matched Policy:

Finance Refund Policy

---

# Future Considerations

Future versions may support:

- Event streaming
- Event subscriptions
- Event export
- Long-term archival
- Compliance reporting
- Timeline visualization

These capabilities are outside the scope of Version 1.

---

# Non-Goals

An Audit Event is not:

- A Request
- A Decision
- A Policy
- A Capability

An Audit Event answers the question:

"What happened?"
