using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;

namespace CsAgentUI.Core.Agent;

public static partial class ToolDispatcher
{
    /// <summary>
    /// Launches Microsoft Excel and controls it via COM using PowerShell.
    /// AOT-safe: shells out to powershell.exe, no P/Invoke, no NuGet.
    ///
    /// Behavior:
    ///   - Windows only (COM is Windows-specific).
    ///   - Reuses an already-running Excel instance if one exists
    ///     (via [Marshal]::GetActiveObject), otherwise starts a new one.
    ///     'visible' is only applied to newly-created instances — an
    ///     existing interactive session is never forced hidden or shown.
    ///   - A new, blank workbook is always added. If 'data' is provided
    ///     (a 2D array of cell values), it is written into that workbook
    ///     starting at A1. No sample/placeholder data is ever inserted.
    ///   - If 'path' is provided, the workbook is saved there — DESTRUCTIVE:
    ///     an existing file at that path is silently overwritten
    ///     (DisplayAlerts is off), so this tool requires confirmation.
    ///   - If 'path' is omitted, the workbook is left open, unsaved.
    ///
    /// The PowerShell script is passed via -EncodedCommand (base64 UTF-16LE)
    /// to avoid all quoting/escaping issues with the embedded script body.
    /// </summary>
    private static async Task<string> StartExcelAsync(string? path, bool visible, JsonArray? data, bool isWindows)
    {
        if (!isWindows)
            return "Error: start_excel — this tool is only supported on Windows (Excel COM automation).";

        try
        {
            string? fullPath = null;
            bool overwriting = false;

            if (!string.IsNullOrWhiteSpace(path))
            {
                fullPath = Path.GetFullPath(path);
                if (!IsSafePath(fullPath))
                    return $"Error: start_excel - Path '{fullPath}' is not allowed. Only files in the current working directory are permitted.";

                overwriting = File.Exists(fullPath);

                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            }

            var script = BuildExcelScript(fullPath, visible, data);
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-NonInteractive");
            psi.ArgumentList.Add("-EncodedCommand");
            psi.ArgumentList.Add(encoded);

            using var proc = new Process { StartInfo = psi };
            proc.Start();

            var outTask = proc.StandardOutput.ReadToEndAsync();
            var errTask = proc.StandardError.ReadToEndAsync();
            var waitTask = proc.WaitForExitAsync();

            if (await Task.WhenAny(waitTask, Task.Delay(60_000)) != waitTask)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                return "Error: start_excel — timed out after 60 s.";
            }

            var output = (await outTask).Trim();
            var error = (await errTask).Trim();

            if (proc.ExitCode != 0)
                return $"Error: start_excel — PowerShell exited with code {proc.ExitCode}.\n{error}\n{output}".Trim();

            var suffix = overwriting ? " (existing file was overwritten)" : "";
            return (string.IsNullOrWhiteSpace(output)
                ? "OK: Excel launched."
                : output.Trim()) + suffix;
        }
        catch (Exception ex)
        {
            return $"Error: start_excel — {ex.Message}";
        }
    }

    /// <summary>
    /// Builds the PowerShell script that drives Excel via COM.
    /// On success it writes a single confirmation line to stdout.
    /// </summary>
    private static string BuildExcelScript(string? fullPath, bool visible, JsonArray? data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("$ErrorActionPreference = 'Stop'");
        sb.AppendLine("try {");

        // Reuse a running Excel instance if one exists; only apply 'visible'
        // when we actually create a new instance, so we never yank an
        // existing interactive session's window state around.
        sb.AppendLine("    $reused = $true");
        sb.AppendLine("    try {");
        sb.AppendLine("        $excel = [Runtime.InteropServices.Marshal]::GetActiveObject('Excel.Application')");
        sb.AppendLine("    } catch {");
        sb.AppendLine("        $reused = $false");
        sb.AppendLine("        $excel = New-Object -ComObject Excel.Application");
        sb.AppendLine("    }");
        sb.AppendLine("    if (-not $reused) {");
        sb.AppendLine($"        $excel.Visible = ${(visible ? "true" : "false")}");
        sb.AppendLine("    }");
        sb.AppendLine("    $excel.DisplayAlerts = $false");
        sb.AppendLine("    $wb = $excel.Workbooks.Add()");
        sb.AppendLine("    $ws = $wb.Worksheets.Item(1)");

        if (data is { Count: > 0 })
        {
            for (int r = 0; r < data.Count; r++)
            {
                if (data[r] is not JsonArray row) continue;
                for (int c = 0; c < row.Count; c++)
                {
                    var literal = CellToPsLiteral(row[c]);
                    if (literal is null) continue; // skip null/empty cells
                    sb.AppendLine($"    $ws.Cells.Item({r + 1},{c + 1}) = {literal}");
                }
            }
        }

        if (!string.IsNullOrEmpty(fullPath))
        {
            var safePath = fullPath.Replace("'", "''");
            sb.AppendLine($"    $wb.SaveAs('{safePath}')");
            sb.AppendLine($"    Write-Output \"OK: Excel launched and workbook saved to '{safePath}'.\"");
        }
        else
        {
            sb.AppendLine("    Write-Output 'OK: Excel launched with a blank workbook.'");
        }

        sb.AppendLine("}");
        sb.AppendLine("catch {");
        sb.AppendLine("    Write-Error $_.Exception.Message");
        sb.AppendLine("    exit 1");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Converts a single JSON cell value into a PowerShell literal suitable
    /// for direct assignment to a Range's value (string/number/bool). Returns
    /// null for a JSON null (meaning: leave the cell untouched).
    /// </summary>
    private static string? CellToPsLiteral(JsonNode? cell)
    {
        if (cell is null) return null;
        if (cell is not JsonValue val) return null;

        if (val.TryGetValue<bool>(out var b)) return b ? "$true" : "$false";
        if (val.TryGetValue<long>(out var l)) return l.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (val.TryGetValue<double>(out var d)) return d.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var s = val.GetValue<string>() ?? "";
        var escaped = s.Replace("'", "''");
        return $"'{escaped}'";
    }
}