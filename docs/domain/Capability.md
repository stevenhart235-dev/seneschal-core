# Capability

## Definition

A Capability represents a discrete operation that an identity can invoke.

Capabilities are the primary objects of governance within Seneschal. Rather than governing applications, APIs, or infrastructure directly, Seneschal governs the capabilities they expose.

A capability should describe **what can be done**, independent of who performs the action or why it is being performed.

## Purpose

Capabilities allow Seneschal to answer the question:

> **What operation is being requested?**

By treating capabilities as first-class objects, organizations gain visibility into the actions available across their technology landscape and can apply consistent governance regardless of the underlying implementation.

## Catalog Fields

- `name` — required, stable unique identifier used by requests and policies
- `displayName` — optional operator-facing name
- `description` — optional explanation of the governed operation
- `owner` — optional accountable team
- `risk` — optional `Low`, `Medium`, `High`, or `Critical` classification
- `technology` — optional explicit Technology Explorer classification
- `category` — optional logical grouping
- `lifecycle` — optional catalog state
- `documentationUrl` — optional HTTP or HTTPS operator reference
- `tags` — optional search and fallback-classification metadata

`name` is the capability identity. Changing descriptive metadata does not create a
new capability, and changing `name` can break policy and audit references. When
metadata is omitted, catalog projections use compatibility defaults for display;
policy matching and evaluation continue to use only the stable identifier.

Category, technology, risk, lifecycle, ownership, documentation, and tags support
discovery and investigation. They do not grant permission, select a policy, or
change a decision. Invalid risk values are configuration errors. Invalid
operator references and tag hygiene issues are reported as warnings.

## Conceptual Shape

```yaml
name: azure.keyvault.secret.read
displayName: Read Azure Key Vault Secret
description: Read a secret from an Azure Key Vault.
owner: Security Engineering
risk: High
technology: azure
category: Secrets
lifecycle: Active
tags: [azure, key-vault, secret]
```

## Examples

| Provider | Capability |
|----------|------------|
| Azure | `azure.keyvault.secret.read` |
| Azure | `azure.storage.blob.write` |
| Terraform | `terraform.plan` |
| Terraform | `terraform.apply` |
| GitHub | `github.pull_request.merge` |
| Kubernetes | `kubernetes.deployment.restart` |
| OpenAI | `openai.chat.completion` |
| MCP | `mcp.tool.invoke` |

## Design Notes

- A capability describes **what** can be done, not **who** is doing it or **why**.
- Capability identifiers should be stable so they remain meaningful in policies, audit events, and reporting.
- Capabilities should be provider-specific when necessary but follow consistent naming conventions.
- Capabilities should not contain authorization logic or embedded policy rules.
- Capabilities should be discoverable, searchable, and versionable as an organization's capability catalog grows.
- Capabilities form the foundation of Seneschal's governance, observability, and audit model.
- Parent/child, related-capability, replacement, and pack provenance metadata are not part of the current catalog contract. Capability packs may compose this same catalog in a future milestone without replacing stable capability IDs.
