using System.Diagnostics;
using System.Text;

namespace CsAgentUI.Core.Agent;

public static partial class ToolDispatcher
{
    /// <summary>
    /// Launches Microsoft Excel and controls it via COM using PowerShell.
    /// AOT-safe: shells out to powershell.exe, no P/Invoke, no NuGet.
    ///
    /// Behavior:
    ///   - Windows only (COM is Windows-specific).
    ///   - If 'path' is provided, a new workbook is created, populated with a
    ///     small sample table, saved to that path, and left open in Excel.
    ///   - If 'path' is omitted, Excel opens with a blank workbook.
    ///   - 'visible' controls whether the Excel window is shown (default true).
    ///
    /// The PowerShell script is passed via -EncodedCommand (base64 UTF-16LE) to
    /// avoid all quoting/escaping issues with the embedded script body.
    /// </summary>
    private static async Task<string> StartExcelAsync(string? path, bool visible, bool isWindows)
    {
        if (!isWindows)
            return "Error: start_excel — this tool is only supported on Windows (Excel COM automation).";

        try
        {
            // Validate the optional path (must be inside the working directory).
            string? fullPath = null;
            if (!string.IsNullOrWhiteSpace(path))
            {
                fullPath = Path.GetFullPath(path);
                if (!IsSafePath(fullPath))
                    return $"Error: start_excel - Path '{fullPath}' is not allowed. Only files in the current working directory are permitted.";

                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            }

            var script = BuildExcelScript(fullPath, visible);

            // Encode the script as base64 UTF-16LE for -EncodedCommand.
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

            // The script writes a single line of output on success.
            return string.IsNullOrWhiteSpace(output)
                ? "OK: Excel launched."
                : output.Trim();
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
    private static string BuildExcelScript(string? fullPath, bool visible)
    {
        var sb = new StringBuilder();
        sb.AppendLine("$ErrorActionPreference = 'Stop'");
        sb.AppendLine("try {");
        sb.AppendLine("    $excel = New-Object -ComObject Excel.Application");
        sb.AppendLine($"    $excel.Visible = ${(visible ? "true" : "false")}");
        sb.AppendLine("    $excel.DisplayAlerts = $false");
        sb.AppendLine("    $wb = $excel.Workbooks.Add()");

        if (!string.IsNullOrEmpty(fullPath))
        {
            // Populate a small sample table in the active worksheet.
            sb.AppendLine("    $ws = $wb.Worksheets.Item(1)");
            sb.AppendLine("    $ws.Name = 'Sample'");
            sb.AppendLine("    $ws.Cells.Item(1,1) = 'Item'");
            sb.AppendLine("    $ws.Cells.Item(1,2) = 'Quantity'");
            sb.AppendLine("    $ws.Cells.Item(1,3) = 'Price'");
            sb.AppendLine("    $ws.Cells.Item(2,1) = 'Widget'");
            sb.AppendLine("    $ws.Cells.Item(2,2) = 10");
            sb.AppendLine("    $ws.Cells.Item(2,3) = 4.99");
            sb.AppendLine("    $ws.Cells.Item(3,1) = 'Gadget'");
            sb.AppendLine("    $ws.Cells.Item(3,2) = 5");
            sb.AppendLine("    $ws.Cells.Item(3,3) = 12.50");
            sb.AppendLine("    $ws.Range('A1:C1').Font.Bold = $true");

            // Save the workbook to the requested path.
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
}
