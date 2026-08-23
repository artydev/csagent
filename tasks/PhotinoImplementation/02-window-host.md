# Task 2 — Window Host (`PhotinoHost`)

**Status:** Not started
**Location:** `src\Presentation\DesktopPhotino\PhotinoHost.cs`

## Objective

Create the Photino window host that opens a native window and loads the CSAgent UI.
This is the Photino equivalent of the existing `DesktopHost` / `CsAgentWindow`
in `src\Presentation\Desktop`.

## Context — existing AOTrino host

The current desktop host (`DesktopHost.Run`) does the following:

1. Reads `ALBERT_API_KEY` from the environment (aborts if missing).
2. Loads memory via `MemoryStore.LoadAsync(args.MemoryFile)` and seeds a system
   message if empty.
3. Creates an `AOTrinoApplication`, a `CsAgentWindow`, resizes/centers/shows it,
   and runs the message loop.
4. The window registers a host object `dotnet` (`DesktopAPI`) and loads an inline
   `data:` URI built from embedded HTML/JS/CSS.

## Subtasks

### 2.1 Create `PhotinoHost.Run(AgentArguments args)`

Mirror the existing host flow:

```csharp
public static void Run(AgentArguments args)
{
    var apiKey = Environment.GetEnvironmentVariable("ALBERT_API_KEY") ?? "";
    if (string.IsNullOrEmpty(apiKey))
    {
        Console.WriteLine("Error: ALBERT_API_KEY env var not set.");
        return;
    }

    var messages = Task.Run(() => MemoryStore.LoadAsync(args.MemoryFile)).Result;
    if (messages.Count == 0)
        messages.Add(CodingAgent.SystemMessage(OperatingSystem.IsWindows()));

    var window = new PhotinoWindow()
        .SetTitle("CSAgent Desktop")
        .SetSize(1280, 800)
        .Center()
        .RegisterCustomSchemeHandler("app", (sender, scheme, url, out contentType) =>
        {
            // Serve embedded assets under the "app://" scheme.
            contentType = "text/html";
            return LoadEmbeddedResource("CsAgentUI.src.Presentation.DesktopPhotino.assets.index.html");
        })
        .Load("app://index.html");

    // Register the .NET bridge object so JS can call into it.
    window.RegisterWebMessageReceivedHandler((sender, message) =>
    {
        // Route incoming JS messages to the agent / bridge.
    });

    window.WaitForClose();
}
```

### 2.2 Serve embedded assets via a custom scheme

Use `RegisterCustomSchemeHandler` to serve the Photino `index.html`, `app.js`, and
`styles.css` from embedded resources under a custom scheme (e.g. `app://`). This
avoids needing a local HTTP server and keeps the app self-contained.

- Map `app://index.html` → embedded `index.html`.
- Map `app://app.js` → embedded `app.js`.
- Map `app://styles.css` → embedded `styles.css`.
- Set the correct `contentType` per resource (`text/html`, `application/javascript`,
  `text/css`).

### 2.3 Window configuration

- Title: `CSAgent Desktop`.
- Size: `1280 x 800` (matching the existing AOTrino window).
- Center on screen.
- Optionally set a background color / dark theme to match the existing UI.

### 2.4 Message loop

Use `WaitForClose()` to block until the window is closed (Photino's equivalent of
`app.Run()`).

## Definition of Done

- [ ] `PhotinoHost.Run` exists and mirrors the AOTrino host flow.
- [ ] A native Photino window opens with the CSAgent UI loaded from embedded assets.
- [ ] Embedded assets are served via a custom scheme handler.
- [ ] Window is sized, centered, and titled correctly.
