# Persistence

Seneschal loads capabilities, identities, policies, and integration keys from
YAML. In-memory persistence remains the default. When explicitly configured,
the PostgreSQL provider durably stores append-only evaluation evidence and the
minimal approval state required for atomic approval creation or consumption.
Other operational state remains process-local or recomputable.

`IAuditEventStore` is the provider-neutral append-only evaluation-evidence
contract. Committed `AuditEvent` records are immutable by contract: an
identical repeated evidence ID is idempotent, while conflicting content under
the same ID fails explicitly. `IEvaluationCommitCoordinator` is the narrow
application transaction boundary for required evaluation evidence and any
approval creation or consumption. PostgreSQL commits those effects in one
database transaction and stores complete evidence as JSONB with indexed fields
and a stable content fingerprint.

Policy evaluation remains storage-independent. Activity, metrics, incidents,
exports, and portal summaries are recomputable projections applied only after
the required evaluation commit succeeds. Projection failure does not invalidate
committed evidence.

[ADR-0009: Operational State and Persistence](../adr/0009-operational-state-and-persistence.md)
defines the intended evidence, mutable-state, projection, storage, transaction,
and rollout boundaries. See the
[PostgreSQL setup guide](../postgresql-persistence.md) for configuration,
migrations, verification, and current limitations.
