# Seneschal Runtime Architecture

## Purpose

Seneschal is the decision point for capability governance. Runtime
integrations ask Seneschal whether a capability request should be allowed,
denied, monitored, or require approval.

Seneschal does not execute the capability. It evaluates the request, records
the decision, and returns an explainable result to the caller.

## Integration Model

The primary v1 integration path is an HTTP call to the Seneschal API evaluate
endpoint. Applications, automation, middleware, CLIs, agents, and tools submit
the identity, capability, resource or environment context, and any additional
request metadata needed for evaluation.

Future integration paths should adapt to the same Core runtime instead of
reimplementing policy logic:

- .NET decision client/SDK
- ASP.NET middleware
- CLI hooks
- MCP server integration
- Sidecar or proxy integration

Seneschal.Core remains the authoritative runtime. Seneschal.Api and future
adapters are transport boundaries around that runtime.

## Request Flow

```text
Application / agent / tool
  -> Seneschal evaluate endpoint
  -> Decision engine
  -> Audit event
  -> Response to caller
```

The caller supplies known identity, capability, and resource context. Seneschal
evaluates policies, resolves the decision, writes an audit event, and returns a
response that the caller can act on.

## Decision Response Contract

Runtime callers should expect a decision response to include:

- `decision`: the resolved action, such as allow, deny, or requires approval.
- `enforcementMode`: whether the result is monitor/log-only or enforce.
- `matchedPolicies`: policies considered or matched during resolution.
- `obligations`: follow-up requirements attached to the decision.
- `reason`: a human-readable explanation of why the decision was made.
- `correlationId` / `eventId`: future identifiers for tracing a runtime call to
  its audit event.

The exact wire shape may vary by adapter, but integrations should preserve
these concepts.

## Monitor vs Enforce

Monitor mode evaluates and audits the request, but the caller may proceed even
when the decision would otherwise deny or require approval. Monitor mode is
used for rollout, discovery, tuning, and non-blocking governance.

Enforce mode means the caller must honor the decision:

- Allow: the caller may proceed.
- Deny: the caller must block the capability request.
- PendingApproval: the caller must pause, route to approval, or decline the
  action according to its own workflow.

## Failure Behavior

Failure behavior is part of the runtime contract.

For v1, enforcement-mode integrations should fail closed if Seneschal is
unavailable or cannot return a valid decision. A caller that cannot obtain a
decision must not silently allow protected capability execution.

Monitor-mode integrations may fail open, but should log or emit a warning when
possible. This is a deliberate design decision: monitor mode supports
observation and adoption, while enforce mode protects capability execution.

## Caching Guidance

Short-lived caching may be acceptable for stable Allow decisions when the
caller can tolerate eventual consistency. Caches should be scoped by the full
decision context, including identity, capability, resource, environment, and
relevant request attributes.

Deny and PendingApproval decisions should not be casually cached. They may
depend on transient policy, approval, or incident context.

Policy changes must eventually invalidate cached decisions. Future SDKs and
sidecars should provide explicit cache lifetimes and invalidation hooks rather
than leaving caching behavior implicit.

## Audit Behavior

Every runtime evaluation should produce an audit event.

Audit should explain:

- who or what requested the capability;
- which capability was requested;
- which resource or environment was involved;
- what decision was returned;
- which policy matched;
- which obligations were attached;
- why the decision was made;
- how long evaluation took.

Audit is not optional bookkeeping. It is the review trail that makes capability
governance explainable after the fact.

## Security Boundaries

Seneschal does not replace identity providers, IAM systems, API gateways,
network controls, or service authentication.

Runtime callers are responsible for authenticating the subject and determining
trusted identity and resource context before asking Seneschal for a decision.
Seneschal governs the capability decision after that context is known.

This keeps Seneschal focused on capability governance instead of becoming a
general authentication or infrastructure authorization system.

## Sprint 6 Implementation Plan

1. .NET decision client/SDK
2. ASP.NET middleware
3. `RequiresCapability` attribute
4. Sample protected API
5. Runtime audit verification

Each step should preserve the principle that integrations call the Core-backed
runtime and do not duplicate policy evaluation or decision resolution.
