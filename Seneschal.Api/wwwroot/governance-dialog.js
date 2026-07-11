(() => {
    let trigger;

    document.querySelectorAll("[data-dialog-trigger]").forEach(button => {
        button.addEventListener("click", () => {
            const dialog = document.getElementById(button.dataset.dialogTrigger);
            if (!dialog) return;
            trigger = button;
            dialog.showModal();
            dialog.querySelector("[data-dialog-close]")?.focus();
        });
    });

    document.querySelectorAll(".governance-dialog").forEach(dialog => {
        dialog.querySelector("[data-dialog-close]")?.addEventListener("click", () => {
            dialog.close();
        });
        dialog.addEventListener("close", () => {
            trigger?.focus();
            trigger = undefined;
        });
    });
})();
