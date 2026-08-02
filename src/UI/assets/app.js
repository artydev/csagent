// Parse Markdown content using marked.js and trigger Prism highlighting
function parseMarkdown(text) {
    const container = document.createElement("div");
    container.className = "markdown-content";
    container.innerHTML = marked.parse(text);

    // Ensure Prism alias exists globally
    if (typeof Prism !== "undefined") {
        // Create alias: Prism uses 'markup' internally for HTML/XML/SVG
        if (Prism.languages.markup && !Prism.languages.html) {
            Prism.languages.html = Prism.languages.markup;
        }
        if (Prism.languages.markup && !Prism.languages.xml) {
            Prism.languages.xml = Prism.languages.markup;
        }

        // Fix language-html mismatch: marked generates 'language-html' but
        // Prism's grammar file is named 'markup'. We need to handle both cases.
        container.querySelectorAll('code[class*="language-"], pre[class*="language-"]').forEach(element => {
            let cls = element.className;
            if (cls.includes("language-html")) {
                cls = cls.replace("language-html", "language-markup");
            }
            if (cls.includes("language-xml")) {
                cls = cls.replace("language-xml", "language-markup");
            }
            // Handle 'text' or 'plain' as plain text (no highlighting)
            if (cls.includes("language-text") || cls.includes("language-plain") || cls.includes("language-plaintext")) {
                cls = cls.replace(/language-(text|plain|plaintext)/g, "language-none");
            }
            element.className = cls;
        });

        // Trigger Prism highlighting on the newly rendered elements
        Prism.highlightAllUnder(container);
    }

    return container;
}

function run() {
    const input = document.getElementById("in");
    const prompt = input.value.trim();
    if (!prompt) return;

    const log = document.getElementById("log");
    const user = document.createElement("div");
    user.className = "user-msg";
    user.innerHTML = `<strong>> User:</strong> ${prompt}`;
    log.appendChild(user);
    input.value = "";

    // Auto-scroll to bottom
    log.scrollTop = log.scrollHeight;

    const stream = new EventSource(
        `/api/chat?prompt=${encodeURIComponent(prompt)}`,
    );

    stream.onmessage = function (event) {
        const message = JSON.parse(event.data);
        const div = document.createElement("div");

        if (message.type === "done") {
            div.className = "done";
            div.innerText = "✓ Task completed successfully";
            log.appendChild(div);
            stream.close();
        } else if (message.type === "warning") {
            div.className = "warning";
            div.innerText = "⚠ " + message.data;
            log.appendChild(div);
        } else if (message.type === "danger") {
            div.className = "danger";
            div.innerText = "✗ " + message.data;
            log.appendChild(div);
        } else {
            div.className = message.type;
            const content =
                typeof message.data === "string"
                    ? message.data
                    : JSON.stringify(message.data);

            if (message.type === "result" || message.type === "thought") {
                const parsedContent = parseMarkdown(content);
                div.appendChild(parsedContent);
            } else {
                div.innerText = `[${message.type}] ${content}`;
            }
            log.appendChild(div);
        }

        // Auto-scroll to bottom
        log.scrollTop = log.scrollHeight;
    };

    stream.onerror = function () {
        console.error("Stream error");
        stream.close();
    };
}
