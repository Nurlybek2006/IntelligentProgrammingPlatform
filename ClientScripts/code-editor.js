import * as monaco from "monaco-editor/editor/editor.api.js";
import "monaco-editor/languages/definitions/cpp/register.js";

const form = document.getElementById("submission-form");
const diffElement = document.getElementById("code-diff");
if (form || diffElement) {
    globalThis.MonacoEnvironment = {
        getWorker() {
            return new Worker(new URL("./editor.worker.js", import.meta.url), { type: "module" });
        }
    };
}
if (form) {
    const source = document.getElementById("SourceCode");
    const editorElement = document.getElementById("code-editor");
    const status = document.getElementById("submission-progress");
    const submit = document.getElementById("submit-code");
    const key = form.dataset.draftKey;
    try {
        const draft = sessionStorage.getItem(key);
        if (draft !== null && form.dataset.validationFailed !== "true") source.value = draft;
    } catch { /* Storage restrictions must not prevent editing or submitting. */ }

    editorElement.hidden = false;
    const editor = monaco.editor.create(editorElement, {
        value: source.value,
        language: "cpp",
        theme: "vs-dark",
        automaticLayout: true,
        minimap: { enabled: false },
        fontSize: 14,
        tabSize: 4,
        ariaLabel: form.dataset.editorLabel
    });
    source.hidden = true;

    const sync = () => {
        source.value = editor.getValue();
        try {
            if (source.value.length <= 128 * 1024) sessionStorage.setItem(key, source.value);
        } catch { /* The form field still preserves the source without browser storage. */ }
    };
    editor.onDidChangeModelContent(sync);
    form.addEventListener("source-sync", sync);
    form.addEventListener("submit", event => {
        sync();
        if (!source.value.trim() || new TextEncoder().encode(source.value).length > 64 * 1024) {
            event.preventDefault();
            status.textContent = form.dataset.sourceLimit;
            editor.focus();
            return;
        }
        if (globalThis.jQuery && !globalThis.jQuery(form).valid()) return;
        submit.disabled = true;
        status.textContent = form.dataset.submitProgress;
    });
    window.addEventListener("pageshow", () => {
        submit.disabled = document.getElementById("RuntimeId").options.length === 0;
        status.textContent = "";
    });
    window.addEventListener("pagehide", sync);
}

if (diffElement) {
    const original = monaco.editor.createModel(document.getElementById("previous-source").value, "cpp");
    const modified = monaco.editor.createModel(document.getElementById("current-source").value, "cpp");
    diffElement.hidden = false;
    const diff = monaco.editor.createDiffEditor(diffElement, {
        theme: "vs-dark", readOnly: true, originalEditable: false,
        automaticLayout: true, renderSideBySide: true,
        useInlineViewWhenSpaceIsLimited: true, renderSideBySideInlineBreakpoint: 800,
        minimap: { enabled: false }, fontSize: 14, scrollBeyondLastLine: false,
        maxComputationTime: 5000, originalAriaLabel: diffElement.dataset.previousLabel,
        modifiedAriaLabel: diffElement.dataset.currentLabel
    });
    diff.setModel({ original, modified });
    document.getElementById("diff-fallback").hidden = true;
    window.addEventListener("pagehide", event => {
        if (!event.persisted) { diff.dispose(); original.dispose(); modified.dispose(); }
    });
}
