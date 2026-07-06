# ADR-0003: Treat Capabilities as First-Class Domain Concepts

## Status

Accepted

## Context

Seneschal governs operations performed by identities against resources.
Representing those operations as strings alone cannot support ownership, risk,
versioning, discovery, or consistent governance across providers.

## Decision

Capability will be a first-class Core domain model with a stable identifier,
name, provider, category, description, risk level, owner, version, and tags.
Capabilities will remain independent of identities, policies, and evaluation
logic.

## Consequences

- Capability identifiers remain stable across requests, policies, and audit.
- Capability metadata can support inventory and governance experiences.
- Existing identifiers remain compatible.
- Policy evaluation continues to use the capability supplied with the request.
