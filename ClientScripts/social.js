import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { element, nearBottom, renderMessages, shouldSend, showCount } from "./social-dom.js";

const config = document.querySelector("[data-social-config]")?.dataset;
if (config) initialize(config);

function initialize(settings) {
    const chat = document.querySelector("[data-chat]");
    const conversationId = Number(chat?.dataset.conversationId || 0);
    const olderPage = chat?.dataset.olderPage === "true";
    const list = chat?.querySelector("[data-message-list]");
    const sendForm = chat?.querySelector("[data-send-form]");
    const readForm = chat?.querySelector("[data-read-form]");
    const body = sendForm?.elements.namedItem("Body");
    const sendButton = sendForm?.querySelector("button[type=submit]");
    const error = chat?.querySelector("[data-chat-error]");
    const status = document.querySelector("[data-connection-status]");
    const dateFormat = new Intl.DateTimeFormat(document.documentElement.lang, { dateStyle: "short", timeStyle: "short" });
    const formatDate = value => dateFormat.format(new Date(value));
    let canSend = !!body && !body.disabled;
    let sending = false;
    let stopped = false;
    let retryTimer;
    let refreshing = false;
    let pendingRefresh = false;
    let readBusy = false;
    let readThrough = 0;
    let pendingBody = null;

    const endpoint = (base, params) => {
        const url = new URL(base, location.origin);
        for (const [key, value] of Object.entries(params)) url.searchParams.set(key, String(value));
        return url.href;
    };
    async function json(url, options = {}) {
        let response;
        try {
            response = await fetch(url, { credentials: "same-origin", cache: "no-store", ...options,
                headers: { Accept: "application/json", ...options.headers } });
        } catch { throw new Error(settings.error); }
        const isJson = response.headers.get("Content-Type")?.includes("application/json");
        let data = null;
        try { if (isJson) data = await response.json(); }
        catch { throw new Error(settings.error); }
        if (!response.ok || response.redirected || !isJson) throw new Error(data?.error || settings.error);
        return data;
    }
    function displayError(message) {
        if (error) { error.textContent = message; error.hidden = !message; }
    }
    function updateComposer() {
        if (!sendForm) return;
        sendForm.hidden = !canSend;
        body.disabled = !canSend || sending;
        sendButton.disabled = !canSend || sending;
        chat.querySelector("[data-chat-unavailable]").hidden = canSend;
    }
    async function markRead() {
        const through = Number(readForm?.elements.namedItem("throughId").value || 0);
        if (!readForm || document.visibilityState !== "visible" || !nearBottom(list) || readBusy || through <= readThrough) return;
        readBusy = true;
        try {
            await json(readForm.action, { method: "POST", body: new FormData(readForm) });
            readThrough = through;
            await refreshCounts();
        } catch { /* The protected read button remains available for a manual retry. */ }
        finally { readBusy = false; }
    }
    async function refreshCounts() {
        const counts = await json(settings.countsUrl);
        showCount(document, "incomingRequests", counts.incomingRequests);
        showCount(document, "unreadMessages", counts.unreadMessages);
        showCount(document, "total", counts.incomingRequests + counts.unreadMessages);
    }
    function renderConversations(data) {
        const container = chat?.querySelector("[data-conversation-list]");
        if (!container) return;
        const nodes = data.rows.map(row => {
            const link = element(document, "a", "conversation-link");
            link.href = endpoint(settings.conversationUrl, { id: row.id });
            if (row.id === conversationId) link.setAttribute("aria-current", "page");
            const avatar = element(document, "span", "user-avatar");
            avatar.dataset.initials = row.avatar.initials;
            avatar.setAttribute("aria-hidden", "true");
            if (/^\/uploads\/avatars\/[a-f0-9]{32}\.webp$/.test(row.avatar.safePath || "")) {
                const img = element(document, "img", "");
                img.src = row.avatar.safePath; img.alt = ""; img.width = 40; img.height = 40;
                img.addEventListener("error", () => img.remove(), { once: true });
                avatar.append(img);
            }
            const summary = element(document, "span", "conversation-summary");
            summary.append(element(document, "strong", "", row.displayName), element(document, "span", "", row.preview || settings.empty));
            if (row.lastMessageAt) {
                const time = element(document, "time", "", formatDate(row.lastMessageAt));
                time.dateTime = row.lastMessageAt; summary.append(time);
            }
            link.append(avatar, summary);
            if (row.unreadCount > 0) link.append(element(document, "span", "badge bg-primary", row.unreadCount));
            return link;
        });
        if (!nodes.length) nodes.push(element(document, "p", "text-muted", settings.noConversations));
        container.replaceChildren(...nodes);
        chat.querySelector("[data-conversations-next]").hidden = !data.hasNextPage;
    }
    async function refreshThread() {
        if (!conversationId) return;
        const data = await json(endpoint(settings.threadUrl, { id: conversationId }));
        canSend = data.canSend;
        updateComposer();
        if (olderPage) { chat.querySelector("[data-latest-messages]").hidden = false; return; }
        renderMessages(document, list, data.messages, settings.empty, formatDate);
        const older = chat.querySelector("[data-older-messages]");
        older.hidden = !data.hasOlder;
        if (data.messages.length) {
            older.href = endpoint(settings.conversationUrl, { id: conversationId, beforeId: data.messages[0].id });
            readForm.elements.namedItem("throughId").value = String(data.messages.at(-1).id);
        }
        await markRead();
    }
    // Events are hints: every refresh rechecks ownership in authenticated MVC queries.
    async function refresh() {
        pendingRefresh = true;
        if (refreshing || stopped) return;
        refreshing = true;
        try {
            while (pendingRefresh && !stopped) {
                pendingRefresh = false;
                try {
                    await refreshThread();
                    if (chat) renderConversations(await json(endpoint(settings.listUrl, { page: chat.dataset.listPage || 1 })));
                    await refreshCounts();
                } catch { if (status) status.textContent = settings.offline; }
            }
        } finally { refreshing = false; }
    }
    sendForm?.addEventListener("submit", async event => {
        event.preventDefault();
        if (sending || !canSend || !sendForm.reportValidity() || !body.value.trim()) return;
        // Preserve the key for an uncertain retry; changed text is a new intended message.
        if (pendingBody !== null && pendingBody !== body.value) sendForm.elements.namedItem("ClientMessageId").value = crypto.randomUUID();
        pendingBody = body.value;
        const payload = new FormData(sendForm);
        sending = true; displayError(""); updateComposer();
        try {
            await json(sendForm.action, { method: "POST", body: payload });
            body.value = ""; pendingBody = null;
            sendForm.elements.namedItem("ClientMessageId").value = crypto.randomUUID();
            chat.querySelector("[data-message-length]").textContent = "0 / 2000";
            if (list) list.scrollTop = list.scrollHeight;
            await refresh();
        } catch (failure) { displayError(failure.message); }
        finally { sending = false; updateComposer(); if (canSend) body.focus(); }
    });
    body?.addEventListener("keydown", event => {
        if (shouldSend(event)) { event.preventDefault(); if (!sending) sendForm.requestSubmit(); }
    });
    body?.addEventListener("input", () => { chat.querySelector("[data-message-length]").textContent = `${body.value.length} / 2000`; });
    list?.addEventListener("scroll", () => void markRead(), { passive: true });
    document.addEventListener("visibilitychange", () => { if (document.visibilityState === "visible") void refresh(); });
    readForm?.addEventListener("submit", event => { event.preventDefault(); readThrough = 0; void markRead(); });
    if (list) list.scrollTop = list.scrollHeight;

    const connection = new HubConnectionBuilder().withUrl(settings.hubUrl).configureLogging(LogLevel.None)
        .withAutomaticReconnect([0, 2000, 10000, 30000]).build();
    for (const name of ["NewMessage", "FriendRequestReceived", "FriendRequestAccepted", "SocialStateChanged", "UnreadCountChanged"])
        connection.on(name, () => void refresh());
    connection.onreconnecting(() => { if (status) status.textContent = settings.connecting; });
    connection.onreconnected(() => { if (status) status.textContent = settings.online; void refresh(); });
    connection.onclose(() => { if (status) status.textContent = settings.offline; scheduleStart(); });
    function scheduleStart() {
        if (!stopped) { clearTimeout(retryTimer); retryTimer = setTimeout(() => void start(), 30000); }
    }
    async function start() {
        if (stopped) return;
        try { await connection.start(); if (status) status.textContent = settings.online; await refresh(); }
        catch { if (status) status.textContent = settings.offline; scheduleStart(); }
    }
    function stop() { stopped = true; clearTimeout(retryTimer); void connection.stop(); }
    window.addEventListener("pagehide", stop);
    window.addEventListener("pageshow", event => {
        if (event.persisted) { stopped = false; void start(); }
    });
    document.querySelector('form[action$="/Account/Logout"]')?.addEventListener("submit", stop);
    if (status) status.textContent = settings.connecting;
    void markRead();
    void start();
}
