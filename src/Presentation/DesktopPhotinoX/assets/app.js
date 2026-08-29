function normaliseLanguageClass(className) {
    return className
        .replace("language-html", "language-markup")
        .replace("language-xml", "language-markup")
        .replace(/language-(text|plain|plaintext)/g, "language-none");
}

function parseMarkdown(text) {
    const container = document.createElement("div");
    container.className = "markdown-content";

    // The desktop shell may be offline or the CDN scripts may be blocked.
    // Never let Markdown rendering prevent an otherwise valid LLM response
    // from appearing in the conversation.
    if (typeof marked === "undefined" || typeof marked.parse !== "function") {
        container.textContent = text;
        return container;
    }

    try {
        container.innerHTML = marked.parse(text);
        container.querySelectorAll('code[class*="language-"], pre[class*="language-"]').forEach((element) => {
            element.className = normaliseLanguageClass(element.className);
        });
        if (typeof Prism !== "undefined" && typeof Prism.highlightAllUnder === "function")
            Prism.highlightAllUnder(container);
    } catch (error) {
        console.warn("Markdown rendering failed; displaying plain text.", error);
        container.textContent = text;
    }

    return container;
}

function appendElement(element) {
    const log = document.getElementById("log");
    if (!log) return;
    log.appendChild(element);
    log.scrollTop = log.scrollHeight;
}

function textMessage(className, prefix, text) {
    const div = document.createElement("div");
    div.className = className;
    div.textContent = prefix + text;
    return div;
}

function appendUserMessage(prompt) {
    const div = document.createElement("div");
    div.className = "user-msg";
    const strong = document.createElement("strong");
    strong.textContent = "> User:";
    div.appendChild(strong);
    div.appendChild(document.createTextNode(` ${prompt}`));
    appendElement(div);
}

function appendThought(text) {
    const div = document.createElement("div");
    div.className = "thought";
    div.appendChild(parseMarkdown(text));
    appendElement(div);
}

function appendToolStart(payload) {
    const div = document.createElement("div");
    div.className = "call";
    const header = document.createElement("div");
    header.className = "call-header";
    const labels = {
        write_file: "📝 Write File",
        read_file: "📖 Read File",
        list_dir: "📂 List Directory",
        search_files: "🔍 Search Files",
        sh: "💻 Shell Command",
        switch_model: "🔄 Switch Model"
    };
    const strong = document.createElement("strong");
    strong.textContent = labels[payload.tool] || `🔧 ${payload.tool}`;
    header.appendChild(strong);
    div.appendChild(header);
    const args = document.createElement("pre");
    args.className = "call-raw";
    args.textContent = payload.arguments || "{}";
    div.appendChild(args);
    appendElement(div);
}

function appendToolResult(payload) {
    const div = document.createElement("div");
    div.className = payload.success ? "result" : "danger";
    const header = document.createElement("div");
    header.className = "result-header";
    header.textContent = payload.success ? "✓ Result" : "✗ Error";
    div.appendChild(header);
    const pre = document.createElement("pre");
    pre.className = "result-content";
    pre.textContent = payload.result || "";
    div.appendChild(pre);
    appendElement(div);
}

function updateStep(payload) {
    const counter = document.getElementById("step-counter");
    if (counter && typeof payload.current === "number" && typeof payload.max === "number")
        counter.textContent = `Step ${payload.current} of ${payload.max}`;
}

function resetStep() {
    const counter = document.getElementById("step-counter");
    if (counter) counter.textContent = "Ready";
}

function handleBridgeMessage(message) {
    const payload = message.payload || {};

    try {
        switch (message.type) {
            case "info.result": {
                const label = document.getElementById("version-label");
                if (label && payload.userName) label.textContent = payload.userName;
                break;
            }
            case "session.created":
                window.currentSessionId = payload.sessionId || message.sessionId;
                if (window.pendingPrompt) {
                    const prompt = window.pendingPrompt;
                    window.pendingPrompt = null;
                    CSAgentBridge.chat(window.currentSessionId, prompt);
                }
                break;
            case "chat.accepted":
                updateStep({ current: 0, max: 30 });
                break;
            case "agent.step":
                updateStep(payload);
                break;
            case "agent.thought":
                appendThought(payload.text || "");
                break;
            case "agent.tool.start":
                appendToolStart(payload);
                break;
            case "agent.tool.result":
                appendToolResult(payload);
                break;
            case "agent.warning":
                appendElement(textMessage("warning", "⚠ ", payload.message || ""));
                break;
            case "agent.danger":
            case "agent.error":
            case "bridge.error":
                appendElement(textMessage("danger", "✗ ", payload.message || "Unknown error"));
                resetStep();
                break;
            case "agent.done":
                appendElement(textMessage("done", "✓ ", payload.message || "Task completed successfully"));
                resetStep();
                break;
            case "agent.cancelled":
                appendElement(textMessage("warning", "⚠ ", "Chat cancelled."));
                resetStep();
                break;
            case "session.closed":
                if (window.currentSessionId === message.sessionId) window.currentSessionId = null;
                resetStep();
                break;
        }
    } catch (error) {
        console.error("Failed to render bridge message", message, error);
        appendElement(textMessage("danger", "✗ UI error: ", error?.message || String(error)));
    }
}

function run() {
    const input = document.getElementById("in");
    const prompt = input.value.trim();
    if (!prompt) return;

    input.value = "";
    appendUserMessage(prompt);

    try {
        if (!window.currentSessionId) {
            window.pendingPrompt = prompt;
            CSAgentBridge.createSession();
            return;
        }

        CSAgentBridge.chat(window.currentSessionId, prompt);
    } catch (error) {
        appendElement(textMessage("danger", "✗ ", error?.message || String(error)));
    }
}

window.addEventListener("DOMContentLoaded", () => {
    try {
        CSAgentBridge.onMessage(handleBridgeMessage);
        CSAgentBridge.info();
        CSAgentBridge.createSession();
    } catch (error) {
        console.error("PhotinoX initialization failed", error);
        appendElement(textMessage("danger", "✗ ", error?.message || String(error)));
    }
});
