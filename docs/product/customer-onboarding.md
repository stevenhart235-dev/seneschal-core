# Customer Onboarding: The First Hour

## Scope: a local evaluation, not a production deployment

This document describes the first hour of adoption using the product as it
exists in this repository today. It is a product-design flow, not a production
deployment guide.

> **Current product boundary**
>
> - Configuration is file-backed.
> - Governance mode and runtime evidence are held in memory.
> - The .NET libraries are source project references, not published packages.
> - The current workflow is appropriate for local evaluation or a controlled
>   development integration.

The walkthrough uses an ASP.NET Core operation that receives a `Deny` decision:

1. Run it in `LogOnly` to observe the deny without interrupting execution.
2. Switch the same runtime to `Enforce`.
3. Repeat the same operation to observe blocking without changing the policy or
   operation.

## How product status is used

- **Current:** The step works with code and UI in this repository.
- **Near-term:** The underlying model exists, but the supported onboarding or
  production-quality implementation does not.
- **Future:** The capability is product direction and must not be assumed to be
  available.

## 1. Start a local Seneschal runtime

**Product status: Current**

### Customer objective

Start Seneschal locally and confirm that it loaded usable configuration.

### Required action

From the repository root, run:

```powershell
dotnet run --project Seneschal.Api --urls http://localhost:5000
```

Then:

- Open `http://localhost:5000`.
- Check `/health`, `/ready`, or `/config/validate` if startup or configuration
  needs diagnosis.

### Seneschal behavior

- Loads capabilities, identities, policies, and integration keys from YAML
  files under `Seneschal.Api/Policies`.
- Starts in `LogOnly` mode.
- Initializes the capability catalog, governance graph, audit store, and
  activity store in memory.

### Value provided

One runtime handles decision evaluation and the governance views used to
inspect results.

### Expected outcome

- The runtime responds on the configured URL.
- Readiness reports loaded configuration.
- The dashboard is accessible.

> **Current limitation:** Local startup and health/configuration diagnostics
> are implemented. Packaged deployment, durable storage, and enterprise
> deployment automation are outside this first-hour path.

## 2. Scope a development integration API key

**Product status: Current for development; Future for production hardening**

### Customer objective

Authorize one application to request decisions only for the identity and
capability used in the walkthrough.

### Required action

Add or modify an entry in
`Seneschal.Api/Policies/integration-keys.yaml`:

- Set a key value.
- Enable the entry.
- Include the exact identity and capability the application will submit.
- Optionally restrict the environment.
- Restart Seneschal after changing the file.

For a local evaluation, the checked-in `dev-sample-key` supports the existing
`Developer` and `DeployApplication` scope. Do not reuse checked-in keys outside
development.

### Seneschal behavior

- Requires the key in the `X-Seneschal-Api-Key` header on `/evaluate`.
- Authenticates the calling integration.
- Rejects requests outside the configured identity, capability, and optional
  environment scope.

> **Identity boundary:** The key authenticates the integration. It does not
> authenticate the end user represented by the decision request.

### Value provided

An early integration cannot submit arbitrary decision requests for unrelated
identities and capabilities.

### Expected outcome

- The walkthrough request is accepted by key authorization.
- An out-of-scope request is rejected.

### Future production hardening

- Secret-backed storage
- Rotation without restart
- Federated authentication
- Request signing
- Replay protection
- Authentication audit events

## 3. Connect the .NET client and ASP.NET Core middleware

**Product status: Current from source; Near-term as a supported package path**

### Customer objective

Connect an ASP.NET Core application to the Seneschal decision endpoint.

### Required action

Add project references to:

- `Seneschal.Client`
- `Seneschal.AspNetCore`

There is no published-package installation path yet.

Configure and register the client:

```csharp
builder.Services.Configure<SeneschalClientOptions>(options =>
{
    options.BaseUrl = new Uri("http://localhost:5000");
    options.ApiKey = builder.Configuration["Seneschal:ApiKey"];
});
builder.Services.AddHttpClient<ISeneschalClient, SeneschalClient>();
```

Register attribute middleware after routing:

```csharp
app.UseRouting();
app.UseSeneschalCapabilityAttributes();
```

Keep the local key in application configuration or a development secret, not
in endpoint code. Use `Seneschal.Samples.ProtectedApi` as the current reference
implementation.

### Seneschal behavior

- The client posts a decision request to `/evaluate` with the configured API
  key header.
