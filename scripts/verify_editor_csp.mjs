// Technical checks only: DOM/Monaco doubles cannot prove browser CSP or layout.
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import vm from "node:vm";
import { addStyleNonce } from "./monaco-csp.mjs";

const factoryPaths = ["domStylesheets.js", "ui/contextview/contextview.js"];
for (const path of factoryPaths) {
    const source = await readFile("node_modules/monaco-editor/esm/vs/base/browser/" + path, "utf8");
    assert.match(addStyleNonce(source), /style\.nonce = document\.querySelector/);
}
assert.throws(() => addStyleNonce("changed upstream factory"));
assert.throws(() => addStyleNonce("const style = document.createElement('style');".repeat(2)));
let inserted;
vm.runInNewContext(addStyleNonce("const style = document.createElement('style');\ncontainer.appendChild(style); style.textContent = '.token { color: red }';"), {
    document: { createElement: () => ({}), querySelector: () => ({ content: "test-request-nonce" }) },
    container: { appendChild: style => { assert.equal(style.nonce, "test-request-nonce"); inserted = style; } }
});
assert.match(inserted.textContent, /token/);
const bundle = await readFile("wwwroot/js/editor/code-editor.js", "utf8");
assert.equal(bundle.split('meta[name="csp-nonce"]').length - 1, 2, "Both bundled factories carry nonce before insertion");
console.log("PASS: Both guarded Monaco stylesheet factories and compiled bundle use the request nonce");

const source = (await readFile("ClientScripts/code-editor.js", "utf8"))
    .replace(/^import .*;\r?\n/gm, "")
    .replaceAll("import.meta.url", '"https://localhost:7115/js/editor/code-editor.js"');

function exercise(validationFailed, draft, throwsStorage = false) {
    const handlers = {}, windowHandlers = {}, storage = new Map([["user-task", draft]]);
    const elements = {
        "submission-form": { dataset: { draftKey: "user-task", validationFailed }, addEventListener: (name, handler) => handlers[name] = handler },
        SourceCode: { value: "server validation source" }, "code-editor": { hidden: true },
        "submission-progress": {}, "submit-code": {}, RuntimeId: { options: [1] }
    };
    let value, changed, focused = false, worker;
    const context = {
        document: { getElementById: id => elements[id] }, TextEncoder, URL,
        window: { addEventListener: (name, handler) => windowHandlers[name] = handler },
        Worker: class { constructor(url, options) { worker = { url, options }; } },
        sessionStorage: {
            getItem: key => { if (throwsStorage) throw Error("disabled"); return storage.get(key); },
            setItem: (key, item) => { if (throwsStorage) throw Error("disabled"); storage.set(key, item); }
        },
        monaco: { editor: { create: (element, options) => {
            assert.equal(options.language, "cpp"); assert.equal(options.automaticLayout, true);
            value = options.value;
            return { getValue: () => value, onDidChangeModelContent: handler => changed = handler, focus: () => focused = true };
        } } }
    };
    vm.runInNewContext(source, context);
    assert.equal(value, validationFailed === "true" || throwsStorage ? "server validation source" : draft);
    assert.equal(elements.SourceCode.hidden, true);
    assert.equal(elements["code-editor"].hidden, false);
    context.MonacoEnvironment.getWorker();
    assert.equal(worker.url.href, "https://localhost:7115/js/editor/editor.worker.js");
    assert.equal(worker.options.type, "module");
    value = 'int main() { return 0; }'; changed();
    assert.equal(elements.SourceCode.value, value);
    if (!throwsStorage) assert.equal(storage.get("user-task"), value);
    let prevented = false;
    handlers.submit({ preventDefault: () => prevented = true });
    assert.equal(prevented, false); assert.equal(elements["submit-code"].disabled, true);
    windowHandlers.pageshow(); assert.equal(elements["submit-code"].disabled, false);
    value = "x".repeat(65537); handlers.submit({ preventDefault: () => prevented = true });
    assert.equal(prevented, true); assert.equal(focused, true);
    value = "draft on page exit"; windowHandlers.pagehide();
    assert.equal(elements.SourceCode.value, value);
}
exercise("false", "saved draft");
exercise("true", "older draft");
exercise("false", "ignored", true);
console.log("PASS: Editor integration doubles verify textarea sync, submit, draft/validation precedence, size bounds and same-origin module worker");
console.log("BROWSER CHECKS STILL REQUIRED: rendering, highlighting, real input, worker startup and CSP console");
