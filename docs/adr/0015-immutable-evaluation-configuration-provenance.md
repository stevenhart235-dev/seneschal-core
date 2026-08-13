# ADR-0015: Immutable Evaluation-Time Configuration Provenance

## Status

Accepted

## Context

Committed decisions already preserve policy and runtime evidence, but historical
rows could not identify the semantic governance configuration used at evaluation
time. Attributing old evidence to current configuration would create false
provenance. Exposure analysis also needs to distinguish a complete observation
window from a partial or unknown retained-evidence interval.

## Decision

Every newly committed operational evaluation records a deterministic semantic
governance configuration fingerprint. The fingerprint includes projected policy
semantics, runtime governance mode, and governance-window semantics. It excludes
formatting, comments, paths, descriptive catalog metadata, integration keys, and
secrets. The stored value is immutable evidence and is never recomputed on read.

Historical evidence without a fingerprint remains readable and is explicitly
reported as provenance unavailable. Seneschal never retroactively assigns the
current fingerprint to old evidence. Matching a historical fingerprint to the
current fingerprint proves semantic configuration equality for the fingerprinted
inputs only; a difference does not identify a specific changed policy or prove a
different evaluation result.

Audit stores may expose an explicit completeness boundary for evidence coverage.
Full coverage is claimed only when that boundary is at or before the requested
window start. Oldest-event timestamps are not completeness boundaries. Stores
without trustworthy boundary metadata report unknown coverage.

## Alternatives considered

### Hash raw configuration files

Rejected because comments, whitespace, paths, and document formatting are not
evaluation semantics and would create false version changes.

### Backfill historical rows with the current fingerprint

Rejected because it would falsely attribute historical decisions to configuration
that may not have existed when they were produced.

### Store complete configuration snapshots immediately

Deferred. Snapshot retention, access control, lifecycle, and potentially sensitive
content require a separate contract. The fingerprint establishes identity without
claiming snapshot availability.

### Infer coverage from the oldest retained event

Rejected because absence of older evidence cannot prove that the store was active
or complete before that event.

## Consequences

- New evidence can be compared with current evaluation-relevant configuration.
- Old evidence remains compatible and truthfully reports unavailable provenance.
- PostgreSQL persists a completeness boundary independently of audit rows.
- Exposure findings can qualify non-observation as full, partial, or unknown.
- Fingerprints do not replace recorded matched-policy and policy-evaluation facts.

## Related documentation

- [Identity Governance Exposure Analysis](../identity-governance-exposure.md)
- [ADR-0014: Separate Configured Governance Exposure from Observed Evidence](0014-configured-governance-and-observed-exposure.md)
- [Audit Domain](../domain/AuditEvent.md)