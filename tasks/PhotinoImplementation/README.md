# Photino Desktop Implementation — Task Set

This folder contains the **task documentation** required to implement a Photino-based
desktop application for CSAgent. It describes *what* must be done and *why*.

> **Important:** The actual implementation files live in
> `src\Presentation\DesktopPhotino` (currently empty). This folder only holds the
> task descriptions / requirements — it is **not** the implementation.

## Goal

Replace (or complement) the current AOTrino/WebView2-based desktop host
(`src\Presentation\Desktop`) with a **Photino.NET** host. Photino is a lightweight,
cross-platform framework that opens native OS windows hosting a Web UI using the
OS's built-in WebKit-based browser control (WebView2 on Windows).

## Why Photino

- Much smaller footprint than Electron (up to ~110x smaller).
- Lower memory usage.
- Cross-platform (Windows, macOS, Linux) from a single .NET codebase.
- Simple API: `PhotinoWindow`, `RegisterCustomSchemeHandler`, `SendMessage`,
  `ReceiveMessage`, `RegisterWebMessageReceivedHandler`.

## Package

- **Photino.NET** — latest stable `4.0.16` (targets `net8.0`, compatible with the
  project's `net10.0-windows` TFM).
- Depends on **Photino.Native** (`>= 4.0.22`), pulled in transitively.

## Target location for implementation

```
src\Presentation\DesktopPhotino\
    PhotinoHost.cs        — window creation + host object registration
    PhotinoAPI.cs         — .NET object exposed to JS (bridge)
    PhotinoObserver.cs    — optional: forward agent events to the UI
    assets\
        index.html        — Photino UI shell
        app.js            — Photino frontend logic (uses window.external / SendMessage)
        styles.css        — Photino UI styling
```

## Task list

| # | Task | File |
|---|------|------|
| 1 | Project setup (package + folder + embedded resources) | `01-project-setup.md` |
| 2 | Window host (`PhotinoHost`) | `02-window-host.md` |
| 3 | Bridge API (`PhotinoAPI`) | `03-bridge-api.md` |
| 4 | Frontend assets (HTML/JS/CSS) | `04-assets.md` |
| 5 | CLI integration (`--desktop` argument) | `05-cli-integration.md` |
| 6 | Build & publish considerations | `06-build-publish.md` |

## Acceptance criteria (overall)

- Running `csagent --desktop` opens a native Photino window.
- The window loads the CSAgent UI from embedded assets (no external server required).
- JS can call into .NET via the Photino bridge (`window.external.sendMessage` /
  `ReceiveMessage`) and .NET can push events to JS (`SendMessage`).
- The agent chat loop (SSE-style events) is wired to the UI.
- The project still builds and publishes (single-file / AOT) with the new package.
