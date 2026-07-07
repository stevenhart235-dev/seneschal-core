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
    const identity = document.getElementById("simIdentity").value;
    const capability = document.getElementById("simCapability").value;
    const environment = document.getElementById("simEnvironment").value;
    const resource = document.getElementById("simResource").value;

    const response = await fetch("/evaluate", {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({
            identity,
            capability,
            context: {
                environment,
                resource
            }
        })
    });

    const result = await response.json();

    document.getElementById("simResult").innerHTML = `
        <h3>Decision Result</h3>
        <p><strong>Decision:</strong> ${result.decision}</p>
        <p><strong>Effective Action:</strong> ${result.effectiveAction}</p>
        <p><strong>Mode:</strong> ${result.mode}</p>
        <p><strong>Matched Policy:</strong> ${result.policyMatched}</p>
        <p><strong>Reason:</strong> ${result.reason}</p>
        <p><strong>Duration:</strong> ${result.durationMs} ms</p>
    `;

    await loadDashboard();
}

document.getElementById("evaluateButton").addEventListener("click", evaluatePolicy);

loadDashboard();