- Middleware reads capability metadata from the selected endpoint.
- Middleware uses `HttpContext.User.Identity.Name` as the identity, or
  `anonymous` when no name is available.
- Middleware submits the resource and optional environment.

### Value provided

The application gets a typed decision and can place governance before the
protected handler without duplicating HTTP and response-mapping logic.

### Expected outcome

- The application starts with a reachable Seneschal client.
- Middleware is in the correct pipeline position.

### Near-term onboarding work

- Published packages
- Standard dependency-registration extensions
- Resilience defaults
- A supported compatibility policy

## 4. Declare one capability and its decision context

**Product status: Current for manual declaration**

### Customer objective

Name one operational action and define the policy result expected in the
walkthrough.

### Required action

Configure the server-side definitions:

- Add a capability to `Seneschal.Api/Policies/capabilities.yaml`, or reuse an
  entry such as `DeployApplication`.
- Ensure the identity exists in `identities.yaml`.
- Allow the identity and capability in the integration-key scope.
- Add an explicit policy for the request context in `policies.yaml`.
- Restart Seneschal after YAML changes.

For the `LogOnly`-to-`Enforce` demonstration, use a request that resolves to
`Deny`.

The existing `Developer` / `DeleteProductionDatabase` / `prod` policy is an
example. Its integration key must also permit that request before policy
evaluation can occur.

Declare the same capability at the endpoint:

```csharp
[RequiresCapability(
    "DeleteProductionDatabase",
    Environment = "prod",
    ResourceId = "customer-database")]
static IResult DeleteDatabase()
{
    return Results.Ok("Operation executed");
}
```

### Identity requirements

- The authenticated principal name must match the configured identity.
- If the sample has no authentication, configure `anonymous` deliberately or
  use the explicit middleware/client path to supply identity context.
- Do not treat a hard-coded identity as a production identity integration.

### Seneschal behavior

- Loads the capability into its catalog at startup.
- Loads the identity and policy definitions used during evaluation.
- Uses endpoint metadata to determine which capability the middleware requests.

> **Important:** Endpoint metadata does not automatically register the
> capability in Seneschal.

### Value provided

The customer creates one shared operation name across:

- Application endpoint
- Integration-key scope
- Capability catalog
- Policy
- Decisions
- Audit evidence

### Expected outcome

- The capability appears in capability views.
- Configuration validation passes.
- The chosen request deterministically resolves to `Deny`.

> **Not implemented:** Runtime registration APIs and automatic capability
> discovery.

## 5. Establish a LogOnly baseline

**Product status: Current**

### Customer objective

Evaluate policy without allowing a deny result to interrupt the operation.

### Required action

- Leave the runtime in its default `LogOnly` mode, or select `LogOnly` at
  `/governance`.
- Keep the ASP.NET Core integration at its default `HonorDecisionMode`
  behavior.

### Seneschal behavior

- Evaluates the request normally.
- Preserves the underlying `Deny`, reason, and matched policy.
- Returns `LogOnly` as the response mode and `logged_only` as the effective
  action.
- Allows middleware using `HonorDecisionMode` to continue the request.

### Value provided

The customer can verify policy and request context before enabling blocking.

### Expected outcome

- `/governance` shows `LogOnly`.
- A deny decision remains observable without preventing handler execution.

> **Current limitation:** Mode is process-local and in memory. It returns to
> `LogOnly` after restart, and mode changes are not administrative audit events.

## 6. Generate the first runtime decision

**Product status: Current**

### Customer objective

Generate a decision from the integrated application rather than a standalone
policy test.

### Required action

- Authenticate as the configured identity when applicable.
- Invoke the protected endpoint once.
- Confirm through the response or side effect that the handler executed.

### Seneschal behavior

1. Middleware sends identity, capability, environment, and resource context.
2. Seneschal checks the integration key.
3. Seneschal evaluates loaded policies and returns an explainable decision.
4. Seneschal records audit and aggregated activity in memory.
5. Because mode is `LogOnly`, middleware honoring the returned mode continues
   to the endpoint despite the deny.

### Value provided

The customer observes governance on an actual application call.

### Expected outcome

- The endpoint reaches its handler.
- Seneschal records a `Deny` decision with `LogOnly` enforcement mode.

> **Current limitation:** The application must decide how to handle Seneschal
> connectivity failures. No complete production resilience policy is supplied.

## 7. Trace the decision across governance views

