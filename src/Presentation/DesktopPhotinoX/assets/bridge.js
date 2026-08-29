// CSAgent PhotinoX bridge transport.
// Application code should use CSAgentBridge and never call window.external directly.
(function () {
    const VERSION = 1;
    let sequence = 0;

    // crypto.randomUUID() is not guaranteed to exist in an embedded WebView
    // loaded from raw StartString content. Keep the bridge usable on all
    // PhotinoX platforms and WebView security contexts.
    function createId() {
        sequence += 1;
        if (window.crypto && typeof window.crypto.randomUUID === "function") {
            try {
                return window.crypto.randomUUID();
            } catch (_) {
                // Fall through to the portable implementation.
            }
        }

        return `req-${Date.now().toString(36)}-${sequence.toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
    }

    function send(type, payload = {}, sessionId = null) {
        const id = createId();
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
            if (!window.external) {
                throw new Error("Photino external bridge unavailable.");
            }

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
