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
    const text = result.dataset;
    const labels = { Success: text.statusSuccess, CompilationError: text.statusCompilationError, RuntimeError: text.statusRuntimeError,
        TimeLimitExceeded: text.statusTimeLimitExceeded, MemoryLimitExceeded: text.statusMemoryLimitExceeded, InternalError: text.statusInternalError };
    const localizedFailure = message => Object.assign(new Error(message), { isRunFeedback: true });
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
        summary.textContent = text.temporary;
        showDiagnostic("");
        if (!source.value.trim() || new TextEncoder().encode(source.value).length > 64 * 1024
            || new TextEncoder().encode(input.value).length > 32 * 1024) {
            status.textContent = text.checkInput;
            status.className = "status-badge";
            showDiagnostic(text.limits);
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
        status.textContent = text.running;
        try {
            const body = new URLSearchParams(new FormData(form));
            const response = await fetch(form.dataset.runUrl, {
                method: "POST", body, credentials: "same-origin", signal: controller.signal,
                headers: { Accept: "application/json" }
            });
            if (response.redirected || response.status === 401) throw localizedFailure(text.signIn);
            if (!response.headers.get("content-type")?.includes("application/json"))
                throw localizedFailure(text.reload);
            const data = await response.json();
            if (!response.ok) throw localizedFailure(data.error || text.retry);
            const state = Object.hasOwn(labels, data.status) ? data.status : "InternalError";
            status.textContent = labels[state];
            status.className = "status-badge " + (state === "Success" ? "Accepted" : state);
            output.textContent = data.output || "";
            showDiagnostic(data.error, data.compilerOutput);
            time.textContent = Number.isFinite(data.executionTimeMs)
                ? text.timeFormat.replace("{0}", data.executionTimeMs.toLocaleString(document.documentElement.lang)) : text.timeUnmeasured;
            if (state === "Success") summary.textContent = text.successSummary;
        } catch (failure) {
            status.textContent = text.statusInternalError;
            status.className = "status-badge InternalError";
            showDiagnostic(failure.name === "AbortError" ? text.interrupted : failure.isRunFeedback ? failure.message : text.retry);
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
