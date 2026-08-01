# ADR-0009: Operational State and Persistence

## Status

Accepted

## Context

Seneschal currently runs as one ASP.NET Core process. YAML files provide policy,
identity, capability, and integration-key configuration. Singleton in-memory
implementations retain decisions, activity, approvals, incidents, runtime mode,
governance-window state, metrics, the capability catalog, and the governance
graph. Restarting the process resets all operational state.

The current state has different authority and lifecycle requirements:

- An evaluation result is evidence of what Seneschal decided from a particular
  request, policy result, runtime mode, governance window, and approval state.
- Approvals, runtime mode, governance windows, and operator incident status are
  mutable controls whose latest value affects operation or workflow.
- Activity summaries, metrics, incident aggregation, catalog entries,
  governance relationships, and explorer views can be reconstructed from more
  authoritative inputs.
- YAML loaders are configuration adapters, not operational persistence.

Treating all of these as equivalent database records would obscure ownership,
make the decision engine storage-aware, and create inconsistent failure
semantics. Production persistence therefore needs explicit boundaries before
an implementation is selected.

## Decision

### State classification

#### Durable append-only evidence

The durable evidence log contains immutable records for:

- every completed evaluation, including the request identifiers and context
  needed for attribution, policy result and explanation, effective decision,
  enforcement mode, governance-window participation, approval correlation,
  execution guidance, and timing;
- approval requests, resolutions, consumption, and other approval transitions;
- runtime-mode and governance-window changes; and
- operator incident acknowledgements and resolutions.

Evidence records are appended, never updated in place. Corrections and
redactions are represented by new records and controlled retention processes,
not silent mutation. Retention, archival, and legally required deletion remain
separate lifecycle operations.

The existing `AuditEvent` is the starting evaluation-evidence contract, but it
does not yet model every administrative transition. Follow-up work may define
additional evidence event types or an envelope without changing policy
evaluation.

#### Durable mutable operational state

The following latest-state records are durable and concurrency-controlled:

- approval state, including pending, approved, rejected, and consumed status;
- current runtime enforcement mode;
- current governance-window definition and enabled/mode state; and
- operator-managed incident status such as open, acknowledged, and resolved.

Each mutation writes its append-only evidence record in the same database
transaction as the current-state change. Optimistic concurrency or equivalent
conditional updates prevent double approval consumption, conflicting
resolution, and lost control-plane updates.

Incident aggregation fields such as occurrence count, first/last seen, and
derived severity are not authoritative mutable state. They are projections;
only the operator-authored status and its transition evidence are authoritative.

#### Recomputable projections

These records and views are rebuilt from durable evidence or YAML sources:

- capability, identity, and policy activity summaries;
- decision metrics and current Prometheus output;
- incident grouping, occurrence counts, first/last-seen times, and severity;
- decision export read models, except any delivery outbox needed to guarantee
  transfer to an external system;
- the capability catalog projected from capability configuration;
- declared governance relationships projected from policy configuration;
- the governance graph, Capability Explorer, Technology Explorer, graph view,
  and other composed portal read models; and
- the deterministic Northwind demo history and its derived views.

Projection records may be stored for query performance, but they are disposable
and versioned. A projector version and checkpoint identify how far a projection
has processed the evidence log. Projection failure or lag does not alter an
already committed decision.

#### Initially YAML-backed configuration

Capabilities, identities, policies, and integration API keys remain YAML-backed
initially. Their current loaders and ASP.NET Core path overrides remain the
configuration boundary. The capability catalog and declared governance graph
continue to project from these files.

Production secret delivery for integration-key YAML must use the deployment
platform's secret mechanism. Moving catalogs, policies, or secrets into a
database is a separate decision and migration.

#### Intentionally transient state

The following remain transient:

- in-flight HTTP requests and decision objects;
- evaluator working sets, loaded-configuration caches, renderer models, and
  health/readiness results;
- `ActivitySource` spans after export;
- process-local caches of durable records and projections; and
- best-effort telemetry already exported to an external telemetry system.

Transient data must not be required to reconstruct authoritative evidence or
mutable operational state.

### Persistence boundaries

