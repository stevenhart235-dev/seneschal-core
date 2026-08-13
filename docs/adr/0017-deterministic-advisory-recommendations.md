# ADR-0017: Deterministic Advisory Recommendations from Findings

## Status

Accepted

## Context

Explainable Exposure Findings identify noteworthy facts using current governance
context, retained evidence, evidence coverage, capability metadata, and immutable
configuration provenance. Operators also need a consistent answer to what they
should consider investigating next. Embedding ad hoc advice in operator pages could
bypass evidence qualification, blur findings with policy mutations, and overload
Execution Guidance, which is the runtime execution contract.

## Decision

Explainable Recommendations are a reusable, deterministic advisory projection of
Explainable Exposure Findings. A recommendation cannot exist without a source
finding and does not independently query exposure, activity, policy, or catalog
data. Each v1 finding produces at most one recommendation for its identity and
capability. Recommendations retain the source finding type, facts, coverage,
observation window, policy references, capability provenance, and configuration
fingerprints.

Recommendations suggest investigation or review. They do not establish business
necessity, prescribe an exact policy mutation, create a proposed change, simulate a
change, authorize execution, or mutate governance or runtime state. They have no
workflow status, persistence, priority, confidence, severity, or composite score.
Execution Guidance remains separate and authoritative for caller execution.

Output ordering and duplicate handling are deterministic. V1 composes the service
into the existing Identity Activity operator read model. No public API is added
until a concrete external consumer requires a versioned contract.

## Alternatives considered

### Generate recommendations directly from exposure analysis

Rejected because it bypasses the documented findings boundary and would allow
recommendations without an explicit, testable source interpretation.

### Generate exact policy changes

Deferred. Proposed changes need their own representation, schema, review boundary,
and connection to the existing non-mutating simulation path. The current evidence
does not justify a specific mutation.

### Persist recommendation workflow status

Deferred because v1 recommendations are derived read models. Accepted, rejected,
resolved, and ignored states require identity, lifecycle, and persistence contracts.

### Reuse Execution Guidance

Rejected because Execution Guidance controls whether an integrated caller may
immediately execute governed work. Analytical operator prompts are neither runtime
instructions nor authorization decisions.

### Add recommendation priority or confidence

Rejected because the repository provides no non-arbitrary semantics for either.
Coverage and curated capability risk remain visible as separate source facts.

## Consequences

- Every recommendation is traceable to facts through one deterministic finding.
- Operators can inspect why a prompt exists and the limits of its evidence.
- Findings semantics and policy evaluation remain unchanged.
- No recommendation can alter policy, approval, simulation, enforcement, or runtime
  state.
- Future proposed-change and human-review milestones must remain explicit layers
  instead of expanding recommendation behavior implicitly.

## Related documentation

- [Explainable Recommendations v1](../explainable-recommendations.md)
- [Explainable Exposure Findings v1](../explainable-exposure-findings.md)
- [ADR-0016: Deterministic Explainable Exposure Findings](0016-deterministic-explainable-exposure-findings.md)
- [ADR-0011: Shared Non-Mutating Evaluation Path](0011-shared-non-mutating-evaluation-path.md)
- [ADR-0010: Execution Guidance as the Canonical Integration Contract](0010-execution-guidance-canonical-integration-contract.md)
