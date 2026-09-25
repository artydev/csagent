
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
    declare interface toolLabelsType {}
