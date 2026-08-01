# Persistence

Seneschal currently loads capabilities, identities, policies, and integration
keys from YAML and keeps operational state in process-local memory. Audit,
approval, incident, activity, runtime-mode, governance-window, metrics, catalog,
and governance-graph state reset or rebuild when the process restarts; durable
persistence is not implemented.

[ADR-0009: Operational State and Persistence](../adr/0009-operational-state-and-persistence.md)
defines the intended evidence, mutable-state, projection, storage, transaction,
and rollout boundaries.
