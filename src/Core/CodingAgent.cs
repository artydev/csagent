using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CsAgentUI;

public sealed class CodingAgent : IDisposable
{
    private readonly LlmClient _client;
    private readonly AgentOptions _opts;
    private readonly IAgentObserver _observer;
    private CancellationTokenSource? _cts;

    private const int ShellTimeoutMs = 60_000;
    private const int TrimThresholdChars = 96_000;

    public CodingAgent(string apiKey, string endpoint, string model, AgentOptions opts, IAgentObserver observer)
    {
        _opts = opts;
        _observer = observer;
        _client = new LlmClient(apiKey, endpoint, model);
    }

    // ── Main loop ────────────────────────────────────────────────────────────

    public async Task RunAsync(JsonArray messages, string memoryFile)
    {
        _cts = new CancellationTokenSource();
        var isWindows = OperatingSystem.IsWindows();

        for (int step = 1; step <= _opts.MaxSteps; step++)
        {
            _cts.Token.ThrowIfCancellationRequested();
            await _observer.OnStep(step, _opts.MaxSteps);

            JsonNode response;
            try
            {
                response = await _client.CompleteChatAsync(messages, Tools, _cts.Token);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                await _observer.OnError($"API error: {ex.Message}");
                return;
            }

            var choice = response["choices"]?[0];
            var message = choice?["message"];
            if (message is null)
            {
                await _observer.OnError("Empty response from API.");
                return;
            }

            var text = message["content"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(text))
                await _observer.OnThought(text);

            messages.Add(message.DeepClone());

            var finishReason = choice?["finish_reason"]?.GetValue<string>();
            var toolCalls = message["tool_calls"]?.AsArray();

            if (toolCalls is null || toolCalls.Count == 0)
            {
                if (finishReason == "stop")
                {
                    await _observer.OnDone("Task complete.");
                    await MemoryStore.SaveAsync(memoryFile, messages);
                    return;
                }
                await _observer.OnDone("Assistant finished.");
                return;
            }

            foreach (var tc in toolCalls)
            {
                if (tc is null) continue;
                _cts.Token.ThrowIfCancellationRequested();

                var callId = tc["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString();
                var funcName = tc["function"]?["name"]?.GetValue<string>() ?? "unknown";
                var argsRaw = tc["function"]?["arguments"]?.GetValue<string>() ?? "{}";

                await _observer.OnToolCall(funcName, PrettyJson(argsRaw));

                string result;
                if (_opts.DryRun)
                {
                    result = "[dry-run] Tool not executed.";
                }
                else if (IsDestructive(funcName))
                {
                    result = UI.Confirm($"Allow destructive action '{funcName}'?")
                        ? await DispatchAsync(funcName, argsRaw, isWindows)
                        : "Tool call declined by user.";
                }
                else
                {
                    result = await DispatchAsync(funcName, argsRaw, isWindows);
                }

                var isError = result.StartsWith("Error:", StringComparison.OrdinalIgnoreCase)
                           || result.StartsWith("Shell error:", StringComparison.OrdinalIgnoreCase);

                await _observer.OnToolResult(result, isError);

                messages.Add(new JsonObject
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = callId,
                    ["content"] = result
                });
            }

            await MemoryStore.SaveAsync(memoryFile, messages);
            TrimHistory(messages);
        }

        await _observer.OnError($"Reached maximum of {_opts.MaxSteps} steps without completing.");
    }

    // ── Tool dispatch ────────────────────────────────────────────────────────

    private async Task<string> DispatchAsync(string name, string argsJson, bool isWindows)
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

                "sh" => await RunShellAsync(
                    args["cmd"]!.GetValue<string>(), isWindows),

                _ => $"Error: Unknown tool '{name}'"
            };
        }
        catch (Exception ex)
        {
            return $"Error: dispatch failed — {ex.Message}";
        }
    }

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
                // Skip hidden directories (starting with '.')
                var dirName = Path.GetFileName(d);
                if (dirName.StartsWith(".")) continue;
                sb.AppendLine($"[DIR]  {Path.GetRelativePath(full, d)}/");
            }

            foreach (var f in Directory.EnumerateFiles(full, "*", opt))
            {
                // Skip files in hidden directories
                var relPath = Path.GetRelativePath(full, f);
                if (relPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(p => p.StartsWith(".")))
                    continue;
                sb.AppendLine($"[FILE] {relPath}  ({Sz(new FileInfo(f).Length)})");
            }

            return sb.Length == 0 ? "(empty)" : sb.ToString().TrimEnd();
        }
        catch (Exception ex) { return $"Error: list_dir — {ex.Message}"; }
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

    // ── Safety checks ────────────────────────────────────────────────────────

    private static bool IsDestructive(string n) => n is "write_file";

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

    private static string PrettyJson(string raw)
    {
        try { return JsonNode.Parse(raw)?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? raw; }
        catch { return raw; }
    }

    private static string Sz(long b) =>
        b < 1024 ? $"{b} B" : b < 1_048_576 ? $"{b / 1024} KB" : $"{b / 1_048_576} MB";

    private static void TrimHistory(JsonArray msgs)
    {
        static int Len(JsonNode? m)
        {
            var c = m?["content"];
            return c is JsonValue v ? v.GetValue<string>().Length : (c?.ToJsonString().Length ?? 0);
        }
        int total = msgs.Sum(Len);
        while (total > TrimThresholdChars && msgs.Count > 3)
        {
            total -= Len(msgs[1]);
            msgs.RemoveAt(1);
        }
    }

    public void Dispose() => _client.Dispose();
    public void Cancel() => _cts?.Cancel();

    // ── Tool definitions ─────────────────────────────────────────────────────

    private static readonly JsonArray Tools = JsonNode.Parse("""
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
          }
        ]
        """)!.AsArray();

    // ── System message ───────────────────────────────────────────────────────

    public static JsonObject SystemMessage(bool isWindows) => new()
    {
        ["role"] = "system",
        ["content"] = $"""
            You are an autonomous, cross-platform coding agent.
            PLATFORM: {(isWindows ? "Windows - use cmd.exe syntax" : "Unix - use bash/sh syntax")}

            RULES:
            - Think step-by-step before acting.
            - Use read_file and list_dir to inspect the workspace before writing.
            - Use write_file for all file creation and modification.
            - Use sh for builds, tests, package installs, and system commands.
            - If a command fails, analyse the error and retry with a fix.
            - When the task is fully complete, say exactly "Task complete." and stop.
            - Never silently swallow errors.
            - ALL DESTRUCTIVE ACTIONS REQUIRE USER APPROVAL
            - FILE OPERATIONS ARE RESTRICTED TO THE CURRENT WORKING DIRECTORY ONLY
            - SHELL COMMANDS ARE FILTERED FOR POTENTIALLY DANGEROUS OPERATIONS
            """
    };
}
