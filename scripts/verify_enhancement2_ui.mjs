// Integration doubles exercise our JS; they do not simulate browser layout or CSP.
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import vm from "node:vm";

const marker = '</textarea></script><img src=x onerror=alert(1)>';
const editorSource = (await readFile("ClientScripts/code-editor.js", "utf8"))
    .replace(/^import .*;\r?\n/gm, "").replaceAll("import.meta.url", '"https://localhost:7115/js/editor/code-editor.js"');
function exerciseDiff(previousLabel, currentLabel, language = "cpp") {
const expectedLanguage = ["cpp", "python"].includes(language) ? language : "plaintext";
const elements = { "code-diff": { hidden: true, dataset: { previousLabel, currentLabel, language } }, "diff-fallback": { hidden: false },
    "previous-source": { value: "\n// " + marker }, "current-source": { value: "int main() {}" } };
const models = [], windowHandlers = {};
let options, selected, disposed = 0, workerUrl;
const diffContext = {
    document: { getElementById: id => elements[id] }, URL,
    Worker: class { constructor(url, config) { workerUrl = url.href; assert.equal(config.type, "module"); } },
    window: { addEventListener: (name, handler) => windowHandlers[name] = handler },
    monaco: { editor: {
        createModel: (value, modelLanguage) => {
            assert.equal(modelLanguage, expectedLanguage);
            const model = { value, dispose: () => disposed++ }; models.push(model); return model;
        },
        createDiffEditor: (element, config) => {
            assert.equal(element, elements["code-diff"]); options = config;
            return { setModel: value => selected = value, dispose: () => disposed++ };
        }
    } }
};
vm.runInNewContext(editorSource, diffContext);
assert.equal(models[0].value, elements["previous-source"].value);
assert.equal(models[1].value, elements["current-source"].value);
assert.equal(selected.original, models[0]); assert.equal(selected.modified, models[1]);
assert.equal(options.readOnly, true); assert.equal(options.originalEditable, false);
assert.equal(options.renderSideBySide, true); assert.equal(options.useInlineViewWhenSpaceIsLimited, true);
assert.equal(options.renderSideBySideInlineBreakpoint, 800); assert.equal(options.automaticLayout, true);
assert.equal(options.originalAriaLabel, previousLabel); assert.equal(options.modifiedAriaLabel, currentLabel);
assert.equal(elements["code-diff"].hidden, false); assert.equal(elements["diff-fallback"].hidden, true);
diffContext.MonacoEnvironment.getWorker();
assert.equal(workerUrl, "https://localhost:7115/js/editor/editor.worker.js");
windowHandlers.pagehide({ persisted: true }); assert.equal(disposed, 0);
windowHandlers.pagehide({ persisted: false }); assert.equal(disposed, 3);
}
for (const labels of [["Алдыңғы әрекет коды", "Қазіргі әрекет коды"], ["Код предыдущей попытки", "Код текущей попытки"], ["Previous attempt source", "Current attempt source"]]) exerciseDiff(...labels);
console.log("PASS: Diff uses localized server labels, textarea text, read-only C++ models, narrow-layout option, local worker and safe disposal");
for (const language of ["python", "plaintext", "unsupported"]) exerciseDiff("Previous", "Current", language);
console.log("PASS: Python diff highlighting and mixed/unsupported language plaintext fallback stay read-only");

const runSource = await readFile("wwwroot/js/custom-run.js", "utf8");
assert.equal(runSource.includes("innerHTML"), false);

