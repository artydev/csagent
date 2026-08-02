// =============================================================================
// CSAgent Console — Frontend Application
// =============================================================================
// Responsibilities:
//   1. Parse Markdown and apply Prism syntax highlighting
//   2. Handle user input and SSE (Server-Sent Events) chat stream
//   3. Render messages into the log container
// =============================================================================

// -----------------------------------------------------------------------------
// SECTION 1 — Markdown & Syntax Highlighting
// -----------------------------------------------------------------------------

/**
 * Normalise language class names so Prism can highlight them correctly.
 *
 * Prism uses 'markup' internally for HTML/XML/SVG, but Marked.js generates
 * 'language-html' / 'language-xml'. We also map plain/text aliases to 'none'
 * so Prism does not attempt highlighting.
 *
 * @param {string} className — The original class attribute value
 * @returns {string} — The corrected class attribute value
 */
function normaliseLanguageClass(className) {
    let result = className;

    // Map 'html' and 'xml' to Prism's internal 'markup'
    result = result.replace("language-html", "language-markup");
    result = result.replace("language-xml", "language-markup");

    // Map plain-text aliases to 'none' (no highlighting)
    result = result.replace(
        /language-(text|plain|plaintext)/g,
        "language-none"
    );

    return result;
}

/**
 * Ensure Prism language aliases are set up globally.
 *
 * Prism's 'markup' grammar covers HTML, XML and SVG, but it does not
 * register 'html' or 'xml' as top-level language keys by default.
 */
function ensurePrismAliases() {
    if (typeof Prism === "undefined") return;

    if (Prism.languages.markup && !Prism.languages.html) {
        Prism.languages.html = Prism.languages.markup;
    }
    if (Prism.languages.markup && !Prism.languages.xml) {
        Prism.languages.xml = Prism.languages.markup;
    }
}

/**
 * Fix language classes on every code/pre element inside a container so that
 * Prism can recognise them.
 *
 * @param {HTMLElement} container — The parent element to search within
 */
function fixCodeLanguageClasses(container) {
    const selector = 'code[class*="language-"], pre[class*="language-"]';
    container.querySelectorAll(selector).forEach((element) => {
        element.className = normaliseLanguageClass(element.className);
    });
}

/**
 * Parse a Markdown string into an HTML element and apply syntax highlighting.
 *
 * @param {string} text — Raw Markdown content
 * @returns {HTMLDivElement} — A div.markdown-content containing the rendered HTML
 */
function parseMarkdown(text) {
    const container = document.createElement("div");
    container.className = "markdown-content";
    container.innerHTML = marked.parse(text);

    ensurePrismAliases();
    fixCodeLanguageClasses(container);
    Prism.highlightAllUnder(container);

    return container;
}

// -----------------------------------------------------------------------------
// SECTION 2 — Message Rendering
// -----------------------------------------------------------------------------

/**
 * Create a DOM element for a "done" message (task completed).
 *
 * @returns {HTMLDivElement}
 */
function createDoneMessage() {
    const div = document.createElement("div");
    div.className = "done";
    div.innerText = "✓ Task completed successfully";
    return div;
}

/**
 * Create a DOM element for a "warning" message.
 *
 * @param {string} text — The warning text
 * @returns {HTMLDivElement}
 */
function createWarningMessage(text) {
    const div = document.createElement("div");
    div.className = "warning";
    div.innerText = "⚠ " + text;
    return div;
}

/**
 * Create a DOM element for a "danger" (error) message.
 *
 * @param {string} text — The error text
 * @returns {HTMLDivElement}
 */
function createDangerMessage(text) {
    const div = document.createElement("div");
    div.className = "danger";
    div.innerText = "✗ " + text;
    return div;
}

/**
 * Create a DOM element for a generic log message (thought, result, etc.).
 *
 * @param {string} type — The message type (used as CSS class)
 * @param {string} content — The text content
 * @returns {HTMLDivElement}
 */
function createGenericMessage(type, content) {
    const div = document.createElement("div");
    div.className = type;

    if (type === "result" || type === "thought") {
        // Render as formatted Markdown
        div.appendChild(parseMarkdown(content));
    } else {
        div.innerText = `[${type}] ${content}`;
    }

    return div;
}

/**
 * Route an incoming SSE message to the correct renderer and append it to the log.
 *
 * @param {object} message — Parsed JSON object with `type` and `data` fields
 * @param {HTMLElement} log — The log container element
 */
function appendMessageToLog(message, log) {
    const content =
        typeof message.data === "string"
            ? message.data
            : JSON.stringify(message.data);

    let element;

    switch (message.type) {
        case "done":
            element = createDoneMessage();
            break;
        case "warning":
            element = createWarningMessage(content);
            break;
        case "danger":
            element = createDangerMessage(content);
            break;
        default:
            element = createGenericMessage(message.type, content);
            break;
    }

    log.appendChild(element);
}

/**
 * Scroll the log container to the bottom.
 *
 * @param {HTMLElement} log
 */
function scrollToBottom(log) {
    log.scrollTop = log.scrollHeight;
}

// -----------------------------------------------------------------------------
// SECTION 3 — User Input
// -----------------------------------------------------------------------------

/**
 * Append the user's prompt to the log as a styled message.
 *
 * @param {string} prompt
 * @param {HTMLElement} log
 */
function appendUserMessage(prompt, log) {
    const userDiv = document.createElement("div");
    userDiv.className = "user-msg";
    userDiv.innerHTML = `<strong>> User:</strong> ${prompt}`;
    log.appendChild(userDiv);
}

// -----------------------------------------------------------------------------
// SECTION 4 — SSE (Server-Sent Events) Stream
// -----------------------------------------------------------------------------

/**
 * Open an SSE connection to the chat endpoint and wire up event handlers.
 *
 * @param {string} prompt — The user's input prompt
 * @param {HTMLElement} log — The log container element
 * @returns {EventSource}
 */
function startChatStream(prompt, log) {
    const url = `/api/chat?prompt=${encodeURIComponent(prompt)}`;
    const stream = new EventSource(url);

    stream.onmessage = function (event) {
        const message = JSON.parse(event.data);
        appendMessageToLog(message, log);
        scrollToBottom(log);

        if (message.type === "done") {
            stream.close();
        }
    };

    stream.onerror = function () {
        console.error("SSE connection error — closing stream.");
        stream.close();
    };

    return stream;
}

// -----------------------------------------------------------------------------
// SECTION 5 — Main Entry Point
// -----------------------------------------------------------------------------

/**
 * Main entry point — called when the user presses Enter in the input field.
 *
 * Reads the prompt, displays it in the log, clears the input, and starts
 * an SSE stream for the response.
 */
function run() {
    const input = document.getElementById("in");
    const prompt = input.value.trim();
    if (!prompt) return;

    const log = document.getElementById("log");

    appendUserMessage(prompt, log);
    input.value = "";
    scrollToBottom(log);

    startChatStream(prompt, log);
}
