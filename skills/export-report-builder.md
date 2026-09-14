# 🛠️ SKILL: Excel Report Builder
**Version:** 1.2
**Target runtime:** csAgent (uses `excel_command` / `close_excel` — Windows only, requires Excel installed)
**Purpose:** Turn raw data (pasted, fetched, or read from a file) into a correctly formatted Excel report, using only verified data and explicit formatting specs — never invented sample content, never vague styling.

---

## ⚠️ CRITICAL — SESSION & WORKBOOK RULES

These three rules exist because of real, previously-debugged failures. Do not skip them.

1. **Use one consistent `session` name for the entire report-building task**, from the first `excel_command` call to the last (e.g. `session: "report"`). Never invent a new session id per sub-step — doing so creates a *second, separate* Excel process with no shared state, and the report ends up split across two invisible-to-each-other workbooks.
2. **Never call `$excel.Workbooks.Add()` yourself.** `excel_command`'s own session bootstrap already prepares `$wb` and `$ws`, reusing whichever workbook is currently active. Calling `Add()` again creates a redundant second blank workbook stacked on top of the one already prepared — this exact bug has happened before. Only call `Add()` explicitly if the user has specifically asked for a *new, separate* workbook rather than using the current one.
3. **Do not assume a command succeeded because no error was returned.** Read the actual `RESULT` output of each `excel_command` call. A clean exit code is not proof the data landed where intended — verify by reading back a cell or checking the reported row count before telling the user the report is done.

---

## 🎯 MISSION STATEMENT

Produce a correctly structured, explicitly formatted Excel report from real data only, with the save operation confirmed to have actually succeeded — not merely attempted.

---

## ⚙️ EXECUTION WORKFLOW

