extern alias PhotinoX;

using System.Text.Json.Nodes;
using CsAgentUI.Shared;
using CsAgentUI.Presentation.DesktopPhotinoX.Protocol;
using PhotinoWindow = PhotinoX::Photino.NET.PhotinoWindow;

namespace CsAgentUI.Presentation.DesktopPhotinoX;

public sealed class PhotinoXAPI : IDisposable
{
    private readonly PhotinoWindow _window;
    private readonly AgentArguments _args;
    private readonly string _apiKey;
    private readonly object _gate = new();
    private readonly Dictionary<string, AgentSession> _sessions = new(StringComparer.Ordinal);
    private bool _disposed;

    public PhotinoXAPI(PhotinoWindow window, AgentArguments args)
    {
        _window = window;
        _args = args;
        _apiKey = Environment.GetEnvironmentVariable("ALBERT_API_KEY") ?? "";
    }

    public void HandleMessage(string raw)
    {
        if (_disposed) return;
        if (!BridgeProtocol.TryParse(raw, out var message, out var error))
        {
            Send(Guid.NewGuid().ToString("N"), MessageTypes.BridgeError, null,
                new JsonObject { ["message"] = error });
            return;
        }

        try
        {
            switch (message!.Type)
            {
                case MessageTypes.InfoGet: SendInfo(message.Id); break;
                case MessageTypes.SessionCreate: CreateSession(message); break;
                case MessageTypes.SessionClose: CloseSession(message); break;
                case MessageTypes.ChatStart: StartChat(message); break;
                case MessageTypes.ChatCancel: CancelChat(message); break;
                case MessageTypes.ApprovalRespond:
                    Send(message.Id, MessageTypes.BridgeError, message.SessionId,
                        new JsonObject { ["message"] = "Interactive approval is not yet exposed by the agent core." });
                    break;
                default:
                    Send(message.Id, MessageTypes.BridgeError, message.SessionId,
                        new JsonObject { ["message"] = $"Unknown message type: '{message.Type}'." });
                    break;
            }
        }
        catch (Exception ex)
        {
            Send(message.Id, MessageTypes.BridgeError, message.SessionId,
                new JsonObject { ["message"] = ex.Message });
        }
    }

    private void CreateSession(BridgeMessage message)
    {
        var sessionId = string.IsNullOrWhiteSpace(message.SessionId)
            ? Guid.NewGuid().ToString("N")
            : message.SessionId!;

        lock (_gate)
        {
            if (_sessions.ContainsKey(sessionId))
            {
                Send(message.Id, MessageTypes.BridgeError, sessionId,
                    new JsonObject { ["message"] = "Session already exists." });
                return;
            }
            _sessions[sessionId] = new AgentSession(sessionId, _args, _apiKey);
        }

        Send(message.Id, MessageTypes.SessionCreated, sessionId,
            new JsonObject { ["sessionId"] = sessionId });
    }

