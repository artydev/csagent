# CSAgent UI — Interface Architecture

This document describes the three user interface implementations in CSAgent:

1. **TUI** — Terminal User Interface (CLI)
2. **Web** — ASP.NET web server with SSE (Server-Sent Events)
3. **Native** — Native desktop window (AOTrino + WebView2, Windows only)

---

## Overview

All three interfaces share the same core agent (`CodingAgent`) and observer pattern. The `IAgentObserver` interface defines the callbacks that each UI must implement:

| Method            | Purpose                                    |
|-------------------|--------------------------------------------|
| `OnStep(n, max)`  | Reports progress (step N of M)             |
| `OnThought(text)` | Assistant's reasoning / Markdown response  |
| `OnToolCall(name, args)` | Tool invocation with name + JSON args |
| `OnToolResult(result, isError)` | Tool execution result |
| `OnDone(message)` | Task completed successfully                |
| `OnError(message)`| Fatal error                                |
| `OnWarning(message)` | Non-fatal warning                      |
| `OnDanger(message)` | Destructive action alert                |

---

## 1. TUI — Terminal User Interface

**Files:**
- `src/Presentation/Tui/TuiHost.cs`
- `src/Presentation/Tui/ConsoleObserver.cs`
- `src/Presentation/Tui/ConsoleRenderer.cs`

**How it works:**

1. `TuiHost.RunAsync()` is the entry point. It loads memory, creates the agent, and enters a read-eval loop.
2. The user types prompts at `> User (type 'exit' to quit):`.
3. Each prompt is sent to `CodingAgent.RunAsync()`.
4. `ConsoleObserver` implements `IAgentObserver` and writes formatted output to the terminal using `ConsoleRenderer`.
5. The loop continues until the user types `exit`.

**Observer (`ConsoleObserver`):**
- `OnStep` → writes `[Step N/M]` to stderr.
- `OnThought` → renders Markdown with syntax highlighting via `ConsoleRenderer`.
- `OnToolCall` → displays tool name and arguments.
- `OnToolResult` → shows result or error.
- `OnDone` / `OnError` / `OnWarning` / `OnDanger` → colored console output.

**Key characteristics:**
- Synchronous console I/O.
- No external dependencies beyond the core agent.
- Best for quick, script-like interactions.

---

## 2. Web — ASP.NET Server with SSE

**Files:**
- `src/Presentation/Web/WebHost.cs`
- `src/Presentation/Web/ApiEndpoints.cs`
- `src/Presentation/Web/SseObserver.cs`
- `src/Presentation/Web/StaticAssets.cs`
- `src/Presentation/Web/assets/index.html`
- `src/Presentation/Web/assets/app.js`
- `src/Presentation/Web/assets/styles.css`

**How it works:**

1. `WebHost.Run()` starts an ASP.NET server on `http://localhost:{port}`.
2. Three static routes serve the frontend:
   - `GET /` → `index.html`
   - `GET /app.js` → JavaScript application
   - `GET /styles.css` → CSS stylesheet
3. The chat endpoint is `GET /api/chat?prompt=...` which returns an SSE (Server-Sent Events) stream.
4. The frontend (plain JS, no framework) opens an `EventSource` to `/api/chat` and renders messages as they arrive.

**Observer (`SseObserver`):**
- Each observer method serialises a JSON message and writes it as an SSE `data:` event.
- The frontend parses these events and renders them into the DOM.

**SSE message format:**
```
data: {"id":1,"type":"thought","data":"Hello world"}\n\n
data: {"id":2,"type":"call","data":{"n":"write_file","a":"{\"path\":\"...\",\"content\":\"...\"}"}}\n\n
data: {"id":3,"type":"result","data":{"r":"File written","e":false}}\n\n
data: {"id":4,"type":"done","data":"Task complete."}\n\n
```

**Message types:**
| Type | Data shape | Description |
|------|-----------|-------------|
| `step` | `{n, m}` | Progress update (not rendered in log) |
| `thought` | string | Markdown-formatted assistant response |
| `call` | `{n: name, a: argsJson}` | Tool invocation |
| `result` | `{r: result, e: isError}` | Tool execution result |
| `done` | string | Task completed |
| `error` | string | Fatal error |
| `warning` | string | Warning message |
| `danger` | string | Destructive action alert |

**Frontend (`app.js`):**
- Uses **Marked.js** for Markdown rendering.
- Uses **Prism.js** for syntax highlighting (Dracula theme).
- Renders messages into a scrollable `#log` container.
- Updates a step counter in the header.
- Supports responsive layout (mobile-friendly).

**Key characteristics:**
- Requires network port (default 5050, configurable via `--port`).
- Opens browser automatically on start.
- Rich UI with syntax highlighting and Markdown.
- SSE provides real-time streaming.
- No build step — plain HTML/CSS/JS served as embedded resources.

---

## 3. Native — Native Desktop Window (AOTrino + WebView2)

**Files:**
- `src/Presentation/WebBrowser/WebBrowserHost.cs`
- `src/Presentation/Web/assets/index.html` (shared with Web UI)
- `src/Presentation/Web/assets/app.js` (shared with Web UI)
- `src/Presentation/Web/assets/styles.css` (shared with Web UI)

**How it works:**

1. `WebBrowserHost.Run()` starts an ASP.NET server on a random loopback port.
2. It creates an `AOTrinoApplication` and a `CsAgentNativeWindow` that hosts a WebView2 control.
3. The WebView2 navigates to the local ASP.NET server URL.
4. A C# host object (`CsAgentHostObject`) is registered with the WebView2 via `AddHostObject("csAgent", ...)`.
5. The JS frontend can call methods on `window.chrome.webview.hostObjects.csAgent` to access agent configuration and system information.
6. The same HTML/JS/CSS assets from the Web UI are reused — the frontend uses SSE to communicate with the local ASP.NET server.

