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
                else if (ToolDispatcher.IsDestructive(funcName))
                {
                    result = UI.Confirm($"Allow destructive action '{funcName}'?")
                        ? await ToolDispatcher.DispatchAsync(funcName, argsRaw, isWindows)
                        : "Tool call declined by user.";
                }
                else
                {
                    result = await ToolDispatcher.DispatchAsync(funcName, argsRaw, isWindows);
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
            """));
        return obj;
    }
}