    private void CloseSession(BridgeMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.SessionId))
        {
            Send(message.Id, MessageTypes.BridgeError, null,
                new JsonObject { ["message"] = "sessionId is required." });
            return;
        }

        AgentSession? session;
        lock (_gate)
        {
            if (!_sessions.Remove(message.SessionId, out session))
            {
                Send(message.Id, MessageTypes.BridgeError, message.SessionId,
                    new JsonObject { ["message"] = "Session not found." });
                return;
            }
        }

        session.Dispose();
        Send(message.Id, MessageTypes.SessionClosed, message.SessionId, null);
    }

    private void StartChat(BridgeMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.SessionId))
        {
            Send(message.Id, MessageTypes.BridgeError, null,
                new JsonObject { ["message"] = "sessionId is required." });
            return;
        }
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            Send(message.Id, MessageTypes.AgentError, message.SessionId,
                new JsonObject { ["message"] = "ALBERT_API_KEY env var not set." });
            return;
        }

        var prompt = message.Payload?["prompt"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(prompt))
        {
            Send(message.Id, MessageTypes.AgentError, message.SessionId,
                new JsonObject { ["message"] = "Empty prompt." });
            return;
        }

        AgentSession session;
        lock (_gate)
        {
            if (!_sessions.TryGetValue(message.SessionId, out session!))
            {
                Send(message.Id, MessageTypes.BridgeError, message.SessionId,
                    new JsonObject { ["message"] = "Session not found." });
                return;
            }
            if (session.IsRunning)
            {
                Send(message.Id, MessageTypes.BridgeError, message.SessionId,
                    new JsonObject { ["message"] = "A chat is already running in this session." });
                return;
            }
        }

        Send(message.Id, MessageTypes.ChatAccepted, message.SessionId, null);
        _ = RunChatAsync(session, message.Id, prompt);
    }

    private async Task RunChatAsync(AgentSession session, string requestId, string prompt)
    {
        try
        {
            await session.RunAsync(prompt, new PhotinoXObserver(this, requestId, session.Id));
        }
        catch (OperationCanceledException)
        {
            Send(requestId, MessageTypes.AgentCancelled, session.Id,
                new JsonObject { ["reason"] = "cancelled" });
        }
        catch (Exception ex)
        {
            Send(requestId, MessageTypes.AgentError, session.Id,
                new JsonObject { ["message"] = ex.Message });
        }
    }

    private void CancelChat(BridgeMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.SessionId))
        {
            Send(message.Id, MessageTypes.BridgeError, null,
                new JsonObject { ["message"] = "sessionId is required." });
            return;
        }

        AgentSession? session;
        lock (_gate) _sessions.TryGetValue(message.SessionId, out session);
        if (session is null)
        {
            Send(message.Id, MessageTypes.BridgeError, message.SessionId,
                new JsonObject { ["message"] = "Session not found." });
            return;
        }

        session.Cancel();
        Send(message.Id, MessageTypes.ChatCancelAccepted, message.SessionId,
            new JsonObject { ["reason"] = "user" });
    }

    private void SendInfo(string requestId)
    {
        Send(requestId, MessageTypes.InfoResult, null, new JsonObject
        {
            ["userName"] = Environment.UserName,
            ["machineName"] = Environment.MachineName
        });
    }

    internal void Send(string requestId, string type, string? sessionId, JsonNode? payload)
    {
        if (_disposed) return;
        _window.SendWebMessage(BridgeProtocol.Create(requestId, type, sessionId, payload).ToJsonString());
    }

    public void Dispose()
    {
        List<AgentSession> sessions;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            sessions = _sessions.Values.ToList();
            _sessions.Clear();
        }
        foreach (var session in sessions) session.Dispose();
    }

    private sealed class PhotinoXObserver : IAgentObserver
    {
        private readonly PhotinoXAPI _api;
        private readonly string _requestId;
        private readonly string _sessionId;

        public PhotinoXObserver(PhotinoXAPI api, string requestId, string sessionId)
        {
            _api = api;
            _requestId = requestId;
            _sessionId = sessionId;
        }

        public Task OnStep(int n, int m)
        {
            _api.Send(_requestId, MessageTypes.AgentStep, _sessionId,
                new JsonObject { ["current"] = n, ["max"] = m });
            return Task.CompletedTask;
        }
        public Task OnThought(string text)
        {
            _api.Send(_requestId, MessageTypes.AgentThought, _sessionId,
                new JsonObject { ["text"] = text });
            return Task.CompletedTask;
        }
        public Task OnToolCall(string name, string args)
        {
            _api.Send(_requestId, MessageTypes.AgentToolStart, _sessionId,
                new JsonObject { ["tool"] = name, ["arguments"] = args });
            return Task.CompletedTask;
        }
        public Task OnToolResult(string result, bool isError)
        {
            _api.Send(_requestId, MessageTypes.AgentToolResult, _sessionId,
                new JsonObject { ["success"] = !isError, ["result"] = result });
            return Task.CompletedTask;
        }
        public Task OnDone(string message)
        {
            _api.Send(_requestId, MessageTypes.AgentDone, _sessionId,
                new JsonObject { ["message"] = message });
            return Task.CompletedTask;
        }
        public Task OnError(string message)
        {
            _api.Send(_requestId, MessageTypes.AgentError, _sessionId,
                new JsonObject { ["message"] = message });
            return Task.CompletedTask;
        }
        public Task OnWarning(string message)
        {
            _api.Send(_requestId, MessageTypes.AgentWarning, _sessionId,
                new JsonObject { ["message"] = message });
            return Task.CompletedTask;
        }
        public Task OnDanger(string message)
        {
            _api.Send(_requestId, MessageTypes.AgentDanger, _sessionId,
                new JsonObject { ["message"] = message });
            return Task.CompletedTask;
        }
    }
}