**Product status: Current for local, in-memory views**

### Customer objective

Confirm what was requested, why Seneschal decided as it did, and where the
capability appears in the product.

### Required action

Review the client decision and these views:

- `/audit`: individual decision record and filters
- `/capability-activity`: request counts and recent capability activity
- `/activity`: activity API response
- `/capability-explorer`: capability catalog and relationship context
- `/capabilities/{capabilityId}/overview`: capability overview API
- `/monitor`: runtime observations and deterministic readiness indicators

### Seneschal behavior

The audit view exposes:

- Identity
- Capability
- Resource and environment
- Decision and enforcement mode
- Matched policies and reason
- Obligations
- Evaluation duration

Activity views aggregate requests and decision counts. Capability views combine
catalog metadata, projected governance relationships, and observed activity.

### Value provided

Architects and control owners can trace an operation from declared capability
through policy decision to runtime evidence.

### Expected outcome

The customer can:

- Find the same capability and decision across response, audit, activity, and
  capability views.
- Explain why the request was denied but allowed to continue.

> **Current limitations**
>
> - Audit and activity disappear on restart.
> - Monitor readiness guidance uses simple deterministic heuristics. It is not
>   generated governance advice.

## 8. Change the runtime to Enforce

**Product status: Current for a global in-memory switch; Near-term for enterprise control**

### Customer objective

Apply the already-observed policy decision to the application request path.

### Required action

- Open `/governance` and select `Enforce`.
- Confirm the application still uses `HonorDecisionMode`.

An integration explicitly fixed to `Monitor` continues requests regardless of
the runtime mode.

### Seneschal behavior

- Changes subsequent evaluation responses to `Enforce` immediately.
- Does not require policy or endpoint changes.
- Stores the selection in the in-memory governance-mode store.

### Value provided

The customer moves the same integration from observation to enforcement
without rewriting the governed operation.

### Expected outcome

- `/governance` reports `Enforce`.
- Subsequent deny decisions can block through ASP.NET Core middleware.

### Near-term enterprise requirements

- Durable mode state
- Scoped mode control
- Authorized administration
- Administrative audit records

## 9. Repeat the operation and verify blocking

**Product status: Current for ASP.NET Core middleware blocking**

### Customer objective

Verify that governance stops the identical operation in `Enforce` mode.

### Required action

Invoke the same endpoint with the same identity and context used in step 6. Do
not change the capability or policy between calls.

### Seneschal behavior

- Returns the same underlying `Deny`, now with `Enforce` mode.
- Prevents attribute or branch middleware from calling the handler.
- Returns HTTP `403` with the decision reason and matched policy.
- Records another audit and activity event.

A pending-approval result blocks with HTTP `409`. No human approval workflow is
implemented in this onboarding path.

### Value provided

The customer verifies the enforcement boundary: Seneschal evaluates policy,
and the application integration prevents execution.

### Expected outcome

- The client receives HTTP `403`.
- The handler does not execute.
- A second record shows the same decision under `Enforce` mode.

> **Enterprise-readiness gap:** Production use still requires authentication
> integration, availability design, durable evidence, administrative controls,
> and tested failure behavior.

## 10. Expand deliberately to more capabilities

**Product status: Current as a manual process; Future as an automated rollout**

### Customer objective

Apply the proven pattern to a small, owned set of operational actions.

### Required action

For each action, manually add:

- Capability metadata
- Identity and integration-key scope
- Applicable policies
- An application enforcement point

Then repeat the rollout cycle:

1. Start in `LogOnly`.
2. Exercise representative requests.
3. Review evidence.
4. Move to `Enforce` only after understanding context and policy outcomes.

### Seneschal behavior

- Evaluates each declared capability through the same request-centric runtime.
- Includes it in catalog, explorer, graph, audit, and activity views.
- Does not scan applications, infer capabilities, generate policy, or select
  enforcement candidates automatically.

### Value provided

The organization develops a controlled capability vocabulary and a repeatable
observation-before-enforcement practice.

### Expected outcome

A small set of deliberately chosen operations is visible and governed. An
owner can explain the policy and enforcement behavior of each operation.

> **Future:** Automated inventory, suggested governance, and guided rollout.

## Recommended ASP.NET Core golden path

Use this sequence for the current product:

1. Run Seneschal beside the application in a development environment.
2. Scope one integration key to one application and a narrow identity and
   capability set.
