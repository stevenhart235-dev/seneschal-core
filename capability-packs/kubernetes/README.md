# Kubernetes Capability Pack

The built-in Kubernetes pack is a curated starter catalog for governing common
workload observation and change, interactive execution, configuration, secret,
and namespace operations. It is intentionally not a complete Kubernetes API,
`kubectl`, or privilege model. Cluster administration, CRDs, RBAC, admission
control, nodes, storage, and network policy are outside version 1.0.0.

- Pack ID: `kubernetes`
- Version: `1.0.0`
- Provider: `Seneschal`

The catalog covers Workload Observation, Workload Management, Interactive
Execution, Configuration, Secrets, and Namespace Management. A few definitions
retain established local catalog categories so identical cross-source merging
remains deterministic.

Risk values are opinionated starter classifications based on operational blast
radius and sensitivity. Observation is generally Low, namespace creation is
Medium, workload changes and sensitive reads are High, and destructive or
secret-modification operations are Critical. These values are authored
metadata, not calculated scores, and do not affect policy decisions.

Validate the pack:

```powershell
seneschal capability pack validate .\capability-packs\kubernetes\kubernetes.capability-pack.yaml
```

Load it by setting `Seneschal:Configuration:CapabilityPacksPath` to the pack
file or its directory. In environment-variable form:

```powershell
$env:Seneschal__Configuration__CapabilityPacksPath = '.\capability-packs\kubernetes'
```

Policies reference stable capability IDs directly, for example:

```yaml
capability: kubernetes.workload.deploy
```

Pack provenance is available through the Capability Catalog and Capability
Explorer. It does not affect policy matching or decisions.
