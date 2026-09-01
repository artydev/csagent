// =============================================================================
// CSAgent Console — Frontend Application (Dracula multiplexer edition)
// =============================================================================
// Talks only to the local CSAgentUI.LeanUI host:
//   GET  /api/chat?prompt=...   (Server-Sent Events stream)
// The backend owns the model + MCP tool loop entirely. This script never
// contacts Ollama or an MCP server directly — it just renders whatever
// typed events the agent streams back, and keeps a local, per-tab log of
// sessions for the terminal-style UI (each session is a local view; the
// agent's own memory/history lives server-side via --memory-file).
// =============================================================================

import { get, set, del } from 'https://cdn.jsdelivr.net/npm/idb-keyval@6/+esm';
import { marked } from 'https://cdn.jsdelivr.net/npm/marked@12/+esm';

/* ──────────────────────────────────────────────────────────────
   DOM REFS
────────────────────────────────────────────────────────────── */
const log            = document.getElementById("log");
const input          = document.getElementById("input");
const sessionBar     = document.getElementById("sessionBar");
const terminalPrompt = document.getElementById("terminalPrompt");
const connDot        = document.getElementById("connDot");
const connStatusEl   = document.getElementById("connStatus");
const stepCounterEl  = document.getElementById("stepCounter");
const toolCountEl    = document.getElementById("toolCount");
const msgCountEl     = document.getElementById("msgCount");
const stopBtn        = document.getElementById("stopBtn");

/* ──────────────────────────────────────────────────────────────
   CONSTANTS
────────────────────────────────────────────────────────────── */
// Same-origin SSE chat endpoint exposed by CsAgentUI.Endpoints via
// app.MapEndpoints(...) in LeanUIHost.Run(). Adjust here if the route differs.
const CHAT_ENDPOINT  = "/api/chat";

const REGISTRY_KEY   = "csagent_registry_config";
const SESSION_PREFIX = "csagent_session_";

// Friendly labels for known tools (falls back to "🔧 <name>" otherwise).
const TOOL_LABELS = {
    write_file: "📝 Write File",
    read_file: "📖 Read File",
    list_dir: "📂 List Directory",
    search_files: "🔍 Search Files",
    sh: "💻 Shell Command",
    switch_model: "🔄 Switch Model"
};

/* ──────────────────────────────────────────────────────────────
   STATE
────────────────────────────────────────────────────────────── */
let registry = { currentActiveId: null, list: [] };

// Persisted, replayable record of the active session:
//   { kind: "user", content }
//   { kind: "event", type, data }   (mirrors the SSE {type,data} payloads;
//                                     "step" events are transient and skipped)
let sessionHistory = [];

// Tracks the tool-block DOM currently awaiting its matching "result" event,
// so a `call` + its later `result` render as one collapsible block instead
// of two separate log lines.
let pendingToolBlock = null;

// ── Command History ──────────────────────────────────────────────
const CMD_HISTORY_MAX = 50;
let cmdHistory   = [];
let historyIndex = -1;
let historyDraft = "";

// ── Active SSE connection ───────────────────────────────────────
let currentStream = null;

function setGenerating(on) {
    if (on) {
        stopBtn.classList.add("visible");
        input.disabled = true;
        input.placeholder = "generating… (Esc to stop)";
        setConnStatus("streaming");
    } else {
        stopBtn.classList.remove("visible");
        input.disabled = false;
        input.placeholder = "enter a prompt or /help…";
        input.focus();
        setConnStatus("ready");
    }
}

/* ──────────────────────────────────────────────────────────────
   SECTION 1 — Markdown & Syntax Highlighting
────────────────────────────────────────────────────────────── */

function normaliseLanguageClass(className) {
    let result = className;
    result = result.replace("language-html", "language-markup");
    result = result.replace("language-xml", "language-markup");
    result = result.replace(/language-(text|plain|plaintext)/g, "language-none");
    return result;
}

function ensurePrismAliases() {
    if (typeof Prism === "undefined") return;
    if (Prism.languages.markup && !Prism.languages.html) Prism.languages.html = Prism.languages.markup;
    if (Prism.languages.markup && !Prism.languages.xml) Prism.languages.xml = Prism.languages.markup;
}

