# ADR-0002: Use a Pluggable Decision Resolution Strategy

## Status

Accepted

## Context

Organizations may require different rules for resolving multiple matching
policies. Priority and decision severity are the current rules, but they should
not be permanently embedded in the resolver.

## Decision

Decision resolution will delegate winner selection to
`IDecisionResolutionStrategy`. The default strategy selects by descending
priority and then by decision severity.

## Consequences

- Alternative conflict-resolution rules can be introduced without replacing the
  resolver.
- The default behavior remains deterministic and explicit.
- Strategies must return one winning policy from a non-empty match set.
- The winning policy is exposed in the decision result.
