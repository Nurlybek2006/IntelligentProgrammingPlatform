import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import vm from "node:vm";
import { element, nearBottom, renderMessages, shouldSend, showCount } from "../ClientScripts/social-dom.js";

class Node {
    constructor() { this.children = []; this.dataset = {}; this.handlers = {}; this.hidden = false; this.disabled = false;
        this.value = ""; this.scrollTop = 0; this.scrollHeight = 100; this.clientHeight = 100; this.textContent = ""; }
    set innerHTML(_) { throw new Error("HTML sink forbidden"); }
    append(...nodes) { this.children.push(...nodes); }
    replaceChildren(...nodes) { this.children = nodes; }
    setAttribute(name, value) { this[name] = value; }
    addEventListener(name, handler) { this.handlers[name] = handler; }
    focus() { this.focused = true; }
}
const documentStub = { createElement: () => new Node() };
const list = new Node();
const hostile = '<script>alert(1)</script>\n<img onerror=attack()> **plain**';
const rows = [{ id: 1, body: hostile, senderName: '<img onerror=attack()>', isMine: false, createdAt: '2026-01-01T00:00:00Z' }];
renderMessages(documentStub, list, [...rows, ...rows], 'Empty', value => value);
assert.equal(list.children.length, 1);
assert.equal(list.children[0].children[1].textContent, hostile);
assert.equal(list.children[0].children[0].children[0].textContent, rows[0].senderName);
assert.equal(list.scrollTop, list.scrollHeight);
list.scrollHeight = 1000; list.scrollTop = 20;
renderMessages(documentStub, list, rows, 'Empty', value => value);
assert.equal(list.scrollTop, 20);
assert.equal(nearBottom(list), false);
assert(shouldSend({ key: 'Enter' }));
for (const event of [{ key: 'Enter', shiftKey: true }, { key: 'Enter', isComposing: true }, { key: 'Enter', repeat: true }, { key: 'x' }]) assert(!shouldSend(event));
const badge = new Node();
showCount({ querySelectorAll: () => [badge] }, 'total', 0); assert(badge.hidden);
showCount({ querySelectorAll: () => [badge] }, 'total', 500); assert.equal(badge.textContent, '99+'); assert(!badge.hidden);
console.log('PASS: textContent XSS protection, event deduplication, preserved scroll, IME/Shift/held Enter and bounded badge counts');