Domain-facing persistence contracts live in `Seneschal.Core` alongside the
existing store interfaces. Database-specific implementations, schema mapping,
migrations, connection management, and transaction orchestration belong in a
separate product-owned infrastructure implementation referenced by the API
composition root. They do not belong in domain models, the policy evaluator,
Razor Pages, or YAML loaders.

Some current interfaces will need refinement. In particular, the runtime-mode
contract should move out of the API layer, incident projection must be separated
from operator status, and approval/evidence writes need a transactional
application-service boundary. Those changes should preserve the current Core
policy-evaluation API.

`IPolicyEvaluator` remains deterministic and storage-independent. It receives a
request, policies, and the applicable control-state snapshot and returns a
result. An application orchestration service performs authorization, reads the
required operational snapshot, invokes the evaluator, and commits durable
effects. The evaluator does not open transactions, query repositories, or emit
database records.

### Evaluation transaction and failure semantics

For a production evaluation:

1. Authenticate and authorize the integration key before evaluation.
2. Read the applicable runtime mode, governance window, and approval state with
   the concurrency information required for a consistent update.
3. Evaluate policy in memory.
4. In one database transaction, conditionally create or consume approval state
   when applicable and append the immutable evaluation evidence.
5. Commit before returning a successful decision response.
6. Update activity, metrics, incidents, graph observations, and external exports
   asynchronously from committed evidence, using idempotent projectors and an
   outbox where delivery guarantees are required.

If the authoritative transaction fails, Seneschal does not return a successful
decision. It returns a retryable service-unavailable response and does not tell
the caller to proceed. This prevents an operation from being authorized without
durable evidence. Projection, metrics, or export failure after the commit does
not change the returned decision.

Evaluation evidence IDs are unique and inserts are idempotent. Approval
consumption is a conditional, single-use update in the same transaction as its
decision evidence. A response can still be lost after commit, so safe request
retry requires a stable caller-provided idempotency contract. Until that
contract is defined, retries may create separate decision evidence even though
each individual commit is atomic.

Administrative mutations follow the same rule: current state and its immutable
transition evidence commit together or neither commits.

### Storage technology

PostgreSQL is the initial production persistence technology. It provides
transactions, conditional updates, JSON support for evolving evidence details,
strong managed-service availability across common deployment platforms, and a
good fit for containerized single-instance growth into multiple API replicas.
A relational core schema should retain indexed identifiers, timestamps,
decision fields, and state transitions; JSON columns may hold structured
explanation details that do not yet justify separate relations.

SQLite is the local development and fast automated-testing approach. It keeps
local setup small and supports repository contract tests, but it is not a
production multi-replica store. PostgreSQL integration tests remain necessary
for transaction isolation, concurrency, JSON, and migration behavior that
SQLite cannot prove.

The database choice does not permit provider-specific behavior to leak into
Core contracts or decision semantics.

## Consequences

- Decisions and control changes survive process and container replacement.
- Successful decisions have committed evidence, while non-authoritative
  projections can recover independently.
- Approval consumption and its decision evidence can be made atomic.
- The policy evaluator remains deterministic, testable, and independent of
  storage latency and provider APIs.
- Portal summaries can lag the evidence log and must expose projection health
  when that lag matters operationally.
- PostgreSQL becomes a production dependency requiring migrations, backup,
  restore, monitoring, retention, and credential management.
- SQLite improves local ergonomics but cannot substitute for PostgreSQL
  concurrency and migration validation.
- Existing store interfaces require incremental refinement rather than a
  database implementation being substituted blindly for every in-memory type.
- Fail-closed evidence semantics add database availability and latency to the
  evaluation service-level objective.

## Alternatives considered

### SQLite for production

SQLite is operationally simple and useful for one local process. Its file-level
deployment, write-concurrency constraints, volume ownership requirements, and
poor fit for multiple replicas make it unsuitable for the production target.
Using it in production would also encourage treating a container filesystem or
single network volume as a database architecture.

### SQL Server for production

SQL Server supplies the required transactions, concurrency, JSON facilities,
tooling, and managed-service options. It is a viable organizational alternative,
especially where SQL Server operations are already standardized. It is not the
initial recommendation because PostgreSQL has a lighter default operational and
licensing footprint for the current containerized product and broader neutral
managed-service availability. Core abstractions should not preclude a later SQL
Server provider.

