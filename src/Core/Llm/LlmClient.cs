using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace CsAgentUI;

public sealed class LlmClient : IDisposable
{
    private readonly HttpClient _http;
    private string     _model;
    private readonly string     _baseUrl;

    public LlmClient(string apiKey, string baseUrl, string model)
    {
        _model   = model;
        _baseUrl = baseUrl.TrimEnd('/');
        _http    = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
    }

    /// <summary>
    /// The currently active model identifier. Can be changed at runtime
    /// (e.g. via the switch_model tool) to switch models mid-session.
    /// </summary>
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
            ["model"]       = _model,
            ["temperature"] = 0.1,
            ["messages"]    = messages.DeepClone()
        };
        if (tools is { Count: > 0 })
        {
            body["tools"]       = tools.DeepClone();
            body["tool_choice"] = "auto";
        }

        var req = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        var res = await _http.PostAsync($"{_baseUrl}/chat/completions", req, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"API {(int)res.StatusCode}: {raw}");

        return JsonNode.Parse(raw) ?? throw new InvalidDataException("Empty API response");
    }

    public void Dispose() => _http.Dispose();
}
