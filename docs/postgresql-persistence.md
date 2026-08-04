# PostgreSQL persistence

PostgreSQL persistence is opt-in. Seneschal uses process-local in-memory stores
unless `Seneschal:Persistence:Provider` is set to `PostgreSql`.

When selected, PostgreSQL is authoritative for evaluation evidence, approvals,
runtime governance mode, and the built-in Governance Window state. After
migration validation, missing singleton control rows initialize idempotently to
the existing defaults (`LogOnly`; window disabled in `Observe` mode), without
creating an operator transition. Existing rows are never overwritten by
startup configuration.

## Local setup

```powershell
docker run --detach --name seneschal-postgres `
  --publish 5432:5432 `
  --env POSTGRES_DB=seneschal `
  --env POSTGRES_USER=seneschal `
  --env POSTGRES_PASSWORD='<local-password>' `
  postgres:17-alpine

$env:Seneschal__Persistence__Provider = 'PostgreSql'
$env:ConnectionStrings__SeneschalPostgreSql = `
  'Host=localhost;Port=5432;Database=seneschal;Username=seneschal;Password=<local-password>'
```

Do not place a real password in source-controlled settings or environment
files.

## Apply migrations

Migrations are explicit. Startup validates connectivity and fails clearly when
migrations are pending, but never creates or resets schema and never applies
migrations.

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update `
  --project Seneschal.Persistence.PostgreSql `
  --startup-project Seneschal.Persistence.PostgreSql
