// CSAgent PhotinoX bridge transport.
// Application code should use bridge.send()/bridge.chat()/bridge.cancel()
// and never call window.external directly.
(function () {
    const VERSION = 1;

    function send(type, payload = {}, sessionId = null) {
        const id = crypto.randomUUID();
        const message = { v: VERSION, id, type, sessionId, payload };

        if (!window.external || typeof window.external.sendMessage !== "function") {
            throw new Error("Photino bridge unavailable.");
        }

        window.external.sendMessage(JSON.stringify(message));
        return id;
    }

    window.CSAgentBridge = {
        version: VERSION,
        send,
        info() { return send("info.get"); },
        createSession(sessionId = null) { return send("session.create", {}, sessionId); },
        closeSession(sessionId) { return send("session.close", {}, sessionId); },
        chat(sessionId, prompt) { return send("chat.start", { prompt }, sessionId); },
        cancel(sessionId) { return send("chat.cancel", {}, sessionId); },
        approve(sessionId, approvalId, approved) {
            return send("approval.respond", { approvalId, approved }, sessionId);
        },
        onMessage(handler) {
            window.external.receiveMessage = function (json) {
                try {
                    const message = JSON.parse(json);
                    if (!message || message.v !== VERSION || !message.type) {
                        console.warn("Ignoring unsupported bridge message", message);
                        return;
                    }
                    handler(message);
                } catch (error) {
                    console.error("Failed to parse bridge message", error);
                }
            };
        }
    };
})();
