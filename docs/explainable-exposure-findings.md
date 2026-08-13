# Explainable Exposure Findings v1

Explainable Exposure Findings are deterministic statements derived from current
governance configuration, recorded evaluation evidence, capability metadata,
evidence coverage, and configuration provenance. Each finding exposes its trigger,
supporting facts, observation window, and what the available facts do not prove.

```text
Governance configuration
+ Observed evidence
+ Evidence quality
+ Configuration provenance
--------------------------------
Explainable finding
```

A finding is not a recommendation, remediation instruction, policy mutation,
prediction, proof of business necessity or unauthorized access, or a numerical
risk score. Capability risk remains curated catalog metadata; v1 does not assign a
separate finding severity or confidence score. Evidence coverage directly qualifies
the statement.

## Finding types

Findings use stable machine-readable `FindingType` values and the following exact
triggers:

- `ObservedOutsideCurrentGovernanceContext`: retained evaluation evidence exists
  for an identity/capability pair that has no current static policy target
  relationship. The finding reports count, most recent observation, window, and
  available fingerprints. It does not prove unauthorized execution.
- `HighRiskConfiguredNotObserved`: a currently configured capability has curated
  `High` or `Critical` risk and has no retained observation in the window. Full
  coverage states that no activity was observed during the fully covered period.
  Partial coverage states only that no use was found in retained evidence. Unknown
  coverage suppresses this finding because the evidence cannot support a useful
  absence statement.
- `HistoricalConfigurationDiffers`: at least one observation has an evaluation-time
  fingerprint different from the current evaluation-relevant configuration
  fingerprint. Counts and fingerprints are reported. Missing provenance does not
  trigger the finding, and a difference does not identify a changed policy or
  prove a different decision.
- `HighRiskCapabilityActivelyObserved`: a `High` or `Critical` capability has at
  least one retained observation in the window. This neutral informational finding
  surfaces an important active governance path; activity is not inherently adverse.
- `MultiplePoliciesContribute`: more than one current policy contributes static
  governance context for the same identity and capability. Policy names, decisions,
  and environments are facts; multiplicity alone does not prove conflict.

An identity's technology and capability counts remain exposure summary facts.
There is no principled v1 threshold at which breadth becomes a finding.

## Evidence qualification examples

With Full coverage:

> Critical capability with no observed use. No activity was observed during the
> fully covered selected period.

With Partial coverage:

> Critical capability with no observed use found in retained evidence. No use was
> found in retained evidence; absence across the complete requested period cannot
> be determined.

Unknown coverage suppresses absence findings. Other finding types preserve the
coverage status and requested UTC window so operators can judge the evidence
boundary directly.

## Ordering and presentation

Output order is deterministic: outside-current-context, configured high-risk
without observation, historical configuration difference, active high-risk, then
multiple-policy context. Within a type, capability risk sorts `Critical`, `High`,
`Medium`, `Low`, followed by stable capability ID.

Identity Activity presents findings beside the exposure summary as expandable,
read-only evidence. It links to capability activity and contributing policies but
o action changes governance. Findings remain an operator read model in v1; no API
endpoint is added merely to mirror the UI.

See [Identity Governance Exposure Analysis](identity-governance-exposure.md),
[ADR-0014](adr/0014-configured-governance-and-observed-exposure.md), and
[ADR-0015](adr/0015-immutable-evaluation-configuration-provenance.md).
