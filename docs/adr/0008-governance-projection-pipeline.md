# ADR-0008: Governance Projection Pipeline

## Status

Accepted

## Context

Seneschal needs governance relationships that can be explored without making
policy evaluation, catalogs, or future discovery systems depend on each
other's internal data models.

The intended flow is:

Sources
    ↓
Projectors
    ↓
Governance Graph
    ↓
Capability Explorer

## Decision

Sources are authoritative for their own domains. Projectors translate source
data into governance relationships.

The Governance Graph is a read model, not a source of truth. Capability
Explorer composes read models and owns no data.

Runtime policy evaluation does not query the Governance Graph.

## Consequences

- New governance sources can be added through projectors.
- Source ownership remains clear.
- Governance relationships can support explorer and visualization features
  without changing runtime enforcement behavior.
- Projection errors are isolated from policy decision logic.
