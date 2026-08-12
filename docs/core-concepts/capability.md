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

# Configuration properties

The YAML catalog uses `name` as the stable capability identifier.

| Property | Expectation |
|-----------|-------------|
| `name` | Required, globally unique, and stable after policies or audit records reference it |
| `displayName` | Operator-facing name |
| `description` | Concise explanation of the governed operation |
| `owner` | Accountable team |
| `risk` | `Low`, `Medium`, `High`, or `Critical` |
| `technology` | Explicit Technology Explorer classification key |
| `category` | Logical grouping within the technology |
| `lifecycle` | Current catalog state, normally `Active` or `Restricted` |
| `documentationUrl` | Optional reference material |
| `tags` | Optional searchable and classification metadata |

All fields except `name` are optional descriptive metadata. Legacy records with
fewer fields remain valid and use display defaults in catalog projections.
Enterprise catalog entries should supply the metadata operators need for
discovery and investigation.

Policies, requests, and audit evidence reference `name`; it remains the stable
evaluation key. Display name, description, owner, risk, technology, category,
lifecycle, documentation, and tags do not grant permission, match policies, or
change decisions. They are not runtime risk inputs.

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
technology.resource.action
```

Examples:

```
azure.keyvault.secret.read
github.workflow.dispatch
terraform.apply.production
kubernetes.pod.logs.read
openai.model.invoke
postgres.schema.migrate
```

Compatibility identifiers that predate this convention remain stable. Avoid
creating a second identifier when an existing capability already represents
the same governed operation.

---

# Risk Levels

Capabilities may be classified by risk.

| Level | Meaning | Example |
|---|---|---|
| Low | Read-only or routine operation with limited impact | Read pod logs or invoke an approved model |
| Medium | Operational change with bounded impact | Scale a deployment or dispatch a workflow |
| High | Sensitive access or material production change | Read a production secret or migrate a schema |
| Critical | Destructive or exceptional privileged action | Destroy production infrastructure or activate break glass |

Risk classifications provide guidance for policy authors but do not determine authorization outcomes.

Policies remain the ultimate source of truth.

## Technology classification

`technology` should use a key recognized by the Technology Explorer:
`azure`, `github`, `terraform`, `kubernetes`, `openai`, `postgresql`,
`slack`, `m365`, or `custom`.

Explicit metadata takes precedence over identifier and tag heuristics.
Omitting technology is appropriate only for intentional legacy or
Unclassified examples.

## Ownership

Every enterprise capability should name one accountable team. Ownership is
descriptive governance context; it does not grant permission or alter policy
evaluation.

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
- Capability packs and catalog provenance

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
