import * as monaco from "monaco-editor/editor/editor.api.js";
import "monaco-editor/languages/definitions/cpp/register.js";
import "monaco-editor/languages/definitions/python/register.js";

const editorLanguage = value => ["cpp", "python"].includes(value) ? value : "plaintext";

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
    const runtime = document.getElementById("RuntimeId");
    const selected = () => runtime.selectedOptions[0];
    let language = editorLanguage(selected()?.dataset.language);
    const key = () => form.dataset.draftKey + ":" + language;
    const drafts = new Map();
    const loadDraft = () => {
        try {
            const draft = sessionStorage.getItem(key());
            if (draft !== null) return draft;
            // Migrate the existing C++ draft without ever assigning it to Python.
            if (language === "cpp") {
                const legacy = sessionStorage.getItem(form.dataset.draftKey);
                if (legacy !== null) {
                    sessionStorage.setItem(key(), legacy);
                    sessionStorage.removeItem(form.dataset.draftKey);
                    return legacy;
                }
            }
        } catch { /* Storage restrictions must not prevent editing or submitting. */ }
        return null;
    };
    try {
        const draft = loadDraft();
        if (draft !== null && form.dataset.validationFailed !== "true") source.value = draft;
    } catch { /* Storage restrictions must not prevent editing or submitting. */ }
    drafts.set(language, source.value);

    editorElement.hidden = false;
    const editor = monaco.editor.create(editorElement, {
        value: source.value,
        language,
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
        drafts.set(language, source.value);
        try {
            if (source.value.length <= 128 * 1024) sessionStorage.setItem(key(), source.value);
        } catch { /* The form field still preserves the source without browser storage. */ }
    };
    editor.onDidChangeModelContent(sync);
    runtime.addEventListener("change", () => {
        sync();
        language = editorLanguage(selected()?.dataset.language);
        const restored = drafts.has(language) ? drafts.get(language) : loadDraft();
        monaco.editor.setModelLanguage(editor.getModel(), language);
        editor.setValue(restored ?? selected()?.dataset.starter ?? "");
        sync();
        status.textContent = "";
        const runResult = document.getElementById("custom-run-result");
        if (runResult) runResult.hidden = true;
    });
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
        submit.disabled = runtime.options.length === 0;
        status.textContent = "";
    });
    window.addEventListener("pagehide", sync);
}

if (diffElement) {
    const language = editorLanguage(diffElement.dataset.language);
    const original = monaco.editor.createModel(document.getElementById("previous-source").value, language);
    const modified = monaco.editor.createModel(document.getElementById("current-source").value, language);
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
