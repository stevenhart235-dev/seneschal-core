# ADR-0005: Introduce a Read-Only Governance Graph

## Status

Accepted

## Context

Relationships among identities, capabilities, resources, and policies are
currently implicit in requests, policies, and audit events. Discovery,
visualization, and the Capability Explorer require a shared relationship model
with provenance and temporal context.

## Decision

Core will define a read-only `IGovernanceGraph` of typed, directional
`GovernanceRelationship` records. Relationships record origin, validity,
evidence, and optional discovery metadata. The graph is queried independently
of runtime evaluation.

## Consequences

- Declared, discovered, observed, and inferred relationships share one model.
- Explorer and visualization features can traverse a consistent domain graph.
- Audit and discovery may project relationships into the graph in future work.
- The graph does not authorize requests or alter current runtime behavior.
