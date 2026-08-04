# Persistence

Seneschal loads capabilities, identities, policies, and integration keys from
YAML. In-memory persistence remains the default. When explicitly configured,
the PostgreSQL provider durably stores append-only evaluation evidence and the
complete existing approval lifecycle: pending, approved, rejected, and consumed
state plus resolution and consumption metadata. PostgreSQL also stores the
current runtime governance mode and built-in Governance Window state.
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

PostgreSQL read models execute through one bounded retry boundary: two retries
with 50 ms and 150 ms delays, a fresh context per attempt, Npgsql-native
transient classification, and request cancellation for asynchronous reads.
This covers authoritative audit/Decision Trace, durable dashboard and activity
aggregates, approval queries, incident projections/operator state, and runtime
mode/Governance Window reads. Exhaustion becomes the API's safe persistence
unavailable response rather than exposing provider exceptions. A detected
administrator shutdown or connection-level I/O failure clears only the affected
Npgsql pool so all connections made stale by a server restart are retired.

The persistence provider also supplies readiness state. InMemory has no
dependency. PostgreSQL readiness opens a fresh connection, checks known pending
migrations, and performs a non-mutating scalar query on every probe under a
two-second bound. This intentionally keeps `/health` as process liveness while
`/ready` returns non-ready during database unavailability or schema mismatch
and recovers automatically after PostgreSQL returns.

Write execution remains outside the retry boundary. The provider's explicit
transactions, deterministic evidence identifiers, idempotency checks, and
optimistic concurrency rules remain unchanged; an ambiguous commit is not
blindly replayed.
Matched-policy activity, duration averages, metrics, incidents, and governance
window projections remain transient.

PostgreSQL owns one versioned singleton row for runtime mode and one for the
Governance Window. Missing rows initialize idempotently to the existing
`LogOnly` and disabled/`Observe` defaults after migration validation, without
operator evidence. Existing durable rows always win over configuration
defaults. Each real change conditionally updates its row and appends immutable
administrative evidence in one transaction; identical no-op retries do not add
evidence and stale conflicting versions fail. InMemory keeps the same defaults
but resets both controls when the process restarts.

Incident detection remains a replayable projection derived from evaluation
evidence. Its grouping fields are capability ID, identity ID, decision reason,
and the first nonblank matched policy. The stable incident ID is `incident-`
plus the lowercase SHA-256 hash of those four trimmed, lowercase, length-prefixed
values in that order. This lets a refreshed projection reconnect to the same
operator state without persisting occurrence counts, timestamps, severity, or
other derived fields as authoritative data.

Only the existing operator-managed status (`Open`, `Acknowledged`, or
`Resolved`) and its concurrency version are durable. PostgreSQL stores them in
`incident_operator_state`; it batch-merges matching rows onto each freshly
derived projection. A missing row means Open/version 0. Rows without a current
projection are retained but not displayed.

Acknowledge and resolve conditionally advance the version and append immutable
`incident_acknowledged` or `incident_resolved` evidence in one transaction.
Stale versions fail explicitly, while failed evidence writes roll back state.
InMemory preserves status during matching projection refreshes but resets all
incident state on process restart.

[ADR-0009: Operational State and Persistence](../adr/0009-operational-state-and-persistence.md)
defines the intended evidence, mutable-state, projection, storage, transaction,
and rollout boundaries. See the
[PostgreSQL setup guide](../postgresql-persistence.md) for configuration,
migrations, verification, and current limitations.

Database changes are explicit and forward-only: startup validates but never
applies migrations. The currently supported production pairing is one release
with its complete migration set, using a stop-the-writer deployment; rolling
N-1 schema compatibility is not yet guaranteed. Production rollback requires a
validated compatible application or restoration of the pre-migration backup.
See the [database migration strategy](../database-migrations.md).
