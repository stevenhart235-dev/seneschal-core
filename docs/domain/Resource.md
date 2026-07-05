# Resource

## Definition

A Resource represents the target of a capability invocation.

Resources are the systems, services, infrastructure, data, or objects that an identity intends to interact with. They provide the context necessary for Seneschal to evaluate governance decisions beyond simply knowing who performed an action.

## Purpose

Resources allow Seneschal to answer the question:

> **What is this capability being used against?**

Policies often depend not only on who is making a request and which capability is being used, but also on the sensitivity, ownership, classification, or environment of the resource being accessed.

## Required Fields

- `type`
- `id`

## Conceptual Shape

```yaml
type: keyvault-secret

id: prod/payment-api/sql-password

environment: production

attributes:
  classification: confidential
  owner: platform-team
  provider: azure
```

## Examples

| Resource Type | Example |
|--------------|---------|
| Secret | `prod/payment-api/sql-password` |
| Repository | `payments-api` |
| Kubernetes Namespace | `payments` |
| Database | `customer-db` |
| Blob Storage | `customer-documents` |
| AI Model | `gpt-4.1` |
| Virtual Machine | `aks-nodepool-01` |
| Terraform Workspace | `production-networking` |

## Design Notes

- Resources identify what is being acted upon rather than how it is accessed.
- Resource identifiers should be stable and uniquely identify the target within an organization.
- Resources may expose provider-specific metadata through attributes while maintaining a consistent governance model.
- Policies should evaluate resource characteristics rather than relying on naming conventions whenever possible.
- Resources should support future expansion without requiring changes to the core decision model.