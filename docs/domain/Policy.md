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

## YAML catalog schema

The API catalog uses `name` as the stable policy identifier. Existing
references, audit evidence, and tests may depend on that value, so a policy is
given a clearer operator-facing name through `displayName` rather than by
renaming `name`.

Each catalog entry supports the following descriptive governance metadata:

| Field | Purpose |
|---|---|
| `displayName` | Concise operator-facing policy name |
| `description` | What the policy governs |
| `owner` | Team accountable for the control |
| `severity` | Governance importance (`low`, `medium`, `high`, or `critical`) |
| `rationale` | Why the control exists |

Targets may use the backward-compatible singular fields `identity`,
`capability`, and `environment`, or the plural `identities`, `capabilities`,
and `environments` fields. Plural targets are projected into the same exact
identity, capability, and environment conditions used by the evaluator; they
do not change matching or decision-resolution behavior.

Policy effects express expected governance outcomes:

- `allow` permits a known, routine path.
- `deny` rejects an unsafe or unsupported path.
- `requires_approval` pauses enforcement until the existing approval
  lifecycle resolves the request.
- `log_only` observes a governed path without blocking it.

Policy owners should align the target identities and capabilities with the
teams responsible for those systems. Reasons are returned in decisions and
must therefore describe the result plainly; rationale supplies the durable
governance justification shown in the Policy Explorer.

## Catalog example

```yaml
name: support-secret-read
displayName: Production Secret Access
description: Requires review before support automation reads production secrets.
owner: Security Engineering
severity: high
rationale: Production credentials must be disclosed only for a reviewed need.
identities:
  - support-ai
capabilities:
  - azure.keyvault.secret.read
environments:
  - production
decision: requires_approval
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
