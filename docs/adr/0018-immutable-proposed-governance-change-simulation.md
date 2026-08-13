# ADR-0018: Immutable Proposed Governance Change Simulation

## Status

Accepted

## Context

Recommendations can identify an investigation worth considering but do not define
or justify policy mutation. Operators need to inspect one explicit hypothetical
change and compare its effect with current governance without installing policy,
writing evidence, consuming approval, or creating a second evaluator.

## Decision

Proposed Governance Change v1 uses versioned semantic operations rather than
JSON/YAML patches. V1 supports only `RemoveCapabilityFromPolicy`. A proposal is
distinct from its source finding and recommendation and is bound to the active
semantic governance fingerprint from which it was derived. V1 rejects stale
proposals instead of rebasing them.

The operation is applied to an immutable request-scoped clone of Policy Schema v1
models. The complete hypothetical policy set passes existing schema, semantic, and
referential validation. It is never installed in `PolicyLoader` or written to disk.

Current and proposed outcomes use one canonical `CoreDecisionService` evaluation
flow and both remain on its non-committing preview path. Comparison snapshots the
normalized request identity, timestamp, operation ID, runtime mode, governance
window, and relevant approval record. Only policy configuration varies.

A proposed semantic fingerprint is labeled hypothetical and is never persisted as
active or historical evaluation provenance. Static before/after relationships are
configuration facts, not proof of all runtime outcomes or real-world risk reduction.
Proposal simulation remains separate from application, change approval, runtime
approval, Execution Guidance, and enforcement.

## Alternatives considered

JSON/YAML patches were rejected because they couple intent to document layout.
Complete replacement policy documents were rejected as the primary proposal model
because they obscure the single reviewed intent. A second evaluator and temporary
DI/loader replacement were rejected because they risk semantic divergence and
active-state contamination. Stale simulation with a warning was rejected because it
would apply an operation to a configuration other than the reviewed base.

## Consequences

- Every candidate remains traceable to a finding and recommendation.
- Invalid, ambiguous, and stale proposals fail deterministically.
- Existing `/evaluate`, `/preflight`, and policy simulation behavior remains intact.
- Runtime outcomes are established only for explicit simulated request contexts.
- Application, persistence, review workflow, rebasing, and additional operations
  require future architecture.

## Related documentation

- [Proposed Governance Changes v1](../proposed-governance-changes.md)
- [ADR-0010](0010-execution-guidance-canonical-integration-contract.md)
- [ADR-0011](0011-shared-non-mutating-evaluation-path.md)
- [ADR-0012](0012-versioned-policy-authoring-contract.md)
- [ADR-0017](0017-deterministic-advisory-recommendations.md)
