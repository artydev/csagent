# Task 6 — Build & Verify

**Status:** ✅ Done
**Depends on:** Tasks 1–5
**Unblocks:** overall completion

---

## Objective

Build the project and verify the retry/backoff behavior works end-to-end.

## Steps

### 1. Build

```bash
dotnet build
```

Confirm zero errors (and no new warnings introduced by the changes).

### 2. Static verification

- Confirm `RetryPolicy` compiles and validates.
- Confirm `LlmClient` retries only on 429 and honors `Retry-After`.
- Confirm CLI flags parse and flow into `AgentOptions.Retry`.

### 3. Manual smoke test (retry path)

Since a real 429 is hard to trigger on demand, verify the retry logic with a
**local stub HTTP server** that returns 429 on the first N requests, then 200.

A minimal approach:

```csharp
// throwaway test harness (not committed) — or a small xUnit test if the project
// has a test project. The repo currently has no test project, so a throwaway
// console harness or a temporary test file is acceptable; delete it afterward.
```

The stub should:
1. Return `429` with a `Retry-After: 1` header on the first call.
2. Return `200` with a valid chat-completions JSON body on the second call.
3. Confirm `CompleteChatAsync` returns the parsed JSON (proving the retry worked).

Also test the exhaustion path: a stub that always returns 429 should cause
`CompleteChatAsync` to throw after `MaxAttempts` attempts.

### 4. Clean up

- Remove any throwaway test harness/stub files.
- Ensure no debug code or dead code remains.

## Acceptance Criteria

- [x] `dotnet build` succeeds with zero errors.
- [x] Retry path verified against a stub (429 → 200 succeeds).
- [x] Exhaustion path verified (always-429 throws after `MaxAttempts`).
- [x] No throwaway files left behind.

## Definition of Done

- [x] Build green.
- [x] Retry and exhaustion paths demonstrated with actual output.
- [x] Workspace clean.

---

## Verification Log

### Build
- `dotnet build -c Release` → **0 errors**, 20 warnings (all pre-existing
  trimming/AOT `IL2026`/`IL3050` and nullability `CS8602`/`CS8604` warnings in
  files untouched by this feature; none in the new retry code).
- Output: `bin\Release\net10.0\CsAgentUI.dll`.

### CLI smoke test
- `dotnet run -- --version` → `CSAgent version 0.5`.
- `dotnet run -- --help` → new flags present:
  - `--max-retries <n>`
  - `--retry-delay <ms>`
  - example `csagent --max-retries 5 --retry-delay 2000`

### Retry-path smoke test (throwaway harness, since removed)
Ran a local `HttpListener` stub against `LlmClient.CompleteChatAsync`:

```
PASS  Scenario 1: succeeds after retry  attempts=2, elapsed=1057ms, content=ok
PASS  Scenario 2: 400 fails fast        attempts=1, msg=API 400: {...}
PASS  Scenario 3: 429 exhausts retries  attempts=3, msg=API 429: {...}

ALL TESTS PASSED
```

- Scenario 1: first call returned 429 with `Retry-After: 1`; the ~1s delay
  confirms the header was honored (not the 50ms base delay). Second call
  returned 200 → parsed JSON returned.
- Scenario 2: 400 threw immediately after 1 attempt (no retry).
- Scenario 3: persistent 429 threw after exactly `MaxAttempts=3` attempts.

### Cleanup
- Throwaway harness (`C:\temp\csagent\.smoketest\`) fully removed.
- `git status --short` clean; no leftover artifacts.
