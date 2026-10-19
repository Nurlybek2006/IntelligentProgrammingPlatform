// Navigation uses classes instead of inline styles so the strict CSP stays intact.
document.documentElement.classList.add("has-ui");
const navToggle = document.querySelector("[data-nav-toggle]");
const navigation = document.getElementById("main-navigation");
navToggle?.addEventListener("click", () => {
    const open = navToggle.getAttribute("aria-expanded") !== "true";
    navToggle.setAttribute("aria-expanded", String(open));
    navigation?.classList.toggle("is-open", open);
});
const dropdowns = [...document.querySelectorAll(".nav-dropdown")];
for (const dropdown of dropdowns) {
    dropdown.addEventListener("toggle", () => {
        if (dropdown.open) for (const other of dropdowns) if (other !== dropdown) other.open = false;
    });
    dropdown.addEventListener("keydown", event => {
        const summary = dropdown.querySelector("summary");
        if (event.key === "Escape") {
            event.preventDefault(); dropdown.open = false; summary.focus();
        } else if (["ArrowDown", "ArrowUp", "Home", "End"].includes(event.key)) {
            event.preventDefault(); dropdown.open = true;
            const items = [...dropdown.querySelectorAll(".dropdown-item")];
            const current = items.indexOf(document.activeElement);
            const index = event.key === "Home" ? 0 : event.key === "End" ? items.length - 1
                : current < 0 ? (event.key === "ArrowDown" ? 0 : items.length - 1)
                : (current + (event.key === "ArrowDown" ? 1 : -1) + items.length) % items.length;
            items[index]?.focus();
        }
    });
    dropdown.addEventListener("focusout", event => {
        if (event.relatedTarget && !dropdown.contains(event.relatedTarget)) dropdown.open = false;
    });
}
document.addEventListener("click", event => {
    for (const dropdown of dropdowns) if (!dropdown.contains(event.target)) dropdown.open = false;
});
navigation?.addEventListener("keydown", event => {
    if (event.key === "Escape" && !event.defaultPrevented && navToggle?.getAttribute("aria-expanded") === "true") {
        navigation.classList.remove("is-open"); navToggle.setAttribute("aria-expanded", "false"); navToggle.focus();
    }
});
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