function fixCodeLanguageClasses(container) {
    const selector = 'code[class*="language-"], pre[class*="language-"]';
    container.querySelectorAll(selector).forEach((element) => {
        element.className = normaliseLanguageClass(element.className);
    });
}

function parseMarkdown(text) {
    const container = document.createElement("div");
    container.className = "markdown-content";
    container.innerHTML = marked.parse(text);

    ensurePrismAliases();
    fixCodeLanguageClasses(container);
    if (typeof Prism !== "undefined") Prism.highlightAllUnder(container);

    return container;
}

/* ──────────────────────────────────────────────────────────────
   SECTION 2 — Message Rendering (SSE event → DOM)
────────────────────────────────────────────────────────────── */

function createDoneMessage() {
    const div = document.createElement("div");
    div.className = "line sys done-line";
    div.textContent = "✓ Task completed successfully";
    return div;
}

function createWarningMessage(text) {
    const div = document.createElement("div");
    div.className = "line sys warn-line";
    div.textContent = "⚠ " + text;
    return div;
}

function createDangerMessage(text) {
    const div = document.createElement("div");
    div.className = "line err";
    div.textContent = "✗ " + text;
    return div;
}

/**
 * Build (but do not insert) a collapsible tool-call block, initially in a
 * "running…" state. The caller keeps a reference so the matching "result"
 * event can fill it in later.
 *
 * @param {string} name    — tool name, e.g. "write_file"
 * @param {string} argsJson — JSON string of the tool arguments
 */
function createToolCallBlock(name, argsJson) {
    const root = document.createElement("div");
    root.className = "tool-block";

    const header = document.createElement("div");
    header.className = "tool-block-header";
    header.innerHTML = `
        <span class="tool-chevron">▶</span>
        <span class="tool-block-label">${TOOL_LABELS[name] || "🔧 " + name}</span>
        <span class="tool-block-badge">running…</span>`;
    root.appendChild(header);
    header.addEventListener("click", () => root.classList.toggle("open"));

    const body = document.createElement("div");
    body.className = "tool-block-body";

    const argsSection = document.createElement("div");
    argsSection.className = "tool-block-section";
    argsSection.innerHTML = `<div class="tool-block-section-label">arguments</div>`;
    const argsCode = document.createElement("div");
    argsCode.className = "tool-block-code";
    try {
        argsCode.textContent = JSON.stringify(JSON.parse(argsJson), null, 2);
    } catch {
        argsCode.textContent = argsJson ?? "";
    }
    argsSection.appendChild(argsCode);
    body.appendChild(argsSection);

    const resultSection = document.createElement("div");
    resultSection.className = "tool-block-section";
    resultSection.innerHTML = `<div class="tool-block-section-label">result</div>`;
    const resultCode = document.createElement("div");
    resultCode.className = "tool-block-code result-text";
    resultCode.textContent = "waiting for result…";
    resultSection.appendChild(resultCode);
    body.appendChild(resultSection);

    root.appendChild(body);

    return { root, header, badge: header.querySelector(".tool-block-badge"), resultCode };
}

/**
 * Apply a "result" event {r, e} to the pending tool block, or — if there
 * is no pending call (e.g. an orphaned result on replay) — render it as a
 * standalone line.
 */
function applyToolResult(resultText, isError, targetLog) {
    if (pendingToolBlock) {
        pendingToolBlock.resultCode.textContent = resultText ?? "";
        pendingToolBlock.badge.textContent = isError ? "error" : "done";
        if (isError) pendingToolBlock.root.classList.add("tool-error", "open");
        pendingToolBlock = null;
        return;
    }
    const div = document.createElement("div");
    div.className = isError ? "line err" : "line sys";
    div.textContent = (isError ? "✗ " : "✓ ") + (resultText ?? "");
    targetLog.appendChild(div);
}

