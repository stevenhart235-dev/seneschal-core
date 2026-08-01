# Persistence

Seneschal currently loads capabilities, identities, policies, and integration
keys from YAML and keeps operational state in process-local memory. Audit,
approval, incident, activity, runtime-mode, governance-window, metrics, catalog,
and governance-graph state reset or rebuild when the process restarts; durable
persistence is not implemented.

`IAuditEventStore` is the provider-neutral append-only evaluation-evidence
contract. Committed `AuditEvent` records are immutable by contract: an
identical repeated evidence ID is idempotent, while conflicting content under
the same ID fails explicitly. `IEvaluationCommitCoordinator` is the narrow
application transaction boundary for required evaluation evidence and any
approval creation or consumption. The default coordinator and stores remain
in-memory; no database provider or durable state is implemented yet.

Policy evaluation remains storage-independent. Activity, metrics, incidents,
exports, and portal summaries are recomputable projections applied only after
the required evaluation commit succeeds. Projection failure does not invalidate
committed evidence.

[ADR-0009: Operational State and Persistence](../adr/0009-operational-state-and-persistence.md)
defines the intended evidence, mutable-state, projection, storage, transaction,
and rollout boundaries.
