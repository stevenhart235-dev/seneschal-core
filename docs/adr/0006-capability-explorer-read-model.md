# ADR-0006: Introduce the Capability Explorer Read Model

## Status

Accepted

## Context

Users need a capability-centered view that combines authoritative capability
metadata with governance relationships. Neither the Capability Catalog nor the
Governance Graph should duplicate the other's responsibility.

## Decision

Core will expose Capability Explorer as a read model composed from the
Capability Catalog and Governance Graph. It returns capability metadata,
relationships, and derived summaries without owning or persisting data.

Capability Explorer is independent of runtime policy evaluation and does not
affect authorization decisions.

## Consequences

- Capability details and relationships can be presented through one query.
- Catalog and graph boundaries remain the authoritative data sources.
- Explorer summaries are derived at query time.
- API and CLI integration can be added separately.
