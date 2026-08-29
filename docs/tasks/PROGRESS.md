# PROGRESS — HTTP 429 Retry Implementation

> **Goal:** Add automatic HTTP 429 (rate limit) retry with exponential backoff to
> `LlmClient.CompleteChatAsync` so the agent survives transient rate limits during
> long autonomous sessions.
>
> **Branch:** `feature/handle_429`
>
> **Status legend:** ⬜ Not started · 🔄 In progress · ✅ Done · ⛔ Blocked

---

## Task List

| # | Task | Status | Notes |
|---|------|--------|-------|
| 1 | [Add retry policy configuration](task-01-retry-config.md) | ✅ | Add `RetryPolicy` options (max attempts, base delay, backoff factor) |
| 2 | [Implement 429 retry/backoff in LlmClient](task-02-llmclient-retry.md) | ✅ | Detect 429, honor `Retry-After`, exponential backoff |
| 3 | [Wire retry policy through CodingAgent](task-03-wire-config.md) | ✅ | Pass retry options from `AgentOptions`/CLI into `LlmClient` |
| 4 | [Add CLI flags & help text](task-04-cli-flags.md) | ✅ | `--max-retries`, `--retry-delay` args + help/docs |
| 5 | [Update README troubleshooting](task-05-readme.md) | ✅ | Document new retry behavior |
| 6 | [Build & verify](task-06-build-verify.md) | ⬜ | `dotnet build`, manual smoke test |

---

## Progress Log

### 2025-XX-XX — Task 5 complete
- Updated `README.md`:
  - 429 troubleshooting entry now documents automatic retry with exponential
    backoff and `Retry-After` header honoring.
  - Added `--max-retries` and `--retry-delay` to the Command-Line Arguments table.
  - Added a rate-limit retry tuning example.
- Verified all three sections render correctly.

### 2025-XX-XX — Plan created
- Created this tracker and the task breakdown.
- Inspected `src/Core/Llm/LlmClient.cs`, `src/Core/Agent/CodingAgent.cs`,
  `src/Core/Agent/AgentOptions.cs`, `src/Core/Llm/LlmSettings.cs`.
- Confirmed current behavior: any non-2xx status throws `HttpRequestException`
  with no retry/backoff. 429 is surfaced to the user as `API error: ...`.

---

## Definition of Done (overall)

- [ ] 429 responses are retried automatically with exponential backoff.
- [ ] `Retry-After` header is honored when present.
- [ ] Retry behavior is configurable (max attempts, base delay).
- [ ] Non-retryable errors (4xx other than 429, 5xx beyond retries) still surface cleanly.
- [ ] CLI flags documented in help and README.
- [ ] `dotnet build` succeeds; manual smoke test confirms retry path.
