# ADR-0007: Make Core the Authoritative Runtime

## Status

Accepted

## Context

Seneschal.Api previously maintained its own policy models, evaluator, decision
rules, and enforcement mode alongside Seneschal.Core. The two runtime paths
could produce different decisions for the same request.

Existing HTTP payloads and YAML configuration remain compatibility contracts
that cannot be replaced directly by Core domain models.

## Decision

Seneschal.Core is the authoritative runtime for policy evaluation and decision
resolution. Seneschal.Api is an adapter that maps HTTP and YAML DTOs into Core,
invokes the Core runtime, and maps results back to existing wire formats.

API DTOs are retained only for HTTP and YAML compatibility. Policy evaluation
and decision resolution must not be reimplemented in Seneschal.Api.

## Consequences

- API and CLI decisions use the same Core evaluation behavior.
- Existing API routes and payload shapes remain stable.
- Compatibility defaults are represented in projected Core policies.
- API code remains responsible for transport, configuration loading, mapping,
  and its current audit persistence.
