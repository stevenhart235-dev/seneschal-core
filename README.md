# Seneschal

**Capability Governance for Humans and AI**

Seneschal is a governance platform that models, observes, and enforces how capabilities are used across applications, AI agents, infrastructure, and automation.

Instead of asking:

> "Who called this API?"

Seneschal asks:

> "Who exercised this capability, under what policy, and should they have been allowed?"

---

## Current Status

**Version:** `v0.2.0-alpha`

### Implemented

- ✅ Policy Decision Engine
- ✅ Policy Evaluation & Resolution
- ✅ Capability Catalog
- ✅ Governance Graph
- ✅ Capability Explorer
- ✅ REST API
- ✅ CLI
- ✅ Capability Explorer Web UI
- ✅ Architecture Decision Records (ADRs)

### In Progress

- 🚧 Runtime Observations
- 🚧 Capability Search
- 🚧 Graph Visualization
- 🚧 SDK
- 🚧 Policy Enforcement

---

## Vision

Capabilities are becoming the common language between:

- AI Agents
- Infrastructure
- APIs
- CI/CD Pipelines
- Cloud Providers

Seneschal provides a single governance model for understanding, observing, and controlling those capabilities.

---

## Example

```bash
seneschal evaluate payment-agent azure.keyvault.secret.read production
```

```
Decision: Allow

Matched Policies:
- platform-secret-access
- prod-secret-read
```

Or explore a capability:

```bash
seneschal capability show azure.keyvault.secret.read
```

Or through the web UI:

```
Capability Explorer
```

---

## Architecture

```
Applications
AI Agents
CI/CD
Infrastructure

        │

        ▼

   Seneschal API

        │

        ▼

Capability Explorer
Decision Engine
Governance Graph
Capability Catalog

        │

        ▼

 Policies
 Identities
 Capabilities
```

---

## Principles

- Capability-first governance
- Explainable decisions
- Observation before enforcement
- Provider-agnostic architecture
- Human and AI identities are first-class citizens

---

## Documentation

- `/docs/VISION.md`
- `/docs/adr`

---

> Seneschal is currently in active development.