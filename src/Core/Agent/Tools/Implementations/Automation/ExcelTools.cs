using System.Diagnostics;
using System.Text;

namespace CsAgentUI.Core.Agent;

public static partial class ToolDispatcher
{
    // ── excel_command / close_excel ─────────────────────────────────────────
    //
    // Live, interactive Excel automation. Unlike a one-shot script, this keeps
    // a single persistent powershell.exe process per session alive across
    // multiple tool calls, with $excel / $wb / $ws surviving between calls —
    // exactly the same pattern as run_terminal's TerminalSession, just with
    // an Excel COM bootstrap sent once at session start.
    //
    // Because the driver process never exits between commands, Excel's
    // UserControl flag isn't even load-bearing here (there's no premature
    // disconnect to trigger it) — it's still set for correctness/safety in
    // case the LLM independently detaches, and so Excel survives session close.

    private static readonly Dictionary<string, ExcelSession> ExcelSessions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object ExcelLock = new();

    private static async Task<string> ExcelCommandAsync(string command, string session, int timeoutMs, bool isWindows)
    {
        try
        {
            if (!isWindows)
                return "Error: excel_command - only supported on Windows (Excel COM automation).";

            if (string.IsNullOrWhiteSpace(command))
                return "Error: excel_command - 'command' argument is required.";

            if (!IsSafeCommand(command, isWindows))
                return $"Error: excel_command - command contains potentially dangerous operations and is not allowed.";

            if (timeoutMs < 1) timeoutMs = 1;
            if (timeoutMs > 300_000) timeoutMs = 300_000;

            ExcelSession sess;
            lock (ExcelLock)
            {
                if (!ExcelSessions.TryGetValue(session, out sess!))
                {
                    sess = new ExcelSession(session);
                    ExcelSessions[session] = sess;
                }
            }

            return await sess.RunAsync(command, timeoutMs);
        }
        catch (Exception ex) { return $"Error: excel_command — {ex.Message}"; }
    }

    private static string CloseExcel(string session)
    {
        try
        {
            lock (ExcelLock)
            {
                if (ExcelSessions.TryGetValue(session, out var sess))
                {
                    sess.Dispose();
                    ExcelSessions.Remove(session);
                    return $"OK: closed Excel session '{session}'. Excel itself was left running (UserControl mode) — quit it explicitly with a '$excel.Quit()' command first if you want it to close too.";
                }
                return $"OK: no active Excel session '{session}' to close.";
            }
        }
        catch (Exception ex) { return $"Error: close_excel — {ex.Message}"; }
    }

    /// <summary>
    /// A persistent, interactive PowerShell process with Excel COM bootstrapped
    /// once at first use. $excel / $wb / $ws remain valid across every
    /// subsequent excel_command call in this session, since it's the same
    /// living process the whole time — no re-attach, no GetActiveObject
    /// guessing on each call.
    /// </summary>
    private sealed class ExcelSession : IDisposable
    {
        private readonly string _id;
        private readonly Process _proc;
        private readonly StringBuilder _output = new();
        private readonly object _lock = new();
        private bool _disposed;
        private bool _bootstrapped;

        public ExcelSession(string id)
        {
            _id = id;

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NoLogo",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            _proc = new Process { StartInfo = psi };
            _proc.Start();

            _proc.OutputDataReceived += (_, e) => { if (e.Data is not null) Append(e.Data); };
            _proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) Append(e.Data); };
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();
        }

        private void Append(string line)
        {
            lock (_lock)
            {
                _output.AppendLine(line);
                if (_output.Length > 512_000)
                    _output.Remove(0, _output.Length - 512_000);
            }
        }

        public async Task<string> RunAsync(string command, int timeoutMs)
        {
            if (_disposed || _proc.HasExited)
                return $"Error: excel_command - session '{_id}' is no longer running. Send another command to start a new one.";

            var toSend = _bootstrapped ? command : Bootstrap() + "\n" + command;

            int startPos;
            lock (_lock) { startPos = _output.Length; }

            var marker = $"__CSAGENT_EXCEL_DONE_{Guid.NewGuid():N}__";
            var line = $"{toSend}; Write-Output '{marker}'";

            await _proc.StandardInput.WriteLineAsync(line);
            await _proc.StandardInput.FlushAsync();

            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                string current;
                lock (_lock) { current = _output.ToString(); }

                if (current.Contains(marker))
                {
                    lock (_lock)
                    {
                        var newText = _output.ToString(startPos, _output.Length - startPos);
                        newText = newText.Replace(marker, "").Trim();
                        _bootstrapped = true;
                        return string.IsNullOrWhiteSpace(newText)
                            ? $"OK (excel session '{_id}'): (no output)"
                            : $"OK (excel session '{_id}'):\n{newText}";
                    }
                }

                await Task.Delay(50);
            }

            return $"Error: excel_command - command timed out after {timeoutMs} ms in session '{_id}'. The session is still running; you can send another command or close it with close_excel.";
        }

        /// <summary>
        /// PowerShell run once, at the start of the session: attach to an
        /// already-running Excel if one exists, otherwise create one; then
        /// ensure a workbook and worksheet reference are ready as $wb/$ws.
        /// </summary>
        private static string Bootstrap() => string.Join("; ", new[]
        {
            "$reused = $true",
            "try { $excel = [Runtime.InteropServices.Marshal]::GetActiveObject('Excel.Application') } " +
                "catch { $reused = $false; $excel = New-Object -ComObject Excel.Application }",
            "if (-not $reused) { $excel.Visible = $true; $excel.UserControl = $true }",
            "$excel.DisplayAlerts = $false",
            "if (-not $wb) { $wb = $excel.Workbooks.Add() }",
            "if (-not $ws) { $ws = $wb.Worksheets.Item(1) }"
        });

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (!_proc.HasExited)
                {
                    // Only end the PowerShell driver — Excel itself (UserControl
                    // mode) is deliberately left running for the user.
                    try { _proc.StandardInput.WriteLine("exit"); _proc.StandardInput.Flush(); } catch { }
                    try { _proc.Kill(entireProcessTree: true); } catch { }
                }
                _proc.Dispose();
            }
            catch { /* best effort */ }
        }
    }
}