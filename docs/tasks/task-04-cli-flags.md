# Task 4 — Add CLI Flags & Help Text

**Status:** ✅ Done
**Depends on:** Task 3
**Unblocks:** Task 5

---

## Objective

Expose the retry configuration via CLI flags so users can tune rate-limit retry
behavior without editing code. Update the help text accordingly.

## Context

- `ArgumentParser` (`src/Shared/ArgumentParser.cs`) parses args into an
  `AgentArguments` record. It has helpers `GetValue(args, params string[] names)`
  and `GetPort(args)`.
- `HelpDisplay` (`src/Shared/HelpDisplay.cs`) renders the `OPTIONS` section.
- `AgentOptions` (Task 3) will carry a `RetryPolicy? Retry` field.

## Changes

### 1. Extend `AgentArguments`

Add fields for the retry knobs:

```csharp
public sealed record AgentArguments(
    string MemoryFile,
    string? ModelOverride,
    string? McpUrl,
    int Port,
    bool IsUiMode,
    bool IsNativeMode,
    bool IsDryRun,
    bool ShowHelp,
    bool ShowVersion,
    bool ShowDoc,
    int MaxRetries = 3,
    int RetryDelayMs = 1000);
```

### 2. Parse the new flags in `ArgumentParser.Parse`

```csharp
var maxRetries = GetInt(args, "--max-retries", 3);
var retryDelayMs = GetInt(args, "--retry-delay", 1000);
```

Add a small `GetInt` helper (mirroring `GetPort`'s pattern):

```csharp
private static int GetInt(string[] args, string name, int fallback)
{
    for (int i = 0; i < args.Length; i++)
        if (args[i] == name && i + 1 < args.Length)
            if (int.TryParse(args[i + 1], out var v) && v > 0)
                return v;
    return fallback;
}
```

### 3. Build a `RetryPolicy` from the parsed args

Wherever `AgentOptions` is constructed from `AgentArguments`, map the new fields:

```csharp
var retry = new RetryPolicy(
    MaxAttempts: args.MaxRetries,
    BaseDelayMs: args.RetryDelayMs).Validate();
```

Pass `retry` into `AgentOptions.Retry`.

### 4. Update `HelpDisplay`

Add to the `OPTIONS` section:

```
    --max-retries <n>     Max attempts for HTTP 429 (rate limit) retries (default: 3)
    --retry-delay <ms>    Base backoff delay in ms before the first retry (default: 1000)
```

And add example lines:

```
    csagent --max-retries 5 --retry-delay 2000     Tune rate-limit retry behavior
```

## Acceptance Criteria

- [ ] `--max-retries` and `--retry-delay` are parsed.
- [ ] Parsed values flow into `AgentOptions.Retry` → `LlmClient`.
- [ ] Invalid/absent flags fall back to defaults.
- [ ] Help text documents both flags.
- [ ] `dotnet build` succeeds.

## Verification

```bash
dotnet build
csagent --help   # confirm new flags appear
```

## Definition of Done

- [ ] Flags parsed and wired through.
- [ ] Help text updated.
- [ ] Build succeeds.
