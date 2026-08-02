# PostgreSQL persistence

PostgreSQL persistence is opt-in. Seneschal uses process-local in-memory stores
unless `Seneschal:Persistence:Provider` is set to `PostgreSql`.

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
migrations are pending, but never creates, resets, or migrates the database.

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

Approval state transitions are Pending to Approved, Pending to Rejected, and
Approved to Consumed. Invalid and repeated transitions fail explicitly. A
concurrency version protects direct updates, conditional updates protect atomic
evaluation commits, and transaction-scoped PostgreSQL advisory locks serialize
duplicate creation for one correlation scope. Approval/rejection state and its
administrative evidence commit together; consumption and the consuming decision
evidence continue to commit together.

## Current limitations

Only evaluation evidence and the existing approval lifecycle are durable.
Incidents, runtime mode, governance windows, activity,
metrics, catalog, and graph projections remain process-local or recomputable.
Matched-policy aggregates and evaluation-duration averages also remain
process-local because those fields are not extracted relational columns; the
PostgreSQL read model does not scan JSONB or invent replacements for them.
Policies, capabilities, identities, and integration keys remain YAML-backed.
Backup, restore, retention, high availability, multitenancy, and secret delivery
remain deployment concerns.

See [ADR 0009](adr/0009-operational-state-and-persistence.md).
