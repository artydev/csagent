# 🛠️ SKILL: CSV Handling
**Version:** 1.0
**Target runtime:** csAgent (uses `read_file`, `parse_output`, `sh`/`run_terminal` as fallback)
**Purpose:** Read and parse CSV files correctly, working around a verified gap in `parse_output`'s delimiter detection rather than silently producing wrong results.

---

## ⚠️ CRITICAL — VERIFIED TOOL LIMITATION

`parse_output`'s CSV mode **only auto-detects tab or comma as the delimiter** (its internal logic: `hasTab ? '\t' : ','`). **Semicolon is never checked.**

This matters because semicolon-delimited CSVs are the norm for anything exported from European/French-locale Excel — comma is the decimal separator there, so semicolon takes over as the field separator. A file like:

```
Nom;Prénom;Montant
Dupont;Jean;1234,56
```

fed straight into `parse_output` with `format: "csv"` will be parsed as **one column**, silently — no error, just wrong. Do not trust `parse_output`'s CSV mode on a file without first verifying the delimiter (Phase 1).

---

## 🎯 MISSION STATEMENT

Extract CSV data accurately — correct delimiter, correct decimal interpretation, correct encoding — before doing anything with it, rather than assuming a generic parser's defaults apply.

---

## ⚙️ EXECUTION WORKFLOW

### PHASE 1 — DELIMITER & ENCODING VERIFICATION
- Read the file's first 2–3 lines via `read_file` before calling `parse_output` at all.
- **Delimiter check:** does splitting the header row by comma produce a plausible number of columns matching visible field names? If not, check semicolon. If semicolon produces a sane column count and comma doesn't, this is a semicolon-delimited file — `parse_output`'s CSV mode will mishandle it (see above).
- **Encoding check:** look for mojibake — accented characters appearing as garbled multi-character sequences (e.g. `Ã©` instead of `é`) is the signature of a Windows-1252/ANSI file being read as UTF-8. If seen, flag it to the user rather than silently passing corrupted text downstream.

### PHASE 2 — DECIMAL FORMAT AWARENESS
- In a semicolon-delimited file, a comma **inside** an already-split numeric field (e.g. `1234,56`) is a **decimal comma**, not a field break and not a thousands separator. Do not re-split on it. Convert to a standard `1234.56` representation only if downstream numeric processing (e.g. writing into an Excel `NumberFormat`-aware cell, or doing arithmetic) requires it — otherwise preserve the original text form.
- Do not assume comma-as-decimal applies to a comma-delimited (US-style) file — there, comma is the field separator and decimals use a period, as `parse_output` already expects correctly.

### PHASE 3 — PARSING STRATEGY BY DELIMITER
- **Tab or standard comma delimiter:** `parse_output` with `format: "csv"` is proven and safe to use directly.
- **Semicolon delimiter:** do **not** attempt to work around this by find-replacing `;` with `,` before parsing — this is dangerous specifically because of decimal commas (Phase 2): a blind replace would make a field-separating comma indistinguishable from a decimal comma already present in numeric values, corrupting the data. Instead, parse the raw text directly (split rows by line, split each row by `;`, respecting simple quoted fields) rather than routing through `parse_output`'s comma/tab-only logic.

### PHASE 4 — SIZE HANDLING
- `read_file` has a 512 KB limit. If a CSV exceeds it, do not silently fail or claim the file is unreadable — fall back to `sh`/`run_terminal` (e.g. `Get-Content -TotalCount N` for a preview, or chunked reads for the full file) to actually get the content.

### PHASE 5 — DOWNSTREAM USE (Excel reports)
- If the parsed data is going into an Excel report, hand off to the **Excel Report Builder** skill's own rules once parsing is correct — same session throughout, never call `Workbooks.Add()` redundantly, apply the Professional Preset by default, verify the save. Do not duplicate or contradict those rules here; this skill's job ends at "correctly parsed data," not at report formatting.

---

## 🛡️ ERROR HANDLING & FALLBACKS

| Scenario | Fallback |
|---|---|
| Comma-split produces an implausible column count | Check semicolon next (Phase 1) — do not force comma just because it's the tool's default. |
| Header-row heuristic looks wrong (e.g. a numeric-looking header, or a headerless file) | Verify against visible content or ask the user — do not trust the guess blindly. |
| Garbled/mojibake accented characters | Flag the likely encoding mismatch explicitly to the user; do not pass corrupted text downstream silently. |
| File exceeds 512 KB | Fall back to `sh`/`run_terminal` for chunked reading (Phase 4) — never truncate silently or report failure without trying the fallback. |
| Ambiguous comma (delimiter vs. decimal) | If it's inside an already-semicolon-split field, it's a decimal. If unclear which delimiter is actually in use, ask rather than guess. |

---

## 🚫 HARD CONSTRAINTS

1. Never assume comma is the delimiter just because `parse_output` defaults to it — verify against actual file content first (Phase 1).
2. Never find-replace delimiters as a shortcut — this risks corrupting decimal-comma values that look identical to field separators.
3. Never treat `parse_output`'s header-detection heuristic as ground truth without a sanity check against visible content.
4. Never silently give up or truncate on a file exceeding 512 KB — use the `sh`/`run_terminal` fallback.
5. Never pass text downstream that shows signs of encoding corruption without flagging it to the user first.
6. When feeding parsed CSV data into an Excel report, defer to the Excel Report Builder skill's own session and formatting rules rather than reimplementing them here.