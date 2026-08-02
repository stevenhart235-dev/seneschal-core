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
Backup, restore, retention, high availability, multitenancy, and secret delivery
remain deployment concerns.

See [ADR 0009](adr/0009-operational-state-and-persistence.md).
