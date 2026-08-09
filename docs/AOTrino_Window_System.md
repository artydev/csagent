# AOTrino Window System

## Overview

AOTrino's window system is a three-layer hierarchy that hosts a WebView2 control inside a native Windows window. Each layer adds specific capabilities:

```
D3D11SwapChainWindow
    └── WebViewWindow (abstract)
            ├── CompositionWebViewWindow (abstract)
            │       └── AOTrinoWindow (concrete)
            └── HwndWebViewWindow (abstract)
```

- **D3D11SwapChainWindow** – Base window with Direct3D 11 swap chain, input handling, and window management.
- **WebViewWindow** – Hosting-agnostic WebView2 window. Owns the environment, WebView, navigation, host objects, scripts, shared-buffer transport, and all window/input plumbing. *Abstract* – does not decide *how* the WebView is hosted.
- **CompositionWebViewWindow** – Hosts the WebView as one visual in a `Windows.UI.Composition` tree (`ICoreWebView2CompositionController` + `RootVisualTarget`). Uses a `WS_EX_NOREDIRECTIONBITMAP` composition window so the WebView composes with other visuals and can be transformed/animated/effected. Forwards mouse/pointer input because a composition-hosted WebView receives no OS input.
- **HwndWebViewWindow** – Hosts the WebView as a classic child HWND window. Receives OS input directly.
- **AOTrinoWindow** – The concrete window most apps derive from. Adds navigation mode, virtual host mapping, WebRoot extraction, and the `__aotrino` shared runtime.

---

## Class Hierarchy Details

### D3D11SwapChainWindow (not shown in provided files)

The ultimate base class providing:
- Win32 window creation and message loop integration
- Direct3D 11 device and swap chain management
- Input event dispatching (mouse, keyboard, pointer)
- Window sizing, moving, and DPI handling
- `InvalidateOnTick` flag (set to `false` by `WebViewWindow` since the WebView renders itself)

### WebViewWindow (abstract)

**File:** `tmp_wvw.cs`

The central class that owns everything WebView2-related except the hosting model.

#### Key Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `WebView` | `ComObject<ICoreWebView2_17>?` | (protected) | The core WebView2 control |
| `Environment` | `ComObject<ICoreWebView2Environment12>?` | (protected) | The WebView2 environment |
| `BaseController` | `ICoreWebView2Controller?` | (protected) | The controller (composition or HWND) |
| `MonitorHandle` | `HMONITOR` | | Current monitor handle |
| `IsFullScreen` | `bool` | | Whether the window occupies the full monitor |
| `CanChangeCursor` | `bool` | `true` | Whether the WebView may change the cursor |
| `SendDoubleClicks` | `bool` | `false` | Whether double-click events are forwarded |
| `AcceptsFileDrops` | `bool` | `false` | Whether Explorer may drop files on this window |

#### Behaviour Virtuals (Browser vs App)

These properties control which browser behaviours are enabled. The defaults are set for an **app window** (minimal browser chrome):

| Virtual | Default | Description |
|---|---|---|
| `AreDefaultContextMenusEnabled` | `false` | Right-click menu (Back, Reload, Save as, View source) |
| `IsStatusBarEnabled` | `false` | Link target strip at bottom-left |
| `AreDevToolsEnabled` | `true` (Debug) / `false` (Release) | F12 DevTools |
| `AreBrowserAcceleratorKeysEnabled` | `false` | Ctrl+R/F5 reload, Ctrl+P print, Ctrl+F find |
| `IsBuiltInErrorPageEnabled` | `false` | Browser's own failure page |
| `ReplacesNavigationErrorPage` | `true` | Replace failure page with AOTrino's own |

#### Abstract Methods (Hosting Model)

| Method | Description |
|---|---|
| `CreateController(ICoreWebView2Environment12, Action)` | Create the WebView2 controller. Must call `SetWebViewController` then invoke the callback. |
| `ForwardMouseInput(...)` | Forward mouse events to composition-hosted WebView (no-op for HWND) |
| `TryForwardPointerInput(...)` | Forward pointer events to composition-hosted WebView (returns `false` for HWND) |

#### Key Methods

