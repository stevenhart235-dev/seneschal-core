(() => {
    const endpoint = "/dashboard?handler=Live";
    const intervalMs = 3000;
    let timer;
    let requestInFlight = false;

    const relativeTime = value => {
        if (!value) return "No evaluations";
        const seconds = Math.max(0, Math.round((Date.now() - new Date(value).getTime()) / 1000));
        if (seconds < 5) return "just now";
        if (seconds < 60) return `${seconds}s ago`;
        const minutes = Math.floor(seconds / 60);
        return minutes < 60 ? `${minutes}m ago` : `${Math.floor(minutes / 60)}h ago`;
    };

    const element = (tag, className, text) => {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    };
    const requiredElement = selector => {
        const matches = document.querySelectorAll(selector);
        if (matches.length !== 1) {
            throw new Error(`Dashboard live target ${selector} resolved to ${matches.length} elements`);
        }
        return matches[0];
    };

    function render(data) {
        requiredElement("#governance-posture-mode").textContent = data.currentMode;
        requiredElement("#runtime-summary-mode").textContent = data.currentMode;
        requiredElement("#live-capability-count").textContent = data.activeCapabilityCount;
        requiredElement("#live-total-decisions").textContent = data.totalDecisions;
        const lastEvaluation = document.querySelector("#live-last-evaluation");
        if (lastEvaluation) {
            lastEvaluation.dataset.relativeTime = data.lastEvaluationUtc || "";
            lastEvaluation.textContent = relativeTime(data.lastEvaluationUtc);
        }

        const workers = document.querySelector("#active-worker-list");
        workers?.replaceChildren(...data.identities.map(identity => {
            const item = element("li");
            const details = element("div");
            details.append(element("strong", "", identity.identity), element("span", "code", identity.latestCapability));
            const presence = element("span", `worker-presence worker-${identity.status.toLowerCase()}`);
            presence.append(element("span"), document.createTextNode(`${identity.status} · ${relativeTime(identity.lastSeenUtc)}`));
            item.append(details, element("span", `decision-badge ${decisionClass(identity.latestDecision)}`, decisionLabel(identity.latestDecision)), presence);
            return item;
        }));

        const affected = data.denied + data.pending;
        const impactValue = document.querySelector("#application-impact-value");
        const impactDescription = document.querySelector("#application-impact-description");
        if (impactValue && impactDescription) {
            impactValue.textContent = data.currentMode === "Enforce" ? affected : 0;
            impactDescription.textContent = data.currentMode === "Enforce"
                ? "Denied and pending operations are projected as blocked while Enforce is active."
                : `${affected} denied or pending operations would continue and be recorded.`;
        }
        const impactBreakdown = document.querySelector("#application-impact-breakdown");
        impactBreakdown?.classList.toggle("is-hidden", data.currentMode !== "Enforce");
        const impactDenied = document.querySelector("#application-impact-denied");
        const impactPending = document.querySelector("#application-impact-pending");
        if (impactDenied) impactDenied.textContent = data.denied;
        if (impactPending) impactPending.textContent = data.pending;
        const denyCount = document.querySelector("#attention-deny-count");
        const pendingCount = document.querySelector("#attention-pending-count");
        if (denyCount) denyCount.textContent = data.denied;
        if (pendingCount) pendingCount.textContent = data.pending;
        const denyDetailCount = document.querySelector("#attention-deny-detail-count");
        const pendingDetailCount = document.querySelector("#attention-pending-detail-count");
        if (denyDetailCount) denyDetailCount.textContent = data.denied;
        if (pendingDetailCount) pendingDetailCount.textContent = data.pending;

        const distribution = document.querySelector("#decision-distribution");
        if (distribution?.classList.contains("reference-donut")) {
            const total = Math.max(1, data.totalDecisions);
            distribution.style.setProperty("--allow", data.allowed * 100 / total);
            distribution.style.setProperty("--deny", data.denied * 100 / total);
            distribution.style.setProperty("--pending", data.pending * 100 / total);
            distribution.classList.toggle("is-empty", data.totalDecisions === 0);
            distribution.setAttribute("aria-label", `Allow ${data.allowed}, Deny ${data.denied}, Pending Approval ${data.pending}`);
            document.querySelector("#distribution-allow-count").textContent = data.allowed;
            document.querySelector("#distribution-deny-count").textContent = data.denied;
            document.querySelector("#distribution-pending-count").textContent = data.pending;
            requiredElement("#distribution-total-count").textContent = data.totalDecisions;
        }

        const topCapabilities = requiredElement("#top-capability-list");
        const highestRequestCount = data.topCapabilities.length === 0 ? 0 : data.topCapabilities[0].totalRequests;
        topCapabilities.replaceChildren(...data.topCapabilities.map(capability => {
            const item = element("li");
            const link = element("a", "", capability.capability);
            link.href = `/capability-activity?capabilityId=${encodeURIComponent(capability.capability)}`;
            const bar = element("span");
            const fill = element("i");
            fill.style.width = `${highestRequestCount > 0 ? capability.totalRequests * 100 / highestRequestCount : 0}%`;
            bar.append(fill);
            item.append(link, bar, element("strong", "", capability.totalRequests));
            return item;
        }));

        document.querySelector("#dashboard-refresh-status").textContent = `Polling active; refreshed ${relativeTime(data.generatedAtUtc)}`;
        const updated = document.querySelector("#dashboard-last-updated");
        if (updated) {
            updated.dateTime = data.generatedAtUtc;
            updated.dataset.relativeTime = data.generatedAtUtc;
            updated.textContent = relativeTime(data.generatedAtUtc);
        }
    }

    async function refresh() {
        if (document.hidden || requestInFlight) return;
        requestInFlight = true;
        try {
            const response = await fetch(endpoint, { headers: { Accept: "application/json" }, cache: "no-store" });
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            render(await response.json());
            requiredElement("#dashboard-live-status-label").textContent = "Live";
        } catch (error) {
            console.error("Dashboard live refresh failed", error);
            requiredElement("#dashboard-live-status-label").textContent = "Unavailable";
        } finally {
            requestInFlight = false;
        }
    }

    function schedule() {
        clearInterval(timer);
        timer = setInterval(refresh, intervalMs);
    }

    document.addEventListener("visibilitychange", () => {
        if (document.hidden) {
            clearInterval(timer);
        } else {
            refresh();
            schedule();
        }
    });

    setInterval(() => document.querySelectorAll("[data-relative-time]").forEach(time => {
        time.textContent = relativeTime(time.dataset.relativeTime);
    }), 1000);
    document.querySelector("#dashboard-refresh")?.addEventListener("click", refresh);
    refresh();
    schedule();
})();
