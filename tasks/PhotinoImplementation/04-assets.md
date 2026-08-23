# Task 4 — Frontend Assets (HTML / JS / CSS)

**Status:** Not started
**Location:** `src\Presentation\DesktopPhotino\assets\`

## Objective

Create the Photino UI assets. These are the Photino equivalents of the existing
`src\Presentation\Desktop\assets\` files, adapted to Photino's message-passing
bridge instead of WebView2 host objects.

## Context — existing assets

The existing desktop UI (`index.html`, `app.js`, `styles.css`) provides:

- A dark-themed console with a header (brand, version, step counter), a scrollable
  log area, and an input box.
- Markdown rendering via **Marked.js** and syntax highlighting via **Prism.js**
  (loaded from CDN).
- SSE-style message rendering: `done`, `warning`, `danger`, `call` (tool call),
  `result` (tool result), `thought`, `step`.
- A `run()` entry point triggered on Enter.

The existing `app.js` currently uses WebView2 host objects
(`chrome.webview.hostObjects.dotnet`) and contains a debug `alert()` in
`startChatStream` — this must be replaced for Photino.

## Subtasks

### 4.1 `index.html`

- Same structure as the existing desktop `index.html` (header, log, input).
- Load Marked.js and Prism.js from CDN (same as existing).
- Reference `app.js` and `styles.css` via the custom scheme
  (`<script src="app://app.js">`, `<link href="app://styles.css">`).
- Keep the `{{Version}}`, `{{Model}}`, `{{MemoryFile}}`, `{{DryRun}}`, `{{OS}}`
  placeholders if the host injects them, or pass them via the bridge `getInfo`
  message instead.

### 4.2 `app.js`

Replace the WebView2 host-object code with Photino's message bridge:

```js
// JS → .NET
function sendToDotnet(payload) {
    window.external.sendMessage(JSON.stringify(payload));
}

// .NET → JS
window.external.receiveMessage = function (json) {
    const msg = JSON.parse(json);
    handleDotnetMessage(msg);
};
```

- Reuse the existing rendering helpers (`parseMarkdown`, `createToolCallMessage`,
  `createToolResultMessage`, `appendMessageToLog`, `updateStepCounter`, etc.).
- Implement `run()` to send the prompt to .NET via `sendToDotnet({ type: "chat", prompt })`.
- Handle incoming `.NET` events (`step`, `message`, `done`, `danger`, `call`,
  `result`) and render them into the log.
- **Remove** the debug `alert()` and the WebView2 `chrome.webview` console-log
  interception (or adapt it to Photino's `window.external`).

### 4.3 `styles.css`

- Reuse the existing dark theme styling.
- Ensure the layout works in the Photino window (full-height container, scrollable
  log, fixed input area).

## Definition of Done

- [ ] `index.html` loads `app.js` and `styles.css` via the custom scheme.
- [ ] `app.js` uses `window.external.sendMessage` / `window.external.receiveMessage`.
- [ ] All existing message types render correctly in the log.
- [ ] No WebView2-specific code or debug `alert()` remains.
- [ ] Enter in the input box sends the prompt to .NET and streams the response.
