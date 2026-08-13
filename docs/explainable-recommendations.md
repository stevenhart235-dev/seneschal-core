# Explainable Recommendations v1

Explainable Recommendations are deterministic, read-only investigation prompts
derived exclusively from Explainable Exposure Findings. A finding answers, "What
facts are noteworthy?" A recommendation answers, "What should an operator consider
reviewing?"

A recommendation does not establish business necessity, prescribe a configuration
mutation, authorize execution, represent a score, or change policy or runtime state.
Execution Guidance remains the separate canonical instruction for whether an
integrated caller may execute governed work.

```text
Governance facts
    |
Exposure analysis
    |
Finding
    |
Recommendation
    |
Future proposed change
    |
Simulation
    |
Future human review
    |
Enforcement
```

A future proposed change would describe an exact configuration mutation. Existing
simulation would evaluate such configuration without committing operational state.
Enforcement remains the runtime path that governs execution. V1 implements only
the Finding and Recommendation analytical layers in this sequence.

## Deterministic mappings

Each supported finding produces at most one recommendation for the same identity
and capability:

- `HighRiskConfiguredNotObserved` -> `ReviewCurrentGovernanceRelationship`:
  review whether the capability should remain represented in current governance
  context. Full coverage reports no observed activity during the fully covered
  period. Partial coverage reports only that no use was found in retained evidence.
  Unknown coverage creates no source finding and therefore no recommendation.
- `ObservedOutsideCurrentGovernanceContext` ->
  `ReviewHistoricalActivityAgainstCurrentGovernance`: compare recorded activity,
  current governance, and available historical fingerprints without assuming the
  cause or proposing that a policy be added.
- `HistoricalConfigurationDiffers` -> `ReviewHistoricalConfigurationChanges`:
  review changes relevant to the historical period. A fingerprint difference
  cannot identify a particular changed policy or invalidate either configuration.
- `HighRiskCapabilityActivelyObserved` -> `ReviewActiveHighRiskGovernancePath`:
  review governing policies, recent evidence, intentionality, and controls. Active
  use is not inherently adverse.
- `MultiplePoliciesContribute` -> `ReviewMultiplePolicyContext`: review contributing
  policies together for clarity. Their coexistence does not establish conflict or
  require consolidation.

Recommendations copy their source finding type, supporting facts, policy references,
coverage, observation window, capability metadata and provenance, and configuration
fingerprints. They never query policies or activity independently. Identical source
findings for a type/identity/capability are deterministically deduplicated.

Ordering follows recommendation type in the source-finding priority sequence, then
curated capability risk (`Critical`, `High`, `Medium`, `Low`), stable capability ID,
and identity ID. No priority, confidence, severity, or numerical score is assigned.

## Examples

### Full coverage, no observed high-risk use

Finding: `Critical capability with no observed use`.

Recommendation: Review whether the capability should remain represented in current
governance context. No activity was observed during the fully covered selected
period; investigate the contributing policies, capability context, and evidence.

### Partial coverage, no observed high-risk use

Finding: `Critical capability with no observed use found in retained evidence`.

Recommendation: Review the current governance relationship, noting that no use was
found in retained evidence and evidence does not cover the complete requested period.

### Observed outside current context

Finding: retained activity exists without a current static policy target relationship.

Recommendation: Review whether current configuration intentionally differs from the
configuration under which the activity occurred. This does not identify why the
contexts differ or prescribe adding policy.

### Actively observed Critical capability

Finding: a capability with curated `Critical` risk has retained activity.

Recommendation: Review governing policies and recent evidence to confirm the path
remains intentional and has appropriate controls. Active use is not inherently bad.

## Operator and contract scope

Identity Activity displays recommendations separately from findings with source
finding type, source facts, evidence qualification, provenance, and investigation
links. It provides no apply, acceptance, removal, revocation, or policy-generation
actions. Recommendations have no persisted status in v1, and no API is introduced
without a concrete external consumer.

See [Explainable Exposure Findings v1](explainable-exposure-findings.md),
[Identity Governance Exposure Analysis](identity-governance-exposure.md), and
[ADR-0017](adr/0017-deterministic-advisory-recommendations.md).
