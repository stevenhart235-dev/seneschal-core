# GitHub Actions Capability Pack

The built-in GitHub Actions pack is a curated starter catalog for governing
workflow observation and execution, deployments, protected environments,
artifacts, secrets, and repository delivery controls. It is intentionally not a
complete GitHub API permission or repository administration model. Organization
administration, billing, runners, teams, app installation, OAuth, security
products, and package registries are outside version 1.0.0.

- Pack ID: `github-actions`
- Version: `1.0.0`
- Provider: `Seneschal`

The catalog covers Workflow Observation, Workflow Execution, Workflow
Management, Deployment, Environment Protection, Artifacts, Secrets, and
Repository Administration. The existing `github.workflow.dispatch` definition
retains its established local metadata so cross-source merging is identical.

Risk values are opinionated starter classifications based on delivery blast
radius and sensitivity. Observation is generally Low, dispatch and cancellation
are Medium, delivery changes are High, and secret access, protection changes,
and destructive branch operations are Critical. These values are authored
metadata, not calculated scores, and do not affect policy decisions.

Validate the pack:

```powershell
seneschal capability pack validate .\capability-packs\github-actions\github-actions.capability-pack.yaml
```

Load it by setting `Seneschal:Configuration:CapabilityPacksPath` to the pack
file or its directory. In environment-variable form:

```powershell
$env:Seneschal__Configuration__CapabilityPacksPath = '.\capability-packs\github-actions'
```

Policies reference stable capability IDs directly, for example:

```yaml
capability: github.deployment.create
```

Pack provenance is available through the Capability Catalog and Capability
Explorer. It does not affect policy matching or decisions.
