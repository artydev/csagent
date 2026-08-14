using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CsAgentUI.Core.Agent;

/// <summary>
/// Pure tool execution logic — no observer, no agent loop.
/// All safety checks (path, command, destructive) are here.
/// </summary>
public static class ToolDispatcher
{
    private const int ShellTimeoutMs = 60_000;
    private const int MaxSearchResults = 200;
    private const long MaxSearchFileBytes = 1_048_576; // skip binary/large files

    /// <summary>
    /// Delegate used by the switch_model tool to change the active model at runtime.
    /// Returns a human-readable confirmation/error message.
    /// </summary>
    public delegate string SwitchModelHandler(string model);

    /// <summary>
    /// Dispatch a tool call by name with the given JSON arguments.
    /// </summary>
    /// <param name="name">The tool name.</param>
    /// <param name="argsJson">JSON string of the tool arguments.</param>
    /// <param name="isWindows">Whether the host OS is Windows.</param>
    /// <param name="switchModel">Optional callback invoked by the switch_model tool.</param>
    public static async Task<string> DispatchAsync(
        string name,
        string argsJson,
        bool isWindows,
        SwitchModelHandler? switchModel = null)
    {
        try
        {
            var args = JsonNode.Parse(argsJson) ?? new JsonObject();
            return name switch
            {
                "write_file" => WriteFile(
                    args["path"]!.GetValue<string>(),
                    args["content"]!.GetValue<string>()),

                "read_file" => ReadFile(
                    args["path"]!.GetValue<string>()),

                "list_dir" => ListDir(
                    args["path"]?.GetValue<string>() ?? ".",
                    args["recursive"]?.GetValue<bool>() ?? false),

                "search_files" => SearchFiles(
                    args["pattern"]!.GetValue<string>(),
                    args["path"]?.GetValue<string>() ?? ".",
                    args["glob"]?.GetValue<string>() ?? "*"),

                "edit_file" => EditFile(
                    args["path"]!.GetValue<string>(),
                    args["edits"]),

                "sh" => await RunShellAsync(
                    args["cmd"]!.GetValue<string>(), isWindows),

                "switch_model" => SwitchModel(
                    args["model"]!.GetValue<string>(),
                    switchModel),

                _ => $"Error: Unknown tool '{name}'"
            };
        }
        catch (Exception ex)
        {
            return $"Error: dispatch failed — {ex.Message}";
        }
    }

    /// <summary>
    /// Returns true if the tool name is considered destructive (requires user confirmation).
    /// </summary>
    public static bool IsDestructive(string name) => name is "write_file" or "edit_file";

