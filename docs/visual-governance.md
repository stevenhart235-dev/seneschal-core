# Visual Governance

## Purpose

Sprint 7 begins Seneschal's move from textual governance views toward an
interactive visual model of capabilities, identities, policies, resources, and
runtime decisions.

The goal is not to make a decorative graph. The goal is to help users answer
governance questions quickly:

- Who can use this capability?
- Which policies govern it?
- Which resources are affected?
- Why did this decision happen?
- What would change if this policy changed?

This document evaluates the visualization foundation for Seneschal's interactive
relationship graph and future visual governance experience.

## Seneschal graph use cases

Seneschal's visual governance surface needs to support:

- Interactive relationship exploration.
- Typed nodes for capabilities, identities, policies, resources, and audit
  events.
- Drag, zoom, and pan.
- Click-to-inspect node details.
- Highlighting direct relationships from a selected node.
- Highlighting decision paths such as:
  identity → capability → policy → decision → audit event.
- Future policy editing and visual authoring.
- A future Twilio Studio-style governance builder for composing governance
  flows.
- Usable performance with hundreds of nodes and a credible path to thousands.
- A maintainable implementation that fits the current ASP.NET/Razor UI without
  requiring a full frontend rewrite.

## D3.js

### Strengths

D3.js is extremely flexible. It is a low-level visualization toolkit rather than
a graph product. That makes it strong when Seneschal needs custom layouts,
custom visual encodings, animation, timelines, or highly tailored interaction
models.

D3 is also mature, widely used, and not tied to a framework. It can be used from
plain JavaScript inside Razor-rendered pages, which fits the current UI
architecture.

D3 is strongest for:

- Custom visual storytelling.
- Timeline and audit visualizations.
- Bespoke decision-path diagrams.
- Fine-grained SVG control.
- Mixing charts, flows, and graph-like visuals on the same page.

### Weaknesses

D3 does not provide a complete graph exploration model out of the box. Seneschal
would need to build or assemble many expected graph behaviors manually:

- Node and edge data model conventions.
- Selection state.
- Drag behavior.
- Zoom and pan behavior.
- Relationship highlighting.
- Node inspection events.
- Graph layout tuning.
- Larger-graph performance strategies.

That is acceptable for a small prototype, but it increases product risk as the
Explorer grows. The more Seneschal becomes an interactive relationship tool, the
more D3 would require custom graph infrastructure.

D3 also has a steeper implementation curve. It rewards precision, but it can
become difficult to maintain if graph behavior is spread across hand-built DOM
manipulation, event handlers, and layout code.

## Cytoscape.js

### Strengths

Cytoscape.js is purpose-built for interactive graph exploration. Its core model
already matches Seneschal's governance graph mental model: nodes, edges, typed
data, styling, selection, layouts, and graph traversal.

It directly supports Seneschal's near-term needs:

- Typed nodes and edges.
- Drag, zoom, and pan.
- Click-to-inspect node details.
- Highlighting neighborhoods and direct relationships.
- Styling by node type, relationship type, risk, or decision.
- Layout switching.
- Graph traversal for decision-path highlighting.
- Better ergonomics for hundreds or thousands of graph elements.

Cytoscape also fits the current ASP.NET/Razor UI well. Razor can server-render
the page shell and emit or fetch a JSON graph contract. Cytoscape can then own
only the graph canvas and interactions, without forcing Seneschal into a
frontend framework.

Cytoscape is strongest for:

- Interactive relationship exploration.
- Capability Explorer graph views.
- Policy relationship views.
- Node inspector panels.
- Path highlighting.
- Incremental movement toward visual governance tooling.

### Weaknesses

Cytoscape is less general than D3. If Seneschal needs highly custom visual
storytelling, timeline compositions, or non-graph visual metaphors, D3 may be
better for those individual views.

Cytoscape also introduces a graph-specific dependency and programming model.
That is a good fit for relationship exploration, but it means Seneschal should
keep the graph data contract clean and avoid leaking Cytoscape-specific concepts
into Core domain models.

For a future Twilio Studio-style builder, Cytoscape can support the underlying
node/edge interaction model, but authoring workflows will still require careful
product design around validation, editing state, undo/redo, and persistence.
Cytoscape is a foundation, not the entire builder.

## Recommendation

Use Cytoscape.js as the primary foundation for Seneschal's interactive
relationship graph.

Seneschal's first visual governance problem is graph exploration, not custom
charting. Cytoscape starts closer to the product need: typed governance nodes,
relationships, selection, highlighting, layouts, and graph traversal. It should
let Sprint 7 deliver useful graph behavior with less custom infrastructure than
D3.

D3 should remain available later for specialized visualizations such as audit
timelines, decision-flow storytelling, or compact dashboard charts. It should
not be the first foundation for the Capability Explorer graph.

The architectural boundary should be:

```text
Core / API read models
    ↓
Graph data contract
    ↓
Razor page shell
    ↓
Cytoscape.js graph component
```

Core should remain visualization-agnostic. The API should expose graph data in a
plain Seneschal contract, not a Cytoscape-specific object model. The UI adapter
can translate that contract into Cytoscape elements.

## Trade-offs

Cytoscape is the practical choice for Sprint 7 because it provides more graph
behavior earlier. The trade-off is accepting a specialized graph dependency.

D3 is the more flexible long-term visualization toolbox. The trade-off is that
Seneschal would have to build a graph interaction layer itself before reaching
the same level of usability.

The recommended approach is therefore not "Cytoscape forever, D3 never." It is:

- Cytoscape for relationship graph exploration.
- D3 later for custom non-graph visualizations where it is clearly the better
  tool.
- A stable Seneschal graph data contract between the backend and UI so either
  renderer can evolve independently.

## Sprint 7 implementation plan

### Commit 1: graph data contract

Define the first UI-facing graph data contract for visual governance.

The contract should include:

- Nodes with id, label, type, metadata, and optional risk/decision attributes.
- Edges with id, sourceId, targetId, relationshipType, label, origin, and
  sourceSystem.
- A scoped graph response for the currently selected capability or entity.

This should be a read model. It should not replace the Governance Graph domain
model or affect runtime evaluation.

### Commit 2: interactive graph prototype

Add a minimal interactive graph to the Capability Explorer using Cytoscape.js.

The prototype should support:

- Rendering scoped graph data.
- Zoom and pan.
- Dragging nodes.
- Basic node and edge labels.

No editing should be introduced.

### Commit 3: typed nodes and styling

Style nodes and edges by governance type:

- Capability
- Identity
- Policy
- Resource
- Audit event

Use restrained styling that fits the existing dark UI. Risk and decision badges
can influence node accents, but the graph should remain readable.

### Commit 4: node inspector panel

Add a node inspector panel that opens when a user selects a node.

The inspector should show:

- Node type.
- Display name.
- Key metadata.
- Related counts where available.
- Links back to existing Explorer, Policy, or Audit pages when possible.

### Commit 5: relationship/path highlighting

Add relationship highlighting for selected nodes and decision paths.

The first version should support:

- Highlight direct neighbors.
- Dim unrelated nodes.
- Highlight selected edge paths.
- Show a friendly empty state when no relationships are available.

Decision-path highlighting should remain read-only and explanatory. It should
not change policy evaluation or runtime enforcement.
