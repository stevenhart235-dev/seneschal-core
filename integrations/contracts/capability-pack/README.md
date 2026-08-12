# Capability Pack Contract

Capability Pack v1 is the language-neutral contract for curated local capability
catalogs. The manifest pins schema revision 1 and its checksum.

A pack contains `pack.id`, a `MAJOR.MINOR.PATCH` `pack.version`, optional
`description` and `provider`, and capabilities using the existing YAML shape.
Pack files contain no policies, inheritance, dependencies, or download locations.

Runtime validation remains authoritative for cross-file conflict checks and
metadata warnings. Use `seneschal capability pack validate <path>` before
configuring a pack.
