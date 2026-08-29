# Task 5 — Update README Troubleshooting & Docs

**Status:** ✅ Done
**Depends on:** Task 4
**Unblocks:** Task 6

---

## Objective

Update `README.md` so the 429 troubleshooting entry reflects the new automatic
retry behavior, and document the new CLI flags in the Command-Line Arguments table.

## Context

- `README.md` line ~359 currently says:
  > ### "API 429: ..."
  > You've hit the rate limit. Wait a moment and try again.
- The Command-Line Arguments table (around line ~200) lists all flags.

## Changes

### 1. Update the 429 troubleshooting entry

Replace the current text with something like:

```markdown
### "API 429: ..."
You've hit the rate limit. CSAgent now retries automatically with exponential
backoff (honoring the server's `Retry-After` header when present). If the error
persists after all retries, the API is still rate-limiting you — wait a moment
and try again. You can tune the retry behavior with `--max-retries` and
`--retry-delay` (see Command-Line Arguments).
```

### 2. Add the new flags to the Command-Line Arguments table

```markdown
| `--max-retries <n>` | Max attempts for HTTP 429 (rate limit) retries (default: `3`) |
| `--retry-delay <ms>` | Base backoff delay in ms before the first retry (default: `1000`) |
```

### 3. (Optional) Add an example

```bash
# Tune rate-limit retry behavior
csagent --max-retries 5 --retry-delay 2000
```

## Acceptance Criteria

- [x] 429 troubleshooting entry mentions automatic retry + backoff.
- [x] Both new flags appear in the Command-Line Arguments table.
- [x] Example added (optional but recommended).

## Verification

Read the updated sections to confirm accuracy and consistency with the code.

## Definition of Done

- [x] README reflects the implemented behavior.
- [x] No stale/contradictory statements remain.
