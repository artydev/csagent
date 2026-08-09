# AOTrino SDK Reference

> A .NET framework for building native Windows applications with WebView2 front ends, backed by Windows.UI.Composition.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Core Types](#core-types)
   - [AOTrinoApplication](#aotrinoapplication)
   - [CompositionApplication](#compositionapplication)
   - [WebViewWindow](#webviewwindow)
   - [CompositionWebViewWindow](#compositionwebviewwindow)
   - [HwndWebViewWindow](#hwndwebviewwindow)
3. [Application Lifecycle](#application-lifecycle)
4. [Window Lifecycle](#window-lifecycle)
5. [WebView2 Integration](#webview2-integration)
6. [Composition Tree](#composition-tree)
7. [Input Forwarding](#input-forwarding)
8. [Host Objects & Scripting](#host-objects--scripting)
9. [Shared Buffers](#shared-buffers)
10. [Navigation & Error Handling](#navigation--error-handling)
11. [Drag & Drop](#drag--drop)
12. [Window Commands (JS ↔ .NET)](#window-commands-js--net)
13. [Tracing & Error Reporting](#tracing--error-reporting)
14. [WebRoot & Embedded Content](#webroot--embedded-content)
15. [Paths & Data](#paths--data)
16. [Security Considerations](#security-considerations)
17. [Customization Points](#customization-points)
18. [Project: AOTrino WebBrowser](#project-aotrino-webbrowser)

---

## Architecture Overview

AOTrino is a **native Windows application framework** that hosts a WebView2 control inside a Windows composition tree. The architecture has three layers:

```
┌─────────────────────────────────────────────────────┐
│                   Your App (.NET)                    │
│  ┌───────────────────────────────────────────────┐  │
│  │            AOTrinoApplication                  │  │
│  │  (CompositionApplication subclass)             │  │
│  │  - Owns Paths, WebRoot, WebView2 runtime       │  │
│  └──────────────┬────────────────────────────────┘  │
│                 │ creates                            │
│  ┌──────────────▼────────────────────────────────┐  │
│  │         WebViewWindow (abstract)               │  │
│  │  - WebView2 environment & controller           │  │
│  │  - Navigation, host objects, scripts           │  │
│  │  - Input forwarding, drag & drop               │  │
│  │  - Window commands (JS ↔ .NET)                 │  │
│  └──────┬─────────────────────┬───────────────────┘  │
│         │                     │                       │
│  ┌──────▼──────────┐  ┌──────▼──────────┐            │
│  │ CompositionWVW  │  │  HwndWebViewWV  │            │
│  │ (Composition)   │  │  (Child HWND)   │            │
│  └─────────────────┘  └─────────────────┘            │
└─────────────────────────────────────────────────────┘
```

### Key Design Decisions

- **No static state**: The SDK exposes no process-global static state. All services are owned by `AOTrinoApplication`.
- **Composition-first**: The primary hosting model uses `Windows.UI.Composition` for the WebView, enabling transforms, animations, and effects.
- **Two hosting models**: Composition (for layered/transparent UIs) and HWND (for classic child-window hosting).
- **Embedded content**: Front-end files are embedded in the assembly and extracted at startup via `WebRoot`.
- **No browser chrome**: Default settings disable browser UI (context menus, status bar, accelerator keys) — the app provides its own.

---

## Core Types

### AOTrinoApplication

The application class. Subclass this (or use it directly) to create an AOTrino app.

```csharp
namespace AOTrino;

public partial class AOTrinoApplication : CompositionApplication
{
    // Constructor
    public AOTrinoApplication(
        Assembly? appAssembly = null,
        string? browserExecutableFolder = null
    );

    // Properties
    public static new AOTrinoApplication? Current { get; }
    public AOTrinoPaths Paths { get; }
    public WebRoot WebRoot { get; }
    public string WebView2Version { get; }
    public string? BrowserExecutableFolder { get; set; }
    public string AOTrinoVersion { get; }

    // Virtual customization points
    protected virtual AOTrinoPaths CreatePaths();
    protected virtual WebRoot CreateWebRoot(Assembly assembly, AOTrinoPaths paths);
    protected virtual void CheckErrorReporting();
    protected virtual void CheckWebView2Runtime(string? version);
    protected virtual void Trace(TraceLevel level, object? message, CallerMemberName string? methodName = null);

    // Trace helpers
    public void TraceInfo(object? message, CallerMemberName string? methodName = null);
    public void TraceWarning(object? message, CallerMemberName string? methodName = null);
    public void TraceError(object? message, CallerMemberName string? methodName = null);
    public void TraceVerbose(object? message, CallerMemberName string? methodName = null);
}
```

#### Constructor Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `appAssembly` | `Assembly?` | The assembly containing the app's embedded resources. Defaults to the entry assembly. |
| `browserExecutableFolder` | `string?` | Path to a fixed-version WebView2 runtime. `null` uses the evergreen runtime. |

#### Key Behaviors

- Installs `WindowSynchronizationContext` so `await` continuations resume on the window message loop.
- Checks for comctl32 v6 (for TaskDialog error reporting).
- Extracts the native WebView2 loader from the AOTrino assembly.
- Validates the WebView2 runtime is installed (exits with code 1 if missing).
- Sets `WEBVIEW2_DEFAULT_BACKGROUND_COLOR` to transparent (`00000000`).
- Starts async extraction of embedded web content via `WebRoot.EnsureFilesAsync()`.

#### Module Initializer

`CheckSupportedWindowsVersion()` runs as a module initializer — before any AOTrino type is accessed — to detect unsupported Windows versions (< 10.0) and show a readable error dialog instead of a cryptic entry-point-not-found crash.

---

### CompositionApplication

The base class that bridges the .NET `Application` with the Windows composition dispatcher.

```csharp
namespace AOTrino;

public partial class CompositionApplication : Application
{
    public CompositionApplication();
    public WindowsDispatcherQueueController DispatcherQueueController { get; }
}
```

- Creates a `WindowsDispatcherQueueController` on the current thread.
- Must be created on a thread that will pump messages (typically the main/UI thread).
- Uses `CreateDispatcherQueueController` from `coremessaging.dll` (Windows 10+).

---

### WebViewWindow

The abstract base class for all WebView2-hosting windows. It owns everything except *how* the WebView is hosted.

```csharp
namespace AOTrino;

public abstract partial class WebViewWindow : D3D11SwapChainWindow
{
    // Constructor
    protected WebViewWindow(
        string? title = null,
        WINDOW_STYLE style = WS_THICKFRAME,
        WINDOW_EX_STYLE extendedStyle = 0,
        RECT? rect = null
    );

    // Properties
    protected ComObject<ICoreWebView2_17>? WebView { get; }
    protected ComObject<ICoreWebView2Environment12>? Environment { get; }
    protected ICoreWebView2Controller? BaseController { get; }
    public HMONITOR MonitorHandle { get; }
    public bool IsFullScreen { get; }
    public virtual bool CanChangeCursor { get; set; }
    public virtual bool SendDoubleClicks { get; set; }

    // Events
    event EventHandler<MouseEventArgs>? MouseMove;
    event EventHandler<MouseEventArgs>? MouseLeave;
    event EventHandler<MouseEventArgs>? MouseHover;
    event EventHandler<MouseWheelEventArgs>? MouseWheel;
    event EventHandler<MouseButtonEventArgs>? MouseButtonDown;
    event EventHandler<MouseButtonEventArgs>? MouseButtonUp;
    event EventHandler<MouseButtonEventArgs>? MouseButtonDoubleClick;
    event EventHandler<PointerActivateEventArgs>? PointerActivate;
    event EventHandler<PointerEnterEventArgs>? PointerEnter;
    event EventHandler<PointerLeaveEventArgs>? PointerLeave;
    event EventHandler<PointerWheelEventArgs>? PointerWheel;
    event EventHandler<PointerPositionEventArgs>? PointerUpdate;
    event EventHandler<PointerContactChangedEventArgs>? PointerContactChanged;
    event EventHandler<KeyEventArgs>? KeyDown;
    event EventHandler<KeyEventArgs>? KeyUp;
    event EventHandler<KeyPressEventArgs>? KeyPress;
    event EventHandler? MonitorChanged;
    event EventHandler<NavigationEventArgs>? NavigationStarting;
    event EventHandler<NavigationEventArgs>? NavigationCompleted;
    event EventHandler<ValueEventArgs<string>>? WebMessageJsonReceived;
    event EventHandler<FileDropEventArgs>? FilesDropped;

    // Methods
    public virtual void Navigate(string url);
    public virtual void NavigateToString(string html);
    public virtual Task NavigateToWebRootAsync();
    public virtual void MaximizeOrRestore();
    public virtual void AddHostObject(string name, DispatchObject hostObject);
    public virtual SharedBuffer CreateSharedBuffer(string name, SharedBufferAccess access = ReadOnly);
    public virtual void AddStartupScript(string script);
    public void AddStartupScriptResource(Assembly assembly, string resourceName);
    public virtual Task<T?> ExecuteScript<T>(string script, JsonTypeInfo<T> typeInfo, bool throwOnError = true);
    public virtual Task<string?> ExecuteScriptAsJson(string script, bool throwOnError = true);
    public virtual HRESULT ExecuteScript(string script, bool throwOnError = true);
    public virtual void BeginDrag();
    public void SetSystemBackdrop(DWM_SYSTEMBACKDROP_TYPE type);

    // Abstract — must be implemented by subclass
    protected abstract void CreateController(ICoreWebView2Environment12 environment, Action onControllerReady);

    // Virtual — override to customize
    protected virtual void ForwardMouseInput(...);
    protected virtual bool TryForwardPointerInput(uint msg, WPARAM wParam, LPARAM lParam);
    protected virtual void ConfigureSettings(ICoreWebView2Settings settings);
    protected virtual string? GetBrowserExecutableFolder();
    protected virtual RECT? GetCaptionRect();
    protected virtual void ControllerCreated();
    protected virtual CoreWebView2EnvironmentOptions? GetEnvironmentOptions();
    protected virtual bool IsAppContentUri(string uri);
    protected virtual string GetNavigationErrorPage(NavigationEventArgs e);
    protected virtual void OnPageError(string message, string? stack);
    protected virtual void SetWindowTitleFromPage(string? title);
    protected virtual string GetSystemJson();
    protected virtual string GetWindowJson();
    protected virtual bool AcceptsFileDrops { get; }
    protected internal virtual DROPEFFECT GetFileDropEffect(DROPEFFECT allowedEffects);
    protected internal virtual void OnFilesDropped(FileDropEventArgs e);
    protected virtual bool AreDefaultContextMenusEnabled { get; }
    protected virtual bool IsStatusBarEnabled { get; }
    protected virtual bool AreDevToolsEnabled { get; }
    protected virtual bool AreBrowserAcceleratorKeysEnabled { get; }
    protected virtual bool IsBuiltInErrorPageEnabled { get; }
    protected virtual bool ReplacesNavigationErrorPage { get; }
}
```

#### Virtual Property Defaults

| Property | Default | Description |
|----------|---------|-------------|
| `AreDefaultContextMenusEnabled` | `false` | Right-click menu with Back, Reload, Save as, View source |
| `IsStatusBarEnabled` | `false` | Link target tooltip strip |
| `AreDevToolsEnabled` | `true` (Debug) / `false` (Release) | F12 DevTools |
| `AreBrowserAcceleratorKeysEnabled` | `false` | Ctrl+R, F5, Ctrl+P, Ctrl+F, etc. |
| `IsBuiltInErrorPageEnabled` | `false` | Edge's own failure page |
| `ReplacesNavigationErrorPage` | `true` | AOTrino's app-aware error page |
| `AcceptsFileDrops` | `false` | Explorer file drop target |
| `CanChangeCursor` | `true` | Allow WebView to change cursor |
| `SendDoubleClicks` | `false` | Forward double-click events |

---

### CompositionWebViewWindow

Hosts the WebView as a visual in a `Windows.UI.Composition` tree. The window uses `WS_EX_NOREDIRECTIONBITMAP`, so the WebView composes with other visuals and can be transformed/animated.

```csharp
namespace AOTrino;

public partial class CompositionWebViewWindow : WebViewWindow, IDropTarget
{
    public CompositionWebViewWindow(
        string? title = null,
        WINDOW_STYLE style = WS_THICKFRAME,
        WINDOW_EX_STYLE extendedStyle = WS_EX_NOREDIRECTIONBITMAP,
        RECT? rect = null
    );

    // Properties
    public CompositorController CompositorController { get; }
    public SpriteVisual RootVisual { get; }
    public Compositor Compositor { get; }
    public CompositionGraphicsDevice? GraphicsDevice { get; }
    public IComObject<ID2D1Device>? D2D1Device { get; }
    public bool IsDropTarget { get; set; }

    // Virtual customization
    protected virtual bool TopMostDesktopWindowTarget { get; }  // default: true
    protected virtual bool UseDirect2D { get; }                 // default: true
    protected virtual SpriteVisual CreateWindowVisual();
    protected virtual Visual WebViewVisualTarget { get; }       // default: RootVisual

    // Drag & drop overrides
    protected virtual void OnAfterDragEnter(...);
    protected virtual HRESULT OnBeforeDragEnter(...);
    protected virtual void OnAfterDragOver(...);
    protected virtual HRESULT OnBeforeDragOver(...);
    protected virtual void OnAfterDragLeave(...);
    protected virtual HRESULT OnBeforeDragLeave(...);
    protected virtual void OnAfterDrop(...);
    protected virtual HRESULT OnBeforeDrop(...);
}
```

#### Key Features

- **Composition tree**: The WebView is one visual among many. Add your own `SpriteVisual` children to `RootVisual` for overlays, effects, animations.
- **No OS input**: Composition-hosted WebViews receive no OS input. Input is forwarded via `SendMouseInput` and `SendPointerInput`.
- **Direct2D support**: When `UseDirect2D` is true (default), creates a D2D device for composition drawing surfaces.
- **Cursor forwarding**: Listens to `CursorChanged` events from the WebView and updates the window cursor.
- **Drag & drop**: Implements `IDropTarget` for OLE drag-and-drop, with before/after hooks for each event.

---

### HwndWebViewWindow

*(Not shown in the provided files, but referenced in comments.)*

Hosts the WebView as a classic child HWND. Input arrives directly from Windows — no input forwarding needed. Uses a standard window style without `WS_EX_NOREDIRECTIONBITMAP`.

---

## Application Lifecycle

```
1. Module Initializer (CheckSupportedWindowsVersion)
   └── Verifies Windows >= 10.0
       └── Exits with code 2 if unsupported

2. AOTrinoApplication Constructor
   ├── Install WindowSynchronizationContext
   ├── CheckErrorReporting (comctl32 v6)
   ├── Set BrowserExecutableFolder
   ├── Create Paths
   ├── Create WebRoot
   ├── Initialize WebView2 native loader
   ├── Check WebView2 runtime (exits with code 1 if missing)
   ├── Set WEBVIEW2_DEFAULT_BACKGROUND_COLOR
   └── Start WebRoot.EnsureFilesAsync()

3. Create Window(s)
   ├── WebViewWindow constructor
   │   ├── Create environment (CreateCoreWebView2EnvironmentWithOptions)
   │   ├── CreateController (abstract — subclass decides composition vs HWND)
   │   ├── Wire navigation events
   │   ├── Wire controller events (accelerator keys)
   │   ├── ApplySettings
   │   ├── Register file drops
   │   └── ControllerCreated (override point)
   └── Navigate to content

4. Message Loop
   ├── WindowProc handles input, sizing, DWM
   ├── WebView renders itself (no swap chain ticking)
   └── Composition commits

5. Shutdown
   └── Dispose windows → Dispose controller → Dispose environment
```

---

## Window Lifecycle

### Construction

1. Base `D3D11SwapChainWindow` creates the native HWND.
2. `WebViewWindow` constructor:
   - Sets `InvalidateOnTick = false` (WebView self-renders).
   - Gets the monitor handle.
   - Sets window corner preference (round or donotround).
   - Creates the WebView2 environment.
   - Calls `CreateController` (abstract).
3. Subclass (`CompositionWebViewWindow`) constructor:
   - Creates `CompositorController`.
   - Creates desktop window target.
   - Creates `RootVisual` and sets it as the composition target.

### Controller Creation (CompositionWebViewWindow)

1. `environment.CreateCoreWebView2CompositionController(Handle, callback)`
2. In callback:
   - Store `ICoreWebView2CompositionController`.
   - Get `ICoreWebView2CompositionController3` for drag-and-drop.
   - Subscribe to `CursorChanged`.
   - Set `RootVisualTarget` to `WebViewVisualTarget`.
   - Set bounds to `ClientRect`.
   - Get `ICoreWebView2` and call `SetWebViewController`.
   - Invoke `onControllerReady`.

### Sizing

- `OnResized` → `SetVisualSize()` (composition) + `_baseController.put_Bounds(ClientRect)`.
- `OnMoving` / `OnMoved` → `NotifyParentWindowPositionChanged()`.

### Focus

- `OnFocusChanged(true)` → `_baseController.MoveFocus(Programmatic)`.

### Disposal

1. `DetachController()` (stops routing bounds/focus to controller).
2. Remove event tokens.
3. Dispose controller, controller3, compositor controller, root visual, D2D device.
4. Base disposal.

---

## WebView2 Integration

### Environment Creation

```csharp
WebView2.Functions.CreateCoreWebView2EnvironmentWithOptions(
    browserFolder,          // null for evergreen, path for fixed version
    userDataPath,           // from AOTrinoApplication.Current?.Paths.WebView2UserDataPath
    options,                // from GetEnvironmentOptions()
    completedHandler
);
```

### Runtime Version Check

In `AOTrinoApplication` constructor:
```csharp
var version = WebView2Utilities.GetAvailableCoreWebView2BrowserVersionString(runtimeFolder);
CheckWebView2Runtime(version);
```

If the runtime is missing, `CheckWebView2Runtime` shows a task dialog with a download link and exits with code 1.

### Fixed Version (Pinned Runtime)

Set `browserExecutableFolder` in the constructor to point at a specific WebView2 runtime distribution. This pins the browser engine so it cannot change under the app. Trade-offs:
- **Pro**: Stable rendering engine, no surprises from updates.
- **Con**: ~150 MB per architecture to ship, security updates are your responsibility.

### Settings

Applied in `ApplySettings()`:

| Setting | Source |
|---------|--------|
| `AreDefaultContextMenusEnabled` | Virtual property |
| `IsStatusBarEnabled` | Virtual property |
| `AreDevToolsEnabled` | Virtual property |
| `IsBuiltInErrorPageEnabled` | Virtual property |
| `AreBrowserAcceleratorKeysEnabled` | Virtual property (via `ICoreWebView2Settings3`) |
| Everything else | `ConfigureSettings(ICoreWebView2Settings)` override |

---

## Composition Tree

### CompositionWebViewWindow Structure

```
CompositorController
  └── Compositor
       └── DesktopWindowTarget (on the HWND)
            └── RootVisual (SpriteVisual)
                 └── WebView Visual (set via RootVisualTarget)
                      └── WebView2 content
```

### Customizing the Visual Tree

Override `WebViewVisualTarget` to host the WebView in a child visual:

```csharp
protected override Visual WebViewVisualTarget
{
    get
    {
        var child = Compositor.CreateSpriteVisual();
        child.Size = new Vector2(800, 600);
        child.Offset = new Vector3(100, 50, 0);
        RootVisual.Children.InsertAtTop(child);
        return child;
    }
}
```

### Adding Your Own Visuals

```csharp
var overlay = Compositor.CreateSpriteVisual();
overlay.Size = new Vector2(200, 200);
overlay.Brush = Compositor.CreateColorBrush(Color.FromArgb(128, 255, 0, 0));
RootVisual.Children.InsertAtTop(overlay);
```

### Direct2D Integration

When `UseDirect2D` is true (default):
- Creates `ID2D1Device` from the D3D device.
- Creates `CompositionGraphicsDevice` for `ICompositionDrawingSurfaceInterop.BeginDraw`.
- Enables drawing on composition surfaces from .NET.

---

## Input Forwarding

### Why Forwarding Is Needed

A composition-hosted WebView (`CompositionWebViewWindow`) receives **no OS input** because it has no HWND of its own. All mouse, pointer, and keyboard input must be forwarded programmatically.

### Mouse Input

Forwarded via `ICoreWebView2CompositionController.SendMouseInput`:

```csharp
// In WebViewWindow.WindowProc:
case WM_MOUSEMOVE:
    ForwardMouseInput(MOVE, keys, 0, point);
    break;

case WM_LBUTTONDOWN:
    ForwardMouseInput(LEFT_BUTTON_DOWN, keys, 0, point);
    break;
```

The `ForwardMouseInput` virtual is a no-op in `HwndWebViewWindow` (the child HWND receives input directly).

### Pointer Input

Forwarded via `ICoreWebView2ExperimentalCompositionController4`:

```csharp
// In WebViewWindow.WindowProc:
case WM_POINTERDOWN:
    if (TryForwardPointerInput(msg, wParam, lParam))
        return 0;
    break;
```

`TryForwardPointerInput`:
1. Tracks pointer IDs that started in the WebView.
2. Creates `ICoreWebView2PointerInfo` from the pointer ID.
3. Calls `SendPointerInput`.

### Keyboard Input

- When the **host window** has focus: `WM_KEYDOWN`/`WM_KEYUP` → `OnKeyDown`/`OnKeyUp`.
- When the **WebView** has focus: `AcceleratorKeyPressed` event → `TryHandleShortcut`.
- Both paths call `TryHandleShortcut`, so F11/F12 work regardless of focus.

### Shortcut Handling

```csharp
protected virtual bool TryHandleShortcut(VIRTUAL_KEY key)
{
    if (key == VK_F12) return TryOpenDevTools();
    if (key == VK_F11 && ForcesGarbageCollectionOnF11)
    {
        GC.Collect(); // forced full collection
        return true;
    }
    return false;
}
```

---

## Host Objects & Scripting

### Adding Host Objects

```csharp
public virtual void AddHostObject(string name, DispatchObject hostObject)
```

Registers a .NET object as `chrome.webview.hostObjects.<name>` (async) and `chrome.webview.hostObjects.sync.<name>` (sync) in JavaScript.

**Usage:**
```csharp
public class MyHostObject : DispatchObject
{
    public string GetData() => "Hello from .NET!";
}

// In ControllerCreated:
window.AddHostObject("myApi", new MyHostObject());
```

**In JavaScript:**
```javascript
const data = await chrome.webview.hostObjects.myApi.getData();
// or synchronously:
const data = chrome.webview.hostObjects.sync.myApi.getData();
```

### Host Object Helper

The SDK attempts to enable full `Task`/`Task<T>` support for host objects via `ICoreWebView2PrivatePartial.AddHostObjectHelper`. This is best-effort (uses undocumented interfaces).

### Startup Scripts

```csharp
// From a string:
window.AddStartupScript("window.myApp = { version: '1.0' };");

// From an embedded resource:
window.AddStartupScriptResource(typeof(MyWindow).Assembly, "MyScript.js");
```

Startup scripts run on every new document AND immediately on the current one.

### Executing Scripts

```csharp
// Fire-and-forget:
window.ExecuteScript("console.log('hello');");

// With typed result:
var result = await window.ExecuteScript<MyType>("getData()", MyTypeContext.Default.MyType);

// Raw JSON result:
var json = await window.ExecuteScriptAsJson("getData()");
```

### Web Messages

Messages from the page arrive via `WebMessageJsonReceived` event. The page posts them using `window.__aotrino.post()` or `chrome.webview.postMessage()`.

The SDK intercepts messages to handle:
- **Window commands** (`__aotrino: "window-command"`)
- **Page errors** (`__aotrino: "page-error"`)

---

## Shared Buffers

A shared-memory channel between .NET and JavaScript for efficient byte transport.

```csharp
public virtual SharedBuffer CreateSharedBuffer(string name, SharedBufferAccess access = ReadOnly)
```

### .NET Side

```csharp
var buffer = window.CreateSharedBuffer("myBuffer", SharedBufferAccess.ReadWrite);
// Write to buffer.Pointer
// Signal the page
```

### JavaScript Side

```javascript
const buffer = await window.__aotrino.getBuffer('myBuffer');
// Read from buffer
```

### Use Cases

- **Graphics**: AOTrino.Graphics uses shared buffers for Direct2D → WebGL surface transfer.
- **Large data**: Efficiently pass binary data without JSON serialization overhead.
- **Real-time**: Low-latency data streaming.

---

## Navigation & Error Handling

### Navigation Events

```csharp
window.NavigationStarting += (s, e) =>
{
    if (!e.Uri.StartsWith("https://myapp.com/"))
        e.Cancel = true; // Block external navigation
};

window.NavigationCompleted += (s, e) =>
{
    if (e.IsSuccess)
        Console.WriteLine($"Loaded: {e.Uri}");
};
```

### Navigation Error Page

When a navigation fails and `ReplacesNavigationErrorPage` is true (default), the SDK shows an app-aware error page instead of Edge's default.

The error page:
- Distinguishes between **app content** (local files) and **web content**.
- For app content: explains that embedded files might be missing, build configuration might be wrong.
- For web content: explains network issues.
- Shows the URI, error status, and version info.

Override `GetNavigationErrorPage` to customize:
```csharp
protected override string GetNavigationErrorPage(NavigationEventArgs e)
{
    return "<html><body><h1>Custom Error</h1></body></html>";
}
```

### Page Errors (JavaScript)

Unhandled JavaScript errors are forwarded to .NET:

```csharp
protected override void OnPageError(string message, string? stack)
{
    // Log, show dialog, or ignore
    TraceError($"Page error: {message}");
}
```

---

## Drag & Drop

### File Drops (Explorer → Window)

Enable with `AcceptsFileDrops = true`:

```csharp
protected override bool AcceptsFileDrops => true;

protected internal override void OnFilesDropped(FileDropEventArgs e)
{
    foreach (var file in e.Files)
    {
        Console.WriteLine($"Dropped: {file}");
    }
}
```

Or subscribe to the event:
```csharp
window.FilesDropped += (s, e) => { /* handle files */ };
```

### OLE Drag & Drop (CompositionWebViewWindow)

The composition window implements `IDropTarget` and forwards drag events to the WebView via `ICoreWebView2CompositionController3`.

Override hooks for each phase:

```csharp
protected override HRESULT OnBeforeDragEnter(IDataObject dataObject,
    MODIFIERKEYS_FLAGS flags, POINTL point, ref DROPEFFECT effect, out bool handled)
{
    // Inspect dataObject, modify effect, or handle entirely
    handled = false;
    return S_OK;
}
```

### Drop Effect

Override `GetFileDropEffect` to control what the cursor shows:

```csharp
protected internal override DROPEFFECT GetFileDropEffect(DROPEFFECT allowedEffects)
{
    // Only allow copy, not move
    return allowedEffects.HasFlag(DROPEFFECT_COPY) ? DROPEFFECT_COPY : DROPEFFECT_NONE;
}
```

---

## Window Commands (JS ↔ .NET)

The SDK injects a runtime that enables JavaScript to control the window.

### JS → .NET Commands

The page posts JSON with `__aotrino: "window-command"`:

```javascript
// Drag the window (from a custom title bar)
window.__aotrino.dragWindow();

// Close the window
window.__aotrino.closeWindow();

// Minimize
window.__aotrino.minimizeWindow();

// Maximize / Restore
window.__aotrino.maximizeWindow();

// Set window title
window.__aotrino.setWindowTitle("My App");
```

### .NET → JS State

The SDK injects system and window state:

```javascript
// System info (injected at page load)
console.log(window.__aotrino.system.doubleClickTimeMs); // e.g., 500

// Window info
console.log(window.__aotrino.window.title); // Current window title
```

### Customizing Window JSON

Override `GetSystemJson()` and `GetWindowJson()` to inject additional state:

```csharp
protected override string GetSystemJson() =>
    $$"""{"doubleClickTimeMs":{{DirectNFunctions.GetDoubleClickTime()}},"theme":"dark"}""";

protected override string GetWindowJson() =>
    $$"""{"title":{{JsonSerializer.Serialize(Text)}},"id":"{{WindowId}}"}""";
```

### Title Management

Override `SetWindowTitleFromPage` to control title changes:

```csharp
protected override void SetWindowTitleFromPage(string? title)
{
    // Decorate the title
    Text = $"{title} — My App";
}
```

---

## Tracing & Error Reporting

### Tracing

```csharp
// From anywhere:
AOTrinoApplication.Current?.TraceInfo("App started");
AOTrinoApplication.Current?.TraceWarning("Something unusual");
AOTrinoApplication.Current?.TraceError("Something failed");
AOTrinoApplication.Current?.TraceVerbose("Detailed debug info");
```

Override `Trace` to redirect logs:

```csharp
protected override void Trace(TraceLevel level, object? message, string? methodName)
{
    File.AppendAllText("app.log", $"[{level}] {message}");
}
```

### Error Reporting

The SDK uses **TaskDialog** for error reporting when comctl32 v6 is available (via app manifest). Falls back to `MessageBox` otherwise.

Override `CheckErrorReporting` to customize:
```csharp
protected override void CheckErrorReporting()
{
    // Always use custom error display
    ShowFatalErrorFunc = MyCustomErrorDisplay;
}
```

### WebView2 Runtime Missing

`CheckWebView2Runtime` shows a dialog with a download link. Override to handle differently:

```csharp
protected override void CheckWebView2Runtime(string? version)
{
    if (!string.IsNullOrWhiteSpace(version)) return;

    // Download silently, or show custom UI
    DownloadAndInstallRuntime();
}
```

---

## WebRoot & Embedded Content

### How It Works

1. Front-end files (HTML, JS, CSS, images) are embedded as assembly resources.
2. At startup, `WebRoot.EnsureFilesAsync()` extracts them to a local folder.
3. The window navigates to the extracted `index.html` via `NavigateToWebRootAsync()`.

### Build Integration

In your `.csproj`:
```xml
<PropertyGroup>
  <AOTrinoEmbedWebRoot>true</AOTrinoEmbedWebRoot>
  <AOTrinoBlazorProject>path/to/blazor/project</AOTrinoBlazorProject>
</PropertyGroup>
```

### Custom WebRoot

Override `CreateWebRoot` to customize content extraction:

```csharp
protected override WebRoot CreateWebRoot(Assembly assembly, AOTrinoPaths paths)
{
    return new CustomWebRoot(assembly, paths);
}
```

---

## Paths & Data

### AOTrinoPaths

Provides standard paths for the application:

| Path | Purpose |
|------|---------|
| `AppTitle` | Application display name |
| `WebView2UserDataPath` | WebView2 user data folder |
| (others) | Application data, cache, etc. |

Override `CreatePaths` to customize:

```csharp
protected override AOTrinoPaths CreatePaths()
{
    return new AOTrinoPaths(Assembly.GetEntryAssembly()!)
    {
        // Customize paths here
    };
}
```

---

## Security Considerations

### WebView2 Runtime

- **Evergreen**: Auto-updates with Edge. Security patches arrive automatically.
- **Fixed Version**: You control when to update. You are responsible for security patches.
- See `docs/SECURITY.md` for detailed guidance.

### Host Objects

- Host objects are callable from JavaScript. Only expose what the page needs.
- Use `DispatchObject` subclasses with explicit methods.
- Consider input validation on all host object methods.

### Navigation

- Default settings disable browser navigation features (reload, back/forward).
- Override `NavigationStarting` to enforce URI allowlists.
- `IsAppContentUri` distinguishes local content from web content.

### Content Security

- Embedded content is extracted to a local folder. Ensure the extraction path is not world-writable.
- The `WEBVIEW2_DEFAULT_BACKGROUND_COLOR` is transparent — ensure your page paints its own background.

---

## Customization Points

### AOTrinoApplication

| Method/Property | Purpose |
|----------------|---------|
| `CreatePaths()` | Customize application paths |
| `CreateWebRoot()` | Customize content extraction |
| `CheckErrorReporting()` | Customize error dialog setup |
| `CheckWebView2Runtime()` | Customize missing runtime handling |
| `Trace()` | Redirect all tracing |
| `BrowserExecutableFolder` | Set fixed-version runtime path |

### WebViewWindow

| Method/Property | Purpose |
|----------------|---------|
| `CreateController()` | **Must implement** — hosting model |
| `ConfigureSettings()` | Additional WebView2 settings |
| `GetBrowserExecutableFolder()` | Per-window runtime choice |
| `GetCaptionRect()` | Custom caption area for dragging |
| `ControllerCreated()` | Post-initialization hook |
| `GetNavigationErrorPage()` | Custom error page HTML |
| `OnPageError()` | Handle JS errors |
| `SetWindowTitleFromPage()` | Control title changes |
| `GetSystemJson()` / `GetWindowJson()` | Inject state to page |
| `AcceptsFileDrops` | Enable file drag-and-drop |
| `AreDefaultContextMenusEnabled` | Show/hide right-click menu |
| `AreDevToolsEnabled` | Enable/disable F12 tools |
| `AreBrowserAcceleratorKeysEnabled` | Enable/disable browser shortcuts |
| `ReplacesNavigationErrorPage` | Use custom error page |

### CompositionWebViewWindow

| Method/Property | Purpose |
|----------------|---------|
| `WebViewVisualTarget` | Where in the composition tree the WebView renders |
| `UseDirect2D` | Enable Direct2D composition surfaces |
| `TopMostDesktopWindowTarget` | Window target Z-order |
| `CreateWindowVisual()` | Custom root visual creation |
| `IsDropTarget` | Enable OLE drag-and-drop |
| `OnBeforeDrag*` / `OnAfterDrag*` | Drag-and-drop hooks |

---

## Example: Minimal App

```csharp
using AOTrino;

class MyApp : AOTrinoApplication
{
    static void Main()
    {
        using var app = new MyApp();
        using var window = new MyWindow();
        window.Show();
        Application.Run();
    }
}

class MyWindow : CompositionWebViewWindow
{
    public MyWindow() : base(title: "My AOTrino App")
    {
        // Configure after construction
    }

    protected override void ControllerCreated()
    {
        _ = NavigateToWebRootAsync();
    }
}
```

## Example: Custom Host Object

```csharp
class FileApi : DispatchObject
{
    public string ReadFile(string path)
    {
        return File.ReadAllText(path);
    }
}

class MyWindow : CompositionWebViewWindow
{
    protected override void ControllerCreated()
    {
        AddHostObject("fileApi", new FileApi());
        _ = NavigateToWebRootAsync();
    }
}
```

```javascript
// In your page:
const content = await chrome.webview.hostObjects.fileApi.readFile('data.json');
```

---

## Project: AOTrino WebBrowser

The **AOTrino WebBrowser** is a concrete presentation project that uses the AOTrino SDK to host the CSAgent web UI in a native Windows window. It lives at `src/Presentation/WebBrowser/`.

### Architecture

```
┌──────────────────────────────────────────────────────────┐
│                   CsAgentUI Process                       │
│                                                           │
│  ┌──────────────────────────────────────────────────┐    │
│  │            WebBrowserHost.Run()                   │    │
│  │                                                   │    │
│  │  1. Start ASP.NET server (random port)            │    │
│  │  2. Create AOTrino window                         │    │
│  │  3. Navigate to http://localhost:{port}            │    │
│  │  4. Pump messages until window closes             │    │
│  │  5. Shutdown server                               │    │
│  └──────────────────────┬───────────────────────────┘    │
│                         │                                 │
│  ┌──────────────────────▼───────────────────────────┐    │
│  │              CsAgentNativeWindow                    │    │
│  │  (AOTrinoWindow subclass)                          │    │
│  │  - CompositionWebViewWindow                        │    │
│  │  - Hosts WebView2 with the web UI                  │    │
│  │  - Registers CsAgentHostObject                     │    │
│  │  - Enforces local-only navigation                  │    │
│  └──────────────────────┬───────────────────────────┘    │
│                         │                                 │
│  ┌──────────────────────▼───────────────────────────┐    │
│  │              CsAgentHostObject                     │    │
│  │  (DispatchObject subclass)                        │    │
│  │  - Exposed as chrome.webview.hostObjects.csAgent  │    │
│  │  - GetVersion(), GetSystemInfo(), IsDryRun()      │    │
│  └──────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────┘
```

### Key Components

#### `WebBrowserHost` (static entry point)

```csharp
public static class WebBrowserHost
{
    public static void Run(AgentArguments args);
}
```

Responsibilities:
1. Finds an available TCP port.
2. Starts the ASP.NET server (same endpoints as `--ui` mode) on that port.
3. Waits for the server to respond.
4. Creates the `CsAgentNativeApp` (AOTrino application) and `CsAgentNativeWindow`.
5. Pumps the Windows message loop until the window closes.
6. Cancels the server task and shuts down.

#### `CsAgentNativeApp`

```csharp
[GeneratedComClass]
public partial class CsAgentNativeApp : AOTrino.AOTrinoApplication
{
    public CsAgentNativeApp();
}
```

A minimal AOTrino application subclass. Uses the entry assembly for embedded resources and paths.

#### `CsAgentNativeWindow`

```csharp
[GeneratedComClass]
public partial class CsAgentNativeWindow : AOTrino.AOTrinoWindow
{
    public CsAgentNativeWindow(string serverUrl, AgentArguments args);

    protected override string? StartUrl => _serverUrl;
    protected override void RegisterHostObjects();
    protected override bool IsNavigationAllowed(Uri uri);
    protected override void OpenExternal(Uri uri);
    protected override void SetWindowTitleFromPage(string? title);
}
```

Key behaviors:
- **StartUrl**: Points to the local ASP.NET server (`http://localhost:{port}`).
- **Navigation**: Only allows `localhost`, `about:`, `data:`, and `blob:` URIs. Everything else is blocked and opened in the default browser.
- **Title**: Prefixes page titles with "CSAgent — ".
- **Window size**: 1280×800, centered on screen.
- **Host objects**: Registers `CsAgentHostObject` as `chrome.webview.hostObjects.csAgent`.

#### `CsAgentHostObject`

```csharp
[GeneratedComClass]
public partial class CsAgentHostObject : DispatchObject
{
    public CsAgentHostObject(AgentArguments args);

    public string GetVersion();
    public string GetMemoryFile();
    public string GetModelOverride();
    public bool IsDryRun();
    public string GetSystemInfo();
}
```

Exposes agent configuration to JavaScript:

| Method | Returns | Description |
|--------|---------|-------------|
| `GetVersion()` | `string` | CSAgent version (e.g. "0.4.0") |
| `GetMemoryFile()` | `string` | Path to the memory/conversation file |
| `GetModelOverride()` | `string` | Model override, or empty string |
| `IsDryRun()` | `bool` | Whether dry-run mode is active |
| `GetSystemInfo()` | `string` | JSON blob with OS, version, dry-run, memory file, model |

### JavaScript Usage

```javascript
// Get agent version
const version = await chrome.webview.hostObjects.csAgent.getVersion();

// Check if dry-run mode is active
const isDryRun = await chrome.webview.hostObjects.csAgent.isDryRun();

// Get full system info
const info = JSON.parse(await chrome.webview.hostObjects.csAgent.getSystemInfo());
console.log(info.os, info.version);
```

### CLI Integration

The `--native` flag activates the WebBrowser host:

```
csagent --native                    # Default port, native window
csagent --native --model gpt-4o     # Custom model
csagent --native --mem my_history.json  # Custom memory file
csagent --native --dry-run          # Dry-run mode
```

### Build Configuration

The `.csproj` must target `net10.0-windows10.0.19041.0` (or later) for AOTrino compatibility:

```xml
<TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
```

The AOTrino SDK is referenced as a source dependency (not NuGet). The `src/Presentation/WebBrowser/WebBrowserHost.cs` file is compiled as part of the project.

### Dependencies

- **AOTrino SDK**: Source dependency (provides `AOTrinoApplication`, `AOTrinoWindow`, `CompositionWebViewWindow`, `DispatchObject`).
- **ASP.NET Core**: For the local HTTP server (already a project dependency via `Microsoft.NET.Sdk.Web`).
- **Windows SDK**: `net10.0-windows10.0.19041.0` TFM provides access to `Windows.UI.Composition`.

### Lifecycle

```
User runs: csagent --native
  │
  ├── ArgumentParser parses --native → IsNativeMode = true
  │
  ├── Program.Main() calls WebBrowserHost.Run(args)
  │
  ├── WebBrowserHost.Run():
  │   ├── Find available TCP port
  │   ├── Start ASP.NET server (background task)
  │   ├── Wait for server to respond
  │   ├── Create CsAgentNativeApp
  │   │   └── AOTrinoApplication constructor:
  │   │       ├── Install WindowSynchronizationContext
  │   │       ├── Check WebView2 runtime
  │   │       └── Extract embedded resources
  │   ├── Create CsAgentNativeWindow(serverUrl, args)
  │   │   └── AOTrinoWindow constructor:
  │   │       ├── Create native HWND (1280×800, centered)
  │   │       ├── Create WebView2 environment
  │   │       ├── Create composition controller
  │   │       └── ControllerCreated():
  │   │           ├── Register CsAgentHostObject
  │   │           └── Navigate to http://localhost:{port}
  │   ├── window.Show()
  │   └── Application.Run()  ← message loop
  │
  ├── User interacts with the native window
  │   ├── WebView2 renders the web UI
  │   ├── JS calls host objects via chrome.webview.hostObjects.csAgent
  │   └── SSE events stream from ASP.NET server
  │
  └── Window closes:
      ├── Application.Run() returns
      ├── Cancel server CancellationTokenSource
      └── Process exits
```

### Error Handling

- **WebView2 runtime missing**: The AOTrino application shows a download dialog and exits with code 1.
- **Server fails to start**: The `WaitForServer` method times out after 10 seconds; the window navigates to an unreachable URL and shows the AOTrino error page.
- **Navigation blocked**: External links are silently opened in the default browser.
- **JavaScript errors**: Forwarded to `OnPageError` and traced via `AOTrinoApplication.TraceError`.

### Extending the WebBrowser Host

To add more host object methods:

```csharp
[GeneratedComClass]
public partial class CsAgentHostObject : DispatchObject
{
    // Add your method
    public string GetCustomData()
    {
        return "your data here";
    }
}
```

To customize the window appearance:

```csharp
public partial class CsAgentNativeWindow : AOTrino.AOTrinoWindow
{
    // Override to set a custom background color
    protected override void ConfigureSettings(ICoreWebView2Settings settings)
    {
        // Custom settings
    }

    // Override to add startup scripts
    protected override void ControllerCreated()
    {
        AddStartupScript("window.myCustomVar = 42;");
        base.ControllerCreated();
    }
}
```

---

*This reference covers the AOTrino SDK as of the current version. For the most up-to-date information, consult the source code and inline documentation.*
