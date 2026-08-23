# Task 1 — Project Setup

**Status:** Not started
**Location:** `src\Presentation\DesktopPhotino\`

## Objective

Prepare the project so the Photino desktop implementation can be added cleanly,
without disturbing the existing AOTrino desktop host.

## Subtasks

### 1.1 Add the Photino.NET package

Add a `PackageReference` to the project file (`CsAgentUI.csproj`):

```xml
<PackageReference Include="Photino.NET" Version="4.0.16" />
```

- `Photino.Native` (`>= 4.0.22`) is pulled in transitively.
- Verify the package is compatible with the project TFM
  `net10.0-windows10.0.19041.0` (Photino.NET targets `net8.0`, so it is compatible).

### 1.2 Create the implementation folder

Create `src\Presentation\DesktopPhotino\` with the following structure:

```
DesktopPhotino\
    PhotinoHost.cs
    PhotinoAPI.cs
    PhotinoObserver.cs        (optional)
    assets\
        index.html
        app.js
        styles.css
```

### 1.3 Register the assets as embedded resources

Add the Photino assets to the `<EmbeddedResource>` items in `CsAgentUI.csproj` so
they can be loaded at runtime without a web server:

```xml
<EmbeddedResource Include="src\Presentation\DesktopPhotino\assets\index.html" />
<EmbeddedResource Include="src\Presentation\DesktopPhotino\assets\app.js" />
<EmbeddedResource Include="src\Presentation\DesktopPhotino\assets\styles.css" />
```

### 1.4 Namespace

Use the namespace `CsAgentUI.Presentation.DesktopPhotino` (matching the existing
`CsAgentUI.Presentation.Desktop` convention) so the new code is discoverable and
consistent with the rest of the codebase.

## Definition of Done

- [ ] `Photino.NET` package reference added and restored successfully.
- [ ] `src\Presentation\DesktopPhotino\` folder exists.
- [ ] Photino assets are embedded resources.
- [ ] Project still builds with the existing AOTrino desktop host intact.