| Method | Description |
|---|---|
| `Navigate(string url)` | Navigate the WebView to a URL |
| `NavigateToString(string html)` | Navigate to an HTML string |
| `NavigateToWebRootAsync()` | Navigate to the app's extracted WebRoot index.html |
| `AddHostObject(string name, DispatchObject)` | Register a JS-callable host object (`chrome.webview.hostObjects.name`) |
| `CreateSharedBuffer(string name, ...)` | Create a named shared-memory channel to the page |
| `AddStartupScript(string script)` | Run a script at the start of every document (and immediately) |
| `AddStartupScriptResource(Assembly, string)` | Load a startup script from an embedded resource |
| `ExecuteScript<T>(string, JsonTypeInfo<T>, ...)` | Execute script and deserialize result |
| `ExecuteScriptAsJson(string, ...)` | Execute script and return raw JSON |
| `ExecuteScript(string, ...)` | Fire-and-forget script execution |
| `BeginDrag()` | Start a native window move (for custom title bars) |
| `MaximizeOrRestore()` | Toggle maximized state |
| `GetFullScreenBounds()` | Get the monitor bounds for full-screen mode |
| `SetSystemBackdrop(DWM_SYSTEMBACKDROP_TYPE)` | Apply Windows 11 Mica/Acrylic/Tabbed backdrop |
| `ClearBrowsingDataAll()` | Clear all browsing data |

#### Virtual Methods for Customisation

| Method | Purpose |
|---|---|
| `ConfigureSettings(ICoreWebView2Settings)` | Additional WebView2 settings beyond the exposed virtuals |
| `GetBrowserExecutableFolder()` | Path to a fixed-version WebView2 runtime (null = evergreen) |
| `GetCaptionRect()` | Custom caption area for `HT_CAPTION` hit testing |
| `GetEnvironmentOptions()` | Custom `CoreWebView2EnvironmentOptions` |
| `ControllerCreated()` | Called once the controller is ready (override to navigate) |
| `IsAppContentUri(string uri)` | Whether a URI is the app's own content |
| `GetNavigationErrorPage(NavigationEventArgs)` | Custom error page HTML |
| `SetWindowTitleFromPage(string? title)` | Accept/reject title changes from the page |
| `OnPageError(string message, string? stack)` | Handle JavaScript errors from the page |
| `TryHandleShortcut(VIRTUAL_KEY key)` | Handle keyboard shortcuts (F11/F12) |
| `TryOpenDevTools()` | Open DevTools (F12) |

#### Events

| Event | Args | Description |
|---|---|---|
| `MouseMove` | `MouseEventArgs` | Mouse moved |
| `MouseLeave` | `MouseEventArgs` | Mouse left the window |
| `MouseHover` | `MouseEventArgs` | Mouse hover |
| `MouseWheel` | `MouseWheelEventArgs` | Mouse wheel |
| `MouseButtonDown` | `MouseButtonEventArgs` | Mouse button pressed |
| `MouseButtonUp` | `MouseButtonEventArgs` | Mouse button released |
| `MouseButtonDoubleClick` | `MouseButtonEventArgs` | Mouse button double-clicked |
| `PointerActivate` | `PointerActivateEventArgs` | Pointer activated |
| `PointerEnter` | `PointerEnterEventArgs` | Pointer entered |
| `PointerLeave` | `PointerLeaveEventArgs` | Pointer left |
| `PointerWheel` | `PointerWheelEventArgs` | Pointer wheel |
| `PointerUpdate` | `PointerPositionEventArgs` | Pointer moved |
| `PointerContactChanged` | `PointerContactChangedEventArgs` | Pointer contact changed |
| `KeyDown` | `KeyEventArgs` | Key pressed |
| `KeyUp` | `KeyEventArgs` | Key released |
| `KeyPress` | `KeyPressEventArgs` | Character input |
| `MonitorChanged` | `EventArgs` | Window moved to a different monitor |
| `NavigationStarting` | `NavigationEventArgs` | Navigation starting |
| `NavigationCompleted` | `NavigationEventArgs` | Navigation completed |
| `WebMessageJsonReceived` | `ValueEventArgs<string>` | Raw JSON from `window.__aotrino.post` / `chrome.webview.postMessage` |
| `FilesDropped` | `FileDropEventArgs` | Files dropped from Explorer |

#### Window Command Protocol (JS → .NET)

The page communicates window commands via `window.__aotrino.post()` with JSON:

```json
{"__aotrino": "window-command", "command": "drag"}
{"__aotrino": "window-command", "command": "close"}
{"__aotrino": "window-command", "command": "minimize"}
{"__aotrino": "window-command", "command": "maximize"}
{"__aotrino": "window-command", "command": "title", "title": "New Title"}
```