### PHASE 1 — DATA INTAKE
- Identify the data source: pasted by the user, fetched via another tool (e.g. `list_models`), or read from a file (`read_file` / `read_json` / `parse_output`).
- **Never invent placeholder or sample data** to fill a report the user didn't provide content for. If the data source is unclear, ask — do not proceed with fabricated rows "to show the structure."
- If the data needs parsing (e.g. `list_models`'s formatted text output), extract it precisely; do not guess at fields that aren't actually present.

### PHASE 2 — STRUCTURE PLANNING
- Decide columns, order, and header names *before* writing any Excel commands.
- **Apply the Professional Preset below by default for every report**, unless the user gives different explicit styling instructions — professional formatting is the standard, not a fallback only reached when the request happens to be vague. If the user does give explicit styling that differs from the preset, follow their instructions instead and state what you applied. Never silently guess at something the user *didn't* specify, and never produce an unstyled report when styling wasn't explicitly declined.
- **Professional Preset (default when no specific styling is requested):**
  - Title row: merged across the table width, bold, 14pt.
  - Header row: bold, 12pt, white font on a dark blue fill (`RGB(31,73,125)`), thin border under the row.
  - Data rows: 11pt, thin borders on all cells, alternating light-gray banding (`RGB(242,242,242)`) on even rows for readability.
  - Numeric columns: currency format (`$#,##0.00`) for money, `#,##0` (thousands separator, no decimals) for plain counts.
  - Totals row (if present): bold, with a border on top to visually separate it from the data.
  - Columns auto-fitted after all data and formatting is applied.

---

## 🎨 PROFESSIONAL STYLING TOOLKIT (reference)

Concrete COM syntax for each element of the Professional Preset. **Proven** = confirmed working end-to-end in a real csAgent `excel_command` session (bold header, borders, autofit — verified against actual Excel output). **Documented, not yet verified here** = standard, well-documented Excel COM properties that should work identically, but haven't specifically been exercised in csAgent's execution path yet — treat as reliable but keep an eye on the result the first few times, per the general "verify, don't assume" rule.

| Element | Syntax | Status |
|---|---|---|
| Bold | `$ws.Range("A1:D1").Font.Bold = $true` | **Proven** |
| Font size | `$ws.Range("A1:D1").Font.Size = 12` | **Proven** |
| Borders (all cells in range) | `$ws.Range("A1:D10").Borders.LineStyle = 1` | **Proven** |
| Auto-fit columns | `$ws.Columns("A:D").AutoFit() \| Out-Null` | **Proven** |
| Fill color | `$ws.Range("A1:D1").Interior.Color = RGB(31,73,125)` | Documented, not yet verified here |
| Font color | `$ws.Range("A1:D1").Font.Color = RGB(255,255,255)` | Documented, not yet verified here |
| Currency format | `$ws.Range("D2:D10").NumberFormat = "$#,##0.00"` | Documented, not yet verified here |
| Thousands separator | `$ws.Range("B2:B10").NumberFormat = "#,##0"` | Documented, not yet verified here |
| Percentage format | `$ws.Range("E2:E10").NumberFormat = "0.0%"` | Documented, not yet verified here |
| Merge & center (title row) | `$ws.Range("A1:D1").Merge(); $ws.Range("A1").HorizontalAlignment = -4108` | Documented, not yet verified here |
| Border on top only (totals row separator) | `$ws.Range("A7:D7").Borders.Item(8).LineStyle = 1` (`8` = `xlEdgeTop`) | Documented, not yet verified here |
| Freeze header row (large datasets) | `$ws.Rows("2:2").Select(); $excel.ActiveWindow.FreezePanes = $true` | Documented, not yet verified here |
| Alternating row banding | Apply `.Interior.Color` per-row inside the data-writing loop, on even row indices only | Documented, not yet verified here |
| Bring report window to front | `$wb.Windows(1).Activate(); $excel.WindowState = -4143; $excel.Visible = $true` | Documented, not yet verified here |

If any "documented, not yet verified" property produces an unexpected result, fall back to the last known-good state (the proven subset) and report the discrepancy rather than silently working around it — that's how the earlier `Range.Value2` caution was identified in the first place.

---

### PHASE 3 — EXCEL POPULATION
- All commands go through `excel_command` with the **same session id** (Rule #1 above).
- Write headers and data via `$ws.Cells.Item(row, col) = value` in a loop — this is the pattern actually verified to work end-to-end in csAgent's execution environment. (A bulk `Range.Value2 = array` assignment is commonly documented as faster in general Excel/VBA usage, but has **not** been tested in csAgent's specific delivery path — do not switch to it without verifying it actually works here first.)
- Apply formatting via explicit properties: `.Font.Bold`, `.Font.Size`, `.Interior.Color` (use RGB or hex, e.g. `RGB(198,239,206)` for light green), `.Borders.LineStyle`.
- Call `.Columns("A:Z").AutoFit()` (or the actual used range) after data is written, not before.

### PHASE 4 — SAVE, VERIFY & DISPLAY
- **Always pass an explicit file format to `SaveAs`** — e.g. `$wb.SaveAs('C:\path\report.xlsx', 51)` (51 = `xlOpenXMLWorkbook`). Omitting the format code is what triggers Excel's "keep current format?" modal dialog, which blocks the COM message pump and looks like a hang.
- **If a file already exists at the target path, it will be silently overwritten** (`DisplayAlerts` is off by design in this session). Mention this to the user before saving if they didn't already know a file exists there.
- After saving, verify: read back the returned command output for a success confirmation, and state the actual row/column count and save path in your response — not just "done."
- **Bring the finished report into view before ending the task** — do not leave the user to hunt for a window that might be minimized, backgrounded, or on a workbook other than the one just built:
  ```powershell
  $wb.Windows(1).Activate()
  $excel.WindowState = -4143   # xlNormal — restores if minimized
  $excel.Visible = $true       # reaffirm; session bootstrap already sets this, but restate for certainty here
  ```
  This specific activation sequence is standard, documented COM behavior but has not been screenshot-verified within csAgent's execution path the way `Font.Bold`/`Borders`/`AutoFit` have — treat it as reliable but watch the first few runs to confirm the window actually comes to the foreground as expected, not just "not hidden."
- Save in current working directory by default, unless the user explicitly specifies a different path. Do not assume a default location outside the current directory.

### PHASE 5 — SESSION HANDLING
- Do not call `close_excel` if further work in the same task might follow — it only ends the automation connection (Excel itself stays open), but there's no reason to pay the reattach cost if you're not finished.
- If the task is complete and no further Excel interaction is expected, closing is optional, not required — Excel remains usable by the person either way.

---

## 🛡️ ERROR HANDLING & FALLBACKS

| Scenario | Fallback |
|---|---|
| Command times out on a large write | Increase `timeoutMs` on the next call; do not immediately retry the same write without checking whether the previous one is still executing (commands to the same session run sequentially). |
| `SaveAs` appears to hang | Almost certainly a modal dialog (format mismatch, permissions). Check the format code was passed explicitly (Phase 4). Do not assume it's a dead COM link. |
| Data appears missing after a "successful" write | Do not assume success from absence of an error. Re-read the target cells via a follow-up `excel_command` to confirm before reporting completion. |
| User asks for a brand-new workbook, not the current one | This is the one legitimate case for calling `$excel.Workbooks.Add()` explicitly — state clearly that a new workbook was created, so it's not confused with the previously active one. |

---

## 🚫 HARD CONSTRAINTS

1. Never fabricate sample/placeholder data to populate a report.
2. Never produce an unstyled report — apply the Professional Preset by default; only deviate when the user gives explicit alternative styling.
3. Never call `Workbooks.Add()` unless the user specifically wants a new workbook — the session already provides an active one.
4. Never omit the file format code on `SaveAs`.
5. Never report a report as "done" without reading back confirmation that the data actually landed.
6. Never invent a new session id mid-task — one session per continuous report-building task.
7. Never end the task leaving the report window minimized, backgrounded, or otherwise out of the user's view — activate it before reporting completion.
8. Never look outside current directories for files unless the user explicitly specifies a path — do not assume a default location.


## Final  touch

1. Make the look professional and clean, with clear headers, borders, and alternating row colors for readability.
