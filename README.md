# Seneschal

Seneschal is a runtime capability governance platform that allows organizations to observe, audit, and enforce the use of high-risk capabilities across applications, automation, and AI systems.

Applications submit identity, capability, environment, and resource context before a privileged operation. Seneschal evaluates configured policy and returns an `Allow`, `Deny`, or `PendingApproval` decision with the matched policy and reason.

Seneschal is under active development. Runtime state and operational evidence are currently held in memory, and the project should not be treated as production-ready.

![Seneschal Dashboard showing live runtime governance activity](docs/images/seneschal-dashboard.png)

## Quick Start

Prerequisites are PowerShell, a compatible .NET SDK, and access to NuGet.org for the initial package restore.

```powershell
git clone https://github.com/stevenhart235-dev/seneschal-core.git
cd seneschal-core
.\demo.ps1
```

The launcher packs the local client package, starts Seneschal and four package-based workers, waits for readiness, and opens the Dashboard automatically.

Stop only the processes created by the launcher with:

```powershell
.\stop-demo.ps1
```

Process output is available under `artifacts/demo/logs/`.

## Implemented Features

- **Runtime capability evaluation** using identity, capability, environment, and resource context
- **Allow, Deny, and Pending Approval decisions** with matched policy and reason
- **LogOnly and Enforce modes** for observing or enforcing policy outcomes
- **Dashboard** with live runtime decisions and projected operational impact
- **Runtime Governance** mode control and consequence summary
- **Audit Trail** and decision trace evidence
- **Capability Activity** and **Identity Activity** views
- **Capability Explorer** backed by the configured capability inventory and runtime activity
- **Incident investigation** for aggregated governance incidents
- **Relationship Graph**, Diagnostics, Policies, Identities, and Resources portal views
- **ASP.NET Core integration** with middleware, attributes, and fluent endpoint protection
- **.NET Client SDK** through the `Seneschal.Client` package
- **GitHub Actions governance gate** for pre-deployment evaluation
- **Terraform/OpenTofu governance gate** for pre-apply evaluation
- **Local demo launcher** through `demo.ps1` and `stop-demo.ps1`

`PendingApproval` is currently a decision state, not an approval-resolution workflow. Runtime mode, audit evidence, activity, metrics, and incidents reset when Seneschal restarts.

## Integrations

| Integration | Current implementation |
|---|---|
| [.NET Client SDK](Seneschal.Client/README.md) | Package-based client for calling `POST /evaluate` |
| [ASP.NET Core](Seneschal.AspNetCore/README.md) | Service registration, middleware, attribute protection, and fluent endpoint protection |
| [GitHub Actions](integrations/github-actions/README.md) | PowerShell governance gate and sample pre-deployment workflow |
| [Terraform/OpenTofu](integrations/terraform/README.md) | PowerShell pre-apply gate and local-only `terraform_data` example |

The GitHub Actions and Terraform/OpenTofu integrations are repository scripts and examples, not Marketplace actions, providers, or custom backends.

## Demo Scenarios

- [Production Freeze](docs/demos/production-freeze.md) — runs an alternate, demo-only policy profile to show GitHub and Terraform requests proceeding in `LogOnly` and blocking in `Enforce`.
- [Multi-application adoption lab](labs/multi-application-adoption/README.md) — continuously produces Allow, Deny, and Pending Approval decisions from four independent workers.

## Architecture

Seneschal sits on the execution path of an integrated operation. The caller remains responsible for honoring the returned effective action.

```text
Application / Automation
          │ capability request
          ▼
    Seneschal Runtime
          │
          ▼
    Policy Evaluation
          │
          ├──► Decision ──► Caller
          │
          └──► Audit / Activity / Metrics ──► Governance Portal
```

The runtime loads capabilities, identities, policies, and scoped integration keys from YAML configuration. See the [golden-path architecture](docs/architecture/golden-path.md) for the recommended application topology.

## Roadmap

The following work is planned and is not part of the current implementation:

- Persistent operational storage and retention
- Durable, administratively managed capability catalog
- Expanded production relationship graph and persistence
- Scheduled, time-zone-aware governance windows
- Scoped break-glass exceptions and restoration behavior
- Approval review and resolution workflow
- Administrative authorization and administrative audit events

## Technology

- .NET 8 and .NET 9
- C# and ASP.NET Core
- Razor Pages
- JavaScript, HTML, and CSS
- xUnit

## Build and Test

```powershell
dotnet build
dotnet test
```

## Documentation

- [ASP.NET Core quickstart](docs/quickstart/aspnet-core-quickstart.md)
- [Customer onboarding](docs/product/customer-onboarding.md)
- [Golden-path architecture](docs/architecture/golden-path.md)
- [Validation strategy](docs/qa/validation-strategy.md)
- [Architecture decision records](docs/adr)
- [Project vision](docs/vision.md)
