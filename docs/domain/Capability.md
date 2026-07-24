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

- `name` — stable unique identifier
- `displayName` — operator-facing name
- `description` — governed operation
- `owner` — accountable team
- `risk` — Low, Medium, High, or Critical
- `technology` — explicit Technology Explorer classification
- `category` — logical grouping
- `lifecycle` — catalog state
- `tags` — optional search and fallback-classification metadata

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
