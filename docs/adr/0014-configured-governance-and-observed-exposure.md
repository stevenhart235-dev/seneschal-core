# ADR-0014: Separate Configured Governance Exposure from Observed Evidence

## Status

Accepted

## Context

Identity investigation needs to compare the capabilities represented by current
policy configuration with capabilities recorded in runtime evidence. Treating
non-use as lack of need, or treating evidence outside current configuration as
unauthorized, would make claims that Seneschal's policy and activity data cannot
prove. Future recommendations and risk features need a trustworthy factual
foundation that does not blur static configuration, runtime evidence, and policy
evaluation.

## Decision

Exposure analysis maintains configured governance context and observed runtime
evidence as separate facts joined by stable identity and capability IDs. Every
analysis uses an explicit inclusive UTC observation window and discloses that
results depend on retained evidence.

A current direct policy target establishes configured governance context only. It
does not establish a definitive authorization outcome. Runtime audit evidence
establishes observation only. Absence of evidence during the window is described
as "No observed use in selected period" and does not imply lack of business need,
overprivilege, or safe removal. Evidence without a current configured relationship
is described neutrally as "Observed outside current configured governance
context" and is not automatically unauthorized.

Exposure analysis reuses policy configuration, `ICapabilityCatalog`, provenance,
and `IAuditEventStore`. It does not call, reproduce, or alter the policy evaluator.
Curated risk metadata may be grouped and counted, but no composite risk or exposure
score is derived.

## Alternatives considered

### Re-evaluate historical requests against current policy

Rejected because that would answer a counterfactual question, could diverge from
the original runtime context, and would turn an operator read model into a second
evaluation path.

### Label configured but unobserved capabilities as excess permissions

Rejected because non-observation cannot prove business necessity, entitlement, or
removal safety.

### Label observed-only capabilities as unauthorized

Rejected because current configuration may differ from historical configuration
and retained evidence may be imported or partial.

### Calculate a composite exposure score

Deferred. A numerical score requires independently documented semantics,
calibration, and evidence quality guarantees.

## Consequences

- Operators receive deterministic, explainable facts rather than recommendations.
- Zero-activity identities remain investigable.
- Observation-window boundaries and evidence retention affect results and must be
  visible.
- Future remediation, recommendation, and risk layers must preserve the factual
  distinction and document any stronger inference separately.
- Policy evaluation and Capability Packs remain unchanged.

## Related documentation

- [Identity Governance Exposure Analysis](../identity-governance-exposure.md)
- [Operator Navigation Spine](../product/operator-navigation-spine.md)
- [ADR-0006: Capability Explorer Read Model](0006-capability-explorer-read-model.md)
- [ADR-0011: Shared Non-Mutating Evaluation Path](0011-shared-non-mutating-evaluation-path.md)