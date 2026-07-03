# Capability

**Status:** Draft

**Version:** 0.1

**Last Updated:** 2026-07-03

---

# Definition

A Capability represents a discrete action that may be requested by an Identity and governed by one or more Policies.

Capabilities define what can be done, not who may do it.

Capabilities are the fundamental units of governance within Seneschal.

---

# Purpose

The Capability model provides a standardized vocabulary for describing actions across AI agents, services, applications, infrastructure, and external systems.

Capabilities enable organizations to govern behavior independently of implementation details.

---

# Responsibilities

A Capability:

- Defines an action that may be requested
- Exists independently of Identities
- May be governed by multiple Policies
- May be assigned to multiple Identities
- May be referenced by Requests
- Participates in Audit history

---

# Required Properties

| Property | Description |
|-----------|-------------|
| Id | Globally unique identifier |
| Name | Unique capability name |
| Category | Logical grouping |
| Risk | Default risk classification |
| Status | Current lifecycle state |

---

# Optional Properties

| Property | Description |
|-----------|-------------|
| Description | Human-readable explanation |
| Owner | Responsible team |
| Labels | Searchable tags |
| Metadata | Arbitrary key/value pairs |
| Version | Capability version |
| Documentation | Reference material |

---

# Status Values

A Capability may exist in one of the following states.

- Draft
- Active
- Deprecated
- Disabled
- Deleted

---

# Relationships

Capability

↑

requested by

↑

Request

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

# Naming

Capability names should be globally unique and descriptive.

Recommended format:

```
domain.action
```

Examples:

```
payments.refund
payments.capture
payments.void

database.query
database.update

storage.read
storage.delete

secrets.read
secrets.write

deployment.restart
deployment.rollback

filesystem.read
filesystem.write
```

---

# Risk Levels

Capabilities may be classified by risk.

Suggested defaults:

Low

Medium

High

Critical

Risk classifications provide guidance for policy authors but do not determine authorization outcomes.

Policies remain the ultimate source of truth.

---

# Design Principles

A Capability should:

- Represent one discrete action
- Remain implementation independent
- Be reusable
- Be discoverable
- Be understandable by humans
- Be versionable

---

# Invariants

A Capability MUST:

- Have a unique name.
- Represent exactly one logical action.
- Exist independently of any Identity.
- Be referenceable by Requests.

A Capability MAY:

- Be assigned to multiple Identities.
- Be governed by multiple Policies.
- Exist without being assigned.

---

# Examples

Examples of Capabilities:

payments.refund

payments.capture

database.query

database.update

storage.delete

deployment.restart

agent.execute

mcp.invoke

---

# Future Considerations

Future versions may support:

- Automatic capability discovery
- Capability inheritance
- Capability composition
- Capability dependencies
- Capability version history
- Capability approval workflows

These features are intentionally outside the scope of Version 1.

---

# Non-Goals

A Capability is not:

- An Identity
- A Request
- A Policy
- A Decision
- A Permission

A Capability answers the question:

"What action exists within the system?"
