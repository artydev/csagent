# Task 6 — Build & Publish Considerations

**Status:** Not started

## Objective

Ensure the project still builds and publishes correctly with the Photino.NET
package, given the project's aggressive publish settings.

## Context — project publish settings

`CsAgentUI.csproj` uses:

```xml
<PublishSingleFile>true</PublishSingleFile>
<PublishAot>true</PublishAot>
<PublishTrimmed>true</PublishTrimmed>
<TrimMode>full</TrimMode>
<PlatformTarget>x64</PlatformTarget>
<DisableRuntimeMarshalling>true</DisableRuntimeMarshalling>
```

These settings can conflict with native packages like Photino.Native, so they must
be validated.

## Subtasks

### 6.1 Verify the build

- Run `dotnet build` and confirm the project compiles with the Photino package.
- Confirm the existing AOTrino desktop host still builds.

### 6.2 Verify AOT / trimming compatibility

Photino.Native is a native library. Check that:

- The native `Photino.Native` binaries are copied to the output correctly.
- `PublishAot` / `PublishTrimmed` do not strip types needed by Photino (add
  `[DynamicDependency]` or trimmer annotations if required).
- `DisableRuntimeMarshalling` does not break Photino's P/Invoke calls (if it does,
  scope the setting or add `[UnmanagedCallersOnly]`/marshalling attributes as needed).

### 6.3 Verify single-file publish

- Run `dotnet publish -c Release -r win-x64 --self-contained` (or the project's
  standard publish command).
- Confirm the Photino native binaries are bundled or deployed alongside the
  single-file executable.
- Confirm the app launches from the published output.

### 6.4 Runtime smoke test

- Launch the published app with the Photino flag.
- Confirm the window opens, the UI loads from embedded assets, and the bridge
  (JS ↔ .NET) works end-to-end.

## Definition of Done

- [ ] `dotnet build` succeeds.
- [ ] `dotnet publish` (single-file, AOT, trimmed) succeeds.
- [ ] Photino native binaries are present in the published output.
- [ ] The published app launches and the Photino UI works.
