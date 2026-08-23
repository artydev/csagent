# Task 5 — CLI Integration (`--desktop` argument)

**Status:** Not started

## Objective

Wire the Photino desktop host into the existing CLI so that `csagent --desktop`
launches the Photino window.

## Context — existing CLI flow

The CLI is parsed by `ArgumentParser` (`src\Shared\ArgumentParser.cs`), which
produces an `AgentArguments` record. The record already has an `IsDesktopMode`
flag (`args.Contains("--desktop")`).

The existing `DesktopHost.Run(args)` is invoked when `--desktop` is passed. The
Photino host must be invoked in the same way.

## Subtasks

### 5.1 Locate the dispatch point

Find where `IsDesktopMode` is handled (likely in `Program.cs` / the main entry
point) and where `DesktopHost.Run(args)` is currently called.

### 5.2 Route `--desktop` to the Photino host

Decide the routing strategy. Two reasonable options:

- **Option A (replace):** Change the `--desktop` branch to call
  `PhotinoHost.Run(args)` instead of `DesktopHost.Run(args)`.
- **Option B (new flag):** Add a new flag (e.g. `--desktop-photino`) so both hosts
  can coexist during migration, and keep `--desktop` for the AOTrino host.

> Recommendation: use **Option B** during development so the existing AOTrino host
> remains available, then switch `--desktop` to Photino once it is stable.

### 5.3 (If Option B) Add the new flag to `ArgumentParser`

Add a new boolean to `AgentArguments` (e.g. `IsPhotinoMode`) and detect it in
`ArgumentParser.Parse`:

```csharp
var isPhotinoMode = args.Contains("--desktop-photino");
```

Update the record constructor call accordingly.

### 5.4 Dispatch

In the main entry point, add a branch:

```csharp
if (args.IsPhotinoMode)
{
    PhotinoHost.Run(args);
    return;
}
```

## Definition of Done

- [ ] `csagent --desktop` (or the chosen flag) launches the Photino window.
- [ ] The existing AOTrino desktop host still works (if kept).
- [ ] `ArgumentParser` and `AgentArguments` updated if a new flag was added.
- [ ] Help text / usage updated to document the new flag.