```

The migration command reads `ConnectionStrings__SeneschalPostgreSql`.
Separating migration execution supports controlled deployment and rollback.
The authoritative [database migration strategy](database-migrations.md)
defines deployment ordering, exact-history validation, Kubernetes ownership,
backup requirements, failure handling, and rollback constraints.

## Connection resiliency and readiness

PostgreSQL historical and investigation reads use a small provider-scoped retry
boundary. A read is attempted once and retried at most twice, after 50 ms and
150 ms. Each attempt creates a new `DbContext`, so a failed pooled connection is
disposed and the complete query is restarted; partially materialized results
are never reused. Async reads honor request cancellation. The older synchronous
approval and operational-control read contracts use the same two short delays,
but cannot accept a cancellation token.

Classification delegates to Npgsql's `NpgsqlException.IsTransient`. In the
current provider this includes network I/O, socket, and timeout failures and
PostgreSQL's transient SQLSTATE set, including connection-class failures,
`57P01` administrator shutdown, crash shutdown, temporarily unavailable or
resource-limited server states, serialization/deadlock/lock failures, and
transaction-resolution-unknown. A standalone `TimeoutException` is also
transient. Request cancellation is never retried. Authentication failure,
invalid connection configuration, missing databases, pending migrations,
constraint violations, EF concurrency conflicts, invalid transitions,
evidence conflicts, and other non-transient SQL errors are not retried.

When an already-created connection fails with `57P01` or an underlying network
I/O/socket failure, Seneschal clears only the Npgsql pool associated with that
connection. This prevents other connections made stale by the same PostgreSQL
restart from surfacing one failure each. Other transient failures, including
timeouts, do not clear the pool. Npgsql and disposal handle the individual
failed connection normally.

After retries are exhausted, HTTP reads return `503 Service Unavailable` with
a stable message and log only safe exception-type context. Raw provider errors,
SQLSTATE values, server details, and connection strings are not returned.
InMemory reads are unchanged and have no retry boundary.

`GET /health` remains a process-liveness check and does not depend on
PostgreSQL. `GET /ready` is provider-aware. InMemory remains immediately ready
when the existing catalog and policy checks pass. With PostgreSQL selected,
each readiness request uses a fresh context, a two-second probe/command bound,
checks the release's migration history for known pending migrations, and runs a
non-mutating `SELECT 1`. It returns HTTP 503 with `status: not_ready` when the
database is unavailable, credentials/database are invalid, or migrations are
pending, and recovers on a later probe without restarting Seneschal. The
response adds only provider name, reachability, and migration-current booleans;
it never includes exception or connection details. Migration state is checked
on every probe rather than cached, so readiness cannot stay healthy through a
database replacement at the cost of two lightweight metadata queries.

Transactional writes are deliberately not automatically retried. Their
explicit transactions enforce evidence idempotency, approval/incident/control
atomicity, and optimistic conflicts, while a lost connection during commit can
leave the commit outcome ambiguous. Replaying such a transaction without a
separate durable outcome-verification protocol could duplicate or misreport an
operator transition. Existing HTTP 409 conflict and safe HTTP 503 provider
failure behavior therefore remains authoritative.

## Start and verify

```powershell
dotnet run --project Seneschal.Api
```

Submit authenticated evaluations, inspect `GET /audit` and `/approvals`, restart
Seneschal without removing PostgreSQL, and confirm evidence and approval state
remain. Pending, approved, rejected, and consumed approvals retain their
resolution and consumption metadata. To return to
transient behavior, unset the variables or select `InMemory`; in-memory
evidence and approvals reset on restart.

The evidence table stores indexed identifiers and timestamps alongside the
complete `AuditEvent` JSONB payload. A SHA-256 fingerprint of canonical JSON
distinguishes identical retries from conflicting writes under the same evidence
ID. A database primary key enforces uniqueness and an identity sequence makes
equal-timestamp ordering deterministic.

Dashboard decision distribution, top capabilities, active identities, and
Technology Explorer evaluation/deny/pending counts use PostgreSQL `GROUP BY`,
conditional `COUNT`, `MAX`, and distinct identity/capability queries over the
extracted evidence columns. Administrative approval-transition evidence is not
counted as a runtime evaluation. Audit Trail preserves timestamp-descending,
append-sequence-ascending ordering and Decision Trace reconstructs the complete
event from JSONB. Live Monitor remains intentionally process-local and shows
newly committed activity rather than becoming a historical database view.
Capability Activity and Identity Activity use relational all-history counts,
distinct identity/capability and environment queries, and last-observed times.
Their detailed timelines reconstruct at most 100 complete JSONB events ordered
by timestamp descending and append sequence ascending, including approval,
operation, policy, runtime-mode, and governance-window context. These summaries
and timelines survive Seneschal restart; InMemory activity and evidence reset.

Approval state transitions are Pending to Approved, Pending to Rejected, and
Approved to Consumed. Invalid and repeated transitions fail explicitly. A
concurrency version protects direct updates, conditional updates protect atomic
evaluation commits, and transaction-scoped PostgreSQL advisory locks serialize
duplicate creation for one correlation scope. Approval/rejection state and its
administrative evidence commit together; consumption and the consuming decision
evidence continue to commit together.

Incident occurrence count, first/last observed time, severity, reason, policy,
capability, and identity remain projections rebuilt from durable evaluation
evidence. Their deterministic incident key reconnects each projection to the
matching `incident_operator_state` row. PostgreSQL stores only status, version,
and update time. Missing state is Open/version 0; orphaned state is retained but
not displayed until matching evidence projects the incident again.

Acknowledge and resolve transitions use optimistic versions and commit the
operator-state change with immutable `incident_acknowledged` or
`incident_resolved` evidence in one transaction. Stale conflicts return HTTP
409 and provider failures return HTTP 503. InMemory incident state remains
process-local and resets on restart.

Runtime-mode and Governance Window mutations likewise commit versioned state
and `runtime_mode_*` or `governance_window_*` administrative evidence in one
transaction. Optimistic versions reject stale conflicting forms with HTTP 409.
Reapplying the already-current value is a no-op and creates no duplicate
evidence. The fixed Production Freeze definition remains product configuration;
its enabled flag, mode, update metadata, reason, and version are operational
state.

## Current limitations

Evaluation evidence, the existing approval lifecycle, runtime mode, the
built-in Governance Window state, and incident operator status/version are
durable. Incident detection and derived fields, activity, metrics, catalog, and
graph projections remain recomputable.
Matched-policy aggregates and evaluation-duration averages also remain
process-local because those fields are not extracted relational columns; the
PostgreSQL read model does not scan JSONB or invent replacements for them.
Policies, capabilities, identities, and integration keys remain YAML-backed.
Read retries cover provider-backed historical, audit, approval, incident, and
runtime-control reads; they do not make a prolonged database outage invisible.
The maximum added retry delay is 200 ms, while provider connection timeout
settings still bound individual connection attempts. Transactional writes are
not replayed after transient connection failures or ambiguous commits.
Backup, restore, retention, high availability, multitenancy, and secret delivery
remain deployment concerns.

See [ADR 0009](adr/0009-operational-state-and-persistence.md).
