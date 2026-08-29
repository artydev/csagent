# Task 3 — Wire Retry Policy Through CodingAgent

**Status:** ⬜ Not started
**Depends on:** Task 1, Task 2
**Unblocks:** Task 4

---

## Objective

Pass the retry configuration from `AgentOptions` into `LlmClient` so the retry
behavior is configurable at the agent level (and later via CLI flags in Task 4).

## Context

- `CodingAgent` (`src/Core/Agent/CodingAgent.cs`) constructs the client:
  ```csharp
  _client = new LlmClient(apiKey, endpoint, model);
  ```
- `AgentOptions` (`src/Core/Agent/AgentOptions.cs`) is a `record`:
  ```csharp
  public sealed record AgentOptions(
      int MaxSteps = 30,
      bool DryRun = false,
      bool Confirm = true);
  ```

## Changes

### 1. Add a retry field to `AgentOptions`

```csharp
public sealed record AgentOptions(
    int MaxSteps = 30,
    bool DryRun = false,
    bool Confirm = true,
    RetryPolicy? Retry = null);
```

### 2. Pass it through `CodingAgent`

In `CodingAgent`'s constructor, forward the retry policy to the client:

```csharp
_client = new LlmClient(apiKey, endpoint, model, opts.Retry);
```

### 3. Update call sites of `AgentOptions`

Search for every `new AgentOptions(...)` construction and ensure the new optional
parameter is either left at its default or wired from CLI parsing (Task 4).

## Acceptance Criteria

- [ ] `AgentOptions` exposes a `Retry` field (nullable, defaults to `null` → `RetryPolicy.Default`).
- [ ] `CodingAgent` forwards `opts.Retry` to `LlmClient`.
- [ ] Existing call sites still compile (parameter is optional).
- [ ] `dotnet build` succeeds.

## Verification

```bash
dotnet build
```

## Definition of Done

- [ ] Wiring complete and compiling.
- [ ] No existing behavior changed when `Retry` is left at its default.
