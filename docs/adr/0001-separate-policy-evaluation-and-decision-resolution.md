# ADR-0001: Separate Policy Evaluation from Decision Resolution

## Status

Accepted

## Context

Policy evaluation determines which policies match a request. Decision resolution
selects the effective outcome when multiple policies match. Combining these
responsibilities made conflict handling difficult to evolve and test independently.

## Decision

Policy evaluation will produce policy matches, and decision resolution will use
those matches to produce the final decision.

## Consequences

- Matching and conflict resolution can evolve independently.
- Resolution behavior can be tested without evaluating policy conditions.
- Decision results retain match details for explainability.
- The runtime requires an explicit resolution step after matching.
