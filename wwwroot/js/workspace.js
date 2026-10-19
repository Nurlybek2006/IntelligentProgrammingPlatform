// Presentation-only tabs: the existing runner still owns result data, requests and busy state.
(() => {
    const tabs = document.querySelector("[data-workspace-tabs]");
    const input = document.getElementById("custom-input-panel");
    const result = document.getElementById("custom-run-result");
    if (!tabs || !input || !result) return;
    const buttons = [...tabs.querySelectorAll("[data-workspace-pane]")];
    const resultButton = buttons.find(button => button.dataset.workspacePane === "result");
    const select = (name, focus = false) => {
        if (name === "result" && result.hidden) return;
        input.hidden = name !== "input";
        result.classList.toggle("workspace-pane-inactive", name !== "result");
        for (const button of buttons) {
            const active = button.dataset.workspacePane === name;
            button.setAttribute("aria-selected", String(active));
            button.tabIndex = active ? 0 : -1;
            if (active && focus) button.focus();
        }
    };
    input.setAttribute("role", "tabpanel"); input.setAttribute("aria-labelledby", "input-tab");
    result.setAttribute("role", "tabpanel"); result.setAttribute("aria-labelledby", "result-tab");
    for (const button of buttons) {
        button.addEventListener("click", () => select(button.dataset.workspacePane));
        button.addEventListener("keydown", event => {
            if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) return;
            event.preventDefault();
            const available = buttons.filter(item => !item.disabled);
            const current = available.indexOf(button);
            const index = event.key === "Home" ? 0 : event.key === "End" ? available.length - 1
                : (current + (event.key === "ArrowRight" ? 1 : -1) + available.length) % available.length;
            select(available[index].dataset.workspacePane, true);
        });
    }
    const observer = new MutationObserver(() => {
        resultButton.disabled = result.hidden;
        select(result.hidden ? "input" : "result");
    });
    observer.observe(result, { attributes: true, attributeFilter: ["hidden"] });
    // A subsequent run can reuse a visible result without changing its hidden attribute.
    document.getElementById("run-code")?.addEventListener("click", () => queueMicrotask(() => {
        resultButton.disabled = result.hidden;
        if (!result.hidden) select("result");
    }));
    resultButton.disabled = result.hidden;
    tabs.hidden = false;
    select(result.hidden ? "input" : "result");
    window.addEventListener("pagehide", event => { if (!event.persisted) observer.disconnect(); });
})();
