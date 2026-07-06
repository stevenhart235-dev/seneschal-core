# API Models

Types in this directory are transport and configuration DTOs. They preserve the
existing HTTP JSON and YAML contracts and are mapped to and from
`Seneschal.Core` domain models at the API boundary.

These types do not define policy evaluation, decision resolution, or other
runtime behavior. Domain and runtime changes belong in `Seneschal.Core`.
