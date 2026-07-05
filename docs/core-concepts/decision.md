# Decision

**Status:** Draft

**Version:** 0.1

**Last Updated:** 2026-07-03

---

# Definition

A Decision represents the outcome of evaluating a Request against applicable Policies.

Decisions are produced by the Seneschal runtime and determine whether a requested Capability may proceed, is denied, is logged, or requires approval.

---

# Purpose

The Decision model provides a consistent, explainable result for every Request evaluated by Seneschal.

A Decision allows operators, auditors, and systems to understand what outcome was produced and why.

---

# Responsibilities

A Decision:

- Represents the result of policy evaluation
- Identifies the evaluated Request
- Identifies the final outcome
- Explains why the outcome occurred
- References matched Policies
- Produces Audit Events

---

# Required Properties

| Property | Description |
|-----------|-------------|
| Id | Globally unique identifier |
| RequestId | Request being evaluated |
| Outcome | Final runtime result |
| Reason | Human-readable explanation |
| Timestamp | Decision creation time |

---

# Optional Properties

| Property | Description |
|-----------|-------------|
| MatchedPolicies | Policies involved in evaluation |
| Effect | Policy effect that influenced the outcome |
| EvaluationDurationMs | Time required to evaluate the Request |
| CorrelationId | Trace identifier |
| ApprovalId | Approval workflow reference |
| Metadata | Additional decision context |

---

# Outcomes

A Decision may produce one of the following outcomes:

- Allowed
- Denied
- Logged
- ApprovalRequired

---

# Invariants

A Decision MUST:

- Reference exactly one Request.
- Have exactly one Outcome.
- Include a Reason.
- Include a timestamp.
- Be immutable after creation.

A Decision MAY:

- Reference one or more matched Policies.
- Reference an Approval workflow.
- Produce one or more Audit Events.

---

# Relationships

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

# Design Principles

A Decision should:

- Be deterministic.
- Be explainable.
- Be auditable.
- Be immutable.
- Be safe to expose to operators.
- Contain enough information to support troubleshooting.

---

# Examples

Allowed Decision:

Request:

payments.read

Outcome:

Allowed

Reason:

Matched Payments Read Policy

---

Denied Decision:

Request:

storage.delete

Outcome:

Denied

Reason:

Production storage deletion is denied by policy.

---

Approval Required Decision:

Request:

payments.refund

Outcome:

ApprovalRequired

Reason:

Production refunds require human approval.

---

Logged Decision:

Request:

database.query

Outcome:

Logged

Reason:

Policy is configured for LogOnly enforcement.

---

# Future Considerations

Future versions may support:

- Decision replay
- Decision simulation
- Decision comparison
- Confidence scoring
- Risk scoring
- Multi-policy explanations
- Approval decision chaining

These features are outside the scope of Version 1.

---

# Non-Goals

A Decision is not:

- A Request
- A Policy
- A Capability
- An Audit Event
- An Approval

A Decision answers the question:

"What was the outcome?"