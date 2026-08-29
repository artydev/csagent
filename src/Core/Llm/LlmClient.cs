using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace CsAgentUI;

public sealed class LlmClient : IDisposable
{
    private readonly HttpClient _http;
    private string _model;
    private readonly string _baseUrl;

    public LlmClient(string apiKey, string baseUrl, string model)
    {
        _model = model;
        _baseUrl = baseUrl.TrimEnd('/');
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public string Model
    {
        get => _model;
        set => _model = value;
    }

    public async Task<JsonNode> CompleteChatAsync(
        JsonArray messages,
        JsonArray? tools = null,
        CancellationToken ct = default)
    {
        var body = new JsonObject
        {
            ["model"] = _model,
            ["temperature"] = 0.1,
            ["messages"] = messages.DeepClone()
        };

        if (tools is { Count: > 0 })
        {
            body["tools"] = tools.DeepClone();
            body["tool_choice"] = "auto";
        }

        var requestJson = body.ToJsonString();
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions")
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        Console.WriteLine($"[LLM] POST {req.RequestUri} model={_model} messages={messages.Count} tools={tools?.Count ?? 0}");

        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);

        Console.WriteLine($"[LLM] HTTP {(int)res.StatusCode} {res.ReasonPhrase}; responseBytes={Encoding.UTF8.GetByteCount(raw)}");

        if (!res.IsSuccessStatusCode)
        {
            var detail = raw.Length > 4000 ? raw[..4000] + "..." : raw;
            throw new HttpRequestException($"API {(int)res.StatusCode} {res.ReasonPhrase}: {detail}");
        }

        var parsed = JsonNode.Parse(raw) ?? throw new InvalidDataException("Empty API response");
        NormalizeAssistantMessage(parsed);
        return parsed;
    }

    /// <summary>
    /// Accept the common OpenAI-compatible response variants used by reasoning
    /// gateways. In particular, some gateways expose reasoning_content and/or
    /// structured content parts rather than a plain content string.
    /// </summary>
    private static void NormalizeAssistantMessage(JsonNode response)
    {
        var message = response["choices"]?[0]?["message"] as JsonObject;
        if (message is null) return;

        if (message["content"] is JsonArray parts)
        {
            var text = new StringBuilder();
            foreach (var part in parts)
            {
                if (part is JsonObject obj && obj["text"] is JsonValue value)
                {
                    try { text.Append(value.GetValue<string>()); }
                    catch (InvalidOperationException) { }
                }
                else if (part is JsonValue scalar)
                {
                    try { text.Append(scalar.GetValue<string>()); }
                    catch (InvalidOperationException) { }
                }
            }
            message["content"] = text.ToString();
        }

        // If a compatible gateway returns only reasoning_content, surface it as
        // content rather than silently completing with an empty UI response.
        var content = message["content"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(content) && message["reasoning_content"] is JsonValue reasoning)
        {
            try
            {
                var reasoningText = reasoning.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(reasoningText))
                    message["content"] = reasoningText;
            }
            catch (InvalidOperationException) { }
        }
    }

    public void Dispose() => _http.Dispose();
}
