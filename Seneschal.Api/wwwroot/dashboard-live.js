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
    const element = (tag, className, text) => {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    };

    function renderDecision(decision, isNew) {
        const item = element("li", isNew && !reducedMotion.matches ? "live-event-new" : "");
        item.dataset.eventId = decision.id;
        const summary = element("div", "live-event-summary");
        const time = element("time", "", relativeTime(decision.timestampUtc));
        time.dateTime = decision.timestampUtc;
        time.dataset.relativeTime = decision.timestampUtc;
        summary.append(time, element("strong", "", decision.identity), element("span", "", "requested"), element("strong", "code", decision.capability));
        const outcome = element("div", "live-event-outcome");
        outcome.append(
            element("span", `decision-badge ${decisionClass(decision.decision)}`, decision.decision.toUpperCase()),
            element("span", "outcome-arrow", "→"),
            element("span", `status-badge mode-${decision.mode.toLowerCase()}`, `Mode: ${decision.mode}`),
            element("strong", `effective-action effective-${decision.effectiveAction.toLowerCase().replaceAll(" ", "-")}`, `PROJECTED: ${decision.effectiveAction.toUpperCase()}`));
        item.append(summary, outcome, element("p", "", decision.reason));
        return item;
    }

    function render(data) {
        const enforce = data.currentMode === "Enforce";
        const posture = document.querySelector("#governance-posture");
        posture.className = `governance-posture mode-posture-${data.currentMode.toLowerCase()}`;
        document.querySelector("#governance-posture-title").textContent = enforce ? "ENFORCEMENT ACTIVE" : "MONITORING ACTIVE";
        document.querySelector("#governance-posture-description").textContent = enforce
            ? "Denied and pending decisions may block integrated operations."
            : "Denied and pending decisions are recorded but do not block.";
        document.querySelector("#governance-posture-mode").textContent = data.currentMode;
        document.querySelector("#live-identity-count").textContent = data.activeIdentityCount;
        document.querySelector("#live-capability-count").textContent = data.activeCapabilityCount;
        document.querySelector("#live-total-decisions").textContent = data.totalDecisions;
        const lastEvaluation = document.querySelector("#live-last-evaluation");
        lastEvaluation.dataset.relativeTime = data.lastEvaluationUtc || "";
        lastEvaluation.textContent = relativeTime(data.lastEvaluationUtc);

        const newIds = data.decisions.filter(decision => !knownEventIds.has(decision.id)).map(decision => decision.id);
        const feed = document.querySelector("#live-decision-feed");
        feed.replaceChildren(...data.decisions.map(decision => renderDecision(decision, newIds.includes(decision.id))));
        document.querySelector("#live-decision-empty")?.remove();
        knownEventIds = new Set(data.decisions.map(decision => decision.id));

        const workers = document.querySelector("#active-worker-list");
        workers.replaceChildren(...data.identities.map(identity => {
            const item = element("li");
            const details = element("div");
            details.append(element("strong", "", identity.identity), element("span", "code", identity.latestCapability));
            const presence = element("span", `worker-presence worker-${identity.status.toLowerCase()}`);
            presence.append(element("span"), document.createTextNode(`${identity.status} · ${relativeTime(identity.lastSeenUtc)}`));
            item.append(details, element("span", `decision-badge ${decisionClass(identity.latestDecision)}`, identity.latestDecision), presence);
            return item;
        }));

        const distribution = document.querySelector("#decision-distribution");
        const values = [["allow", "Allow", data.allowed], ["deny", "Deny", data.denied], ["pending", "Pending", data.pending]];
        distribution.replaceChildren(...values.map(([kind, label, count]) => {
            const row = element("div", `distribution-${kind}`);
            row.style.setProperty("--decision-count", count);
            row.append(element("span", "", label), element("strong", "", count));
            return row;
        }));

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
