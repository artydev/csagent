using System.Text.Json.Nodes;

namespace CsAgentUI.Presentation.DesktopPhotinoX.Protocol;

public sealed record BridgeMessage(
    int V,
    string Id,
    string Type,
    string? SessionId,
    JsonNode? Payload);

public static class BridgeProtocol
{
    public const int Version = 1;

    public static bool TryParse(string json, out BridgeMessage? message, out string error)
    {
        message = null;
        error = "";

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or ArgumentException)
        {
            error = "Invalid JSON message.";
            return false;
        }

        if (node is not JsonObject obj)
        {
            error = "Message must be a JSON object.";
            return false;
        }

        try
        {
            var version = obj["v"]?.GetValue<int>() ?? 0;
            var id = obj["id"]?.GetValue<string>() ?? "";
            var type = obj["type"]?.GetValue<string>() ?? "";
            var sessionId = obj["sessionId"]?.GetValue<string>();

            if (version != Version)
            {
                error = $"Unsupported protocol version: {version}.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(id))
            {
                error = "Message id is required.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(type))
            {
                error = "Message type is required.";
                return false;
            }

            message = new BridgeMessage(version, id, type, sessionId, obj["payload"]?.DeepClone());
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            error = "Invalid message fields.";
            return false;
        }
    }

    public static JsonObject Create(string id, string type, string? sessionId = null, JsonNode? payload = null)
    {
        var message = new JsonObject
        {
            ["v"] = Version,
            ["id"] = id,
            ["type"] = type
        };

        if (sessionId is not null)
            message["sessionId"] = sessionId;
        if (payload is not null)
            message["payload"] = payload;

        return message;
    }
}

public static class MessageTypes
{
    public const string InfoGet = "info.get";
    public const string SessionCreate = "session.create";
    public const string SessionClose = "session.close";
    public const string ChatStart = "chat.start";
    public const string ChatAccepted = "chat.accepted";
    public const string ChatCancel = "chat.cancel";
    public const string ApprovalRespond = "approval.respond";

    public const string InfoResult = "info.result";
    public const string SessionCreated = "session.created";
    public const string SessionClosed = "session.closed";
    public const string AgentStep = "agent.step";
    public const string AgentThought = "agent.thought";
    public const string AgentToolStart = "agent.tool.start";
    public const string AgentToolResult = "agent.tool.result";
    public const string AgentWarning = "agent.warning";
    public const string AgentDanger = "agent.danger";
    public const string AgentApprovalRequired = "agent.approval.required";
    public const string AgentDone = "agent.done";
    public const string AgentCancelled = "agent.cancelled";
    public const string AgentError = "agent.error";
    public const string BridgeError = "bridge.error";
}