function createGenericMessage(type, content) {
    const div = document.createElement("div");
    div.className = type;

    if (type === "thought") {
        div.appendChild(parseMarkdown(content));
    } else {
        div.className = "line sys";
        div.textContent = `[${type}] ${content}`;
    }

    return div;
}

/**
 * Route an incoming event ({type, data}) to the correct renderer and
 * append it to the log. Handles "call"/"result" pairing specially since
 * they need to merge into one collapsible block rather than two lines.
 */
function appendMessageToLog(message, targetLog) {
    switch (message.type) {
        case "done":
            targetLog.appendChild(createDoneMessage());
            return;
        case "warning":
            targetLog.appendChild(createWarningMessage(
                typeof message.data === "string" ? message.data : JSON.stringify(message.data)
            ));
            return;
        case "danger":
        case "error":
            targetLog.appendChild(createDangerMessage(
                typeof message.data === "string" ? message.data : JSON.stringify(message.data)
            ));
            return;
        case "call": {
            const { n: name, a: argsJson } = message.data || {};
            const block = createToolCallBlock(name, argsJson);
            targetLog.appendChild(block.root);
            pendingToolBlock = block;
            return;
        }
        case "result": {
            const { r: resultText, e: isError } = message.data || {};
            applyToolResult(resultText, isError, targetLog);
            return;
        }
        default:
            targetLog.appendChild(createGenericMessage(
                message.type,
                typeof message.data === "string" ? message.data : JSON.stringify(message.data)
            ));
            return;
    }
}

function scrollToBottom(targetLog) {
    targetLog.scrollTop = targetLog.scrollHeight;
}

/* ──────────────────────────────────────────────────────────────
   SECTION 3 — Step Counter
────────────────────────────────────────────────────────────── */

function updateStepCounter(data) {
    if (!stepCounterEl) return;
    if (data && typeof data.n === "number" && typeof data.m === "number") {
        stepCounterEl.textContent = `step ${data.n}/${data.m}`;
    }
}

function resetStepCounter() {
    if (stepCounterEl) stepCounterEl.textContent = "ready";
}

/* ──────────────────────────────────────────────────────────────
   SECTION 4 — Terminal print helper (client-side / system lines)
────────────────────────────────────────────────────────────── */

function print(text, cls = "sys") {
    const div = document.createElement("div");
    div.className = "line " + cls;
    div.textContent = text;
    log.appendChild(div);
    scrollToBottom(log);
    return div;
}

function printBanner() {
    const lines = [
        "╔══════════════════════════════════════════╗",
        "║   🤖  CSAGENT CONSOLE  ·  DRACULA        ║",
        "║       LeanUI  ·  SSE agent terminal      ║",
        "╚══════════════════════════════════════════╝",
    ];
    lines.forEach(l => print(l, "banner-line"));
    const hr = document.createElement("hr");
    hr.className = "log-divider";
    log.appendChild(hr);
}

function appendUserMessage(promptText, targetLog, sessionLabel) {
    return print(`user@agent:[${sessionLabel}]~$ ${promptText}`, "user");
}

function updateStatusBar() {
    const userMsgs = sessionHistory.filter(e => e.kind === "user").length;
    const toolCalls = sessionHistory.filter(e => e.kind === "event" && e.type === "call").length;
    msgCountEl.textContent = `${userMsgs} msg${userMsgs !== 1 ? 's' : ''}`;
    toolCountEl.textContent = `${toolCalls} tool${toolCalls !== 1 ? 's' : ''}`;
}

function setConnStatus(state) {
    // state: "ready" | "streaming" | "error"
    connDot.className = "status-dot " + (state === "error" ? "offline" : "online");
    connStatusEl.className = state === "error" ? "offline" : "";
    connStatusEl.style.color = state === "error" ? "var(--red)" : "var(--green)";
    connStatusEl.textContent =
        state === "error" ? "disconnected" :
        state === "streaming" ? "streaming" : "ready";
}

/* ──────────────────────────────────────────────────────────────
   SECTION 5 — Persistence (IndexedDB via idb-keyval)
────────────────────────────────────────────────────────────── */

function persistEntry(entry) {
    sessionHistory.push(entry);
}