**Host Object (`CsAgentHostObject`):**
- `GetVersion()` → `string` — Returns the CSAgent version.
- `GetMemoryFile()` → `string` — Returns the memory file path.
- `GetModelOverride()` → `string` — Returns the model override (or empty string).
- `IsDryRun()` → `bool` — Returns whether dry-run mode is active.
- `GetSystemInfo()` → `string` — Returns system information as a JSON string.

**Key characteristics:**
- Native desktop window with no browser tabs.
- Requires WebView2 runtime (prompts to download if missing).
- Uses AOTrino library for native window management.
- Starts an ASP.NET server on a random loopback port (no port conflicts).
- The server is automatically shut down when the window closes.
- Reuses the same frontend assets as the Web UI mode.
- Navigation is restricted to the local server and `about:` / `data:` URIs.
- External links open in the default browser.

---

## Comparison

| Feature | TUI | Web | Native |
|---------|-----|-----|--------|
| **Dependencies** | None | ASP.NET | AOTrino + WebView2 |
| **Network** | No | Yes (port 5050) | Yes (random port, loopback only) |
| **UI Richness** | Text-only | Rich HTML/CSS | Rich HTML/CSS |
| **Startup** | Instant | ~1s (server) | ~1s (server + window) |
| **Markdown** | ANSI-coloured | Full rendering | Full rendering |
| **Syntax Highlighting** | Basic ANSI | Prism.js | Prism.js |
| **Platform** | Cross-platform | Cross-platform | Windows only |
| **Use Case** | Quick CLI | Remote/browser | Desktop app |

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────┐
│                     Program.Main()                       │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌─────────┐ │
│  │ --help   │  │ --version│  │ --doc    │  │ (modes) │ │
│  └──────────┘  └──────────┘  └──────────┘  └────┬────┘ │
│                                                   │      │
│                    ┌──────────────────────────────┘      │
│                    ▼                                     │
│         ┌─────────────────────┐                          │
│         │   AgentArguments    │                          │
│         └─────────┬───────────┘                          │
│                   │                                      │
│        ┌──────────┼──────────┐                           │
│        ▼          ▼          ▼                           │
│   ┌────────┐ ┌────────┐ ┌────────────┐                   │
│   │TuiHost │ │WebHost │ │WebBrowser  │                   │
│   │        │ │        │ │Host        │                   │
│   └───┬────┘ └───┬────┘ └─────┬──────┘                   │
│       │          │            │                          │
│       ▼          ▼            ▼                          │
│  ┌─────────┐ ┌─────────┐ ┌──────────────┐               │
│  │Console  │ │  SSE    │ │ ASP.NET      │               │
│  │Observer │ │Observer │ │ server       │               │
│  └────┬────┘ └────┬────┘ │ (random port)│               │
│       │           │      └──────┬───────┘               │
│       │           │             │                        │
│       │           │      ┌──────▼───────┐               │
│       │           │      │ AOTrino      │               │
│       │           │      │ WebView2     │               │
│       │           │      │ Window       │               │
│       │           │      └──────┬───────┘               │
│       │           │             │                        │
│       └───────────┼─────────────┘                        │
│                   ▼                                      │
│          ┌────────────────┐                              │
│          │  CodingAgent   │                              │
│          │  (IAgentObserver)                             │
│          └────────────────┘                              │
└─────────────────────────────────────────────────────────┘
```

---

## Adding a New Interface

To add a new UI interface:

1. Create a new directory under `src/Presentation/<Name>/`.
2. Implement `IAgentObserver` with your rendering logic.
3. Create a host class (like `TuiHost`, `WebHost`, or `WebBrowserHost`) that:
   - Parses relevant CLI arguments.
   - Creates the `CodingAgent` with your observer.
   - Handles user input and calls `agent.RunAsync()`.
4. Add a new mode flag in `ArgumentParser.cs` (e.g., `--my-ui`).
5. Add the mode branch in `Program.Main()`.
6. If you need frontend assets, embed them as resources and reference them in the `.csproj`.

---

## Embedded Resources

All UI assets (HTML, JS, CSS) are embedded as resources in the assembly:

```xml
<EmbeddedResource Include="src\Presentation\Web\assets\index.html" />
<EmbeddedResource Include="src\Presentation\Web\assets\app.js" />
<EmbeddedResource Include="src\Presentation\Web\assets\styles.css" />
```

The resource names follow the pattern: `{RootNamespace}.{relative-path-with-dots}`

For example, `CsAgentUI.src.Presentation.Web.assets.index.html`.

The `StaticAssets` class loads these at runtime.

---

## Startup Flow

### Web UI
```
Program.Main() → WebHost.Run()
  → Create WebApplication
  → Map static routes (/, /app.js, /styles.css)
  → Map /api/chat endpoint
  → Start server
  → Open browser
  → User types prompt → SSE stream → SseObserver → frontend renders
```

### Native UI
```
Program.Main() → WebBrowserHost.Run()
  → Find available random port
  → Start ASP.NET server on random port (background task)
  → Wait for server to be ready
  → Create AOTrinoApplication
  → Create CsAgentNativeWindow
    → Navigate to local ASP.NET server
    → Register CsAgentHostObject
  → Show window
  → Pump messages until window closes
  → Cancel server token → server shuts down
```

### TUI
```
Program.Main() → TuiHost.RunAsync()
  → Load memory
  → Create CodingAgent with ConsoleObserver
  → Read-eval loop
  → User types prompt → agent.RunAsync() → ConsoleObserver → terminal output
```
