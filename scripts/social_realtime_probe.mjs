// Loopback-only real SignalR JSON protocol probe. Cookies arrive on stdin and are never logged.
import WebSocket from "ws";
import readline from "node:readline";
const sockets = [];
const events = [];
const heartbeat = setInterval(() => sockets.forEach(socket => {
    if (socket.readyState === WebSocket.OPEN) socket.send('{"type":6}\x1e');
}), 10000);
heartbeat.unref();
const reply = value => process.stdout.write(JSON.stringify(value) + "\n");
async function connect(cookie) {
    return await new Promise((resolve, reject) => {
        const socket = new WebSocket("wss://localhost:7115/hubs/chat", {
            rejectUnauthorized: false, headers: { Cookie: cookie, Origin: "https://localhost:7115" }
        });
        const received = [];
        events.push(received);
        let buffer = "";
        const timer = setTimeout(() => reject(new Error("Handshake timeout")), 10000);
        socket.on("error", reject);
        socket.on("open", () => socket.send(JSON.stringify({ protocol: "json", version: 1 }) + "\x1e"));
        socket.on("message", data => {
            buffer += data.toString();
            let end;
            while ((end = buffer.indexOf("\x1e")) >= 0) {
                const frame = JSON.parse(buffer.slice(0, end)); buffer = buffer.slice(end + 1);
                if (frame.type === undefined && !frame.error) { clearTimeout(timer); resolve(socket); }
                else if (frame.type === 1 || frame.type === 3) received.push(frame);
            }
        });
    });
}
async function waitUntil(predicate) {
    const deadline = Date.now() + 10000;
    while (!predicate()) { if (Date.now() > deadline) throw new Error("Event timeout"); await new Promise(resolve => setTimeout(resolve, 25)); }
}
for await (const line of readline.createInterface({ input: process.stdin })) {
    try {
        const command = JSON.parse(line);
        if (command.op === "connect") {
            for (const cookie of command.cookies) sockets.push(await connect(cookie));
            reply({ connected: sockets.length });
        } else if (command.op === "wait") {
            await waitUntil(() => events[command.client].filter(item => item.target === command.event).length >= command.count);
            reply({ events });
        } else if (command.op === "invoke") {
            sockets[command.client].send(JSON.stringify({ type: 1, invocationId: command.id, target: command.method, arguments: command.args }) + "\x1e");
            await waitUntil(() => events[command.client].some(item => item.invocationId === command.id));
            reply({ completion: events[command.client].find(item => item.invocationId === command.id) });
        } else if (command.op === "snapshot") reply({ events });
        else if (command.op === "close") {
            sockets.forEach(socket => socket.terminate());
            process.stdout.write(JSON.stringify({ closed: true }) + "\n", () => process.exit(0));
            break;
        }
    } catch { reply({ error: "SignalR test failed" }); process.exitCode = 1; break; }
}
sockets.forEach(socket => socket.terminate());
clearInterval(heartbeat);