async function saveAllToBrowser() {
    try {
        await set(REGISTRY_KEY, registry);
        if (registry.currentActiveId) await set(SESSION_PREFIX + registry.currentActiveId, sessionHistory);
    } catch (err) { console.error("Storage write error:", err); }
}

async function loadSessionData(sessionId) {
    try {
        const saved = await get(SESSION_PREFIX + sessionId);
        sessionHistory = saved || [];
        renderTerminalScreen();
        renderTopMultiplexerBar();
    } catch (err) { print("DB error: " + err.message, "err"); }
}

/* ──────────────────────────────────────────────────────────────
   SECTION 6 — Session / Multiplexer UI
────────────────────────────────────────────────────────────── */

function renderTopMultiplexerBar() {
    sessionBar.innerHTML = "";

    registry.list.forEach((session, index) => {
        const wrapper = document.createElement("div");
        wrapper.className = "session-wrapper";

        const tab = document.createElement("span");
        const isActive = session.id === registry.currentActiveId;
        tab.className = `session-tab ${isActive ? 'active' : ''}`;
        tab.textContent = `${index}: ${session.name}`;

        tab.addEventListener("click", () => { if (!isActive) switchSession(index); });

        tab.addEventListener("dblclick", (e) => {
            e.stopPropagation();
            const editorInput = document.createElement("input");
            editorInput.type = "text";
            editorInput.className = "rename-input";
            editorInput.value = session.name;

            const finishRename = async () => {
                const fresh = editorInput.value.trim();
                if (fresh && fresh !== session.name) {
                    session.name = fresh;
                    await saveAllToBrowser();
                    renderTerminalScreen();
                }
                renderTopMultiplexerBar();
            };

            editorInput.addEventListener("keydown", (ke) => {
                if (ke.key === "Enter") finishRename();
                if (ke.key === "Escape") renderTopMultiplexerBar();
            });
            editorInput.addEventListener("blur", finishRename);
            wrapper.replaceChild(editorInput, tab);
            editorInput.focus(); editorInput.select();
        });

        wrapper.appendChild(tab);

        const delBtn = document.createElement("span");
        delBtn.className = "delete-btn";
        delBtn.textContent = "✕";
        delBtn.title = "Delete session";
        delBtn.addEventListener("click", (e) => { e.stopPropagation(); handleDeleteSessionConfirmation(index); });
        wrapper.appendChild(delBtn);
        sessionBar.appendChild(wrapper);
    });

    const actions = document.createElement("div");
    actions.className = "bar-actions-group";

    const newBtn = document.createElement("span");
    newBtn.className = "bar-btn new-session-btn";
    newBtn.textContent = "+ New";
    newBtn.title = "Create new session";
    newBtn.addEventListener("click", () => handleCreateSession());
    actions.appendChild(newBtn);

    const wipeBtn = document.createElement("span");
    wipeBtn.className = "bar-btn wipe-workspace-btn";
    wipeBtn.textContent = "Wipe All";
    wipeBtn.title = "Erase all sessions";
    wipeBtn.addEventListener("click", () => handleWipeAllSessionsConfirmation());
    actions.appendChild(wipeBtn);

    sessionBar.appendChild(actions);
    updateStatusBar();
}

function renderTerminalScreen() {
    log.innerHTML = "";
    pendingToolBlock = null;

    const cur = registry.list.find(s => s.id === registry.currentActiveId);
    const name = cur ? cur.name : "none";
    terminalPrompt.textContent = `user@agent:[${name}]~$`;

    if (!cur) {
        printBanner();
        print("No active session. Click '+ New' or type /new to begin.", "sys");
        return;
    }

    if (sessionHistory.length === 0) {
        printBanner();
        print(`Session [${name}] ready.`, "sys");
        print("Type /help for available commands.", "sys");
        return;
    }

    sessionHistory.forEach(entry => {
        if (entry.kind === "user") {
            appendUserMessage(entry.content, log, name);
        } else if (entry.kind === "event") {
            appendMessageToLog({ type: entry.type, data: entry.data }, log);
        }
    });
    scrollToBottom(log);
}

