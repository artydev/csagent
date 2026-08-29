# Task 6 — Build & Verify

**Status:** ⬜ Not started
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

- [ ] `dotnet build` succeeds with zero errors.
- [ ] Retry path verified against a stub (429 → 200 succeeds).
- [ ] Exhaustion path verified (always-429 throws after `MaxAttempts`).
- [ ] No throwaway files left behind.

## Definition of Done

- [ ] Build green.
- [ ] Retry and exhaustion paths demonstrated with actual output.
- [ ] Workspace clean.
