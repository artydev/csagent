# PROGRESS — Photino Desktop Implementation

> Tracking file for the Photino desktop implementation.
> Implementation files live in `src\Presentation\DesktopPhotino\` (currently empty).
> Task descriptions live in this folder (`tasks\PhotinoImplementation\`).

## Legend

- ⬜ **Not started** — no work done yet.
- 🔄 **In progress** — actively being worked on.
- ✅ **Done** — complete and verified.
- ⚠️ **Blocked** — waiting on something / needs attention.

---

## Task Status

| # | Task | File | Status | Notes |
|---|------|------|--------|-------|
| 1 | Project setup (package + folder + embedded resources) | `01-project-setup.md` | ✅ Done | Photino.NET 4.0.16 added; folder + placeholder assets created; embedded resources registered; build OK (0 errors) |
| 2 | Window host (`PhotinoHost`) | `02-window-host.md` | ✅ Done | `PhotinoHost` opens a 1280×800 native window, serves embedded assets via custom `app://` scheme, wires the bridge; build OK (0 errors) |
| 3 | Bridge API (`PhotinoAPI`) | `03-bridge-api.md` | ✅ Done | `PhotinoAPI` implements JSON message protocol (getInfo/chat/cancel), forwards agent events (step/message/call/result/done/danger) to JS; build OK (0 errors, 0 warnings) |
| 4 | Frontend assets (HTML/JS/CSS) | `04-assets.md` | ✅ Done | `index.html`/`app.js`/`styles.css` implemented; Photino message bridge (`window.external.sendMessage`/`receiveMessage`); all event types render; no WebView2 code or debug `alert()`; assets embedded & served via `app://`; build OK (0 errors) |
| 5 | CLI integration (`--desktop` argument) | `05-cli-integration.md` | ✅ Done | `--desktop` now routes to `PhotinoHost.Run(parsed)` (Option A — replace); removed STA-thread wrapper; help text updated; build OK (0 errors) |
| 6 | Build & publish considerations | `06-build-publish.md` | ⬜ Not started | |

---

## Implementation Files (target: `src\Presentation\DesktopPhotino\`)

| File | Status | Notes |
|------|--------|-------|
| `PhotinoHost.cs` | ✅ Done | Window host + `app://` embedded asset serving + bridge wiring |
| `PhotinoAPI.cs` | ✅ Done | JSON message protocol; getInfo/chat/cancel; agent event forwarding |
| `PhotinoObserver.cs` | ✅ Done | Implemented as nested `PhotinoObserver` inside `PhotinoAPI` |
| `assets\index.html` | ✅ Done | Full UI shell; loads `app.js`/`styles.css` via `app://`; Marked.js + Prism.js from CDN |
| `assets\app.js` | ✅ Done | Photino message bridge; renders all event types; no WebView2 code or debug `alert()` |
| `assets\styles.css` | ✅ Done | Dracula dark theme; full-height layout, scrollable log, fixed input |

---

## Changelog

| Date | Change |
|------|--------|
| — | Task set created in `tasks\PhotinoImplementation\` (README + 6 task docs). No implementation started yet. |
| — | **Task 1 done:** Added `Photino.NET` 4.0.16 package ref; created `src\Presentation\DesktopPhotino\` with placeholder assets; registered assets as embedded resources; verified restore + build (0 errors). |
| — | **Task 2 done:** Implemented `PhotinoHost` — native 1280×800 window, custom `app://` scheme serving embedded assets, bridge wiring via `RegisterWebMessageReceivedHandler`. |
| — | **Task 3 done:** Implemented `PhotinoAPI` — JSON message protocol (`getInfo`/`chat`/`cancel`), machine/user/exe info, agent loop integration, and event forwarding (`step`/`message`/`call`/`result`/`done`/`danger`) back to JS via `SendWebMessage`. Observer implemented as nested `PhotinoObserver`. Build verified: 0 errors, 0 warnings. |
| — | **Task 4 done:** Implemented the Photino frontend assets. `index.html` loads `app.js`/`styles.css` via the custom `app://` scheme and pulls Marked.js + Prism.js from CDN. `app.js` replaces the WebView2 host-object code with Photino's message bridge (`window.external.sendMessage` / `window.external.receiveMessage`), renders all event types (`step`/`message`/`call`/`result`/`done`/`danger`), and sends `chat` prompts to .NET on Enter. `styles.css` reuses the Dracula dark theme with a full-height layout. No WebView2 code or debug `alert()` remains. Verified all three assets are embedded and served under `app://`; build OK (0 errors). |
| — | **Task 5 done:** Routed `--desktop` to the Photino host (Option A — replace). `Program.cs` now calls `PhotinoHost.Run(parsed)` directly (no STA thread wrapper needed for Photino); removed the now-unused `using System.Runtime.InteropServices`. Updated `HelpDisplay.cs` to document `--desktop` as the Photino desktop window mode (MODES + EXAMPLES). The AOTrino `DesktopHost` remains compiled in the codebase. Build verified: 0 errors. |
| — | **Build hygiene:** Excluded `_reflect\**` and `tools\**` from the app compile (standalone tool projects); removed stale `obj`/`bin` to fix duplicate-attribute build errors. |

---

## Open Questions / Decisions

- **Routing strategy (Task 5):** ✅ **Decided — Option A (replace).** `--desktop` now launches the Photino host (`PhotinoHost.Run`). The AOTrino `DesktopHost` remains compiled in the codebase but is no longer dispatched from the CLI.
- **Bridge protocol:** ✅ **Confirmed.** JS ↔ .NET uses Photino's `window.external.sendMessage` / `window.external.receiveMessage`; agent events are wrapped as `{ type: "event", event: <name>, data: <payload> }`.
- **AOT/trimming:** Verify Photino.Native works with `PublishAot` /
  `PublishTrimmed` / `DisableRuntimeMarshalling` (Task 6).

---

## Next Steps

1. Start **Task 6** — build & publish considerations (verify Photino.Native works with `PublishAot` / `PublishTrimmed` / `DisableRuntimeMarshalling`).
