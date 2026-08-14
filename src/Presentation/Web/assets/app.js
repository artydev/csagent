// =============================================================================
// CSAgent Console — Frontend Application
// =============================================================================
// Responsibilities:
//   1. Parse Markdown and apply Prism syntax highlighting
//   2. Handle user input and SSE (Server-Sent Events) chat stream
//   3. Render messages into the log container
//   4. Display step counter in the header
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
 * Create a DOM element for a tool call message.
 *
 * Displays the tool name prominently and formats the arguments
 * as a structured list of key-value pairs.
 *
 * @param {string} name — The tool name (e.g. "write_file", "read_file")
 * @param {string} argsJson — JSON string of the tool arguments
 * @returns {HTMLDivElement}
 */
function createToolCallMessage(name, argsJson) {
    const div = document.createElement("div");
    div.className = "call";

    // Tool name header
    const header = document.createElement("div");
    header.className = "call-header";

    // Map tool names to readable labels with icons
    const toolLabels = {
        "write_file": "📝 Write File",
        "read_file": "📖 Read File",
        "list_dir": "📂 List Directory",
        "search_files": "🔍 Search Files",
        "sh": "💻 Shell Command",
        "switch_model": "🔄 Switch Model"
    };
    header.innerHTML = `<strong>${toolLabels[name] || "🔧 " + name}</strong>`;
    div.appendChild(header);

    // Parse and display arguments
    try {
        const args = JSON.parse(argsJson);
        const argList = document.createElement("div");
        argList.className = "call-args";

        for (const [key, value] of Object.entries(args)) {
            const argRow = document.createElement("div");
            argRow.className = "call-arg-row";

            const keySpan = document.createElement("span");
            keySpan.className = "call-arg-key";
            keySpan.textContent = key + ":";
            argRow.appendChild(keySpan);

            const valSpan = document.createElement("span");
            valSpan.className = "call-arg-value";

            // Truncate very long values
            let displayVal = String(value);
            if (displayVal.length > 300) {
                displayVal = displayVal.substring(0, 300) + `... (${displayVal.length} chars total)`;
            }
            valSpan.textContent = displayVal;
            argRow.appendChild(valSpan);

            argList.appendChild(argRow);
        }

        div.appendChild(argList);
    } catch {
        // Fallback: show raw JSON in a styled pre block
        const raw = document.createElement("pre");
        raw.className = "call-raw";
        raw.textContent = argsJson;
        div.appendChild(raw);
    }

    return div;
}

/**
 * Create a DOM element for a tool result message.
 *
 * Tool results are raw data (file contents, command output, errors),
 * NOT Markdown. They are displayed as plain text in a code block
 * to avoid Markdown rendering issues (e.g. '#' in file contents
 * being treated as headings).
 *
 * @param {string} content — The raw result text
 * @param {boolean} isError — Whether this is an error result
 * @returns {HTMLDivElement}
 */
function createToolResultMessage(content, isError) {
    const div = document.createElement("div");
    div.className = isError ? "danger" : "result";

    // Show a brief header
    const header = document.createElement("div");
    header.className = "result-header";
    header.textContent = isError ? "✗ Error" : "✓ Result";
    div.appendChild(header);

    // Wrap content in a pre block for plain-text display
    const pre = document.createElement("pre");
    pre.className = "result-content";
    pre.textContent = content;
    div.appendChild(pre);

    return div;
}

/**
 * Create a DOM element for a generic log message.
 *
 * @param {string} type — The message type (used as CSS class)
 * @param {string} content — The text content
 * @returns {HTMLDivElement}
 */
function createGenericMessage(type, content) {
    const div = document.createElement("div");
    div.className = type;

    if (type === "thought") {
        // Assistant thoughts are Markdown-formatted text
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
    let element;

    switch (message.type) {
        case "done":
            element = createDoneMessage();
            break;
        case "warning":
            element = createWarningMessage(
                typeof message.data === "string" ? message.data : JSON.stringify(message.data)
            );
            break;
        case "danger":
            element = createDangerMessage(
                typeof message.data === "string" ? message.data : JSON.stringify(message.data)
            );
            break;
        case "call":
            // Tool call messages have data: { n: toolName, a: argsJson }
            if (message.data && typeof message.data === "object" && message.data.n) {
                element = createToolCallMessage(message.data.n, message.data.a);
            } else {
                element = createGenericMessage(message.type, JSON.stringify(message.data));
            }
            break;
        case "result":
            // Tool result messages have data: { r: resultText, e: isError }
            if (message.data && typeof message.data === "object" && "r" in message.data) {
                element = createToolResultMessage(message.data.r, message.data.e);
            } else {
                element = createGenericMessage(message.type, JSON.stringify(message.data));
            }
            break;
        default:
            element = createGenericMessage(
                message.type,
                typeof message.data === "string" ? message.data : JSON.stringify(message.data)
            );
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
// SECTION 3 — Step Counter
// -----------------------------------------------------------------------------

/**
 * Update the step counter in the header.
 *
 * The step event data has the shape { n: currentStep, m: maxSteps }.
 * When the task is done or an error occurs, reset to "Ready".
 *
 * @param {object} data — The step data object
 */
function updateStepCounter(data) {
    const counter = document.getElementById("step-counter");
    if (!counter) return;

    if (data && typeof data.n === "number" && typeof data.m === "number") {
        counter.textContent = `Step ${data.n} of ${data.m}`;
    }
}

/**
 * Reset the step counter to its idle state.
 */
function resetStepCounter() {
    const counter = document.getElementById("step-counter");
    if (counter) counter.textContent = "Ready";
}

// -----------------------------------------------------------------------------
// SECTION 4 — User Input
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
// SECTION 5 — SSE (Server-Sent Events) Stream
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

        // Handle step events in the header counter, not in the log
        if (message.type === "step") {
            updateStepCounter(message.data);
            return;
        }

        appendMessageToLog(message, log);
        scrollToBottom(log);

        if (message.type === "done" || message.type === "error" || message.type === "danger") {
            resetStepCounter();
            stream.close();
        }
    };

    stream.onerror = function () {
        console.error("SSE connection error — closing stream.");
        resetStepCounter();
        stream.close();
    };

    return stream;
}

// -----------------------------------------------------------------------------
// SECTION 6 — Main Entry Point
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
