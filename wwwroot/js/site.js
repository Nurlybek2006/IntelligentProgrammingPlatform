// Give slow, explicitly requested form actions a simple accessible waiting state.
document.querySelectorAll("form[data-busy-form]").forEach(form => {
    form.addEventListener("submit", () => {
        form.querySelectorAll('button[type="submit"]').forEach(button => { button.disabled = true; });
        const status = form.querySelector('[role="status"]');
        if (status) status.textContent = form.dataset.busyForm;
    });
    window.addEventListener("pageshow", () => {
        form.querySelectorAll('button[type="submit"]').forEach(button => { button.disabled = false; });
        const status = form.querySelector('[role="status"]');
        if (status) status.textContent = "";
    });
});
