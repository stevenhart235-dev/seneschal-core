# Proposed Change Review v1

Proposed Change Review is the human-readable composition of existing deterministic
services:

```text
Evidence -> Finding -> Recommendation -> Candidate change -> Simulation -> Review
Review -> [future explicit approval/application] -> Enforcement
```

The review route is `/proposed-change-review?identityId=<id>&capabilityId=<id>`.
Identity Activity links eligible recommendations to it without placing proposal
contents in the URL. Ineligible chains explain why no deterministic candidate is
available.

The page presents evidence coverage and window, finding and recommendation
limitations, the hypothetical `RemoveCapabilityFromPolicy` operation, active/base/
proposed fingerprints, current and proposed canonical evaluator results, actual
differences, and the static governance-context comparison. It links back to identity,
capability, policy, and evidence investigation.

`PROPOSED - NOT APPLIED` and the read-only notice are the governing UX boundary.
The page has no application, acceptance, approval, remediation, or policy-writing
actions. Rendering calls the same non-committing proposal simulation used by the API
and CLI; it does not write audit, activity, approval, incident, metrics, export, or
configuration state.

A changed-outcome example may show `Allow / Proceed` becoming `Deny / Block`. A
no-outcome-change example states that static governance differs while the simulated
runtime outcome for that one request did not change. Neither result establishes
business necessity, success, failure, complete runtime blast radius, or a calculated
risk score.

Stale proposals never display old simulation results as current. They are labeled
`STALE PROPOSAL` and require regeneration. Proposal application and human approval
remain future capabilities.

See [Proposed Governance Changes v1](proposed-governance-changes.md) and
[ADR-0018](adr/0018-immutable-proposed-governance-change-simulation.md).
