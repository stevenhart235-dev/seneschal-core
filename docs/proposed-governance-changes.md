# Proposed Governance Changes v1

A Proposed Governance Change is a hypothetical, inspectable semantic operation. It
does not modify policy. V1 supports only `RemoveCapabilityFromPolicy` and validates
the immutable resulting policy snapshot through Policy Schema v1 plus existing
semantic and catalog-reference validation.

```text
Governance facts
  -> Exposure
  -> Finding
  -> Recommendation
  -> Candidate proposed change
  -> Current vs proposed simulation
  -> Future human review/application
  -> Enforcement
```

A recommendation may offer a candidate only for
`ReviewCurrentGovernanceRelationship` sourced from
`HighRiskConfiguredNotObserved`, with Full or Partial coverage, one contributing
policy, one exact identity, an explicit capability target, no wildcard ambiguity,
a valid result, and a matching base fingerprint. Otherwise Seneschal reports `No
deterministic proposal available`. A recommendation does not prove its candidate
should be applied.

## Fingerprints and stale proposals

`BaseGovernanceConfigurationFingerprint` binds the proposal to active configuration.
V1 rejects a mismatch with `Base configuration changed`; it never silently rebases.
Simulation returns current and `ProposedGovernanceConfigurationFingerprint` values.
The latter is hypothetical, never active, and never stored as historical runtime
provenance.

## Simulation

```powershell
seneschal policy change simulate .\proposal.yaml `
  --url http://localhost:5077 `
  --api-key $env:SENESCHAL_API_KEY `
  --identity Developer `
  --capability DeployApplication `
  --environment dev
```

The CLI calls authenticated `POST /policy-changes/simulate`. API-key identity,
capability, and environment scope is enforced. Current and proposed sides use the
same canonical non-committing evaluator, timestamp, operation ID, runtime mode,
governance-window snapshot, and relevant approval view. Canonical Execution
Guidance determines `ShouldProceed`; unknown guidance remains fail closed.

Output compares decisions, effective actions, guidance, winning/matched policies,
reasons, approval/window effects, and explicitly simulated differences.

## Static governance context comparison

Static comparison reports configured capability, Critical, and High counts plus
the affected identity, capability, policy, environments, technology, and curated
risk. This is not risk reduction, safe removal, complete blast radius, or proof of
every runtime outcome. Historical observations remain unchanged. Only explicit
request simulations establish runtime outcome differences.

V1 does not persist proposals or status, write policy files, apply changes, provide
accept/reject workflow, enumerate all runtime contexts, or implement additional
operations. The first operator interface is CLI/API; Razor proposal interaction is
deferred to avoid coupling review UI to an unimplemented application workflow.

## Operator review

[Proposed Change Review v1](proposed-change-review.md) composes evidence, findings, recommendations, candidate generation, simulation, and static comparison into one read-only operator route. It does not add application or approval behavior.
