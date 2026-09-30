import * as monaco from "monaco-editor/editor/editor.api.js";
import "monaco-editor/languages/definitions/cpp/register.js";

const form = document.getElementById("submission-form");
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

    globalThis.MonacoEnvironment = {
        getWorker() {
            return new Worker(new URL("./editor.worker.js", import.meta.url), { type: "module" });
        }
    };
    editorElement.hidden = false;
    const editor = monaco.editor.create(editorElement, {
        value: source.value,
        language: "cpp",
        theme: "vs-dark",
        automaticLayout: true,
        minimap: { enabled: false },
        fontSize: 14,
        tabSize: 4,
        ariaLabel: "C++ source code editor"
    });
    source.hidden = true;

    const sync = () => {
        source.value = editor.getValue();
        try {
            if (source.value.length <= 128 * 1024) sessionStorage.setItem(key, source.value);
        } catch { /* The form field still preserves the source without browser storage. */ }
    };
    editor.onDidChangeModelContent(sync);
    form.addEventListener("submit", event => {
        sync();
        if (!source.value.trim() || new TextEncoder().encode(source.value).length > 64 * 1024) {
            event.preventDefault();
            status.textContent = "Enter source code of at most 64 KB (UTF-8).";
            editor.focus();
            return;
        }
        if (globalThis.jQuery && !globalThis.jQuery(form).valid()) return;
        submit.disabled = true;
        status.textContent = "Compiling and checking your solution…";
    });
    window.addEventListener("pageshow", () => {
        submit.disabled = document.getElementById("RuntimeId").options.length === 0;
        status.textContent = "";
    });
    window.addEventListener("pagehide", sync);
}
