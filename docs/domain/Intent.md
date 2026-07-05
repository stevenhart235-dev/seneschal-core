# Intent

## Definition

An Intent describes the desired outcome of using a capability.

While a capability defines **what** operation is being performed, an intent defines **why** that operation is being performed. This distinction allows Seneschal to make governance decisions based on business context rather than technical implementation alone.

## Purpose

Intent allows Seneschal to answer the question:

> **Why is this capability being used?**

Two requests may invoke the same capability but represent entirely different business operations. Capturing intent enables more expressive policies, richer audit trails, and better organizational visibility.

## Required Fields

- `action`
- `reason`

## Conceptual Shape

```yaml
action: deploy

reason: Deploy version 3.7.2 to the production environment.
```

## Examples

| Capability | Intent |
|------------|--------|
| `terraform.apply` | Deploy production infrastructure |
| `azure.keyvault.secret.read` | Retrieve application connection string |
| `github.pull_request.merge` | Promote approved release |
| `openai.chat.completion` | Summarize customer support tickets |
| `mcp.tool.invoke` | Generate infrastructure configuration |

## Design Notes

- Intent describes the desired business outcome rather than the technical operation.
- Intent should be human-readable whenever possible.
- Intent should complement a capability, not duplicate it.
- Intent enables policies to distinguish between similar technical actions performed for different purposes.
- Intent should remain independent of the implementation details of the capability being invoked.