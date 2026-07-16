(() => {
    const endpoint = "/monitor";
    const intervalMs = 3000;
    let timer;

    const relativeTime = value => {
        if (!value) return "No evaluations observed";
        const seconds = Math.max(0, Math.round((Date.now() - new Date(value).getTime()) / 1000));
        if (seconds < 5) return "just now";
        if (seconds < 60) return `${seconds}s ago`;
        const minutes = Math.floor(seconds / 60);
        return minutes < 60 ? `${minutes}m ago` : `${Math.floor(minutes / 60)}h ago`;
    };

    const updateRelativeTimes = () => document.querySelectorAll("[data-relative-time]").forEach(time => {
        time.textContent = relativeTime(time.dateTime || time.dataset.relativeTime);
    });

    const setPollingState = (label, stale) => {
        const status = document.querySelector("#monitor-polling-status");
        const health = document.querySelector("#monitor-health-polling");
        if (status) {
            status.classList.toggle("is-stale", stale);
            status.lastChild.textContent = ` ${label}`;
        }
        if (health) health.textContent = stale ? "Stale" : "Current";
    };

    async function refresh() {
        if (document.hidden) return;
        try {
            const response = await fetch(endpoint, {
                headers: { Accept: "text/html" },
                cache: "no-store"
            });
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            const nextDocument = new DOMParser().parseFromString(await response.text(), "text/html");
            const nextConsole = nextDocument.querySelector("#monitor-console");
            const currentConsole = document.querySelector("#monitor-console");
            if (!nextConsole || !currentConsole) throw new Error("Monitor console missing");
            currentConsole.replaceWith(nextConsole);
            setPollingState("Current · every 3 seconds", false);
            updateRelativeTimes();
        } catch {
            setPollingState("Stale · refresh unavailable", true);
        }
    }

    function schedule() {
        clearInterval(timer);
        timer = setInterval(refresh, intervalMs);
    }

    document.addEventListener("visibilitychange", () => {
        if (document.hidden) {
            clearInterval(timer);
            setPollingState("Paused while tab is hidden", false);
        } else {
            refresh();
            schedule();
        }
    });

    setInterval(updateRelativeTimes, 1000);
    updateRelativeTimes();
    schedule();
})();
