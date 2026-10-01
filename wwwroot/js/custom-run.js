const form = document.getElementById("submission-form");
const run = document.getElementById("run-code");
if (form && run) {
    const submit = document.getElementById("submit-code");
    const source = document.getElementById("SourceCode");
    const input = document.getElementById("CustomInput");
    const result = document.getElementById("custom-run-result");
    const status = document.getElementById("run-status");
    const summary = document.getElementById("run-summary");
    const output = document.getElementById("run-output");
    const error = document.getElementById("run-error");
    const compiler = document.getElementById("run-compiler");
    const time = document.getElementById("run-time");
    const labels = { Success: "Success", CompilationError: "Compilation error", RuntimeError: "Runtime error",
        TimeLimitExceeded: "Time limit exceeded", MemoryLimitExceeded: "Memory limit exceeded", InternalError: "Unavailable" };
    let active;
    const showDiagnostic = (message, compilation = "") => {
        error.textContent = message || "";
        compiler.textContent = compilation || "";
        document.getElementById("run-error-panel").hidden = !message;
        document.getElementById("run-compiler-panel").hidden = !compilation;
    };
    run.addEventListener("click", async () => {
        if (active) return;
        form.dispatchEvent(new Event("source-sync"));
        result.hidden = false;
        output.textContent = "";
        time.textContent = "";
        summary.textContent = "Temporary result only. No attempt is saved.";
        showDiagnostic("");
        if (!source.value.trim() || new TextEncoder().encode(source.value).length > 64 * 1024
            || new TextEncoder().encode(input.value).length > 32 * 1024) {
            status.textContent = "Check input";
            status.className = "status-badge";
            showDiagnostic("Enter source of at most 64 KiB and custom input of at most 32 KiB (UTF-8).");
            return;
        }
        const controller = new AbortController();
        active = controller;
        const timeout = setTimeout(() => controller.abort(), 110000);
        const submitWasDisabled = submit.disabled;
        run.disabled = true;
        submit.disabled = true;
        result.setAttribute("aria-busy", "true");
        status.className = "status-badge";
        status.textContent = "Running…";
        try {
            const body = new URLSearchParams(new FormData(form));
            const response = await fetch(form.dataset.runUrl, {
                method: "POST", body, credentials: "same-origin", signal: controller.signal,
                headers: { Accept: "application/json" }
            });
            if (response.redirected || response.status === 401) throw new Error("Sign in again to run your code.");
            if (!response.headers.get("content-type")?.includes("application/json"))
                throw new Error("Run could not finish. Reload the page and try again.");
            const data = await response.json();
            if (!response.ok) throw new Error(data.error || "Run could not finish. Please try again.");
            const state = Object.hasOwn(labels, data.status) ? data.status : "InternalError";
            status.textContent = labels[state];
            status.className = "status-badge " + (state === "Success" ? "Accepted" : state);
            output.textContent = data.output || "";
            showDiagnostic(data.error, data.compilerOutput);
            time.textContent = Number.isFinite(data.executionTimeMs) ? `Execution time: ${data.executionTimeMs} ms (wall clock)` : "Execution time: not measured";
            if (state === "Success") summary.textContent = "Program finished. Output was not compared with an expected answer. No attempt saved.";
        } catch (failure) {
            status.textContent = "Unavailable";
            status.className = "status-badge InternalError";
            showDiagnostic(failure.name === "AbortError" ? "Run was interrupted. Please try again." : failure.message);
        } finally {
            clearTimeout(timeout);
            active = null;
            run.disabled = document.getElementById("RuntimeId").options.length === 0;
            submit.disabled = submitWasDisabled;
            result.setAttribute("aria-busy", "false");
        }
    });
    window.addEventListener("pagehide", () => active?.abort());
}
