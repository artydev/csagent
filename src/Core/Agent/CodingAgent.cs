using System.Text.Json.Nodes;
using CsAgentUI.Core.Agent;
using CsAgentUI.Shared;

namespace CsAgentUI;

public sealed class CodingAgent : IDisposable
{
    private readonly LlmClient _client;
    private readonly AgentOptions _opts;
    private readonly IAgentObserver _observer;
    private CancellationTokenSource? _cts;

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

        // Callback used by the switch_model tool to change the active model.
        ToolDispatcher.SwitchModelHandler switchModel = (model) =>
        {
            _client.Model = model;
            return $"OK: model switched to '{model}'.";
        };

        for (int step = 1; step <= _opts.MaxSteps; step++)
        {
            _cts.Token.ThrowIfCancellationRequested();
            await _observer.OnStep(step, _opts.MaxSteps);

            JsonNode response;
            try
            {
                response = await _client.CompleteChatAsync(messages, ToolDispatcher.ToolDefinitions, _cts.Token);
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

                await _observer.OnToolCall(funcName, JsonHelpers.PrettyJson(argsRaw));

                string result;
                if (_opts.DryRun)
                {
                    result = "[dry-run] Tool not executed.";
                }
                else if (_opts.Confirm && ToolDispatcher.IsDestructive(funcName))
                {
                    result = UI.Confirm($"Allow destructive action '{funcName}'?")
                        ? await ToolDispatcher.DispatchAsync(funcName, argsRaw, isWindows, switchModel)
                        : "Tool call declined by user.";
                }
                else
                {
                    result = await ToolDispatcher.DispatchAsync(funcName, argsRaw, isWindows, switchModel);
                }

                var isError = result.StartsWith("Error:", StringComparison.OrdinalIgnoreCase)
                           || result.StartsWith("Shell error:", StringComparison.OrdinalIgnoreCase);

                await _observer.OnToolResult(result, isError);

                messages.Add(JsonHelpers.ToolResult(callId, result));
            }

            await MemoryStore.SaveAsync(memoryFile, messages);
            JsonHelpers.TrimHistory(messages);
        }

        await _observer.OnError($"Reached maximum of {_opts.MaxSteps} steps without completing.");
    }

    public void Dispose() => _client.Dispose();
    public void Cancel() => _cts?.Cancel();

    // ── System message ───────────────────────────────────────────────────────

    public static JsonObject SystemMessage(bool isWindows)
    {
        var obj = new JsonObject();
        obj.Add("role", JsonValue.Create("system"));
        obj.Add("content", JsonValue.Create($"""
            You are an autonomous, cross-platform coding agent.
            PLATFORM: {(isWindows ? "Windows - use cmd.exe syntax" : "Unix - use bash/sh syntax")}
                        # Working Behavior Guidelines

            ## 1. Task Anchoring (never forget the task)
            - At the start of every task, restate the user's goal in one or two sentences so it stays in view.
            - Keep the original task as the anchor throughout the work. If you feel yourself drifting, re-read the stated goal before continuing.
            - Do not silently change scope. If the task needs clarification, ask the user directly instead of guessing.

            ## 2. Minimal, Focused Inspection (stop re-scanning)
            - Inspect the workspace ONCE, up front, to gather what you need (structure, relevant files, config).
            - Do not re-read files you have already seen unless the task genuinely requires updated state.
            - Do not run repeated directory listings or searches for the same thing.
            - Inspect only what is relevant to the task — not the whole repo.

            ## 3. Act, Don't Loop
            - After the initial inspection, move to execution. Prefer making progress over more exploration.
            - If a command fails, analyze the specific error and retry with a targeted fix — don't restart the whole investigation.

            ## 4. Ask When Ambiguous
            - If the task is unclear or has multiple reasonable interpretations, ask the user rather than exploring endlessly or guessing.

            ## 5. Tools at My Disposal
            Use the right tool for the job, and only when needed:

            ### File operations
            - `write_file` — create or overwrite a file (parent dirs auto-created).
            - `read_file` — read a text file's content.
            - `read_json` — read/pretty-print a JSON file; optional dot-path query to extract a sub-value.
            - `list_dir` / `tree` — list a directory's contents (tree shows structure visually).
            - `search_files` — grep for a text pattern across files.
            - `edit_file` — precise find-and-replace edits without rewriting the whole file.
            - `copy_file` / `move_file` / `delete_file` — copy, rename, or delete a file.

            ### Archives
            - `zip` / `unzip` — create or extract a zip archive.

            ### Parsing
            - `parse_output` — parse command output into structured JSON (json / keyvalue / csv / auto).

     

            ### Web
            - `http_request` — make an HTTP request (GET/POST/etc.) to a URL.
            - `web_search` — search the web for docs, errors, or solutions.
            - `fetch_url` — fetch a webpage and return its readable text.

            ### Model
            - `switch_model` — change the active LLM model when the user asks.

            ### Tool usage principles
            - Prefer the simplest tool that gets the job done.
          
            - Don't call tools unnecessarily — only when they add value to the current task.
                         
            """));
        return obj;
    }
}
