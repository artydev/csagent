using System.Text.Json;
using System.Text.Json.Nodes;

namespace CsAgentUI.Shared;

/// <summary>
/// Shared JSON helpers — AOT-safe, no trimming warnings.
/// </summary>
public static class JsonHelpers
{
    /// <summary>
    /// Create a chat message JSON object with role and content.
    /// AOT-safe: uses JsonValue.Create instead of implicit conversions.
    /// </summary>
    public static JsonObject Message(string role, string content)
    {
        var obj = new JsonObject();
        obj.Add("role", JsonValue.Create(role));
        obj.Add("content", JsonValue.Create(content));
        return obj;
    }

    /// <summary>
    /// Create a tool result message JSON object.
    /// </summary>
    public static JsonObject ToolResult(string callId, string content)
    {
        var obj = new JsonObject();
        obj.Add("role", JsonValue.Create("tool"));
        obj.Add("tool_call_id", JsonValue.Create(callId));
        obj.Add("content", JsonValue.Create(content));
        return obj;
    }

    /// <summary>
    /// Pretty-print a JSON string (indented).
    /// </summary>
    public static string PrettyJson(string raw)
    {
        try { return JsonNode.Parse(raw)?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? raw; }
        catch { return raw; }
    }

    /// <summary>
    /// Trim conversation history to stay under the character threshold.
    /// Keeps the system message (index 0) and at least 3 messages.
    /// </summary>
    public static void TrimHistory(JsonArray msgs, int thresholdChars = 96_000)
    {
        static int Len(JsonNode? m)
        {
            var c = m?["content"];
            return c is JsonValue v ? v.GetValue<string>().Length : (c?.ToJsonString().Length ?? 0);
        }
        int total = msgs.Sum(Len);
        while (total > thresholdChars && msgs.Count > 3)
        {
            total -= Len(msgs[1]);
            msgs.RemoveAt(1);
        }
    }
}
