// Wait for Prism to load
function waitForPrism() {
    return new Promise((resolve) => {
        if (typeof Prism !== "undefined") {
            resolve();
        } else {
            const check = setInterval(() => {
                if (typeof Prism !== "undefined") {
                    clearInterval(check);
                    resolve();
                }
            }, 100);
        }
    });
}

document.addEventListener("DOMContentLoaded", async function () {
    await waitForPrism();

    if (typeof Prism !== "undefined") {
        Prism.plugins.autoloader.languages_path =
            "https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/components/";
    }
});

function parseMarkdown(text) {
    const container = document.createElement("div");

    // Split by code blocks first
    const parts = text.split(/(```[\s\S]*?```)/);

    parts.forEach(part => {
        if (part.match(/^```/)) {
            // This is a code block
            const codeMatch = part.match(/```(\w+)?\n([\s\S]*?)```/);
            if (codeMatch) {
                const language = codeMatch[1] || "javascript";
                const code = codeMatch[2].trim();

                const preElement = document.createElement("pre");
                preElement.className = "language-wrapper";
                const codeElement = document.createElement("code");
                codeElement.className = `language-${language}`;
                codeElement.textContent = code;
                preElement.appendChild(codeElement);
                container.appendChild(preElement);
            }
        } else {
            // This is regular text, parse markdown syntax
            let html = part;

            // Headers: ### -> h3, ## -> h2, # -> h1
            html = html.replace(/^### (.*?)$/gm, '<h3>$1</h3>');
            html = html.replace(/^## (.*?)$/gm, '<h2>$1</h2>');
            html = html.replace(/^# (.*?)$/gm, '<h1>$1</h1>');

            // Bold: **text** -> <strong>text</strong>
            html = html.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');

            // Italic: *text* -> <em>text</em>
            html = html.replace(/\*(.*?)\*/g, '<em>$1</em>');

            // Code inline: `code` -> <code>code</code>
            html = html.replace(/`(.*?)`/g, '<code class="inline-code">$1</code>');

            // Split by line breaks and create paragraphs
            const lines = html.split('\n');
            lines.forEach(line => {
                line = line.trim();
                if (line === '') return;

                if (!line.match(/^<h[1-3]/)) {
                    const p = document.createElement("p");
                    p.innerHTML = line;
                    container.appendChild(p);
                } else {
                    const tempDiv = document.createElement("div");
                    tempDiv.innerHTML = line;
                    container.appendChild(tempDiv.firstChild);
                }
            });
        }
    });

    return container;
}

function highlightCode(element) {
    if (typeof Prism === "undefined") {
        console.error("Prism not loaded");
        return;
    }

    try {
        const codeElements = element.querySelectorAll('code[class*="language-"]');
        codeElements.forEach(codeBlock => {
            Prism.highlightElement(codeBlock);
        });
    } catch (e) {
        console.error("Highlighting error:", e);
    }
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
                // Parse markdown content
                const parsedContent = parseMarkdown(content);
                div.appendChild(parsedContent);
            } else {
                div.innerText = `[${message.type}] ${content}`;
            }
            log.appendChild(div);

            // Highlight code blocks after rendering
            if (message.type === "result" || message.type === "thought") {
                // Use multiple timeouts to ensure DOM is ready and Prism is ready
                setTimeout(() => {
                    highlightCode(div);
                }, 100);

                // Double-check highlighting
                setTimeout(() => {
                    highlightCode(div);
                }, 300);
            }
        }

        // Auto-scroll to bottom
        log.scrollTop = log.scrollHeight;
    };

    stream.onerror = function () {
        console.error("Stream error");
        stream.close();
    };
}