extern alias PhotinoX;

using System.Text.Json;
using System.Text.Json.Nodes;
using CsAgentUI.Shared;
using PhotinoWindow = PhotinoX::Photino.NET.PhotinoWindow;

namespace CsAgentUI.Presentation.DesktopPhotinoX;

/// <summary>
/// PhotinoX bridge — exposes .NET functionality to the JavaScript UI running inside
/// the PhotinoX window, and allows .NET to push events back to the UI.
///
/// PhotinoX does not expose arbitrary host objects like WebView2. Instead it uses a
/// message-passing model:
///   - JS → .NET:  window.external.sendMessage(jsonString) triggers HandleMessage.
///   - .NET → JS:  window.SendWebMessage(jsonString) invokes window.external.receiveMessage.
///
/// The bridge therefore implements a small JSON message protocol (see 3.1).
/// </summary>
public sealed class PhotinoXAPI : IDisposable
{
    private readonly PhotinoWindow _window;
    private readonly AgentArguments _args;
    private readonly string _apiKey;
    private readonly object _gate = new();
    private CodingAgent? _agent;
    private CancellationTokenSource? _cts;

    public PhotinoXAPI(PhotinoWindow window, AgentArguments args)
    {
        _window = window;
        _args = args;
        _apiKey = Environment.GetEnvironmentVariable("ALBERT_API_KEY") ?? "";
    }

    // ── Info exposed to JS (mirrors DesktopAPI) ──────────────────────────────

    public string? MachineName => Environment.MachineName;
    public string UserName => Environment.UserName;
    public string? ExePath => Environment.ProcessPath?.Substring(0, Math.Min(Environment.ProcessPath.Length, 100));

    // ── Message handling (JS → .NET) ─────────────────────────────────────────

    /// <summary>
    /// Entry point for messages arriving from JS via window.external.sendMessage.
    /// </summary>
    public void HandleMessage(string message)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(message);
        }
        catch (JsonException)
        {
            SendError(null, "Invalid JSON message.");
            return;
        }

        if (node is not JsonObject obj)
        {
            SendError(null, "Message must be a JSON object.");
            return;
        }

        var id = obj["id"]?.GetValue<int?>();
        var type = obj["type"]?.GetValue<string>() ?? "";

        switch (type)
        {
            case "getInfo":
                SendInfo(id);
                break;
            case "chat":
                var prompt = obj["prompt"]?.GetValue<string>() ?? "";
                _ = Task.Run(() => RunChatAsync(id, prompt));
                break;
            case "cancel":
                CancelChat();
                break;
            default:
                SendError(id, $"Unknown message type: '{type}'.");
                break;
        }
    }

    // ── Agent loop (chat) ────────────────────────────────────────────────────

    private async Task RunChatAsync(int? id, string prompt)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            SendError(id, "ALBERT_API_KEY env var not set.");
            return;
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            SendError(id, "Empty prompt.");
            return;
        }

        lock (_gate)
        {
            if (_agent is not null)
            {
                SendError(id, "A chat is already running.");
                return;
            }
            _cts = new CancellationTokenSource();
            _agent = new CodingAgent(
                _apiKey,
                LlmSettings.Endpoint,
                _args.ModelOverride ?? LlmSettings.Model,
                new AgentOptions(MaxSteps: 30, DryRun: _args.IsDryRun, Confirm: true),
                new PhotinoXObserver(this),
                _args.McpUrl);
        }

        try
        {
            var messages = await MemoryStore.LoadAsync(_args.MemoryFile);
            if (messages.Count == 0)
                messages.Add(CodingAgent.SystemMessage(OperatingSystem.IsWindows()));

            messages.Add(JsonHelpers.Message("user", prompt));

            await _agent.RunAsync(messages, _args.MemoryFile);
        }
        catch (OperationCanceledException)
        {
            SendEvent("done", new JsonObject { ["message"] = JsonValue.Create("Chat cancelled.") });
        }
        catch (Exception ex)
        {
            SendEvent("danger", new JsonObject { ["message"] = JsonValue.Create(ex.Message) });
        }
        finally
        {
            lock (_gate)
            {
                _agent?.Dispose();
                _agent = null;
                _cts?.Dispose();
                _cts = null;
            }
        }
    }

    private void CancelChat()
    {
        lock (_gate)
        {
            _cts?.Cancel();
        }
    }

    // ── Outbound messages (.NET → JS) ────────────────────────────────────────

    private void SendInfo(int? id)
    {
        var data = new JsonObject
        {
            ["machineName"] = JsonValue.Create(MachineName),
            ["userName"] = JsonValue.Create(UserName),
            ["exePath"] = JsonValue.Create(ExePath),
        };
        Send(new JsonObject
        {
            ["id"] = id is null ? null : JsonValue.Create(id.Value),
            ["type"] = JsonValue.Create("info"),
            ["data"] = data,
        });
    }

    private void SendError(int? id, string message)
    {
        Send(new JsonObject
        {
            ["id"] = id is null ? null : JsonValue.Create(id.Value),
            ["type"] = JsonValue.Create("error"),
            ["data"] = JsonValue.Create(message),
        });
    }

    private void SendEvent(string eventName, JsonObject? data)
    {
        Send(new JsonObject
        {
            ["type"] = JsonValue.Create("event"),
            ["event"] = JsonValue.Create(eventName),
            ["data"] = data,
        });
    }

    private void Send(JsonObject message)
    {
        _window.SendWebMessage(message.ToJsonString());
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _cts?.Cancel();
            _agent?.Dispose();
            _agent = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    // ── Observer that forwards agent events to JS ────────────────────────────

    private sealed class PhotinoXObserver : IAgentObserver
    {
        private readonly PhotinoXAPI _api;

        public PhotinoXObserver(PhotinoXAPI api) => _api = api;

        public Task OnStep(int n, int m)
        {
            _api.SendEvent("step", new JsonObject
            {
                ["n"] = JsonValue.Create(n),
                ["m"] = JsonValue.Create(m),
            });
            return Task.CompletedTask;
        }

        public Task OnThought(string text)
        {
            _api.SendEvent("message", new JsonObject
            {
                ["type"] = JsonValue.Create("thought"),
                ["data"] = JsonValue.Create(text),
            });
            return Task.CompletedTask;
        }

        public Task OnToolCall(string name, string args)
        {
            _api.SendEvent("call", new JsonObject
            {
                ["name"] = JsonValue.Create(name),
                ["args"] = JsonValue.Create(JsonHelpers.PrettyJson(args)),
            });
            return Task.CompletedTask;
        }

        public Task OnToolResult(string result, bool isError)
        {
            _api.SendEvent("result", new JsonObject
            {
                ["result"] = JsonValue.Create(result),
                ["isError"] = JsonValue.Create(isError),
            });
            return Task.CompletedTask;
        }

        public Task OnDone(string message)
        {
            _api.SendEvent("done", new JsonObject { ["message"] = JsonValue.Create(message) });
            return Task.CompletedTask;
        }

        public Task OnError(string message)
        {
            _api.SendEvent("danger", new JsonObject { ["message"] = JsonValue.Create(message) });
            return Task.CompletedTask;
        }

        public Task OnWarning(string message)
        {
            _api.SendEvent("message", new JsonObject
            {
                ["type"] = JsonValue.Create("warning"),
                ["data"] = JsonValue.Create(message),
            });
            return Task.CompletedTask;
        }

        public Task OnDanger(string message)
        {
            _api.SendEvent("danger", new JsonObject { ["message"] = JsonValue.Create(message) });
            return Task.CompletedTask;
        }
    }
}