    /// <summary>
    /// The JSON tool definitions for the LLM API.
    /// </summary>
    public static readonly JsonArray ToolDefinitions = JsonNode.Parse("""
        [
          {
            "type": "function",
            "function": {
              "name": "write_file",
              "description": "Write (or overwrite) a text file. Parent directories are created automatically.",
              "parameters": {
                "type": "object",
                "properties": {
                  "path":    { "type": "string", "description": "File path." },
                  "content": { "type": "string", "description": "UTF-8 content to write." }
                },
                "required": ["path", "content"]
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "read_file",
              "description": "Read a text file and return its content.",
              "parameters": {
                "type": "object",
                "properties": {
                  "path": { "type": "string", "description": "File path." }
                },
                "required": ["path"]
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "list_dir",
              "description": "List files and subdirectories in a directory.",
              "parameters": {
                "type": "object",
                "properties": {
                  "path":      { "type": "string",  "description": "Directory to list. Defaults to '.'." },
                  "recursive": { "type": "boolean", "description": "Whether to list recursively." }
                },
                "required": []
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "search_files",
              "description": "Recursively search for a text pattern (grep) inside files under a directory. Returns matching file paths and line numbers. Use this to find where symbols, strings, or code are referenced.",
              "parameters": {
                "type": "object",
                "properties": {
                  "pattern": { "type": "string", "description": "The literal text or substring to search for (case-insensitive)." },
                  "path":    { "type": "string", "description": "Directory to search. Defaults to '.'." },
                  "glob":    { "type": "string", "description": "Optional file glob filter, e.g. '*.cs' or '*.js'. Defaults to '*'." }
                },
                "required": ["pattern"]
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "edit_file",
              "description": "Apply precise find-and-replace edits to an existing text file without rewriting the whole file. Provide an array of edits, each with an 'old_string' (exact text to find, must appear exactly once) and a 'new_string' (replacement). All edits are applied atomically; if any edit fails, no changes are written.",
              "parameters": {
                "type": "object",
                "properties": {
                  "path": { "type": "string", "description": "File path to edit." },
                  "edits": {
                    "type": "array",
                    "description": "List of edits to apply. Each edit replaces an exact old_string with a new_string.",
                    "items": {
                      "type": "object",
                      "properties": {
                        "old_string": { "type": "string", "description": "Exact text to find. Must appear exactly once in the file." },
                        "new_string": { "type": "string", "description": "Replacement text." }
                      },
                      "required": ["old_string", "new_string"]
                    }
                  }
                },
                "required": ["path", "edits"]
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "sh",
              "description": "Execute a shell command. Uses cmd.exe on Windows, /bin/sh elsewhere.",
              "parameters": {
                "type": "object",
                "properties": {
                  "cmd": { "type": "string", "description": "Shell command to run." }
                },
                "required": ["cmd"]
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "switch_model",
              "description": "Switch the active LLM model for the current session. Use this when the user asks to change or switch the model.",
              "parameters": {
                "type": "object",
                "properties": {
                  "model": { "type": "string", "description": "The model identifier to switch to (e.g. 'openai/gpt-oss-120b')." }
                },
                "required": ["model"]
              }
            }
          }
        ]
        """)!.AsArray();

    // ── write_file ───────────────────────────────────────────────────────────

    private static string WriteFile(string path, string content)
    {
        try
        {
            var full = Path.GetFullPath(path);
            if (!IsSafePath(full))
                return $"Error: write_file - Path '{full}' is not allowed for writing. Only files in the current working directory are permitted.";

            var dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(full, content, new UTF8Encoding(false));
            return $"OK: wrote {new FileInfo(full).Length} bytes to '{full}'";
        }
        catch (Exception ex) { return $"Error: write_file — {ex.Message}"; }
    }

    // ── read_file ────────────────────────────────────────────────────────────