function harness(culture = "en-US") {
    const ids = ["submission-form", "run-code", "submit-code", "SourceCode", "CustomInput", "custom-run-result",
        "run-status", "run-summary", "run-output", "run-error", "run-compiler", "run-time", "run-error-panel", "run-compiler-panel", "RuntimeId"];
    const nodes = Object.fromEntries(ids.map(id => [id, {
        disabled: false, hidden: false, textContent: "", value: "", handlers: {}, attributes: {},
        addEventListener(name, handler) { this.handlers[name] = handler; },
        setAttribute(name, value) { this.attributes[name] = value; }
    }]));
    for (const node of Object.values(nodes)) Object.defineProperty(node, "innerHTML", { set() { throw new Error("Untrusted HTML sink used"); } });
    nodes["submission-form"].dataset = { runUrl: "/CustomRuns/Run" };
    nodes["custom-run-result"].dataset = {
        temporary: "Temporary result only. No attempt is saved.", checkInput: "Check input",
        limits: "Enter source of at most 64 KiB and custom input of at most 32 KiB (UTF-8).",
        running: "Running…", signIn: "Sign in again to run your code.", reload: "Run could not finish. Reload the page and try again.",
        retry: "Run could not finish. Please try again.", interrupted: "Run was interrupted. Please try again.",
        timeFormat: "Execution time: {0} ms (wall clock)", timeUnmeasured: "Execution time: not measured",
        successSummary: "Program finished. Output was not compared with an expected answer. No attempt saved.",
        statusSuccess: "Success", statusCompilationError: "Compilation error", statusRuntimeError: "Runtime error",
        statusTimeLimitExceeded: "Time limit exceeded", statusMemoryLimitExceeded: "Memory limit exceeded", statusInternalError: "Unavailable"
    };
    if (culture === "kk-KZ") Object.assign(nodes["custom-run-result"].dataset,
        { running: "Орындалуда…", statusSuccess: "Сәтті", timeFormat: "Орындау уақыты: {0} мс" });
    if (culture === "ru-RU") Object.assign(nodes["custom-run-result"].dataset,
        { running: "Выполняется…", statusSuccess: "Успешно", timeFormat: "Время выполнения: {0} мс" });
    nodes["submission-form"].dispatchEvent = event => {
        assert.equal(event.type, "source-sync"); nodes.SourceCode.value = state.editorValue;
    };
    nodes.RuntimeId.options = [1]; nodes.RuntimeId.value = "python-runtime-id"; nodes.CustomInput.value = "2 3";
    let resolveRequest, rejectRequest;
    const state = { nodes, requests: 0, editorValue: "int main() {}", aborted: false };
    const lifecycle = {};
    vm.runInNewContext(runSource, {
        document: { getElementById: id => nodes[id], documentElement: { lang: culture } }, TextEncoder, URLSearchParams, AbortController, Event,
        FormData: class { constructor() { assert.equal(nodes.RuntimeId.disabled, false); return [["RuntimeId", nodes.RuntimeId.value], ["SourceCode", nodes.SourceCode.value], ["CustomInput", nodes.CustomInput.value], ["__RequestVerificationToken", "fixture-token"]]; } },
        setTimeout: callback => { state.timeout = callback; return 1; }, clearTimeout: () => state.cleared = true,
        window: { addEventListener: (name, handler) => lifecycle[name] = handler },
        fetch: (url, request) => {
            state.requests++; assert.equal(url, "/CustomRuns/Run"); assert.equal(request.method, "POST");
            assert.equal(request.credentials, "same-origin");
            assert.equal(request.body.get("__RequestVerificationToken"), "fixture-token");
            assert.equal(request.body.get("SourceCode"), state.editorValue);
            assert.equal(request.body.get("CustomInput"), nodes.CustomInput.value);
            assert.equal(request.body.get("RuntimeId"), "python-runtime-id");
            assert.equal(nodes.RuntimeId.disabled, true);
            return new Promise((resolve, reject) => {
                resolveRequest = resolve; rejectRequest = reject;
                request.signal.addEventListener("abort", () => { state.aborted = true; const failure = new Error("aborted"); failure.name = "AbortError"; reject(failure); });
            });
        }
    });
    return { ...state, state, nodes, click: () => nodes["run-code"].handlers.click(), pagehide: () => lifecycle.pagehide(),
        resolve: (data, ok = true, redirected = false, type = "application/json") => resolveRequest({ ok, redirected, status: ok ? 200 : 400, headers: { get: () => type }, json: async () => data }),
        reject: () => rejectRequest(new Error("network failure")) };
}

const success = harness();
const pending = success.click();
assert.equal(success.nodes["run-code"].disabled, true); assert.equal(success.nodes["submit-code"].disabled, true);
await success.click(); assert.equal(success.state.requests, 1, "Repeated click cannot start a second request");
success.resolve({ status: "Success", output: marker, error: marker, compilerOutput: marker, executionTimeMs: 12 });
await pending;
for (const id of ["run-output", "run-error", "run-compiler"]) assert.equal(success.nodes[id].textContent, marker);
assert.equal(success.nodes["run-status"].textContent, "Success");
assert.equal(success.nodes["run-code"].disabled, false); assert.equal(success.nodes["submit-code"].disabled, false);
assert.equal(success.nodes.RuntimeId.disabled, false);
assert.equal(success.nodes["custom-run-result"].attributes["aria-busy"], "false");
assert.equal(success.state.cleared, true);
console.log("PASS: Run syncs source, sends POST/token/input, prevents duplicate clicks and renders hostile output as text");

for (const culture of ["kk-KZ", "ru-RU", "en-US"]) {
    const test = harness(culture); const request = test.click(); const labels = test.nodes["custom-run-result"].dataset;
    assert.equal(test.nodes["run-status"].textContent, labels.running);
    test.resolve({ status: "Success", executionTimeMs: 1234 }); await request;
    assert.equal(test.nodes["run-status"].textContent, labels.statusSuccess);
    assert.equal(test.nodes["run-time"].textContent, labels.timeFormat.replace("{0}", (1234).toLocaleString(culture)));
}
console.log("PASS: Run reads server-localized labels and formats numbers for kk-KZ, ru-RU and en-US");

for (const mode of ["compile", "http-error", "network", "redirect", "html", "abort", "timeout", "unknown-status"]) {
    const test = harness(); const request = test.click();
    if (mode === "network") test.reject();
    else if (mode === "abort") test.pagehide();
    else if (mode === "timeout") test.state.timeout();
    else test.resolve({ status: mode === "unknown-status" ? marker : "CompilationError", error: "safe diagnostic", compilerOutput: marker }, mode !== "http-error", mode === "redirect", mode === "html" ? "text/html" : "application/json");
    await request;
    assert.equal(test.nodes["run-code"].disabled, false, mode); assert.equal(test.nodes["submit-code"].disabled, false, mode);
    assert.equal(test.nodes["run-status"].className.includes(marker), false);
    if (mode === "abort" || mode === "timeout") assert.equal(test.state.aborted, true);
    if (mode === "network") assert.equal(test.nodes["run-error"].textContent, test.nodes["custom-run-result"].dataset.retry);
}
for (const oversizeSource of [true, false]) {
    const test = harness();
    if (oversizeSource) test.state.editorValue = "я".repeat(32769);
    else test.nodes.CustomInput.value = "я".repeat(16385);
    await test.click(); assert.equal(test.state.requests, 0); assert.equal(test.nodes["run-status"].textContent, "Check input");
}
console.log("PASS: Validation, compilation/HTTP/network failures, auth redirect and cancellation restore controls safely");
console.log("REAL BROWSER STILL REQUIRED: rendered diff, highlighting, interaction, responsive layout, worker startup and CSP console");