async function handleCreateSession(customName = null) {
    if (registry.currentActiveId) {
        try { await set(SESSION_PREFIX + registry.currentActiveId, sessionHistory); }
        catch (e) { console.error("Failed to save current session before switching:", e); }
    }

    const id   = "s_" + Date.now();
    const name = customName?.trim() || `session-${registry.list.length}`;
    registry.list.push({ id, name });
    registry.currentActiveId = id;
    sessionHistory = [];
    await saveAllToBrowser();
    renderTopMultiplexerBar();
    renderTerminalScreen();
}

async function switchSession(index) {
    const t = registry.list[index];
    if (!t || t.id === registry.currentActiveId) return;

    if (registry.currentActiveId) {
        try { await set(SESSION_PREFIX + registry.currentActiveId, sessionHistory); }
        catch (e) { console.error("Failed to save session before switch:", e); }
    }

    registry.currentActiveId = t.id;
    await set(REGISTRY_KEY, registry);
    await loadSessionData(t.id);
}

async function handleDeleteSessionConfirmation(index) {
    const s = registry.list[index];
    if (!s) return;
    if (confirm(`Delete session "${s.name}"?`)) {
        await del(SESSION_PREFIX + s.id);
        registry.list.splice(index, 1);
        if (registry.currentActiveId === s.id) {
            if (registry.list.length > 0) {
                registry.currentActiveId = registry.list[Math.max(0, index - 1)].id;
                await set(REGISTRY_KEY, registry);
                await loadSessionData(registry.currentActiveId);
            } else {
                registry.currentActiveId = null;
                sessionHistory = [];
                await set(REGISTRY_KEY, registry);
                renderTopMultiplexerBar();
                renderTerminalScreen();
            }
        } else {
            await set(REGISTRY_KEY, registry);
            renderTopMultiplexerBar();
        }
    }
}

async function handleWipeAllSessionsConfirmation() {
    if (prompt("Type WIPE to erase all sessions:") === "WIPE") {
        for (const s of registry.list) await del(SESSION_PREFIX + s.id);
        await del(REGISTRY_KEY);
        registry.list = [];
        registry.currentActiveId = null;
        sessionHistory = [];
        await handleCreateSession("general");
    }
}

async function handleSlashCommand(raw) {
    const parts = raw.trim().split(" ");
    const cmd   = parts[0].toLowerCase();
    const args  = parts.slice(1).join(" ");

    if (cmd === "/new") {
        await handleCreateSession(args || null);
    } else if (cmd === "/clear") {
        log.innerHTML = "";
        printBanner();
    } else if (cmd === "/help") {
        const cmds = [
            ["  /new [name]", "create a new session tab"],
            ["  /clear",      "clear the terminal output"],
            ["  /help",       "show this help message"],
        ];
        const keys = [
            ["  ↑ / ↓",       "cycle through command history"],
            ["  Esc",         "stop current generation"],
            ["  Enter",       "send prompt"],
        ];
        print("── commands ──────────────────────────────────", "sys");
        cmds.forEach(([c, d]) => print(`${c.padEnd(18)} — ${d}`, "sys"));
        print("── keyboard shortcuts ────────────────────────", "sys");
        keys.forEach(([c, d]) => print(`${c.padEnd(18)} — ${d}`, "sys"));
        print("──────────────────────────────────────────────", "sys");
    } else {
        print(`unknown command: ${cmd}  (try /help)`, "err");
    }
}

/* ──────────────────────────────────────────────────────────────
   SECTION 7 — SSE Chat Stream
────────────────────────────────────────────────────────────── */

