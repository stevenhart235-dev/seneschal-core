# ADR-0004: Introduce the Capability Catalog as a Product Boundary

## Status

Accepted

## Context

Capabilities require an authoritative inventory that can grow beyond a storage
repository into ownership, risk, relationship, and discovery views.

## Decision

Core will expose `ICapabilityCatalog` as the product boundary for capability
lookup and search. Catalog results use `CapabilityCatalogEntry` so relationship
and observation projections can be added without changing the Capability model.

## Consequences

- Storage and loading mechanisms remain implementation details.
- Inventory can be queried by stable ID, ownership, and risk.
- Catalog entries provide a stable expansion point for future product views.
- The catalog is not consulted during policy evaluation at this stage.