3. Reference `Seneschal.Client` and `Seneschal.AspNetCore` from source.
4. Register `ISeneschalClient` through `AddHttpClient`; configure URL and key
   outside code.
5. Call `UseRouting()`, then `UseSeneschalCapabilityAttributes()` with its
   default `HonorDecisionMode` behavior.
6. Add `[RequiresCapability]` to one endpoint whose principal name and context
   map cleanly to Seneschal configuration.
7. Define the capability, identity, key scope, and explicit policy in YAML.
8. Validate a deny in `LogOnly`, inspect evidence, switch to `Enforce`, and
   repeat the request.

### Choose the integration style by context needs

- **Attribute protection:** Shortest path when capability, environment, and
  resource metadata are static.
- **Direct `ISeneschalClient`:** Better when identity or resource context must
  be assembled from the request or domain model.
- **Path middleware:** Useful for a whole pipeline branch, but currently uses
  fixed configuration values.

## Concepts the customer must understand

- A capability is the governed operation, not a role, permission, resource,
  endpoint, or identity.
- Seneschal evaluates requests; the ASP.NET Core integration blocks handler
  execution.
- Integration keys authenticate and scope callers. They do not establish end
  user identity.
- Capability and identity names must match across requests, YAML, policies,
  and key scopes.
- `LogOnly` changes runtime effect, not the policy decision. A logged deny
  remains a deny in the evidence.
- A missing matching allow policy should resolve safely, not implicitly permit
  an operation.
- Configuration loads from files at startup.
- Governance mode, audit, and activity state are process-local and non-durable.
- Enabling enforcement does not by itself make an integration production-ready.

## Current onboarding friction

### Distribution and configuration

- No packaged installer, container-first onboarding flow, or published .NET
  package path is documented by the repository.
- Customers edit multiple YAML files and manually align capability, identity,
  policy, key scope, and application metadata.
- Configuration changes require restart.
- Validation exists, but there is no guided configuration workflow.

### Identity and runtime control

- Attribute middleware derives identity from the ASP.NET Core principal and
  falls back to `anonymous`; this mapping is not configurable in that path.
- The mode switch is global, in memory, unaudited, and resets to `LogOnly` on
  restart.
- Customers must deliberately create a deny scenario to demonstrate the
  difference between `LogOnly` and `Enforce`.

### Production operation

- Audit and activity evidence are lost on restart.
- The client supplies no production retry, timeout, circuit-breaking,
  fail-open, or fail-closed policy.
- Approval decisions can block, but no human approval workflow exists.
- Automatic capability discovery and policy generation are not implemented.

## State after the first hour

Assuming the repository builds and local configuration is understood, the
customer has:

- A running local Seneschal instance
- One scoped development integration key
- One ASP.NET Core endpoint connected through the client and middleware
- One manually cataloged capability with explicit identity and policy context
- Evidence of the same deny in `LogOnly` and `Enforce`
- Audit, activity, and capability-centered views for the operation
- A concrete production-hardening work list

> **The customer does not yet have:** A durable governance service,
> production key management, automated discovery, generated policy, approval
> orchestration, or a production availability and failure-mode design.

## Future onboarding improvements

### Discover candidate capabilities

**Status: Future**

Seneschal should ingest candidate capabilities from:

- Application metadata
- Agent and MCP tool definitions
- Pipelines
- Other execution systems

Discovery should propose catalog entries with provenance and ownership for
review. It should not silently create enforced controls.

### Suggest governance actions

**Status: Future**

Seneschal should use these inputs to identify ungoverned or high-consequence
capabilities and propose next actions:

- Observed requests
- Existing policy coverage
- Risk metadata
- Organizational context

The current monitor contains deterministic activity-based readiness indicators.
That is not a general suggested-governance implementation.

### Guide policy creation

**Status: Future**

A guided flow should:

1. Start from an observed capability request.
2. Show relevant identity and resource context.
3. Help an owner construct explicit policy.
4. Validate likely effects.
5. Support a `LogOnly`-to-`Enforce` rollout.

Today, customers edit YAML and validate behavior by executing requests.

### Manage production credentials

**Status: Future**

Production onboarding should:

- Integrate with an enterprise secret store or workload identity.
- Avoid plaintext key material in product configuration.
- Rotate credentials without restart.
- Record authentication administration.
- Provide revocation and usage visibility.

The current integration-key file is a development trust boundary, not a
production credential system.
