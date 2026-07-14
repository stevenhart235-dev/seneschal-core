(() => {
    const container = document.getElementById('interactiveGraph');
    if (!container) return;

    const emptyState = document.getElementById('graphEmptyState');
    const resetButton = document.getElementById('resetGraphButton');
    const fitButton = document.getElementById('fitGraphButton');
    const selectedCapabilityId = container.dataset.capabilityId;

    if (!window.d3) {
        showEmpty('The local D3 asset could not be loaded.');
        return;
    }

    fetch('/graph')
        .then(response => {
            if (!response.ok) throw new Error('Graph data could not be loaded.');
            return response.json();
        })
        .then(renderGraph)
        .catch(error => showEmpty(error.message));

    function renderGraph(graph) {
        const centerId = `capability:${selectedCapabilityId}`;
        const allNodes = graph.nodes || [];
        const allEdges = graph.edges || [];
        const directlyRelatedEdges = allEdges.filter(edge =>
            edge.sourceId === centerId || edge.targetId === centerId);
        const nodeById = new Map(allNodes.map(node => [node.id, node]));
        const relatedPolicyIds = new Set(
            directlyRelatedEdges
                .flatMap(edge => [edge.sourceId, edge.targetId])
                .filter(id => nodeById.get(id)?.type === 'Policy'));
        const policyContextEdges = allEdges.filter(edge => {
            const touchesRelatedPolicy = relatedPolicyIds.has(edge.sourceId) ||
                relatedPolicyIds.has(edge.targetId);
            const otherId = relatedPolicyIds.has(edge.sourceId)
                ? edge.targetId
                : edge.sourceId;
            return touchesRelatedPolicy &&
                ['Identity', 'Resource'].includes(nodeById.get(otherId)?.type);
        });
        const relatedEdges = [...new Map(
            [...directlyRelatedEdges, ...policyContextEdges]
                .map(edge => [
                    `${edge.sourceId}\0${edge.targetId}\0${edge.relationshipType}`,
                    edge
                ])).values()];
        const includedIds = new Set([centerId]);
        relatedEdges.forEach(edge => {
            includedIds.add(edge.sourceId);
            includedIds.add(edge.targetId);
        });

        const nodes = allNodes
            .filter(node => includedIds.has(node.id))
            .filter(node => ['Capability', 'Identity', 'Policy', 'Resource'].includes(node.type))
            .map(node => ({ ...node, metadata: node.metadata || {} }));
        const nodeIds = new Set(nodes.map(node => node.id));
        const edges = relatedEdges
            .filter(edge => nodeIds.has(edge.sourceId) && nodeIds.has(edge.targetId))
            .map((edge, index) => ({
                ...edge,
                id: `edge-${index}`,
                source: edge.sourceId,
                target: edge.targetId
            }));

        const center = nodes.find(node => node.id === centerId);
        if (!center) {
            showEmpty('The selected capability is not present in graph data.');
            return;
        }

        const width = Math.max(container.clientWidth, 720);
        const height = Math.max(container.clientHeight, 560);
        center.fx = width / 2;
        center.fy = height / 2;

        const svg = d3.select(container)
            .append('svg')
            .attr('viewBox', `0 0 ${width} ${height}`)
            .attr('aria-hidden', 'true');
        const viewport = svg.append('g');
        const zoom = d3.zoom()
            .scaleExtent([0.35, 3])
            .on('zoom', event => viewport.attr('transform', event.transform));
        svg.call(zoom);

        const links = viewport.append('g')
            .attr('class', 'd3-graph-links')
            .selectAll('line')
            .data(edges)
            .join('line');
        const linkLabels = viewport.append('g')
            .attr('class', 'd3-graph-edge-labels')
            .selectAll('text')
            .data(edges)
            .join('text')
            .attr('class', 'd3-edge-label')
            .text(edge => edge.label || edge.relationshipType);

        let explicitlySelectedNodeId = null;

        const node = viewport.append('g')
            .attr('class', 'd3-graph-nodes')
            .selectAll('g')
            .data(nodes)
            .join('g')
            .attr('class', item => `d3-graph-node d3-node-${item.type.toLowerCase()}`)
            .attr('tabindex', 0)
            .attr('role', 'button')
            .attr('aria-label', item => `${item.type}: ${item.label}`)
            .on('mouseenter', (_, item) => showRelationshipLabels(item.id))
            .on('mouseleave', () => showRelationshipLabels(explicitlySelectedNodeId))
            .on('click', (_, item) => selectNode(item, node, true))
            .on('keydown', (event, item) => {
                if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    selectNode(item, node, true);
                }
            })
            .call(d3.drag()
                .on('start', dragStarted)
                .on('drag', dragged)
                .on('end', dragEnded));

        node.append('circle')
            .attr('r', item => item.type === 'Capability' ? 43 : 33);
        node.append('text')
            .attr('class', 'd3-node-label')
            .attr('text-anchor', 'middle')
            .selectAll('tspan')
            .data(item => nodeLabelLines(item))
            .join('tspan')
            .attr('x', 0)
            .attr('dy', (_, index) => index === 0 ? '-0.55em' : '1.2em')
            .text(line => line);

        const simulation = d3.forceSimulation(nodes)
            .force('link', d3.forceLink(edges)
                .id(item => item.id)
                .distance(210)
                .strength(0.65))
            .force('charge', d3.forceManyBody().strength(-900))
            .force('collision', d3.forceCollide().radius(item =>
                item.type === 'Capability' ? 74 : 68).strength(1))
            .force('x', d3.forceX(item => groupTarget(item, width, height).x)
                .strength(item => item.type === 'Capability' ? 0 : 0.16))
            .force('y', d3.forceY(item => groupTarget(item, width, height).y)
                .strength(item => item.type === 'Capability' ? 0 : 0.16))
            .on('tick', () => {
                links
                    .attr('x1', edge => edge.source.x)
                    .attr('y1', edge => edge.source.y)
                    .attr('x2', edge => edge.target.x)
                    .attr('y2', edge => edge.target.y);
                linkLabels
                    .attr('x', edge => (edge.source.x + edge.target.x) / 2)
                    .attr('y', edge => (edge.source.y + edge.target.y) / 2);
                node.attr('transform', item => `translate(${item.x},${item.y})`);
            });

        resetButton.addEventListener('click', () => {
            center.fx = width / 2;
            center.fy = height / 2;
            nodes.filter(item => item !== center).forEach(item => {
                item.fx = null;
                item.fy = null;
            });
            svg.transition().duration(200).call(zoom.transform, d3.zoomIdentity);
            simulation.alpha(0.7).restart();
        });
        fitButton.addEventListener('click', fitToGraph);

        function fitToGraph() {
            const bounds = viewport.node().getBBox();
            if (!bounds.width || !bounds.height) return;
            const scale = Math.min(
                2,
                0.88 / Math.max(bounds.width / width, bounds.height / height));
            const translateX = width / 2 - scale * (bounds.x + bounds.width / 2);
            const translateY = height / 2 - scale * (bounds.y + bounds.height / 2);
            svg.transition().duration(250).call(
                zoom.transform,
                d3.zoomIdentity.translate(translateX, translateY).scale(scale));
        }

        function dragStarted(event, item) {
            if (!event.active) simulation.alphaTarget(0.25).restart();
            item.fx = item.x;
            item.fy = item.y;
        }

        function dragged(event, item) {
            item.fx = event.x;
            item.fy = event.y;
        }

        function dragEnded(event, item) {
            if (!event.active) simulation.alphaTarget(0);
            if (item.type !== 'Capability') {
                item.fx = null;
                item.fy = null;
            }
        }

        selectNode(center, node, false);

        function showRelationshipLabels(nodeId) {
            linkLabels.classed('is-visible', edge =>
                Boolean(nodeId) && edgeTouchesNode(edge, nodeId));
        }

        function selectNode(selected, nodes, revealRelationships) {
            explicitlySelectedNodeId = revealRelationships ? selected.id : null;
            nodes.classed('is-selected', node => node.id === selected.id);
            showRelationshipLabels(explicitlySelectedNodeId);
            renderNodeDetails(selected);
        }
    }

    function nodeLabelLines(node) {
        const label = node.label || node.metadata.domainId || node.id;
        const words = label.replace(/[._-]+/g, ' ').split(/\s+/).filter(Boolean);
        const lines = [];

        for (const word of words) {
            const candidate = lines.length === 0
                ? word
                : `${lines[lines.length - 1]} ${word}`;
            if (candidate.length <= 17 && lines.length > 0) {
                lines[lines.length - 1] = candidate;
            } else if (lines.length < 2) {
                lines.push(word.length > 17 ? `${word.slice(0, 15)}…` : word);
            }
        }

        if (words.join(' ').length > lines.join(' ').length && lines.length > 0) {
            lines[lines.length - 1] = `${lines[lines.length - 1].slice(0, 15)}…`;
        }

        return lines.length > 0 ? lines : [label.slice(0, 17)];
    }

    function renderNodeDetails(selected) {
        const metadata = selected.metadata || {};
        document.getElementById('inspectorDefaultState').hidden = true;
        document.getElementById('inspectorDetails').hidden = false;
        const badge = document.getElementById('inspectorTypeBadge');
        badge.hidden = false;
        badge.textContent = selected.type;
        badge.className = `badge graph-inspector-type graph-inspector-type-${selected.type.toLowerCase()}`;
        document.getElementById('inspectorName').textContent = selected.label;
        document.getElementById('inspectorId').textContent = metadata.domainId || selected.id;
        document.getElementById('inspectorDescription').textContent =
            metadata.description || metadata.reason || 'No description is available.';
        renderMetadata(metadata);
        renderLinks(selected, metadata.domainId || selected.id);
    }

    function edgeTouchesNode(edge, nodeId) {
        const sourceId = typeof edge.source === 'object'
            ? edge.source.id
            : edge.source;
        const targetId = typeof edge.target === 'object'
            ? edge.target.id
            : edge.target;
        return sourceId === nodeId || targetId === nodeId;
    }

    function groupTarget(node, width, height) {
        const targets = {
            Identity: { x: width * 0.24, y: height * 0.3 },
            Policy: { x: width * 0.76, y: height * 0.3 },
            Resource: { x: width * 0.5, y: height * 0.78 }
        };
        return targets[node.type] || { x: width / 2, y: height / 2 };
    }

    function renderMetadata(metadata) {
        const list = document.getElementById('inspectorMetadata');
        list.replaceChildren();
        const excluded = new Set(['domainId', 'description', 'documentationUrl']);
        Object.entries(metadata)
            .filter(([key, value]) => !excluded.has(key) && value && `${value}`.trim())
            .forEach(([key, value]) => {
                const term = document.createElement('dt');
                term.textContent = formatLabel(key);
                const description = document.createElement('dd');
                description.textContent = value;
                list.append(term, description);
            });
    }

    function renderLinks(node, domainId) {
        const links = document.getElementById('inspectorLinks');
        links.replaceChildren();
        const destinations = {
            Capability: [
                ['Open Capability Explorer', `/capability-explorer?capabilityId=${encodeURIComponent(domainId)}`],
                ['View Capability Activity', `/capability-activity?capabilityId=${encodeURIComponent(domainId)}`]
            ],
            Identity: [['View Identity Activity', `/identity-activity?identityId=${encodeURIComponent(domainId)}`]],
            Policy: [['View Policies', '/policies']],
            Resource: [['View Resources', '/resources']]
        };
        (destinations[node.type] || []).forEach(([label, href]) => {
            const link = document.createElement('a');
            link.className = 'button-link';
            link.href = href;
            link.textContent = label;
            links.appendChild(link);
        });
        const documentationUrl = node.metadata?.documentationUrl;
        if (documentationUrl && /^https?:\/\//i.test(documentationUrl)) {
            const link = document.createElement('a');
            link.href = documentationUrl;
            link.target = '_blank';
            link.rel = 'noopener noreferrer';
            link.textContent = 'Open documentation';
            links.appendChild(link);
        }
    }

    function formatLabel(value) {
        return value
            .replace(/([a-z])([A-Z])/g, '$1 $2')
            .replace(/[-_]/g, ' ')
            .replace(/^./, character => character.toUpperCase());
    }

    function showEmpty(message) {
        container.hidden = true;
        emptyState.hidden = false;
        const paragraph = emptyState.querySelector('p');
        if (paragraph) paragraph.textContent = message;
    }
})();
