# Persistence

Seneschal loads capabilities, identities, policies, and integration keys from
YAML. In-memory persistence remains the default. When explicitly configured,
the PostgreSQL provider durably stores append-only evaluation evidence and the
complete existing approval lifecycle: pending, approved, rejected, and consumed
state plus resolution and consumption metadata.
Other operational state remains process-local or recomputable.

`IAuditEventStore` is the provider-neutral append-only evaluation-evidence
contract. Committed `AuditEvent` records are immutable by contract: an
identical repeated evidence ID is idempotent, while conflicting content under
the same ID fails explicitly. `IEvaluationCommitCoordinator` is the narrow
application transaction boundary for required evaluation evidence and any
approval creation, resolution, or consumption. PostgreSQL commits those effects in one
database transaction and stores complete evidence as JSONB with indexed fields
and a stable content fingerprint.

Approval transitions are limited to Pending to Approved, Pending to Rejected,
and Approved to Consumed. PostgreSQL uses conditional updates and a concurrency
version to reject stale or duplicate transitions. Transaction-scoped advisory
locks serialize duplicate approval creation for the same correlation scope.
In-memory approval state follows the same lifecycle but resets on restart.

Policy evaluation remains storage-independent. Activity, metrics, incidents,
exports, and portal summaries are recomputable projections applied only after
the required evaluation commit succeeds. Projection failure does not invalidate
committed evidence.

When PostgreSQL is selected, Dashboard decision counts, capability activity,
identity activity, and Technology Explorer evaluation totals are queried from
the extracted `evaluation_evidence` columns. Audit Trail and Decision Trace
continue to reconstruct complete events from authoritative JSONB. Live Monitor
remains a process-local operational stream; it is not a historical read model.
Capability Activity and Identity Activity use the same durable relational
summaries and reconstruct their newest 100 detailed events from JSONB in
canonical evidence order. Their PostgreSQL history therefore survives process
restart, while the InMemory versions remain process-local.
Matched-policy activity, duration averages, metrics, incidents, and governance
window state remain transient because their required aggregation fields are not
currently extracted for relational evidence queries.

[ADR-0009: Operational State and Persistence](../adr/0009-operational-state-and-persistence.md)
defines the intended evidence, mutable-state, projection, storage, transaction,
and rollout boundaries. See the
[PostgreSQL setup guide](../postgresql-persistence.md) for configuration,
migrations, verification, and current limitations.
