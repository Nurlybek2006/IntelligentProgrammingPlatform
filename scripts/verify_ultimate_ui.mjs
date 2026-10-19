// Exercise the actual navigation and console scripts with small DOM/event doubles.
// This checks behavior, not browser layout or screenshots.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";

class Node {
    constructor() {
        this.handlers = {}; this.attrs = {}; this.dataset = {}; this.hidden = false; this.disabled = false;
        this.classes = new Set();
        this.classList = { add: key => this.classes.add(key), remove: key => this.classes.delete(key),
            toggle: (key, enabled) => enabled ? this.classes.add(key) : this.classes.delete(key) };
    }
    addEventListener(name, handler) { (this.handlers[name] ??= []).push(handler); }
    fire(name, extra = {}) {
        const event = { defaultPrevented: false, target: this, preventDefault() { this.defaultPrevented = true; }, ...extra };
        for (const handler of this.handlers[name] ?? []) handler(event);
        return event;
    }
    setAttribute(key, value) { this.attrs[key] = value; }
    getAttribute(key) { return this.attrs[key]; }
    focus() { doc.activeElement = this; }
    contains(node) { return node === this || this.items?.includes(node) || node === this.summary; }
    querySelector(selector) { return selector === "summary" ? this.summary : null; }
    querySelectorAll(selector) { return selector === ".dropdown-item" ? this.items : []; }
}
const doc = new Node();
doc.documentElement = new Node();
const toggle = new Node(), navigation = new Node();
const dropdowns = [new Node(), new Node(), new Node()];
for (const dropdown of dropdowns) { dropdown.summary = new Node(); dropdown.items = [new Node(), new Node(), new Node()]; }
doc.querySelector = selector => selector === "[data-nav-toggle]" ? toggle : null;
doc.querySelectorAll = selector => selector === ".nav-dropdown" ? dropdowns : [];
doc.getElementById = id => id === "main-navigation" ? navigation : null;
const win = new Node();
vm.runInNewContext(readFileSync("wwwroot/js/site.js", "utf8"), { document: doc, window: win });
toggle.fire("click"); assert.equal(toggle.attrs["aria-expanded"], "true"); assert(navigation.classes.has("is-open"));
navigation.fire("keydown", { key: "Escape" }); assert.equal(toggle.attrs["aria-expanded"], "false"); assert.equal(doc.activeElement, toggle);
const first = dropdowns[0]; first.open = true; first.fire("toggle"); assert.equal(dropdowns[1].open, false);
doc.activeElement = first.summary; first.fire("keydown", { key: "ArrowUp" }); assert.equal(doc.activeElement, first.items[2]);
first.fire("keydown", { key: "ArrowDown" }); assert.equal(doc.activeElement, first.items[0]);
first.fire("keydown", { key: "End" }); assert.equal(doc.activeElement, first.items[2]);
first.fire("keydown", { key: "Escape" }); assert.equal(first.open, false); assert.equal(doc.activeElement, first.summary);
first.open = true; first.fire("focusout", { relatedTarget: toggle }); assert.equal(first.open, false);
first.open = true; doc.fire("click", { target: toggle }); assert.equal(first.open, false);
assert(doc.documentElement.classes.has("has-ui"));
console.log("PASS: navigation groups, Escape focus restoration, dropdown exclusivity, arrow keys, outside click and focus exit");

const tabs = new Node(), input = new Node(), result = new Node(), run = new Node();
const inputButton = new Node(), resultButton = new Node();
inputButton.dataset.workspacePane = "input"; resultButton.dataset.workspacePane = "result";
tabs.querySelectorAll = () => [inputButton, resultButton];
result.hidden = true;
doc.querySelector = selector => selector === "[data-workspace-tabs]" ? tabs : null;
doc.getElementById = id => ({ "custom-input-panel": input, "custom-run-result": result, "run-code": run })[id];
let notify, disconnected = false;
class Observer { constructor(callback) { notify = callback; } observe() {} disconnect() { disconnected = true; } }
vm.runInNewContext(readFileSync("wwwroot/js/workspace.js", "utf8"), { document: doc, window: win, MutationObserver: Observer, queueMicrotask });
assert.equal(tabs.hidden, false); assert.equal(resultButton.disabled, true); assert.equal(input.hidden, false);
assert.equal(input.attrs.role, "tabpanel");
result.hidden = false; notify();
assert.equal(input.hidden, true); assert.equal(resultButton.attrs["aria-selected"], "true");
inputButton.fire("click"); assert.equal(input.hidden, false); assert(result.classes.has("workspace-pane-inactive"));
run.fire("click"); await Promise.resolve(); assert.equal(input.hidden, true); assert(!result.classes.has("workspace-pane-inactive"));
resultButton.fire("keydown", { key: "ArrowLeft" }); assert.equal(doc.activeElement, inputButton); assert.equal(inputButton.tabIndex, 0);
result.hidden = true; notify(); assert.equal(resultButton.disabled, true); assert.equal(input.hidden, false);
win.fire("pagehide", { persisted: true }); assert.equal(disconnected, false);
win.fire("pagehide", { persisted: false }); assert.equal(disconnected, true);
console.log("PASS: console tabs preserve input/result state, repeat Run selects output, keyboard roving focus and BFcache lifecycle");

const css = readFileSync("wwwroot/css/site.css", "utf8");
const colors = Object.fromEntries([...css.matchAll(/--([a-z-]+): (#[0-9a-f]{6});/g)].map(match => [match[1], match[2]]));
const luminance = hex => {
    const rgb = hex.slice(1).match(/../g).map(value => parseInt(value, 16) / 255).map(value => value <= .04045 ? value / 12.92 : ((value + .055) / 1.055) ** 2.4);
    return rgb[0] * .2126 + rgb[1] * .7152 + rgb[2] * .0722;
};
for (const [fg, bg] of [["text-primary", "bg-main"], ["text-secondary", "bg-surface"], ["text-muted", "bg-elevated"],
    ["accent", "accent-soft"], ["accent-ink", "accent"], ["success", "success-soft"], ["warning", "warning-soft"],
    ["danger", "danger-soft"], ["info", "info-soft"], ["orange", "orange-soft"]]) {
    const a = luminance(colors[fg]), b = luminance(colors[bg]);
    const ratio = (Math.max(a, b) + .05) / (Math.min(a, b) + .05);
    assert(ratio >= 4.5, `${fg}/${bg}: ${ratio.toFixed(2)}`);
}
console.log("PASS: ten text/status/primary-button token pairs meet WCAG AA 4.5:1; rendered visual audit remains manual");
console.log("ULTIMATE UI LOGIC CHECKS PASSED. No browser rendering claimed.");