These are handled automatically by `HandleWindowCommand` in `WebViewWindow`.

#### Page Error Reporting (JS → .NET)

Unhandled JavaScript errors are forwarded:

```json
{"__aotrino": "page-error", "message": "...", "stack": "..."}
```

Handled by `HandlePageError` → `OnPageError` virtual.

---

### CompositionWebViewWindow (abstract)

**File:** `tmp_compwv.cs`

Extends `WebViewWindow` to host the WebView in a `Windows.UI.Composition` visual tree.

#### Key Concepts

- **NoRedirectionBitmap window** – The window uses `WS_EX_NOREDIRECTIONBITMAP`, meaning Windows does not compose a bitmap for it. Instead, the application provides a `RootVisualTarget` (a `CompositionTarget`) that the DWM composites directly.
- **CompositorController** – Manages the `Windows.UI.Composition.Compositor` and commits changes.
- **RootVisual** – A `SpriteVisual` that fills the window. The WebView renders into this visual (or a child visual if `WebViewVisualTarget` is overridden).
- **Input Forwarding** – A composition-hosted WebView receives no OS input. The window forwards mouse and pointer events via `ICoreWebView2CompositionController.SendMouseInput` and `SendPointerInput`.

#### Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `CompositorController` | `CompositorController` | (public) | The composition controller |
| `RootVisual` | `SpriteVisual` | (public) | Root visual filling the window |
| `Compositor` | `Compositor` | (public) | Shortcut to `CompositorController.Compositor` |
| `GraphicsDevice` | `CompositionGraphicsDevice?` | (public) | Created after device resources are ready |
| `D2D1Device` | `IComObject<ID2D1Device>?` | (public) | Direct2D device (when `UseDirect2D` is true) |
| `Controller` | `ComObject<ICoreWebView2CompositionController>?` | (protected) | The composition controller |
| `DoUseDirect2D` | `bool` | (protected) | Whether Direct2D is used |
| `TopMostDesktopWindowTarget` | `bool` | `true` | Whether the desktop window target is topmost |
| `UseDirect2D` | `bool` | `true` | Whether to create a Direct2D device |
| `WebViewVisualTarget` | `Visual` | `RootVisual` | The visual the WebView renders into |

#### Virtual Methods

| Method | Description |
|---|---|
| `CreateWindowVisual()` | Creates the `SpriteVisual` for `RootVisual` |
| `SetVisualSize()` | Updates `RootVisual.Size` to match `ClientRect` |
| `OnAfterDragEnter` / `OnBeforeDragEnter` | Drag-drop lifecycle hooks |
| `OnAfterDragOver` / `OnBeforeDragOver` | Drag-over lifecycle hooks |
| `OnAfterDragLeave` / `OnBeforeDragLeave` | Drag-leave lifecycle hooks |
| `OnAfterDrop` / `OnBeforeDrop` | Drop lifecycle hooks |

#### Drag & Drop

`CompositionWebViewWindow` implements `IDropTarget` and forwards drag-drop events to the WebView via `ICoreWebView2CompositionController3`. The `OnBefore*` / `OnAfter*` virtuals allow interception at each stage.

The `IsDropTarget` property (distinct from `AcceptsFileDrops` in `WebViewWindow`) controls whether OLE drag-drop is registered. This is for the WebView's own HTML5 drag-drop, not for file drops from Explorer (which is handled by `WebViewWindow.AcceptsFileDrops`).

---

### AOTrinoWindow (concrete)

**File:** `tmp_win.cs`

The concrete window class that most applications derive from. Adds:

- **NavigationMode** – Controls where the window may navigate.
- **VirtualHostName** – Serves the WebRoot over `https://{VirtualHostName}/` instead of `file://`.
- **WebRoot extraction** – Waits for the embedded WebRoot to be extracted before navigating.
- **`__aotrino` shared runtime** – Injected into every page.

#### NavigationMode Enum

| Value | Description |
|---|---|
| `Local` (default) | Window stays on the app's own content. Off-app links open in the default browser. |
| `Web` | Window becomes a mini browser. All browser behaviours are re-enabled. |

#### Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `NavigationMode` | `NavigationMode` | `Local` | Where the window may navigate |
| `VirtualHostName` | `string?` | `null` | Host name for virtual host mapping (null = `file://`) |
| `StartUrl` | `string?` | WebRoot index.html | URL to navigate to on startup |
| `ReplacesNavigationErrorPage` | `bool` | `true` (Local) / `false` (Web) | Whether to replace error pages |

#### Behaviour Virtuals (delegated to NavigationMode)

| Virtual | Local (default) | Web |
|---|---|---|
| `AreDefaultContextMenusEnabled` | `false` | `true` |
| `IsStatusBarEnabled` | `false` | `true` |
| `AreBrowserAcceleratorKeysEnabled` | `false` | `true` |
| `IsBuiltInErrorPageEnabled` | `false` | `true` |

#### Virtual Methods

| Method | Description |
|---|---|
| `RegisterHostObjects()` | Override to call `AddHostObject` before navigation |
| `IsNavigationAllowed(Uri)` | Decide whether a navigation may proceed. Default enforces `NavigationMode`. |
| `OpenExternal(Uri)` | Open a URI in the default OS handler |
| `MapVirtualHost()` | Map `VirtualHostName` to the extracted WebRoot folder |
| `NavigateToStartAsync()` | Extract WebRoot, map virtual host, then navigate to `StartUrl` |

#### Navigation Flow

1. `ControllerCreated()` is called by `WebViewWindow` after the controller is ready.
2. `AOTrinoWindow.ControllerCreated()`:
   - Calls `EnsureSharedRuntime()` – injects `window.__aotrino` runtime.
   - Calls `RegisterHostObjects()` – override to add host objects.
   - Calls `NavigateToStartAsync()`.
3. `NavigateToStartAsync()`:
   - Awaits `WebRoot.EnsureFilesAsync()` (extraction from embedded resources).
   - Calls `MapVirtualHost()` if `VirtualHostName` is set.
   - Navigates to `StartUrl`.

#### Virtual Host Mapping

When `VirtualHostName` is set (e.g., `"app.example.com"`), the WebRoot is served over `https://app.example.com/index.html` instead of `file://`. This gives the page a proper `https` origin, which is necessary for ES modules (CORS blocks modules on `file://` origins).

The mapping is done via `SetVirtualHostNameToFolderMapping` with `DENY_CORS` access (the page loads its own files, other origins are refused).

#### Navigation Mode Enforcement

`OnNavigationStarting` checks `IsNavigationAllowed`:
- **Web mode**: All URIs are allowed.
- **Local mode**: Only `file://`, the virtual host, and `about:/data:/blob:` schemes are allowed. Everything else is cancelled and opened externally.

`SetWindowTitleFromPage` is also gated:
- **Web mode**: Page cannot rename the window (the window title identifies the app, not the site).
- **Local mode**: Page may rename the window.

---

## Shared Runtime (`window.__aotrino`)

Every AOTrino app gets the `__aotrino` runtime injected into every page. It provides:

### Properties

| Property | Description |
|---|---|
| `window.__aotrino.system` | System information (e.g., `doubleClickTimeMs`) |
| `window.__aotrino.window` | Window information (e.g., `title`) |

### Methods

| Method | Description |
|---|---|
| `window.__aotrino.post(json)` | Post a JSON message to the .NET side |
| `window.__aotrino.dragWindow()` | Start a native window drag |
| `window.__aotrino.closeWindow()` | Close the window |
| `window.__aotrino.minimizeWindow()` | Minimize the window |
| `window.__aotrino.maximizeWindow()` | Maximize/restore the window |
| `window.__aotrino.setWindowTitle(title)` | Set the window title |
| `window.__aotrino.getBuffer(name)` | Get a shared memory buffer by name |

### Shared Buffers

Created on the .NET side via `CreateSharedBuffer(name, access)`. The page reads them via `window.__aotrino.getBuffer(name)`. Used for high-performance data transfer (e.g., Direct2D → WebGL surfaces in `AOTrino.Graphics`).

---

## Host Objects

Register JS-callable .NET objects via `AddHostObject(name, dispatchObject)`:

```csharp
protected override void RegisterHostObjects()
{
    AddHostObject("myApi", new MyApiObject());
}
```

In JavaScript:
```javascript
// Async (returns a Promise)
const result = await chrome.webview.hostObjects.myApi.doSomething();

// Sync (blocking)
const result = chrome.webview.hostObjects.sync.myApi.doSomething();
```

