# Proposed Governance Change Contract

This language-neutral contract describes a hypothetical, non-applying governance
change. Version v1 revision 1 supports only `RemoveCapabilityFromPolicy`.
Documents are strictly validated; unknown fields, operations, versions, and
revisions are rejected. Simulation validates and applies the operation to an
immutable policy snapshot and never writes policy YAML or runtime state.