async function exercise(initialFailure) {
    const nodes = Object.fromEntries(['list', 'body', 'send', 'read', 'button', 'error', 'status', 'unavailable', 'length', 'older', 'latest', 'conversations', 'next'].map(key => [key, new Node()]));
    const values = { Body: nodes.body, ClientMessageId: { value: 'original-key' }, ConversationId: { value: '7' } };
    nodes.send.elements = { namedItem: name => values[name] };
    nodes.send.action = 'https://localhost:7115/Messages/Send';
    nodes.send.querySelector = () => nodes.button;
    nodes.send.reportValidity = () => true;
    nodes.read.action = 'https://localhost:7115/Messages/Read';
    nodes.read.elements = { namedItem: name => name === 'throughId' ? through : { value: '7' } };
    const through = { value: '0' };
    const settings = { hubUrl: '/hubs/chat', countsUrl: '/Social/Counts', listUrl: '/Messages/ListData', threadUrl: '/Messages/ThreadData',
        conversationUrl: '/Messages/Conversation', online: 'Online', offline: 'Offline', connecting: 'Connecting', error: 'Retry', empty: 'Empty' };
    const selectors = { '[data-message-list]': nodes.list, '[data-send-form]': nodes.send, '[data-read-form]': nodes.read,
        '[data-chat-error]': nodes.error, '[data-chat-unavailable]': nodes.unavailable, '[data-message-length]': nodes.length,
        '[data-older-messages]': nodes.older, '[data-latest-messages]': nodes.latest, '[data-conversation-list]': nodes.conversations,
        '[data-conversations-next]': nodes.next };
    const chat = { dataset: { conversationId: '7', olderPage: 'false', listPage: '1' }, querySelector: key => selectors[key] };
    const doc = { createElement: () => new Node(), visibilityState: 'hidden', documentElement: { lang: 'en' }, handlers: {},
        querySelector: key => key === '[data-social-config]' ? { dataset: settings } : key === '[data-chat]' ? chat : key === '[data-connection-status]' ? nodes.status : null,
        querySelectorAll: () => [], addEventListener(name, handler) { this.handlers[name] = handler; } };
    let events = {}, reconnect, starts = 0, stopped = false, connectedUrl, retryCallback, delaySend, failSend = true, canSend = true;
    const sent = [], read = [], lifecycle = {};
    const connection = { on(name, handler) { events[name] = handler; }, onreconnecting() {}, onreconnected(handler) { reconnect = handler; }, onclose() {},
        async start() { starts++; if (initialFailure && starts === 1) throw new Error('offline'); }, async stop() { stopped = true; } };
    class Builder {
        withUrl(url) { connectedUrl = url; return this; } configureLogging() { return this; }
        withAutomaticReconnect(delays) { assert.equal(delays.length, 4); return this; } build() { return connection; }
    }
    class FormDataStub {
        constructor(form) { this.form = form; this.body = values.Body.value; this.key = values.ClientMessageId.value; this.through = through.value; }
    }
    const context = { document: doc, window: { addEventListener(name, handler) { lifecycle[name] = handler; } }, location: { origin: 'https://localhost:7115' },
        Intl, URL, crypto: { randomUUID: () => 'new-key' }, HubConnectionBuilder: Builder, LogLevel: { None: 6 }, FormData: FormDataStub,
        element, nearBottom, renderMessages, shouldSend, showCount, clearTimeout() {}, setTimeout(callback) { retryCallback = callback; return 1; },
        async fetch(url, options) {
            assert.equal(options.credentials, 'same-origin'); assert.equal(options.cache, 'no-store');
            let data;
            if (String(url).includes('/Send')) {
                sent.push(options.body);
                if (delaySend) await delaySend;
                if (failSend) { failSend = false; throw new Error('offline'); }
                data = { id: 1, conversationId: 7 };
            } else if (String(url).includes('/Read')) { read.push(options.body.through); data = { success: true }; }
            else if (String(url).includes('ThreadData')) data = { canSend, hasOlder: false, messages: rows };
            else if (String(url).includes('ListData')) data = { rows: [], hasNextPage: false };
            else data = { unreadMessages: 1, incomingRequests: 2 };
            return { ok: true, headers: { get: () => 'application/json' }, json: async () => data };
        } };
    const source = (await readFile('ClientScripts/social.js', 'utf8')).replace(/^import .*;\r?\n/gm, '');
    vm.runInNewContext(source, context);
    const tick = () => new Promise(resolve => setImmediate(resolve));
    await tick();
    assert.equal(connectedUrl, '/hubs/chat');
    assert.equal(read.length, 0, 'Hidden document never marks messages read');
    if (initialFailure) { assert.equal(nodes.status.textContent, 'Offline'); assert(retryCallback); }
    nodes.body.value = hostile;
    await nodes.send.handlers.submit({ preventDefault() {} });
    assert.equal(nodes.body.value, hostile); assert.equal(values.ClientMessageId.value, 'original-key'); assert(!nodes.button.disabled);
    assert.equal(nodes.error.textContent, settings.error, 'Network failures use the localized safe message');
    let release;
    delaySend = new Promise(resolve => release = resolve);
    const pending = nodes.send.handlers.submit({ preventDefault() {} });
    assert(nodes.button.disabled && nodes.body.disabled);
    await nodes.send.handlers.submit({ preventDefault() {} });
    assert.equal(sent.length, 2, 'Busy form does not issue a duplicate POST');
    release(); await pending;
    assert.equal(sent[0].key, sent[1].key, 'Uncertain delivery retries use original idempotency key');
    assert.equal(nodes.body.value, ''); assert.equal(values.ClientMessageId.value, 'new-key');
    doc.visibilityState = 'visible'; doc.handlers.visibilitychange(); await tick();
    assert(read.length >= 1);
    canSend = false; events.SocialStateChanged(); await tick();
    assert(nodes.send.hidden && nodes.body.disabled && !nodes.unavailable.hidden);
    if (initialFailure) { retryCallback(); await tick(); assert.equal(starts, 2); }
    reconnect(); await tick(); assert.equal(nodes.status.textContent, 'Online');
    assert(!stopped);
    const priorStarts = starts;
    lifecycle.pagehide(); assert(stopped);
    lifecycle.pageshow({ persisted: true }); await tick();
    assert.equal(starts, priorStarts + 1, 'Back-forward cache restoration reconnects and resynchronizes');
}
await exercise(false);
await exercise(true);
console.log('PASS: actual client handlers preserve failed drafts/keys, prevent busy duplicate POSTs, work with hub offline, retry initial connect, resync and stop read on hidden tabs');
console.log('SOCIAL UI LOGIC CHECKS PASSED. Browser layout not claimed.');
