# Architecture

**Status:** Draft

**Version:** 0.1

**Last Updated:** 2026-07-03

---

# Core Runtime Architecture

Seneschal is organized around a request-driven governance runtime.

The primary runtime flow is:

```text
Identity
  ↓
Request
  ↓
Capability Catalog
  ↓
Policy Engine
  ↓
Decision
  ↓
Audit Event