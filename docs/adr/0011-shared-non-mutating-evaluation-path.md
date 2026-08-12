# ADR-0011: Shared Non-Mutating Evaluation Path for Preflight and Simulation

## Status

Accepted

## Context

Operators and integrations need to inspect the result of a real policy
evaluation before executing work. A useful preview must include the same policy
matching, decision resolution, approval interpretation, runtime-mode
projection, governance-window effects, EffectiveAction, and Execution Guidance
as operational evaluation.

A separate simulation evaluator would duplicate policy semantics and could
disagree with the committing path. Calling the operational endpoint for a
preview would instead create evidence, approvals, incidents, projections, and
telemetry for work that was never attempted.

## Decision

`POST /evaluate` remains the committing operational evaluation path.
`POST /preflight` is the canonical non-mutating evaluation path.

Both paths use `CoreDecisionService` and the same Core policy evaluator. The
service's `Evaluate` and `Preview` entry points call the same private
evaluation flow; only `Evaluate` crosses the commit boundary. Preflight
therefore reuses current policy and decision semantics rather than implementing
a simulation engine.

`seneschal preflight` and `seneschal policy simulate` consume the shared
`/preflight` path for different operator experiences. Preflight emphasizes
endpoint, credential, catalog, scope, and guidance readiness. Policy simulation
presents the hypothetical governance outcome and explanation.

Preview may calculate:

- normal policy decisions and matched policies;
- reasons and policy evaluation details;
- approval effects, including how an existing approval would affect the
  decision;
- runtime-mode and governance-window effects;
- EffectiveAction; and
- Execution Guidance.

Deny and RequireApproval are valid preview outcomes, not transport or
integration failures. Whether hypothetical work may execute is determined by
the canonical Execution Guidance contract described in
[ADR-0010](0010-execution-guidance-canonical-integration-contract.md).

## Mutation boundary

Preview reads the current policies and relevant operational state needed to
calculate the result. It may construct transient request, result, tracing, and
planned approval objects in memory. It must not commit:

- audit evidence;
- approval creation or consumption;
- incidents;
- activity or export events;
- decision metrics; or
- runtime-mode or governance-window state changes.

The current boundary is enforced in `CoreDecisionService`: evidence and the
planned approval mutation are sent to the evaluation commit coordinator only
when `commit` is true. Activity, export, metrics, and incident recording occur
after that commit call on the same committing branch. Preview returns the
calculated response without entering that branch.

Authentication, catalog validation, and integration-key scope checks at
`/preflight` remain request validation, not policy simulation semantics.

## Alternatives considered

### Separate simulation evaluator

This was rejected because it would duplicate policy matching, decision
resolution, approval handling, governance-window behavior, and guidance
mapping. Test parity would reduce but not remove the risk that preview approves
work the operational evaluator blocks, or vice versa.

### Call `/evaluate` and discard the response effects

This was rejected because committing evaluation is intentionally observable.
It may append evidence, create or consume approvals, update incidents and
activity, emit exports, and record metrics. Those effects cannot be treated as
a harmless preview or reliably undone.

### Implement simulation in each CLI

This was rejected because clients do not own the authoritative policies or
operational state and would need to reproduce server semantics. The two CLI
commands instead present the same server-side preview for different workflows.

## Consequences

- Preview and operational evaluation share policy and decision semantics.
- Preview reflects current approval, runtime-mode, and governance-window state
  without mutating that state.
- Preview results can include Deny or RequireApproval while the request itself
  succeeds.
- The non-mutating guarantee depends on keeping all durable and projected
  writes behind the explicit commit branch.
- Transient computation and tracing may still occur because non-mutating means
  no committed governance or operational state, not no CPU work or diagnostics.
- Changes to evaluation semantics are exercised through one service path
  instead of coordinated simulation and operational implementations.

## Future implications

Future simulation capabilities should extend this shared evaluation path unless
a separate ADR documents why they cannot. Proposed-policy comparison should
provide proposed policy input to a non-committing evaluation context and compare
results without replacing the authoritative evaluator or crossing the mutation
boundary. The policy input format, isolation model, and comparison response are
not defined by this ADR.

## Related documentation

- [Policy Evaluation](../reference/policy-evaluation.md)
- [Approval Execution Guidance](../product/approval-execution-guidance.md)
- [Seneschal CLI](../../Seneschal.Cli/README.md)
- [ADR-0007: Make Core the Authoritative Runtime](0007-api-runtime-convergence.md)
- [ADR-0009: Operational State and Persistence](0009-operational-state-and-persistence.md)
- [ADR-0010: Execution Guidance as the Canonical Integration Contract](0010-execution-guidance-canonical-integration-contract.md)