    private static string ReadFile(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            if (!IsSafePath(full))
                return $"Error: read_file - Path '{full}' is not allowed for reading. Only files in the current working directory are permitted.";

            if (!File.Exists(full)) return $"Error: not found '{full}'";
            var len = new FileInfo(full).Length;
            if (len > 512_000) return $"Error: file too large ({len / 1024} KB). Use sh to grep/head.";
            return File.ReadAllText(full, Encoding.UTF8);
        }
        catch (Exception ex) { return $"Error: read_file — {ex.Message}"; }
    }

    // ── list_dir ─────────────────────────────────────────────────────────────

    private static string ListDir(string path, bool recursive)
    {
        try
        {
            var full = Path.GetFullPath(path);
            if (!IsSafePath(full))
                return $"Error: list_dir - Path '{full}' is not allowed for listing. Only directories in the current working directory are permitted.";

            if (!Directory.Exists(full)) return $"Error: directory not found '{full}'";

            var opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var sb = new StringBuilder();

            foreach (var d in Directory.EnumerateDirectories(full, "*", opt))
            {
                var dirName = Path.GetFileName(d);
                if (dirName.StartsWith(".")) continue;
                sb.AppendLine($"[DIR]  {Path.GetRelativePath(full, d)}/");
            }

            foreach (var f in Directory.EnumerateFiles(full, "*", opt))
            {
                var relPath = Path.GetRelativePath(full, f);
                if (relPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(p => p.StartsWith(".")))
                    continue;
                sb.AppendLine($"[FILE] {relPath}  ({Sz(new FileInfo(f).Length)})");
            }

            return sb.Length == 0 ? "(empty)" : sb.ToString().TrimEnd();
        }
        catch (Exception ex) { return $"Error: list_dir — {ex.Message}"; }
    }

    // ── search_files (grep) ──────────────────────────────────────────────────

    private static string SearchFiles(string pattern, string path, string glob)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return "Error: search_files - 'pattern' argument is required.";

            var full = Path.GetFullPath(path);
            if (!IsSafePath(full))
                return $"Error: search_files - Path '{full}' is not allowed for searching. Only directories in the current working directory are permitted.";

            if (!Directory.Exists(full)) return $"Error: directory not found '{full}'";

            var sb = new StringBuilder();
            var count = 0;
            var needle = pattern;

            foreach (var file in Directory.EnumerateFiles(full, glob, SearchOption.AllDirectories))
            {
                var relPath = Path.GetRelativePath(full, file);
                var parts = relPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // Skip hidden directories (starting with '.')
                if (parts.Any(p => p.StartsWith(".")))
                    continue;

                // Skip build output directories (bin/ and obj/)
                if (parts.Any(p => p.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                                   p.Equals("obj", StringComparison.OrdinalIgnoreCase)))
                    continue;

                var fi = new FileInfo(file);
                if (fi.Length > MaxSearchFileBytes) continue;

                // Skip binary files (detect null bytes in the first chunk)
                if (IsBinaryFile(file)) continue;

                string[] lines;
                try { lines = File.ReadAllLines(file, Encoding.UTF8); }
                catch { continue; }

                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains(needle, StringComparison.OrdinalIgnoreCase))
                    {
                        sb.AppendLine($"{relPath}:{i + 1}: {lines[i].Trim()}");
                        if (++count >= MaxSearchResults)
                        {
                            sb.AppendLine($"... (truncated at {MaxSearchResults} matches)");
                            return sb.ToString().TrimEnd();
                        }
                    }
                }
            }

            return count == 0
                ? $"No matches for '{pattern}' under '{full}'."
                : sb.ToString().TrimEnd();
        }
        catch (Exception ex) { return $"Error: search_files — {ex.Message}"; }
    }

    // ── edit_file ────────────────────────────────────────────────────────────

    private static string EditFile(string path, JsonNode? editsNode)
    {
        try
        {
            var full = Path.GetFullPath(path);
            if (!IsSafePath(full))
                return $"Error: edit_file - Path '{full}' is not allowed for editing. Only files in the current working directory are permitted.";

            if (!File.Exists(full)) return $"Error: not found '{full}'";

            if (editsNode is not JsonArray edits || edits.Count == 0)
                return "Error: edit_file - 'edits' must be a non-empty array of {old_string, new_string} objects.";

            var original = File.ReadAllText(full, Encoding.UTF8);
            var working = original;
            var applied = new List<string>();

            foreach (var edit in edits)
            {
                if (edit is not JsonObject obj ||
                    obj["old_string"] is not JsonValue oldVal ||
                    obj["new_string"] is not JsonValue newVal)
                    return "Error: edit_file - each edit must be an object with 'old_string' and 'new_string' string fields.";

                var oldStr = oldVal.GetValue<string>();
                var newStr = newVal.GetValue<string>();

                if (string.IsNullOrEmpty(oldStr))
                    return "Error: edit_file - 'old_string' cannot be empty.";

                // Count occurrences in the current working text.
                int idx = working.IndexOf(oldStr, StringComparison.Ordinal);
                if (idx < 0)
                    return $"Error: edit_file - 'old_string' not found in '{full}':\n{oldStr}";

                if (working.IndexOf(oldStr, idx + oldStr.Length, StringComparison.Ordinal) >= 0)
                    return $"Error: edit_file - 'old_string' appears more than once in '{full}'. Provide more context to make it unique:\n{oldStr}";

                working = working.Remove(idx, oldStr.Length).Insert(idx, newStr);
                applied.Add(oldStr);
            }

            // All edits validated — write atomically.
            File.WriteAllText(full, working, new UTF8Encoding(false));
            return $"OK: applied {applied.Count} edit(s) to '{full}'.";
        }
        catch (Exception ex) { return $"Error: edit_file — {ex.Message}"; }
    }

    // ── sh ───────────────────────────────────────────────────────────────────

    private static async Task<string> RunShellAsync(string cmd, bool isWindows)
    {
        try
        {
            if (!IsSafeCommand(cmd, isWindows))
                return $"Error: sh - Command '{cmd}' contains potentially dangerous operations and is not allowed.";

            var (file, shellArgs) = isWindows
                ? ("cmd.exe", $"/d /s /c \"{cmd}\"")
                : ("/bin/sh", $"-c \"{cmd.Replace("\"", "\\\"")}\"");

            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = shellArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            proc.Start();
            var outTask = proc.StandardOutput.ReadToEndAsync();
            var errTask = proc.StandardError.ReadToEndAsync();
            var waitTask = proc.WaitForExitAsync();

            if (await Task.WhenAny(waitTask, Task.Delay(ShellTimeoutMs)) != waitTask)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                return "Error: command timed out (60s).";
            }

            var output = ((await outTask) + (await errTask)).Trim();
            var prefix = proc.ExitCode == 0 ? $"OK (exit 0):\n" : $"Error (exit {proc.ExitCode}):\n";
            return string.IsNullOrWhiteSpace(output)
                ? prefix.TrimEnd()
                : prefix + output;
        }
        catch (Exception ex) { return $"Shell error: {ex.Message}"; }
    }

    // ── switch_model ─────────────────────────────────────────────────────────

    private static string SwitchModel(string model, SwitchModelHandler? switchModel)
    {
        if (string.IsNullOrWhiteSpace(model))
            return "Error: switch_model - 'model' argument is required.";

        if (switchModel is null)
            return "Error: switch_model - model switching is not available in this context.";

        return switchModel(model.Trim());
    }

    // ── Safety checks ────────────────────────────────────────────────────────

    private static bool IsSafePath(string fullPath)
    {
        try
        {
            var currentDir = Directory.GetCurrentDirectory();
            var normalizedCurrent = Path.GetFullPath(currentDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedPath = Path.GetFullPath(fullPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return normalizedPath.StartsWith(normalizedCurrent, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSafeCommand(string cmd, bool isWindows)
    {
        var lowerCmd = cmd.ToLowerInvariant();

        if (isWindows)
        {
            var windowsDangerous = new[]
            {
                "format ", "format.", "del /f", "del /s", "rd /s", "rmdir /s",
                "reg delete", "reg add", "reg import",
                "net user", "net localgroup", "net share", "net use",
                "takeown", "icacls", "cacls",
                "attrib -r -s -h", "bcdedit", "diskpart",
                "powershell start-process -verb runas", "runas",
                "shutdown", "reboot",
                "\\windows\\system32\\", "\\windows\\system\\", "\\program files\\",
            };
            foreach (var pattern in windowsDangerous)
                if (lowerCmd.Contains(pattern)) return false;
        }
        else
        {
            var unixDangerous = new[]
            {
                "sudo ", "chmod", "shutdown", "reboot", "dd ", "mkfs",
                "/etc/", "/usr/bin/", "/bin/",
            };
            foreach (var pattern in unixDangerous)
                if (lowerCmd.Contains(pattern)) return false;
        }

        return true;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Detects whether a file is binary by scanning the first 8 KB for null bytes
    /// or a high proportion of non-printable characters.
    /// </summary>
    private static bool IsBinaryFile(string filePath)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buffer = new byte[Math.Min(fs.Length, 8192)];
            int read = fs.Read(buffer, 0, buffer.Length);

            int nullCount = 0;
            int nonPrintableCount = 0;
            for (int i = 0; i < read; i++)
            {
                if (buffer[i] == 0) nullCount++;
                else if (buffer[i] < 9 || (buffer[i] > 13 && buffer[i] < 32)) nonPrintableCount++;
            }

            // Binary if there are null bytes or a high ratio of control characters.
            if (nullCount > 0) return true;
            if (read > 0 && (double)nonPrintableCount / read > 0.30) return true;
            return false;
        }
        catch
        {
            return true; // If we can't read it, treat as binary to be safe.
        }
    }

    private static string Sz(long b) =>
        b < 1024 ? $"{b} B" : b < 1_048_576 ? $"{b / 1024} KB" : $"{b / 1_048_576} MB";
}
