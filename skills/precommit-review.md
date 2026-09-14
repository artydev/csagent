# 🛠️ SKILL: Pre-Commit Review & Message Drafting
**Version:** 1.0
**Target runtime:** csAgent (uses `git_status`, `git_diff`, `git_branch`, `git_commit`)
**Purpose:** Ensure every commit reflects a reviewed, understood change with an accurate message — never a blind `git_commit` call based on assumption.

---

## ⚠️ CRITICAL — VERIFIED TOOL BEHAVIOR

`git_commit` unconditionally runs `git add -A` before committing. **There is no selective-staging option.** Every modified, added, or deleted file in the working tree — not just the ones you have in mind — gets included, with no warning and no opt-out.

This means `git_status` is not optional due-diligence here; it is **the only way to know what is actually about to be committed.** A stray temp file, a leftover debug edit, or an unrelated change from earlier in the session will be silently swept in if `git_status` isn't checked first. `git_commit` is already marked destructive in `ToolDispatcher` (requires user confirmation) — but confirmation only protects against *accidentally running the tool*, not against a commit that's technically successful while including the wrong things.

---

## 🎯 MISSION STATEMENT

Never call `git_commit` without first knowing, precisely, what will be staged and why — and never write a commit message that doesn't reflect what the diff actually shows.

---

## ⚙️ EXECUTION WORKFLOW

### PHASE 1 — STATUS REVIEW (mandatory, before anything else)
- Call `git_status` first, every time. Read the full output — do not skim for "looks about right."
- **Because `git add -A` stages everything:** if `git_status` shows files that shouldn't be part of this commit (unrelated changes, accidental saves, generated files that shouldn't be tracked), stop and flag this to the user before proceeding. Do not commit around the problem by hoping it's fine.
- If nothing is staged or changed, say so plainly — do not invent a commit for an empty working tree.

### PHASE 2 — BRANCH CHECK
- Call `git_branch` to confirm which branch is currently active.
- If the current branch is `main` or `master` and no feature branch is in use, **flag this before committing** — this is a nudge, not a hard block (the user may genuinely intend a direct commit to main), but it should never happen silently.

### PHASE 3 — DIFF REVIEW
- Call `git_diff` (unstaged) and, if relevant, `git_diff` with `staged: true` after Phase 1's implicit staging awareness — review the actual content of the change, not just the file list from Phase 1.
- **Scan the diff for things that should not ship:**
  - Hardcoded secrets, API keys, tokens, passwords
  - Stray debug output (`console.log`, `print`, `Write-Host` left in from troubleshooting)
  - Commented-out code blocks with no explanation
  - Unaddressed `TODO`/`FIXME` markers introduced in this change (pre-existing ones elsewhere in the file are not this commit's concern)
- If any of the above is found, flag it to the user before committing — do not silently commit through it, and do not silently strip it out either without being asked to.

### PHASE 4 — MESSAGE DRAFTING
- Write the commit message from what the diff **actually shows** — never a generic placeholder ("update files," "fix stuff," "changes") disconnected from the real content.
- Use Conventional Commits style: `type(scope): summary` — e.g. `fix(excel): reuse active workbook instead of creating a new one`, `feat(skills): add CSV handling skill`.
- Common types: `feat`, `fix`, `refactor`, `docs`, `chore`, `test`. Pick the one that actually matches what changed, not the one that sounds best.
- Keep the summary line concise (under ~72 characters is the conventional target); if the change needs more explanation, add it as a body after a blank line — don't cram everything into the summary.

### PHASE 5 — COMMIT
- Only now call `git_commit`, with the message drafted in Phase 4.
- After the call, read the actual returned output — confirm it reports success, not just that the call completed without throwing. `git_commit` requires user confirmation regardless (it's destructive); this phase begins only after that confirmation has been given.

---

## 🛡️ ERROR HANDLING & FALLBACKS

| Scenario | Fallback |
|---|---|
| `git_status` shows unexpected/unrelated files | Stop, flag to the user, do not proceed to commit until resolved. |
| Currently on `main`/`master` with no branch strategy evident | Flag it (Phase 2) — proceed only if the user confirms this is intentional. |
| Diff contains a likely secret or credential | Do not commit. Flag it explicitly and specifically (what file, what looks like a secret) rather than a vague warning. |
| Diff is empty despite the user asking to commit | Say so plainly — there is nothing to commit — rather than inventing a message for a no-op commit. |
| `git_commit` returns a non-zero/error result | Report the actual error text back; do not assume success and do not retry blindly. |

---

## 🚫 HARD CONSTRAINTS

1. Never call `git_commit` without first calling `git_status` in the same task — staging is unconditional and total, so this is the only visibility into what's about to happen.
2. Never write a commit message that doesn't reflect the actual diff content — no generic placeholders.
3. Never silently commit through a flagged issue (unexpected files, possible secret, direct-to-main) without the user's explicit go-ahead.
4. Never silently strip out something flagged in Phase 3 (debug code, TODOs) without being asked to — flagging and fixing are different actions.
5. Never assume `git_commit` succeeded without reading its actual returned output.