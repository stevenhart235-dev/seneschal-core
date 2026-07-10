# Application Integration Golden Path

## Purpose

This document defines the recommended architecture for integrating a new
enterprise application with Seneschal. It describes one opinionated path:
centralized evaluation through the Seneschal API, with enforcement at the
application boundary.

The current implementation supports this path for local and controlled
development use. Audit, activity, metrics, and runtime mode are currently held
in memory; production persistence and operational hardening remain separate
work.

## Architecture

```text
  User / Service
        |
        v
+-------------------------+       HTTPS + integration API key
| ASP.NET Core App        |-------------------------------------+
|                         |                                     |
| [RequiresCapability]    |                                     v
| Seneschal middleware    |                           +--------------------+
| .NET client SDK         |                           | Seneschal API      |
+-------------------------+                           |                    |
        |                                             | Auth + mapping     |
        | execute only when permitted                 | Core evaluation    |
        v                                             | Runtime mode       |
  Governed operation                                 +---------+----------+
                                                              |
                                      +-----------------------+----------------+
                                      |                       |                |
                                      v                       v                v
                                Audit events          Activity aggregates   Metrics
                                      |                       |
                                      +-----------+-----------+
                                                  |
                                                  v
                                         Seneschal Portal
```

## Runtime components

The golden path has four runtime boundaries:

- **Application enforcement point:** ASP.NET Core middleware prevents the
  governed operation from running when an enforced decision blocks it.
- **Seneschal API:** Authenticates the integration request, maps the HTTP
  contract, and invokes the authoritative Core runtime.
- **Seneschal Core:** Evaluates policy and resolves one deterministic,
  explainable decision.
- **Observability path:** Records audit evidence, aggregate activity, and
  metrics from the completed decision.

Seneschal evaluates the operation. The application remains responsible for
executing or blocking it.

## SDK placement

Place `Seneschal.Client` and `Seneschal.AspNetCore` inside each integrating
application process.

The SDK should:

- Build the capability request from trusted application context.
- Call the centralized Seneschal API.
- Apply the returned runtime mode and decision.
- Keep policy evaluation out of the application.

> **Current packaging:** Both libraries are source project references. They
> are not currently distributed as published packages.

Do not embed a second policy engine, copy policy rules into middleware, or let
the SDK maintain an independent governance model.

## API placement

Run the Seneschal API as a dedicated internal service reachable from governed
applications over a low-latency, authenticated network path.

Use one API deployment as the decision boundary for:

- Runtime evaluation through `POST /evaluate`
- Capability, identity, and policy configuration loading
- Audit and activity queries
- Portal read models
- Runtime governance mode

Place the API near its application callers when latency and availability
requirements justify it. Multiple-instance deployment requires shared durable
state and coordinated runtime mode, which the current in-memory implementation
does not yet provide.

## Policy evaluation flow

For each governed operation:

1. The application authenticates the user, service, or workload.
2. Middleware identifies the capability and supplies trusted identity,
   resource, and environment context.
3. The .NET client calls `POST /evaluate` with its integration API key.
4. The API authorizes the integration key and its request scope.
5. API models are mapped into the Core request model.
6. Core evaluates loaded policies and resolves `Allow`, `Deny`, or
   `RequireApproval`.
7. The API records the result and returns the decision, reason, matched policy,
   effective action, and runtime mode.
8. Middleware either continues to the operation or returns a blocking response.

The Governance Graph and Capability Explorer are not consulted during runtime
policy evaluation. They are read models for explanation and exploration.

## Audit flow

Every authenticated evaluation that completes the current decision path creates
an audit event containing:

- Timestamp and decision identifier
- Identity and capability
- Resource and environment
- Decision and enforcement mode
- Matched policies and obligations
- Explanation and evaluation duration

The same event is passed to audit storage, activity aggregation, metrics,
export handling, and governance-incident tracking.

> **Current limitation:** Audit storage is an unbounded in-memory list and is
> lost on restart. The registered exporter is a null exporter. Enterprise use
> requires durable, queryable audit storage with retention and export delivery
> semantics.

Integration-authentication failures occur before evaluation and are not
currently represented as governance audit events.

## Activity aggregation

The current activity store aggregates completed evaluations by:

- Capability
- Identity
- Matched policy

