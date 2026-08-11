# Policy Evaluation

## Non-mutating simulation

`POST /preflight` is the canonical policy-simulation backend. It runs the same
policy, approval, enforcement-mode, governance-window, and Execution Guidance
resolution as runtime evaluation, but does not commit the evaluation. It creates
no audit event or approval mutation and does not update activity, metrics,
incidents, runtime mode, policies, or governance-window state.

The response includes the full matched-policy list and, when a governance window
applies, its name, mode, reason, and whether it changed the resolved result.
`seneschal policy simulate ...` presents this response; it does not implement a
second evaluator.

Deny and RequireApproval are valid simulation results, including under LogOnly
and Enforce modes. Callers determine hypothetical execution exclusively through
the canonical Execution Guidance contract: `Proceed` and `ContinueLogOnly`
produce `ShouldProceed = true`; `Block`, `Pause`, missing guidance, and unknown
guidance produce `ShouldProceed = false`. The CLI treats unknown guidance as a
malformed contract and exits non-zero.
