// Incoming text is always inserted as a text node; HTML/Markdown is never interpreted.
export function element(doc, tag, className, text) {
    const node = doc.createElement(tag);
    if (className) node.className = className;
    if (text !== undefined) node.textContent = String(text);
    return node;
}

export function nearBottom(list) {
    return list.scrollHeight - list.scrollTop - list.clientHeight < 48;
}

export function shouldSend(event) {
    return event.key === "Enter" && !event.shiftKey && !event.isComposing && !event.repeat;
}

export function renderMessages(doc, list, messages, emptyLabel, formatDate) {
    const pinned = nearBottom(list);
    const oldTop = list.scrollTop;
    const seen = new Set();
    const nodes = [];
    for (const message of messages) {
        if (seen.has(message.id)) continue;
        seen.add(message.id);
        const bubble = element(doc, "article", `message-bubble${message.isMine ? " message-mine" : ""}`);
        bubble.dataset.messageId = String(message.id);
        const meta = element(doc, "div", "message-meta");
        const time = element(doc, "time", "", formatDate(message.createdAt));
        time.dateTime = message.createdAt;
        meta.append(element(doc, "strong", "", message.senderName), time);
        bubble.append(meta, element(doc, "p", "message-body", message.body));
        nodes.push(bubble);
    }
    if (!nodes.length) nodes.push(element(doc, "p", "text-muted", emptyLabel));
    list.replaceChildren(...nodes);
    list.scrollTop = pinned ? list.scrollHeight : oldTop;
}

export function showCount(doc, key, count) {
    for (const node of doc.querySelectorAll(`[data-social-count="${key}"]`)) {
        node.textContent = count > 99 ? "99+" : String(count);
        node.hidden = count === 0;
    }
}