The `WebViewHostObjectHelper` (best-effort, via private WebView2 interfaces) enables full `Task`/`Task<T>` support for async host object methods.

---

## File Drops

Two separate drag-drop systems:

### 1. Explorer File Drops (`WebViewWindow.AcceptsFileDrops`)

When `true`, the window registers an OLE drop target that provides real file paths (unlike HTML5 drag-drop which only gives file names and bytes).

- Override `GetFileDropEffect` to control the drop effect cursor.
- Override `OnFilesDropped` or subscribe to `FilesDropped` event.

### 2. HTML5 Drag-Drop (`CompositionWebViewWindow.IsDropTarget`)

Controls whether the WebView's own HTML5 drag-drop is enabled. The `IDropTarget` implementation forwards events to `ICoreWebView2CompositionController3`.

---

## Error Handling

### Navigation Errors

When a navigation fails:
1. If `ReplacesNavigationErrorPage` is `true` (default for `Local` mode), the browser's error page is replaced with AOTrino's own.
2. `GetNavigationErrorPage` generates an HTML page that explains whether the failure is in the app's own content or an external URL.
3. For app content, it suggests likely causes (missing embedded front end, wrong executable, wrong start file name).
4. The replacement happens only once per error (`_navigationErrorShown` flag prevents infinite loops).

### JavaScript Errors

Unhandled JavaScript errors are caught by the `__aotrino` runtime and posted to .NET as `{"__aotrino": "page-error", ...}`. The default `OnPageError` traces the error; override to show it in the app's UI or swallow it.

---

## Window Styling

### Corners

- Full-screen: `DWMWCP_DONOTROUND`
- Normal: `DWMWCP_ROUND` (rounded corners on Windows 11)

### System Backdrop

Call `SetSystemBackdrop(type)` to apply Windows 11 materials:
- `DWMSBT_DISABLE` – No backdrop
- `DWMSBT_MAINWINDOW` – Mica
- `DWMSBT_TABBEDWINDOW` – Tabbed
- `DWMSBT_AERO` – Acrylic

### Hit Testing

`WM_NCHITTEST` is handled to provide custom resize borders (`BorderWidth`/`BorderHeight`) and a caption area (`GetCaptionRect`). The window frame is extended into the client area via `DwmExtendFrameIntoClientArea` with negative margins (removes the standard frame).

---

## Typical Usage

### Minimal App Window

```csharp
public class MyAppWindow : AOTrinoWindow
{
    public MyAppWindow()
        : base(title: "My Application")
    {
    }
}
```

### With Virtual Host (for ES modules)

```csharp
public class MyAppWindow : AOTrinoWindow
{
    public MyAppWindow() : base(title: "My App") { }

    protected override string? VirtualHostName => "myapp.localhost";
}
```

### With Custom Host Objects

```csharp
public class MyAppWindow : AOTrinoWindow
{
    public MyAppWindow() : base(title: "My App") { }

    protected override void RegisterHostObjects()
    {
        AddHostObject("fileSystem", new FileSystemHost());
        AddHostObject("settings", new SettingsHost());
    }
}
```

### Browser Window

```csharp
public class BrowserWindow : AOTrinoWindow
{
    public BrowserWindow() : base(title: "Browser")
    {
        NavigationMode = NavigationMode.Web;
    }
}
```

### With File Drop Support

```csharp
public class MyAppWindow : AOTrinoWindow
{
    public MyAppWindow() : base(title: "My App") { }

    protected override bool AcceptsFileDrops => true;

    protected internal override void OnFilesDropped(FileDropEventArgs e)
    {
        foreach (var file in e.Files)
        {
            // Process file
        }
    }
}
```

---

## Disposal Order

When a `CompositionWebViewWindow` is disposed:

1. `DetachController()` – stops routing bounds/focus to the controller (prevents accessing disposed COM objects during teardown).
2. Remove `CursorChanged` event handler.
3. Dispose `_controller3`, `_controller`, `CompositorController`, `RootVisual`, `D2D1Device`.
4. Call base `WebViewWindow.Dispose`:
   - Remove navigation event handlers.
   - Revoke drag-drop.
   - Dispose environment.

---

## Threading Model

- The window and WebView2 run on the UI thread (STA).
- WebRoot extraction runs on a worker thread; the continuation resumes on the window's synchronization context.
- `OleInitialize` is called for drag-drop support (requires STA).
- The `CompositorController` commits changes on the UI thread.
