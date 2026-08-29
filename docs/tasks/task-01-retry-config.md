# Task 1 — Add Retry Policy Configuration

**Status:** ⬜ Not started
**Depends on:** none
**Unblocks:** Task 2, Task 3, Task 4

---

## Objective

Introduce a small, immutable configuration type that describes how the LLM client
should retry transient failures (specifically HTTP 429). This keeps the retry
parameters explicit, testable, and configurable rather than hardcoded inside the
HTTP call.

## Context

- `LlmClient` (`src/Core/Llm/LlmClient.cs`) currently has no retry logic.
- `AgentOptions` (`src/Core/Agent/AgentOptions.cs`) is a `record` used to pass
  agent-level options around.
- `LlmSettings` (`src/Core/Llm/LlmSettings.cs`) holds endpoint/model constants.

## Design

Create a new file `src/Core/Llm/RetryPolicy.cs`:

```csharp
namespace CsAgentUI;

/// <summary>
/// Describes how transient API failures (HTTP 429) are retried.
/// </summary>
public sealed record RetryPolicy(
    int MaxAttempts = 3,          // total attempts including the first
    int BaseDelayMs = 1000,       // initial backoff delay
    double BackoffFactor = 2.0,   // multiplier applied after each retry
    int MaxDelayMs = 30000);      // cap on the backoff delay
```

### Field semantics

| Field | Default | Meaning |
|-------|---------|---------|
| `MaxAttempts` | `3` | Total number of attempts (1 initial + 2 retries). Must be ≥ 1. |
| `BaseDelayMs` | `1000` | Delay before the first retry, in milliseconds. |
| `BackoffFactor` | `2.0` | Multiplier applied to the delay after each retry (exponential). |
| `MaxDelayMs` | `30000` | Upper bound on any single delay, to avoid unbounded waits. |

### Validation

Add a small static factory or validation helper so invalid values fail fast:

```csharp
public static RetryPolicy Default { get; } = new();

public RetryPolicy Validate()
{
    if (MaxAttempts < 1)
        throw new ArgumentOutOfRangeException(nameof(MaxAttempts), "MaxAttempts must be >= 1");
    if (BaseDelayMs < 0)
        throw new ArgumentOutOfRangeException(nameof(BaseDelayMs), "BaseDelayMs must be >= 0");
    if (BackoffFactor < 1.0)
        throw new ArgumentOutOfRangeException(nameof(BackoffFactor), "BackoffFactor must be >= 1.0");
    if (MaxDelayMs < BaseDelayMs)
        throw new ArgumentOutOfRangeException(nameof(MaxDelayMs), "MaxDelayMs must be >= BaseDelayMs");
    return this;
}
```

## Acceptance Criteria

- [ ] `RetryPolicy` record exists in `src/Core/Llm/RetryPolicy.cs`.
- [ ] Defaults match the table above.
- [ ] `Validate()` throws on invalid values.
- [ ] `dotnet build` succeeds (no callers yet, so no wiring required).

## Verification

```bash
dotnet build
```

## Definition of Done

- [ ] File created and compiles.
- [ ] Defaults and validation documented in the file.