Per-capability activity includes total, allowed, denied, pending approval, last
used, and average evaluation duration. Aggregates are updated synchronously
from the decision event.

> **Current limitation:** Activity is process-local, in memory, and reset on
> restart. It has no daily buckets, trends, per-capability top callers, or
> per-capability environment counts.

For production scale, retain the same logical projection boundary while moving
aggregation to durable or rebuildable storage.

## Portal interaction

The portal is a read and administration surface over the Seneschal API process.
It is not in the application request path.

Use it to review:

- Capability inventory and relationships in Capability Explorer
- Per-capability decision totals and recent decisions
- Capability and identity activity
- Audit records and decision explanations
- Dashboard summaries
- Monitor readiness and governance-drift indicators
- Current runtime governance mode

Portal availability must not be required for an application to obtain a runtime
decision.

## Runtime governance mode

Adopt each capability through two stages:

1. **LogOnly:** Evaluate and record the actual decision, but allow middleware
   using `HonorDecisionMode` to continue.
2. **Enforce:** Honor `Allow`; block `Deny`; block `RequireApproval` until an
   application workflow can handle it.

Use the default ASP.NET Core `HonorDecisionMode` behavior so the application
follows the mode returned by Seneschal. Avoid fixing the integration permanently
to `Monitor` or forcing local enforcement independently of the runtime.

> **Current limitation:** Runtime mode is global, in memory, unaudited, and
> resets to `LogOnly` on restart. Durable, scoped, authorized mode management is
> required before broad production enforcement.

## Integration API keys

Give each integrating application its own key and restrict it to the identities,
capabilities, and optional environment that application may submit.

The key authenticates the integration, not the end user. The application must
establish trusted subject identity before constructing the evaluation request.

Current keys are loaded from `integration-keys.yaml` and sent in
`X-Seneschal-Api-Key`.

> **Current limitation:** File-backed plaintext keys are a development trust
> boundary. The enterprise path requires secret-backed configuration,
> rotation, revocation, and authentication-event visibility.

## Recommended ASP.NET Core integration

Use attribute middleware for endpoints with a stable capability declaration:

```csharp
builder.Services.Configure<SeneschalClientOptions>(options =>
{
    options.BaseUrl = new Uri(builder.Configuration["Seneschal:BaseUrl"]!);
    options.ApiKey = builder.Configuration["Seneschal:ApiKey"];
});
builder.Services.AddHttpClient<ISeneschalClient, SeneschalClient>();

var app = builder.Build();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSeneschalCapabilityAttributes();
```

Declare the capability at the operation boundary:

```csharp
[RequiresCapability(
    "orders.refund",
    Environment = "production",
    ResourceId = "orders-api")]
static IResult RefundOrder()
{
    return Results.Ok();
}
```

The authenticated principal name becomes the request identity; without one,
attribute middleware uses `anonymous`. Ensure the capability, identity, policy,
and integration-key scope are configured consistently in Seneschal.

Use direct `ISeneschalClient` evaluation when identity, resource, or other
decision context must be constructed from application domain data. Use branch
middleware only when one fixed capability and context applies to the entire
branch.

## Future integration points

Future adapters should submit the same Core-backed capability request rather
than implement independent policy behavior:

- **MCP:** Evaluate a capability before an MCP tool invocation.
- **LangGraph:** Evaluate before a graph node performs an external or
  consequential action.
- **CI/CD:** Evaluate deployment, release, rollback, and administrative
  pipeline capabilities before execution.
- **Terraform:** Evaluate plan, apply, destroy, and other infrastructure
  capabilities at the automation boundary.

These adapters are future integration points. They must preserve the same
identity, capability, resource, decision, audit, and runtime-mode semantics.

## Why this architecture?

This architecture keeps one authoritative policy and decision path while
placing enforcement next to the operation that must be controlled.

It provides:

- Consistent decisions across applications
- Application-owned, trusted request context
- Low-friction adoption through `LogOnly` before enforcement
- One audit and activity path for every completed evaluation
- A portal that observes governance without entering the execution path
- Adapter boundaries that can extend to agents and automation without moving
  policy logic into each integration

The result is a narrow responsibility split: applications authenticate and
execute, Seneschal evaluates and explains, and the integration enforces the
returned decision.
