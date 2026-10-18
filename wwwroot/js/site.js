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
// Keep initials visible when a managed image is missing or cannot be decoded.
document.querySelectorAll('.user-avatar img').forEach(image => {
    const fallback = () => { image.hidden = true; };
    image.addEventListener('error', fallback);
    if (image.complete && image.naturalWidth === 0) fallback();
});
