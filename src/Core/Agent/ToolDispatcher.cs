using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
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
    private const int MaxTreeEntries = 500;            // cap tree output to avoid huge responses
    private const int MaxParseOutputBytes = 512_000;   // cap parse_output input size

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

                "read_json" => ReadJson(
                    args["path"]!.GetValue<string>(),
                    args["query"]?.GetValue<string>()),

                "list_dir" => ListDir(
                    args["path"]?.GetValue<string>() ?? ".",
                    args["recursive"]?.GetValue<bool>() ?? false),

                "tree" => Tree(
                    args["path"]?.GetValue<string>() ?? ".",
                    args["depth"]?.GetValue<int>() ?? -1),

                "search_files" => SearchFiles(
                    args["pattern"]!.GetValue<string>(),
                    args["path"]?.GetValue<string>() ?? ".",
                    args["glob"]?.GetValue<string>() ?? "*"),

                "edit_file" => EditFile(
                    args["path"]!.GetValue<string>(),
                    args["edits"]),

                "copy_file" => CopyFile(
                    args["source"]!.GetValue<string>(),
                    args["destination"]!.GetValue<string>()),

                "move_file" => MoveFile(
                    args["source"]!.GetValue<string>(),
                    args["destination"]!.GetValue<string>()),

                "delete_file" => DeleteFile(
                    args["path"]!.GetValue<string>()),

                "zip" => Zip(
                    args["source"]!.GetValue<string>(),
                    args["destination"]!.GetValue<string>()),

                "unzip" => Unzip(
                    args["archive"]!.GetValue<string>(),
                    args["destination"]!.GetValue<string>()),

                "parse_output" => ParseOutput(
                    args["output"]!.GetValue<string>(),
                    args["format"]?.GetValue<string>() ?? "auto",
                    args["query"]?.GetValue<string>()),

                "git_status" => await GitStatusAsync(
                    args["path"]?.GetValue<string>() ?? "."),

                "git_diff" => await GitDiffAsync(
                    args["path"]?.GetValue<string>() ?? ".",
                    args["staged"]?.GetValue<bool>() ?? false),

                "git_log" => await GitLogAsync(
                    args["path"]?.GetValue<string>() ?? ".",
                    args["count"]?.GetValue<int>() ?? 20),

                "git_branch" => await GitBranchAsync(
                    args["path"]?.GetValue<string>() ?? "."),

                "git_commit" => await GitCommitAsync(
                    args["path"]?.GetValue<string>() ?? ".",
                    args["message"]!.GetValue<string>()),

                "sh" => await RunShellAsync(
                    args["cmd"]!.GetValue<string>(), isWindows),

                "http_request" => await HttpRequestAsync(
                    args["url"]!.GetValue<string>(),
                    args["method"]?.GetValue<string>() ?? "GET",
                    args["headers"] as JsonObject,
                    args["body"]?.GetValue<string>(),
                    args["timeoutMs"]?.GetValue<int>() ?? 30_000),

                "web_search" => await WebSearchAsync(
                    args["query"]!.GetValue<string>(),
                    args["maxResults"]?.GetValue<int>() ?? 5),

                "fetch_url" => await FetchUrlAsync(
                    args["url"]!.GetValue<string>(),
                    args["maxChars"]?.GetValue<int>() ?? 20_000),

                "run_terminal" => await RunTerminalAsync(
                    args["cmd"]!.GetValue<string>(),
                    args["session"]?.GetValue<string>() ?? "default",
                    args["timeoutMs"]?.GetValue<int>() ?? 60_000,
                    isWindows),

                "close_terminal" => CloseTerminal(
                    args["session"]?.GetValue<string>() ?? "default"),

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
    public static bool IsDestructive(string name) =>
        name is "write_file" or "edit_file" or "git_commit" or "move_file" or "delete_file" or "unzip";

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
              "name": "read_json",
              "description": "Read a JSON file and return it as pretty-printed JSON. Optionally provide a 'query' (a dot-path like 'a.b[0].c') to extract just a sub-value. Use this to inspect structured data files (config, package.json, lockfiles, etc.).",
              "parameters": {
                "type": "object",
                "properties": {
                  "path":  { "type": "string", "description": "Path of the JSON file to read." },
                  "query": { "type": "string", "description": "Optional dot-path to extract a sub-value, e.g. 'dependencies.react' or 'scripts[0]'." }
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
              "name": "tree",
              "description": "Display a visual, indented directory tree of the given path. Directories are shown with a trailing '/'. Use 'depth' to limit how many levels deep to recurse (-1 for unlimited). Hidden directories (starting with '.') and build output (bin/, obj/) are skipped.",
              "parameters": {
                "type": "object",
                "properties": {
                  "path":  { "type": "string",  "description": "Directory to display. Defaults to '.'." },
                  "depth": { "type": "integer", "description": "Maximum recursion depth. -1 (default) means unlimited." }
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
              "name": "copy_file",
              "description": "Copy a file from source to destination. Parent directories of the destination are created automatically.",
              "parameters": {
                "type": "object",
                "properties": {
                  "source":      { "type": "string", "description": "Path of the file to copy." },
                  "destination": { "type": "string", "description": "Destination path for the copy." }
                },
                "required": ["source", "destination"]
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "move_file",
              "description": "Move (rename) a file from source to destination. Parent directories of the destination are created automatically. Destructive — requires user confirmation.",
              "parameters": {
                "type": "object",
                "properties": {
                  "source":      { "type": "string", "description": "Path of the file to move." },
                  "destination": { "type": "string", "description": "Destination path for the move." }
                },
                "required": ["source", "destination"]
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "delete_file",
              "description": "Permanently delete a file. Destructive — requires user confirmation.",
              "parameters": {
                "type": "object",
                "properties": {
                  "path": { "type": "string", "description": "Path of the file to delete." }
                },
                "required": ["path"]
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "zip",
              "description": "Create a zip archive from a source file or directory. If the source is a directory, its contents are archived recursively.",
              "parameters": {
                "type": "object",
                "properties": {
                  "source":      { "type": "string", "description": "File or directory to archive." },
                  "destination": { "type": "string", "description": "Path of the .zip archive to create." }
                },
                "required": ["source", "destination"]
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "unzip",
              "description": "Extract a zip archive into a destination directory. The destination directory is created if it does not exist. Destructive — overwrites existing files, requires user confirmation.",
              "parameters": {
                "type": "object",
                "properties": {
                  "archive":     { "type": "string", "description": "Path of the .zip archive to extract." },
                  "destination": { "type": "string", "description": "Directory to extract into." }
                },
                "required": ["archive", "destination"]
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "parse_output",
              "description": "Parse a block of command output into structured data and return it as pretty-printed JSON. Use 'format' to hint the format: 'json' (parse as JSON), 'keyvalue' (parse 'key=value' or 'key: value' lines), 'csv' (parse comma/tab-separated rows), or 'auto' (default, auto-detect). Optionally provide a 'query' (dot-path) to extract just a sub-value from the parsed result.",
              "parameters": {
                "type": "object",
                "properties": {
                  "output": { "type": "string", "description": "The raw command output text to parse." },
                  "format": { "type": "string", "description": "Parsing format: 'json', 'keyvalue', 'csv', or 'auto' (default)." },
                  "query":  { "type": "string", "description": "Optional dot-path to extract a sub-value from the parsed result." }
                },
                "required": ["output"]
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "git_status",
              "description": "Show the working tree status (modified, staged, untracked files) of the git repository containing the given path.",
              "parameters": {
                "type": "object",
                "properties": {
                  "path": { "type": "string", "description": "Directory inside the git repo. Defaults to '.'." }
                },
                "required": []
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "git_diff",
              "description": "Show uncommitted changes. By default shows unstaged changes; set 'staged' to true to show staged (index) changes.",
              "parameters": {
                "type": "object",
                "properties": {
                  "path":  { "type": "string", "description": "Directory inside the git repo. Defaults to '.'." },
                  "staged": { "type": "boolean", "description": "If true, show staged changes instead of unstaged. Defaults to false." }
                },
                "required": []
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "git_log",
              "description": "Show the recent commit history of the git repository.",
              "parameters": {
                "type": "object",
                "properties": {
                  "path":  { "type": "string", "description": "Directory inside the git repo. Defaults to '.'." },
                  "count": { "type": "integer", "description": "Number of commits to show. Defaults to 20." }
                },
                "required": []
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "git_branch",
              "description": "Show the current branch and list all local branches of the git repository.",
              "parameters": {
                "type": "object",
                "properties": {
                  "path": { "type": "string", "description": "Directory inside the git repo. Defaults to '.'." }
                },
                "required": []
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "git_commit",
              "description": "Stage all changes and create a commit with the given message. Destructive — requires user confirmation.",
              "parameters": {
                "type": "object",
                "properties": {
                  "path":    { "type": "string", "description": "Directory inside the git repo. Defaults to '.'." },
                  "message": { "type": "string", "description": "Commit message." }
                },
                "required": ["message"]
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
              "name": "http_request",
              "description": "Make an HTTP request to a URL and return the status, headers, and body. Use this to call web APIs or fetch remote resources.",
              "parameters": {
                "type": "object",
                "properties": {
                  "url":       { "type": "string", "description": "The absolute http/https URL to request." },
                  "method":    { "type": "string", "description": "HTTP method: GET, POST, PUT, PATCH, DELETE, etc. Defaults to GET." },
                  "headers":   { "type": "object", "description": "Optional request headers as a JSON object of string values." },
                  "body":      { "type": "string", "description": "Optional request body (sent for POST/PUT/PATCH)." },
                  "timeoutMs": { "type": "integer", "description": "Timeout in milliseconds. Defaults to 30000, max 120000." }
                },
                "required": ["url"]
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "web_search",
              "description": "Search the web for docs, errors, or solutions. Returns a list of ranked results with titles, URLs, and snippets.",
              "parameters": {
                "type": "object",
                "properties": {
                  "query":      { "type": "string",  "description": "The search query text." },
                  "maxResults": { "type": "integer", "description": "Maximum number of results to return. Defaults to 5, max 10." }
                },
                "required": ["query"]
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "fetch_url",
              "description": "Retrieve the content of a webpage or URL and return it as readable text. Use this to read articles, docs, or any web page. Returns the page title and the visible text content (HTML tags stripped).",
              "parameters": {
                "type": "object",
                "properties": {
                  "url":      { "type": "string",  "description": "The absolute http/https URL of the page to fetch." },
                  "maxChars": { "type": "integer", "description": "Maximum number of characters of text to return. Defaults to 20000, max 100000." }
                },
                "required": ["url"]
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "run_terminal",
              "description": "Run a command in an interactive, persistent shell session. State (current directory, environment variables, etc.) is preserved between calls to the same session. Use this for long-running or stateful workflows (e.g. starting a dev server, running a REPL, or chaining commands that depend on prior state). Each session keeps its own shell process alive until closed with close_terminal.",
              "parameters": {
                "type": "object",
                "properties": {
                  "cmd":       { "type": "string",  "description": "The command to run in the session's shell." },
                  "session":   { "type": "string",  "description": "Optional session id. Defaults to 'default'. Use distinct ids for independent sessions." },
                  "timeoutMs": { "type": "integer", "description": "Timeout in milliseconds to wait for output. Defaults to 60000, max 300000." }
                },
                "required": ["cmd"]
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "close_terminal",
              "description": "Close and terminate a persistent terminal session created by run_terminal, releasing its shell process. Use this when you are done with a session to free resources.",
              "parameters": {
                "type": "object",
                "properties": {
                  "session": { "type": "string", "description": "Optional session id to close. Defaults to 'default'." }
                },
                "required": []
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

    // ── read_json ────────────────────────────────────────────────────────────

    private static string ReadJson(string path, string? query)
    {
        try
        {
            var full = Path.GetFullPath(path);
            if (!IsSafePath(full))
                return $"Error: read_json - Path '{full}' is not allowed for reading. Only files in the current working directory are permitted.";

            if (!File.Exists(full)) return $"Error: not found '{full}'";

            var len = new FileInfo(full).Length;
            if (len > 512_000) return $"Error: file too large ({len / 1024} KB). Use sh to grep/head.";

            var text = File.ReadAllText(full, Encoding.UTF8);
            JsonNode? node;
            try { node = JsonNode.Parse(text); }
            catch (JsonException ex) { return $"Error: read_json - invalid JSON in '{full}': {ex.Message}"; }

            if (node is null) return $"Error: read_json - '{full}' contains no JSON value.";

            if (!string.IsNullOrWhiteSpace(query))
            {
                var result = QueryJson(node, query);
                if (result is null)
                    return $"Error: read_json - query '{query}' not found in '{full}'.";
                node = result;
            }

            return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex) { return $"Error: read_json — {ex.Message}"; }
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

    // ── tree ─────────────────────────────────────────────────────────────────

    private static string Tree(string path, int depth)
    {
        try
        {
            var full = Path.GetFullPath(path);
            if (!IsSafePath(full))
                return $"Error: tree - Path '{full}' is not allowed for listing. Only directories in the current working directory are permitted.";

            if (!Directory.Exists(full)) return $"Error: directory not found '{full}'";

            var sb = new StringBuilder();
            var count = 0;

            // Root line: show the directory name (or '.' for the current dir).
            var rootName = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(rootName)) rootName = full;
            sb.AppendLine(rootName + "/");

            AppendTreeLevel(full, "", depth, sb, ref count);

            if (count >= MaxTreeEntries)
                sb.AppendLine($"... (truncated at {MaxTreeEntries} entries)");

            return sb.ToString().TrimEnd();
        }
        catch (Exception ex) { return $"Error: tree — {ex.Message}"; }
    }

    /// <summary>
    /// Recursively appends the contents of a directory to the tree output using
    /// box-drawing characters. 'prefix' carries the indentation for ancestors.
    /// </summary>
    private static void AppendTreeLevel(
        string dir,
        string prefix,
        int depth,
        StringBuilder sb,
        ref int count)
    {
        if (count >= MaxTreeEntries) return;

        // Gather and sort entries: directories first, then files, each alphabetically.
        var dirs = new List<string>();
        var files = new List<string>();

        foreach (var d in Directory.EnumerateDirectories(dir))
        {
            var name = Path.GetFileName(d);
            if (name.StartsWith(".")) continue;
            if (name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("obj", StringComparison.OrdinalIgnoreCase)) continue;
            dirs.Add(name);
        }

        foreach (var f in Directory.EnumerateFiles(dir))
        {
            var name = Path.GetFileName(f);
            if (name.StartsWith(".")) continue;
            files.Add(name);
        }

        dirs.Sort(StringComparer.OrdinalIgnoreCase);
        files.Sort(StringComparer.OrdinalIgnoreCase);

        var entries = dirs.Select(d => (Name: d, IsDir: true))
                          .Concat(files.Select(f => (Name: f, IsDir: false)))
                          .ToList();

        for (int i = 0; i < entries.Count; i++)
        {
            if (count >= MaxTreeEntries) return;

            var (name, isDir) = entries[i];
            var isLast = i == entries.Count - 1;

            // Branch glyph: '└── ' for the last child, '├── ' otherwise.
            var branch = isLast ? "└── " : "├── ";
            sb.AppendLine(prefix + branch + name + (isDir ? "/" : ""));
            count++;

            if (isDir && (depth < 0 || depth > 0))
            {
                // Child prefix: '    ' for last child, '│   ' otherwise.
                var childPrefix = prefix + (isLast ? "    " : "│   ");
                var childDepth = depth < 0 ? -1 : depth - 1;
                AppendTreeLevel(Path.Combine(dir, name), childPrefix, childDepth, sb, ref count);
            }
        }
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

    // ── copy_file / move_file / delete_file ─────────────────────────────────

    private static string CopyFile(string source, string destination)
    {
        try
        {
            var srcFull = Path.GetFullPath(source);
            var dstFull = Path.GetFullPath(destination);
            if (!IsSafePath(srcFull) || !IsSafePath(dstFull))
                return $"Error: copy_file - Paths must be inside the current working directory.";

            if (!File.Exists(srcFull)) return $"Error: copy_file - source not found '{srcFull}'";

            var dir = Path.GetDirectoryName(dstFull);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

            File.Copy(srcFull, dstFull, overwrite: true);
            return $"OK: copied '{srcFull}' -> '{dstFull}' ({Sz(new FileInfo(dstFull).Length)})";
        }
        catch (Exception ex) { return $"Error: copy_file — {ex.Message}"; }
    }

    private static string MoveFile(string source, string destination)
    {
        try
        {
            var srcFull = Path.GetFullPath(source);
            var dstFull = Path.GetFullPath(destination);
            if (!IsSafePath(srcFull) || !IsSafePath(dstFull))
                return $"Error: move_file - Paths must be inside the current working directory.";

            if (!File.Exists(srcFull)) return $"Error: move_file - source not found '{srcFull}'";

            var dir = Path.GetDirectoryName(dstFull);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

            File.Move(srcFull, dstFull, overwrite: true);
            return $"OK: moved '{srcFull}' -> '{dstFull}'";
        }
        catch (Exception ex) { return $"Error: move_file — {ex.Message}"; }
    }

    private static string DeleteFile(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            if (!IsSafePath(full))
                return $"Error: delete_file - Path '{full}' is not allowed for deletion. Only files in the current working directory are permitted.";

            if (!File.Exists(full)) return $"Error: delete_file - not found '{full}'";

            File.Delete(full);
            return $"OK: deleted '{full}'";
        }
        catch (Exception ex) { return $"Error: delete_file — {ex.Message}"; }
    }

    // ── zip / unzip ──────────────────────────────────────────────────────────

    private static string Zip(string source, string destination)
    {
        try
        {
            var srcFull = Path.GetFullPath(source);
            var dstFull = Path.GetFullPath(destination);
            if (!IsSafePath(srcFull) || !IsSafePath(dstFull))
                return $"Error: zip - Paths must be inside the current working directory.";

            if (!File.Exists(srcFull) && !Directory.Exists(srcFull))
                return $"Error: zip - source not found '{srcFull}'";

            var dir = Path.GetDirectoryName(dstFull);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

            // Remove any pre-existing archive so creation is clean.
            if (File.Exists(dstFull)) File.Delete(dstFull);

            if (Directory.Exists(srcFull))
            {
                ZipFile.CreateFromDirectory(srcFull, dstFull, CompressionLevel.Optimal, includeBaseDirectory: false);
            }
            else
            {
                // Single file: create the archive and add the file as its only entry.
                using var archive = ZipFile.Open(dstFull, ZipArchiveMode.Create);
                archive.CreateEntryFromFile(srcFull, Path.GetFileName(srcFull), CompressionLevel.Optimal);
            }

            return $"OK: created archive '{dstFull}' ({Sz(new FileInfo(dstFull).Length)})";
        }
        catch (Exception ex) { return $"Error: zip — {ex.Message}"; }
    }

    private static string Unzip(string archive, string destination)
    {
        try
        {
            var arcFull = Path.GetFullPath(archive);
            var dstFull = Path.GetFullPath(destination);
            if (!IsSafePath(arcFull) || !IsSafePath(dstFull))
                return $"Error: unzip - Paths must be inside the current working directory.";

            if (!File.Exists(arcFull)) return $"Error: unzip - archive not found '{arcFull}'";

            Directory.CreateDirectory(dstFull);
            ZipFile.ExtractToDirectory(arcFull, dstFull, overwriteFiles: true);

            var count = Directory.EnumerateFiles(dstFull, "*", SearchOption.AllDirectories).Count();
            return $"OK: extracted '{arcFull}' -> '{dstFull}' ({count} file(s))";
        }
        catch (Exception ex) { return $"Error: unzip — {ex.Message}"; }
    }

    // ── parse_output ─────────────────────────────────────────────────────────

    private static string ParseOutput(string output, string format, string? query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(output))
                return "Error: parse_output - 'output' argument is required.";

            if (output.Length > MaxParseOutputBytes)
                return $"Error: parse_output - output too large ({output.Length / 1024} KB). Max is {MaxParseOutputBytes / 1024} KB.";

            var fmt = (format ?? "auto").Trim().ToLowerInvariant();
            JsonNode? parsed;

            switch (fmt)
            {
                case "json":
                    try { parsed = JsonNode.Parse(output); }
                    catch (JsonException ex) { return $"Error: parse_output - invalid JSON: {ex.Message}"; }
                    break;

                case "keyvalue":
                    parsed = ParseKeyValue(output);
                    break;

                case "csv":
                    parsed = ParseCsv(output);
                    break;

                case "auto":
                default:
                    parsed = ParseAuto(output);
                    break;
            }

            if (parsed is null)
                return "Error: parse_output - could not parse the output into structured data.";

            if (!string.IsNullOrWhiteSpace(query))
            {
                var result = QueryJson(parsed, query);
                if (result is null)
                    return $"Error: parse_output - query '{query}' not found in parsed result.";
                parsed = result;
            }

            return parsed.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex) { return $"Error: parse_output — {ex.Message}"; }
    }

    /// <summary>
    /// Auto-detects the format: tries JSON first, then key=value / key: value lines,
    /// then CSV/TSV rows, and finally falls back to a plain text object.
    /// </summary>
    private static JsonNode? ParseAuto(string output)
    {
        var trimmed = output.Trim();

        // 1) Try JSON.
        try { return JsonNode.Parse(trimmed); }
        catch { /* not JSON */ }

        // 2) Try key=value / key: value lines.
        var kv = ParseKeyValue(trimmed);
        if (kv is JsonObject kvObj && kvObj.Count > 0)
            return kvObj;

        // 3) Try CSV/TSV rows.
        var csv = ParseCsv(trimmed);
        if (csv is JsonArray csvArr && csvArr.Count > 0)
            return csvArr;

        // 4) Fallback: wrap as a plain text object.
        return new JsonObject { ["text"] = trimmed };
    }

    /// <summary>
    /// Parses lines of the form "key=value" or "key: value" into a JSON object.
    /// </summary>
    private static JsonNode ParseKeyValue(string output)
    {
        var obj = new JsonObject();
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            int sep = line.IndexOf('=');
            if (sep < 0) sep = line.IndexOf(':');
            if (sep <= 0) continue;

            var key = line[..sep].Trim().Trim('"', '\'');
            var value = line[(sep + 1)..].Trim().Trim('"', '\'');

            if (key.Length == 0) continue;
            obj[key] = CoerceScalar(value);
        }
        return obj;
    }

    /// <summary>
    /// Parses comma- or tab-separated rows into a JSON array of objects (using the
    /// first row as headers when present) or arrays of scalars.
    /// </summary>
    private static JsonNode ParseCsv(string output)
    {
        var lines = output.Split('\n')
                          .Select(l => l.TrimEnd('\r'))
                          .Where(l => l.Trim().Length > 0)
                          .ToList();

        if (lines.Count == 0) return new JsonArray();

        // Detect delimiter: prefer tab if present, else comma.
        var hasTab = lines.Any(l => l.Contains('\t'));
        var delim = hasTab ? '\t' : ',';

        var rows = lines.Select(l => SplitDelimited(l, delim)).ToList();
        var arr = new JsonArray();

        // Heuristic: if the first row has all non-numeric, non-empty cells, treat as header.
        var first = rows[0];
        bool hasHeader = first.Count > 0 &&
                         first.All(c => c.Length > 0 && !double.TryParse(c, out _));

        int start = hasHeader ? 1 : 0;

        for (int i = start; i < rows.Count; i++)
        {
            var cells = rows[i];
            if (hasHeader)
            {
                var rowObj = new JsonObject();
                for (int c = 0; c < cells.Count; c++)
                {
                    var header = c < first.Count ? first[c].Trim() : $"col{c}";
                    if (header.Length == 0) header = $"col{c}";
                    rowObj[header] = CoerceScalar(cells[c].Trim());
                }
                arr.Add(rowObj);
            }
            else
            {
                var rowArr = new JsonArray();
                foreach (var cell in cells) rowArr.Add(CoerceScalar(cell.Trim()));
                arr.Add(rowArr);
            }
        }

        return arr;
    }

    /// <summary>
    /// Splits a line by the delimiter, respecting simple double-quoted fields.
    /// </summary>
    private static List<string> SplitDelimited(string line, char delim)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (ch == delim && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }
        result.Add(current.ToString());
        return result;
    }

    /// <summary>
    /// Converts a string to a JSON scalar (number, bool, or string) when possible.
    /// </summary>
    private static JsonNode CoerceScalar(string value)
    {
        if (long.TryParse(value, out var l)) return JsonValue.Create(l)!;
        if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var d))
            return JsonValue.Create(d)!;
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase)) return JsonValue.Create(true)!;
        if (value.Equals("false", StringComparison.OrdinalIgnoreCase)) return JsonValue.Create(false)!;
        if (value.Equals("null", StringComparison.OrdinalIgnoreCase)) return JsonValue.Create((string?)null)!;
        return JsonValue.Create(value)!;
    }

    // ── git_* tools ──────────────────────────────────────────────────────────

    private static async Task<string> GitStatusAsync(string path)
    {
        var full = Path.GetFullPath(path);
        if (!IsSafePath(full))
            return $"Error: git_status - Path '{full}' is not allowed. Only paths in the current working directory are permitted.";

        return await RunGitAsync(full, "status --short --branch");
    }

    private static async Task<string> GitDiffAsync(string path, bool staged)
    {
        var full = Path.GetFullPath(path);
        if (!IsSafePath(full))
            return $"Error: git_diff - Path '{full}' is not allowed. Only paths in the current working directory are permitted.";

        var args = staged ? "diff --cached" : "diff";
        return await RunGitAsync(full, args);
    }

    private static async Task<string> GitLogAsync(string path, int count)
    {
        var full = Path.GetFullPath(path);
        if (!IsSafePath(full))
            return $"Error: git_log - Path '{full}' is not allowed. Only paths in the current working directory are permitted.";

        if (count < 1) count = 1;
        if (count > 100) count = 100;
        return await RunGitAsync(full, $"log --oneline -n {count}");
    }

    private static async Task<string> GitBranchAsync(string path)
    {
        var full = Path.GetFullPath(path);
        if (!IsSafePath(full))
            return $"Error: git_branch - Path '{full}' is not allowed. Only paths in the current working directory are permitted.";

        return await RunGitAsync(full, "branch --list");
    }

    private static async Task<string> GitCommitAsync(string path, string message)
    {
        var full = Path.GetFullPath(path);
        if (!IsSafePath(full))
            return $"Error: git_commit - Path '{full}' is not allowed. Only paths in the current working directory are permitted.";

        if (string.IsNullOrWhiteSpace(message))
            return "Error: git_commit - 'message' argument is required.";

        // Stage all changes, then commit.
        var addResult = await RunGitAsync(full, "add -A");
        if (addResult.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
            return addResult;

        var commitResult = await RunGitAsync(full, $"commit -m \"{message.Replace("\"", "\\\"")}\"");
        return commitResult;
    }

    /// <summary>
    /// Runs a git command in the given working directory and returns its output.
    /// </summary>
    private static async Task<string> RunGitAsync(string workingDir, string gitArgs)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = gitArgs,
                    WorkingDirectory = workingDir,
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
                return "Error: git command timed out (60s).";
            }

            var output = ((await outTask) + (await errTask)).Trim();
            var prefix = proc.ExitCode == 0 ? $"OK (exit 0):\n" : $"Error (exit {proc.ExitCode}):\n";
            return string.IsNullOrWhiteSpace(output)
                ? prefix.TrimEnd()
                : prefix + output;
        }
        catch (Exception ex) { return $"Git error: {ex.Message}"; }
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

    // ── http_request ─────────────────────────────────────────────────────────

    private static async Task<string> HttpRequestAsync(
        string url,
        string method,
        JsonObject? headers,
        string? body,
        int timeoutMs)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
                return "Error: http_request - 'url' argument is required.";

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return $"Error: http_request - invalid URL '{url}'. Only http/https URLs are allowed.";

            if (timeoutMs < 1) timeoutMs = 1;
            if (timeoutMs > 120_000) timeoutMs = 120_000;

            using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };

            using var request = new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()), uri);

            if (headers is not null)
            {
                foreach (var kvp in headers)
                {
                    if (kvp.Value is null) continue;
                    var value = kvp.Value.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(value)) continue;

                    // Content headers must be set on the content, not the request.
                    if (kvp.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!request.Headers.TryAddWithoutValidation(kvp.Key, value))
                        return $"Error: http_request - invalid header '{kvp.Key}'.";
                }
            }

            if (!string.IsNullOrEmpty(body))
            {
                var contentType = "application/json";
                if (headers is not null && headers["Content-Type"] is JsonValue ct)
                    contentType = ct.GetValue<string>();

                request.Content = new StringContent(body, Encoding.UTF8, contentType);
            }

            using var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            var sb = new StringBuilder();
            sb.AppendLine($"Status: {(int)response.StatusCode} {response.ReasonPhrase}");
            sb.AppendLine($"Headers:");
            foreach (var h in response.Headers)
                sb.AppendLine($"  {h.Key}: {string.Join(", ", h.Value)}");
            sb.AppendLine($"Body:");
            sb.Append(responseBody);

            return sb.ToString().TrimEnd();
        }
        catch (TaskCanceledException)
        {
            return $"Error: http_request - request to '{url}' timed out after {timeoutMs} ms.";
        }
        catch (HttpRequestException ex)
        {
            return $"Error: http_request - {ex.Message}";
        }
        catch (Exception ex) { return $"Error: http_request — {ex.Message}"; }
    }

    // ── web_search ───────────────────────────────────────────────────────────

    private static async Task<string> WebSearchAsync(string query, int maxResults)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
                return "Error: web_search - 'query' argument is required.";

            if (maxResults < 1) maxResults = 1;
            if (maxResults > 10) maxResults = 10;

            // DuckDuckGo Instant Answer API — free, no API key required.
            var url = "https://api.duckduckgo.com/?q=" + Uri.EscapeDataString(query) +
                      "&format=json&no_html=1&skip_disambig=1";

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CsAgentUI/1.0");

            var json = await client.GetStringAsync(url);
            var root = JsonNode.Parse(json);

            var sb = new StringBuilder();
            var count = 0;

            // Abstract answer (if present) is the most relevant result.
            var abstractText = root?["Abstract"]?.GetValue<string>();
            var abstractUrl = root?["AbstractURL"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(abstractText))
            {
                sb.AppendLine($"[Abstract] {abstractText}");
                if (!string.IsNullOrWhiteSpace(abstractUrl))
                    sb.AppendLine($"  URL: {abstractUrl}");
                sb.AppendLine();
                count++;
            }

            // Related topics.
            if (root?["RelatedTopics"] is JsonArray topics)
            {
                foreach (var topic in topics)
                {
                    if (count >= maxResults) break;

                    if (topic is JsonObject obj)
                    {
                        var text = obj["Text"]?.GetValue<string>();
                        var firstUrl = obj["FirstURL"]?.GetValue<string>();
                        if (string.IsNullOrWhiteSpace(text)) continue;

                        sb.AppendLine($"{count + 1}. {text}");
                        if (!string.IsNullOrWhiteSpace(firstUrl))
                            sb.AppendLine($"   URL: {firstUrl}");
                        sb.AppendLine();
                        count++;
                    }
                    else if (topic is JsonObject nested && nested["Topics"] is JsonArray subTopics)
                    {
                        foreach (var sub in subTopics)
                        {
                            if (count >= maxResults) break;
                            if (sub is not JsonObject subObj) continue;

                            var text = subObj["Text"]?.GetValue<string>();
                            var firstUrl = subObj["FirstURL"]?.GetValue<string>();
                            if (string.IsNullOrWhiteSpace(text)) continue;

                            sb.AppendLine($"{count + 1}. {text}");
                            if (!string.IsNullOrWhiteSpace(firstUrl))
                                sb.AppendLine($"   URL: {firstUrl}");
                            sb.AppendLine();
                            count++;
                        }
                    }
                }
            }

            if (count == 0)
                return $"No results found for '{query}'.";

            return sb.ToString().TrimEnd();
        }
        catch (TaskCanceledException)
        {
            return $"Error: web_search - request timed out for query '{query}'.";
        }
        catch (HttpRequestException ex)
        {
            return $"Error: web_search - {ex.Message}";
        }
        catch (Exception ex) { return $"Error: web_search — {ex.Message}"; }
    }

    // ── fetch_url ───────────────────────────────────────────────────────────

    private static async Task<string> FetchUrlAsync(string url, int maxChars)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
                return "Error: fetch_url - 'url' argument is required.";

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return $"Error: fetch_url - invalid URL '{url}'. Only http/https URLs are allowed.";

            if (maxChars < 1) maxChars = 1;
            if (maxChars > 100_000) maxChars = 100_000;

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) CsAgentUI/1.0");

            using var response = await client.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
                return $"Error: fetch_url - HTTP {(int)response.StatusCode} {response.ReasonPhrase} for '{url}'.";

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            var html = await response.Content.ReadAsStringAsync();

            // If it's not HTML (e.g. JSON, plain text), return it directly.
            if (!contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
            {
                var trimmed = html.Trim();
                if (trimmed.Length > maxChars)
                    trimmed = trimmed[..maxChars] + "\n... (truncated)";
                return $"URL: {url}\nContent-Type: {contentType}\n\n{trimmed}";
            }

            // Extract the <title> for context.
            var title = "";
            var titleMatch = System.Text.RegularExpressions.Regex.Match(
                html, "<title[^>]*>(.*?)</title>", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
            if (titleMatch.Success)
                title = System.Net.WebUtility.HtmlDecode(titleMatch.Groups[1].Value).Trim();

            // Strip scripts/styles and tags, then collapse whitespace.
            var text = System.Text.RegularExpressions.Regex.Replace(
                html, "<script[^>]*>.*?</script>", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
            text = System.Text.RegularExpressions.Regex.Replace(
                text, "<style[^>]*>.*?</style>", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", " ");
            text = System.Net.WebUtility.HtmlDecode(text);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"[ \t]+", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\n\s*\n+", "\n\n");
            text = text.Trim();

            if (text.Length > maxChars)
                text = text[..maxChars] + "\n... (truncated)";

            var sb = new StringBuilder();
            sb.AppendLine($"URL: {url}");
            if (!string.IsNullOrWhiteSpace(title))
                sb.AppendLine($"Title: {title}");
            sb.AppendLine();
            sb.Append(text);

            return sb.ToString().TrimEnd();
        }
        catch (TaskCanceledException)
        {
            return $"Error: fetch_url - request to '{url}' timed out.";
        }
        catch (HttpRequestException ex)
        {
            return $"Error: fetch_url - {ex.Message}";
        }
        catch (Exception ex) { return $"Error: fetch_url — {ex.Message}"; }
    }

    // ── run_terminal / close_terminal ───────────────────────────────────────

    // Persistent interactive shell sessions keyed by session id.
    private static readonly Dictionary<string, TerminalSession> TerminalSessions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object TerminalLock = new();

    private static async Task<string> RunTerminalAsync(string cmd, string session, int timeoutMs, bool isWindows)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(cmd))
                return "Error: run_terminal - 'cmd' argument is required.";

            if (!IsSafeCommand(cmd, isWindows))
                return $"Error: run_terminal - Command '{cmd}' contains potentially dangerous operations and is not allowed.";

            if (timeoutMs < 1) timeoutMs = 1;
            if (timeoutMs > 300_000) timeoutMs = 300_000;

            TerminalSession term;
            lock (TerminalLock)
            {
                if (!TerminalSessions.TryGetValue(session, out term!))
                {
                    term = new TerminalSession(session, isWindows);
                    TerminalSessions[session] = term;
                }
            }

            return await term.RunAsync(cmd, timeoutMs);
        }
        catch (Exception ex) { return $"Error: run_terminal — {ex.Message}"; }
    }

    private static string CloseTerminal(string session)
    {
        try
        {
            lock (TerminalLock)
            {
                if (TerminalSessions.TryGetValue(session, out var term))
                {
                    term.Dispose();
                    TerminalSessions.Remove(session);
                    return $"OK: closed terminal session '{session}'.";
                }
                return $"OK: no active terminal session '{session}' to close.";
            }
        }
        catch (Exception ex) { return $"Error: close_terminal — {ex.Message}"; }
    }

    /// <summary>
    /// A persistent interactive shell process. Commands are written to its stdin
    /// and output is read asynchronously, preserving state (cwd, env) across calls.
    /// </summary>
    private sealed class TerminalSession : IDisposable
    {
        private readonly string _id;
        private readonly bool _isWindows;
        private readonly Process _proc;
        private readonly StringBuilder _output = new();
        private readonly object _lock = new();
        private bool _disposed;

        public TerminalSession(string id, bool isWindows)
        {
            _id = id;
            _isWindows = isWindows;

            var psi = new ProcessStartInfo
            {
                FileName = isWindows ? "cmd.exe" : "/bin/bash",
                Arguments = isWindows ? "/Q" : "--norc --noprofile -i",
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

            // Continuously drain stdout/stderr into the shared buffer.
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
                // Cap the buffer to avoid unbounded growth.
                if (_output.Length > 512_000)
                    _output.Remove(0, _output.Length - 512_000);
            }
        }

        public async Task<string> RunAsync(string cmd, int timeoutMs)
        {
            if (_disposed || _proc.HasExited)
                return $"Error: run_terminal - session '{_id}' is no longer running. Start a new session.";

            // Snapshot the current buffer position so we only return new output.
            int startPos;
            lock (_lock) { startPos = _output.Length; }

            // Write the command followed by a unique sentinel marker so we know
            // when the command has finished producing output.
            var marker = $"__CSAGENT_DONE_{Guid.NewGuid():N}__";
            var line = _isWindows
                ? $"{cmd} & echo {marker}"
                : $"{cmd}; echo {marker}";

            await _proc.StandardInput.WriteLineAsync(line);
            await _proc.StandardInput.FlushAsync();

            // Wait for the marker to appear in the output, or until timeout.
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                string current;
                lock (_lock) { current = _output.ToString(); }

                if (current.Contains(marker))
                {
                    // Strip the marker line and return everything since startPos.
                    lock (_lock)
                    {
                        var newText = _output.ToString(startPos, _output.Length - startPos);
                        newText = newText.Replace(marker, "").Trim();
                        return string.IsNullOrWhiteSpace(newText)
                            ? $"OK (session '{_id}'): (no output)"
                            : $"OK (session '{_id}'):\n{newText}";
                    }
                }

                await Task.Delay(50);
            }

            return $"Error: run_terminal - command timed out after {timeoutMs} ms in session '{_id}'. The session is still running; you can send another command or close it with close_terminal.";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (!_proc.HasExited)
                {
                    try { _proc.StandardInput.WriteLine(_isWindows ? "exit" : "exit"); _proc.StandardInput.Flush(); } catch { }
                    try { _proc.Kill(entireProcessTree: true); } catch { }
                }
                _proc.Dispose();
            }
            catch { /* best effort */ }
        }
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
    /// Resolves a dot-path query (e.g. "a.b[0].c") against a JSON node.
    /// Supports object properties and array indices. Returns null if not found.
    /// </summary>
    private static JsonNode? QueryJson(JsonNode node, string query)
    {
        var current = node;
        var token = new StringBuilder();

        for (int i = 0; i < query.Length; i++)
        {
            var ch = query[i];

            if (ch == '.')
            {
                if (token.Length > 0)
                {
                    current = Step(current, token.ToString());
                    if (current is null) return null;
                    token.Clear();
                }
            }
            else if (ch == '[')
            {
                if (token.Length > 0)
                {
                    current = Step(current, token.ToString());
                    if (current is null) return null;
                    token.Clear();
                }

                // Read the index until ']'.
                var idxEnd = query.IndexOf(']', i);
                if (idxEnd < 0) return null;
                var idxText = query[(i + 1)..idxEnd].Trim().Trim('"', '\'');
                if (!int.TryParse(idxText, out var idx)) return null;

                if (current is JsonArray arr)
                {
                    if (idx < 0 || idx >= arr.Count) return null;
                    current = arr[idx];
                }
                else
                {
                    return null;
                }

                i = idxEnd;
            }
            else
            {
                token.Append(ch);
            }
        }

        if (token.Length > 0)
        {
            current = Step(current, token.ToString());
            if (current is null) return null;
        }

        return current;
    }

    /// <summary>
    /// Steps one level into an object property or array index.
    /// </summary>
    private static JsonNode? Step(JsonNode node, string key)
    {
        if (node is JsonObject obj)
        {
            if (obj.TryGetPropertyValue(key, out var value)) return value;
            return null;
        }
        if (node is JsonArray arr && int.TryParse(key, out var idx))
        {
            if (idx >= 0 && idx < arr.Count) return arr[idx];
            return null;
        }
        return null;
    }

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
