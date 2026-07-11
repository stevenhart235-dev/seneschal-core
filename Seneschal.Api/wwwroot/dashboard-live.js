(() => {
    const endpoint = "/dashboard?handler=Live";
    const intervalMs = 3000;
    let timer;
    let knownEventIds = new Set(
        [...document.querySelectorAll("[data-event-id]")].map(element => element.dataset.eventId));
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");

    const relativeTime = value => {
        if (!value) return "No evaluations";
        const seconds = Math.max(0, Math.round((Date.now() - new Date(value).getTime()) / 1000));
        if (seconds < 5) return "just now";
        if (seconds < 60) return `${seconds}s ago`;
        const minutes = Math.floor(seconds / 60);
        return minutes < 60 ? `${minutes}m ago` : `${Math.floor(minutes / 60)}h ago`;
    };

    const decisionClass = value => `decision-${value.toLowerCase()}`;
    const decisionLabel = value => value === "PendingApproval" ? "Pending Approval" : value;
    const element = (tag, className, text) => {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    };

    function renderDecision(decision, isNew) {
        const item = element("li", isNew && !reducedMotion.matches ? "live-event-new" : "");
        item.dataset.eventId = decision.id;
        const time = element("time", "", relativeTime(decision.timestampUtc));
        time.dateTime = decision.timestampUtc;
        time.dataset.relativeTime = decision.timestampUtc;
        const request = element("div", "reference-event-request");
        request.append(element("a", "", decision.identity), element("span", "", "requested"), element("a", "", decision.capability), element("small", "", decision.reason));
        request.children[0].href = `/identity-activity?identityId=${encodeURIComponent(decision.identity)}`;
        request.children[2].href = `/capability-activity?capabilityId=${encodeURIComponent(decision.capability)}`;
        const outcome = element("div", "reference-event-result");
        outcome.append(
            element("span", `decision-badge ${decisionClass(decision.decision)}`, decisionLabel(decision.decision)),
            element("strong", `effective-action effective-${decision.effectiveAction.toLowerCase().replaceAll(" ", "-")}`, `Projected: ${decision.effectiveAction}`),
            element("small", "", decision.mode));
        item.append(time, request, outcome);
        return item;
    }

    function render(data) {
        const enforce = data.currentMode === "Enforce";
        const posture = document.querySelector("#governance-posture");
        posture.className = `reference-kpi mode-kpi mode-posture-${data.currentMode.toLowerCase()}`;
        document.querySelector("#governance-posture-title").textContent = enforce ? "Enforcing" : "Monitoring";
        document.querySelector("#governance-posture-description").textContent = enforce
            ? "Deny and pending projected blocked"
            : "Decisions recorded; operations continue";
        document.querySelector("#governance-posture-mode").textContent = data.currentMode;
        document.querySelector("#live-identity-count").textContent = data.activeIdentityCount;
        document.querySelector("#live-capability-count").textContent = data.activeCapabilityCount;
        document.querySelector("#live-total-decisions").textContent = data.totalDecisions;
        const lastEvaluation = document.querySelector("#live-last-evaluation");
        if (lastEvaluation) {
            lastEvaluation.dataset.relativeTime = data.lastEvaluationUtc || "";
            lastEvaluation.textContent = relativeTime(data.lastEvaluationUtc);
        }

        const newIds = data.decisions.filter(decision => !knownEventIds.has(decision.id)).map(decision => decision.id);
        const feed = document.querySelector("#live-decision-feed");
        feed.replaceChildren(...data.decisions.slice(0, 6).map(decision => renderDecision(decision, newIds.includes(decision.id))));
        document.querySelector("#live-decision-empty")?.remove();
        knownEventIds = new Set(data.decisions.map(decision => decision.id));

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
                : `${affected} denied or pending operations are projected to continue and be recorded.`;
        }
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
        }

        document.querySelector("#dashboard-refresh-status").textContent = `Polling active; refreshed ${relativeTime(data.generatedAtUtc)}`;
        if (newIds.length) document.querySelector("#live-update-label").textContent = `${newIds.length} new evaluation${newIds.length === 1 ? "" : "s"}`;
    }

    async function refresh() {
        if (document.hidden) return;
        try {
            const response = await fetch(endpoint, { headers: { Accept: "application/json" }, cache: "no-store" });
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            render(await response.json());
            document.querySelector("#dashboard-live-state").lastChild.textContent = "Live";
        } catch {
            document.querySelector("#dashboard-live-state").lastChild.textContent = "Refresh unavailable";
        }
    }

    function schedule() {
        clearInterval(timer);
        timer = setInterval(refresh, intervalMs);
    }

    document.addEventListener("visibilitychange", () => {
        const label = document.querySelector("#dashboard-live-state");
        if (document.hidden) {
            clearInterval(timer);
            label.lastChild.textContent = "Paused";
        } else {
            label.lastChild.textContent = "Live";
            refresh();
            schedule();
        }
    });

    setInterval(() => document.querySelectorAll("[data-relative-time]").forEach(time => {
        time.textContent = relativeTime(time.dataset.relativeTime);
    }), 1000);
    schedule();
})();
