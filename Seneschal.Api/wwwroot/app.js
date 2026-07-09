function populateSelect(id, items, property) {
    const select = document.getElementById(id);
    select.innerHTML = "";

    items.forEach(item => {
        const option = document.createElement("option");
        option.value = item[property];
        option.text = item[property];
        select.appendChild(option);
    });
}

async function loadDashboard() {
    const policies = await fetch("/policies").then(r => r.json());
    const capabilities = await fetch("/capabilities").then(r => r.json());
    const identities = await fetch("/identities").then(r => r.json());
    const audit = await fetch("/audit").then(r => r.json());

    document.getElementById("policyCount").innerText = policies.length;
    document.getElementById("capabilityCount").innerText = capabilities.length;
    document.getElementById("identityCount").innerText = identities.length;
    document.getElementById("auditCount").innerText = audit.length;

    populateSelect("simIdentity", identities, "name");
    populateSelect("simCapability", capabilities, "name");

    const tbody = document.querySelector("#auditTable tbody");
    tbody.innerHTML = "";

    audit.reverse().slice(0, 10).forEach(event => {
        const row = document.createElement("tr");

        row.innerHTML = `
            <td>${new Date(event.timestampUtc).toLocaleString()}</td>
            <td>${event.identityId}</td>
            <td>${event.capabilityId}</td>
            <td>${event.decision}</td>
        `;

        tbody.appendChild(row);
    });
}

async function evaluatePolicy() {
    document.getElementById("simResult").innerHTML = `
        <h3>Policy Simulator Disabled</h3>
        <p class="muted">
            The secured <code>/evaluate</code> endpoint requires an integration
            API key. Seneschal does not place integration keys in browser
            JavaScript. Use the protected sample API or a server-side
            integration to evaluate capabilities.
        </p>
    `;
}

document.getElementById("evaluateButton").addEventListener("click", evaluatePolicy);

loadDashboard();