function startChatStream(promptText, targetLog) {
    const url = `${CHAT_ENDPOINT}?prompt=${encodeURIComponent(promptText)}`;
    const stream = new EventSource(url);

    stream.onmessage = (event) => {
        let message;
        try { message = JSON.parse(event.data); } catch { return; }

        if (message.type === "step") {
            updateStepCounter(message.data);
            return;
        }

        appendMessageToLog(message, targetLog);
        persistEntry({ kind: "event", type: message.type, data: message.data });
        scrollToBottom(targetLog);
        updateStatusBar();

        if (message.type === "done" || message.type === "error" || message.type === "danger") {
            resetStepCounter();
            stream.close();
            currentStream = null;
            setGenerating(false);
            saveAllToBrowser();
        }
    };

    stream.onerror = () => {
        print("✗ Connection to agent lost.", "err");
        resetStepCounter();
        stream.close();
        currentStream = null;
        setGenerating(false);
        setConnStatus("error");
        saveAllToBrowser();
    };

    return stream;
}

function stopGeneration() {
    if (!currentStream) return;
    currentStream.close();
    currentStream = null;
    resetStepCounter();
    pendingToolBlock = null;
    print("⚠ Generation stopped by user.", "sys");
    setGenerating(false);
    saveAllToBrowser();
}

async function askAI(promptText) {
    const cur   = registry.list.find(s => s.id === registry.currentActiveId);
    const label = cur ? cur.name : "~";

    appendUserMessage(promptText, log, label);
    persistEntry({ kind: "user", content: promptText });
    updateStatusBar();
    await saveAllToBrowser();

    pendingToolBlock = null;
    setGenerating(true);
    currentStream = startChatStream(promptText, log);
}

/* ──────────────────────────────────────────────────────────────
   STOP BUTTON
────────────────────────────────────────────────────────────── */
stopBtn.addEventListener("click", stopGeneration);

/* ──────────────────────────────────────────────────────────────
   INPUT HANDLER (command history + stop on Esc + submit)
────────────────────────────────────────────────────────────── */
input.addEventListener("keydown", async (e) => {

    if (e.key === "Escape") {
        stopGeneration();
        return;
    }

    if (e.key === "ArrowUp") {
        e.preventDefault();
        if (cmdHistory.length === 0) return;
        if (historyIndex === -1) {
            historyDraft = input.value;
            historyIndex = cmdHistory.length - 1;
        } else if (historyIndex > 0) {
            historyIndex--;
        }
        input.value = cmdHistory[historyIndex];
        requestAnimationFrame(() => { input.selectionStart = input.selectionEnd = input.value.length; });
        return;
    }

    if (e.key === "ArrowDown") {
        e.preventDefault();
        if (historyIndex === -1) return;
        if (historyIndex < cmdHistory.length - 1) {
            historyIndex++;
            input.value = cmdHistory[historyIndex];
        } else {
            historyIndex = -1;
            input.value = historyDraft;
        }
        requestAnimationFrame(() => { input.selectionStart = input.selectionEnd = input.value.length; });
        return;
    }

    if (e.key !== "Enter") {
        if (historyIndex !== -1) historyIndex = -1;
        return;
    }

    const value = input.value.trim();
    if (!value) return;
    input.value  = "";
    historyIndex = -1;
    historyDraft = "";

    if (cmdHistory[cmdHistory.length - 1] !== value) {
        cmdHistory.push(value);
        if (cmdHistory.length > CMD_HISTORY_MAX) cmdHistory.shift();
    }

    if (value.startsWith("/")) {
        print(`client:[cmd]~$ ${value}`, "user");
        await handleSlashCommand(value);
    } else {
        if (!registry.currentActiveId) {
            print("No active session. Type /new to start one.", "err");
            return;
        }
        await askAI(value);
    }
});

/* ──────────────────────────────────────────────────────────────
   BOOT
────────────────────────────────────────────────────────────── */
async function boot() {
    try {
        const saved = await get(REGISTRY_KEY);
        if (saved?.list?.length) {
            registry = saved;
        } else {
            const id = "s_" + Date.now();
            registry.list.push({ id, name: "general" });
            registry.currentActiveId = id;
        }
    } catch {
        const id = "s_" + Date.now();
        registry.list.push({ id, name: "general" });
        registry.currentActiveId = id;
    }

    setConnStatus("ready");
    renderTopMultiplexerBar();
    await loadSessionData(registry.currentActiveId);
    updateStatusBar();

    input.focus();
}

boot();
