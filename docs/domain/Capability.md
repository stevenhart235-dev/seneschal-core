# Capability

## Definition

A Capability represents a discrete operation that an identity can invoke.

Capabilities are the primary objects of governance within Seneschal. Rather than governing applications, APIs, or infrastructure directly, Seneschal governs the capabilities they expose.

A capability should describe **what can be done**, independent of who performs the action or why it is being performed.

## Purpose

Capabilities allow Seneschal to answer the question:

> **What operation is being requested?**

By treating capabilities as first-class objects, organizations gain visibility into the actions available across their technology landscape and can apply consistent governance regardless of the underlying implementation.

## Required Fields

- `id`
- `provider`
- `category`
- `risk`
- `description`

## Conceptual Shape

```yaml
id: azure.keyvault.secret.read

provider: azure

category: secret-management

risk: high

description: Read a secret value from Azure Key Vault.

tags:
  - secrets
  - production
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