### PostgreSQL for both production and every local test

This gives the highest provider fidelity but increases setup time and makes
fast unit and repository-contract feedback depend on a running service. Local
SQLite plus a required PostgreSQL integration suite balances speed with
production confidence.

### Persist every current in-memory object directly

This would duplicate evidence into mutable aggregates, make rebuilds difficult,
and treat portal read models as sources of truth. Separating evidence,
operational state, and projections gives each the correct consistency and
retention model.

### Asynchronous audit after returning the decision

This minimizes evaluation latency but can authorize an operation whose evidence
is lost during a crash or delivery failure. Seneschal instead commits required
evidence before returning success and moves only projections and exports off the
critical path.

## Rollout plan

1. Specify stable evidence identifiers, idempotency behavior, retention fields,
   repository contract tests, and the transactional application-service port.
2. Add an infrastructure project with SQLite and PostgreSQL implementations and
   migration tooling, without changing the evaluator.
3. Introduce the append-only evaluation-evidence store first. Run it in shadow
   mode against the in-memory path and compare counts and records.
4. Put approval creation/consumption and evaluation evidence behind one
   transaction, then cut over approval reads and writes.
5. Persist runtime mode, governance-window state, and operator incident status,
   with a transition evidence record for every mutation.
6. Rebuild activity, metrics, and incident aggregation from evidence. Add
   projector versions, checkpoints, replay, and lag diagnostics.
7. Rebuild YAML-derived catalog and governance-graph projections at startup or
   configuration version changes; persist them only if query scale requires it.
8. Exercise migration, backup/restore, concurrency, retry, and failure-injection
   tests against PostgreSQL before enabling more than one API replica.
9. Remove in-memory production registrations only after parity, rollback, and
   recovery procedures are proven. Retain in-memory fakes for focused tests.

## Open questions

- What caller-provided request or idempotency key becomes part of the public
  evaluation contract?
- What retention, archival, legal-hold, and redaction rules apply to evidence?
- What tenant or organizational boundary must appear in every key and query?
- Which transaction isolation level meets approval-consumption and
  control-state consistency requirements without excessive contention?
- Should evaluation evidence record full configuration snapshots, immutable
  configuration version identifiers, or both?
- What recovery-point and recovery-time objectives govern PostgreSQL backup and
  restore?
- Which projections require synchronous freshness, and how will the portal show
  lag or rebuild status?
- Does external audit export require a transactional outbox, and what delivery
  deduplication contract will consumers use?
- Which evidence fields require encryption, tokenization, or access filtering?

## Out-of-scope items

- Implementing a database, package, migration, or repository in this decision
- Moving YAML-backed policy, identity, capability, or integration-key
  configuration into a database
- Selecting a secret manager or Kubernetes secret-distribution mechanism
- Designing analytics warehouses, SIEM schemas, or long-term archive formats
- Changing policy matching, decision resolution, or enforcement semantics
- Multi-region active-active evaluation
- General event sourcing of every Seneschal domain object
- Durable workflow engines, schedulers, or distributed locks beyond the stated
  operational transaction requirements

## Follow-up implementation issues

1. Define the evaluation evidence schema, stable idempotency contract,
   transactional application-service port, and provider-neutral repository
   contract tests.
2. Add SQLite and PostgreSQL infrastructure implementations with versioned
   migrations and PostgreSQL integration tests.
3. Make approval creation and single-use consumption atomic with evaluation
   evidence append.
4. Add durable runtime-mode and governance-window stores with administrative
   transition evidence.
5. Separate incident aggregation projection data from operator-managed incident
   status.
6. Add evidence-driven activity, metrics, and incident projectors with
   checkpoints, replay, and lag diagnostics.
7. Define retention, archival, backup/restore, and evidence-access controls.
8. Add failure-injection and concurrency tests for database unavailability,
   commit ambiguity, duplicate requests, and competing approval consumption.

The first implementation issue should be item 1. Establishing the evidence and
transaction contracts before choosing ORM mappings prevents database concerns
from leaking into the evaluator and gives both SQLite and PostgreSQL a common,
testable behavioral target.
