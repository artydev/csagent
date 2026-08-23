# Task 3 — Bridge API (`PhotinoAPI`)

**Status:** Not started
**Location:** `src\Presentation\DesktopPhotino\PhotinoAPI.cs`

## Objective

Expose .NET functionality to the JavaScript UI running inside the Photino window,
and allow .NET to push events back to the UI. This is the Photino equivalent of the
existing `DesktopAPI` (which is registered as the `dotnet` host object in the
AOTrino/WebView2 host).

## Context — existing `DesktopAPI`

The current AOTrino bridge exposes simple properties to JS:

```csharp
public string? MachineName => Environment.MachineName;
public string UserName => Environment.UserName;
public string? ExePath => Environment.ProcessPath?.Substring(0, Math.Min(...));
```

In WebView2 these are exposed as a host object (`chrome.webview.hostObjects.dotnet`)
callable synchronously/async from JS.

## Photino bridge model

Photino does **not** expose arbitrary host objects like WebView2. Instead it uses a
**message-passing** model:

- **JS → .NET:** `window.external.sendMessage(jsonString)` triggers the
  `RegisterWebMessageReceivedHandler` callback on the .NET side.
- **.NET → JS:** `window.SendMessage(jsonString)` invokes the `window.external.receiveMessage`
  handler registered in JS.

So the bridge must be implemented as a JSON message protocol rather than direct
property access.

## Subtasks

### 3.1 Define a JSON message protocol

Define a small set of message types exchanged between JS and .NET, e.g.:

```jsonc
// JS → .NET (request)
{ "id": 1, "type": "getInfo" }
{ "id": 2, "type": "chat", "prompt": "..." }
{ "id": 3, "type": "approve", "actionId": "..." }

// .NET → JS (response / event)
{ "id": 1, "type": "info", "data": { "machineName": "...", "userName": "...", "exePath": "..." } }
{ "type": "event", "event": "step", "data": { "n": 1, "m": 5 } }
{ "type": "event", "event": "message", "data": { "type": "thought", "data": "..." } }
```

### 3.2 Implement `PhotinoAPI`

Create a class that:

- Holds a reference to the `PhotinoWindow` (to call `SendMessage` back to JS).
- Handles incoming messages from `RegisterWebMessageReceivedHandler`.
- Exposes the same info as `DesktopAPI` (`MachineName`, `UserName`, `ExePath`).
- Routes `chat` messages to the agent loop and forwards SSE-style events
  (`step`, `message`, `done`, `danger`, `call`, `result`) back to JS via
  `window.SendMessage`.

### 3.3 Wire the bridge in `PhotinoHost`

In `PhotinoHost`, connect the pieces:

```csharp
var api = new PhotinoAPI(window, args);
window.RegisterWebMessageReceivedHandler((sender, message) => api.HandleMessage(message));
```

## Definition of Done

- [ ] `PhotinoAPI` exists and exposes machine/user/exe info.
- [ ] JS can send messages to .NET via `window.external.sendMessage`.
- [ ] .NET can push events to JS via `window.SendMessage`.
- [ ] Chat prompts from JS reach the agent loop and events stream back to the UI.
