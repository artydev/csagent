using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace CsAgentUI;

public class SseObserver(HttpResponse res) : IAgentObserver
{
    private static int _msgId = 0;

    private async Task Send(string type, object data)
    {
      
        var id = Interlocked.Increment(ref _msgId);
        var payload = new SseMessage(id, type, data);
        var json = JsonSerializer.Serialize(payload, WebJsonContext.Default.SseMessage);
        Debug.WriteLine(json);
        await res.WriteAsync($"data: {json}\n\n");
        await res.Body.FlushAsync();
    }

    public Task OnStep(int n, int m) => Send("step", new SseStep(n, m));
    public Task OnThought(string t) => Send("thought", "ceci est un simple message");
    public Task OnToolCall(string n, string a) => Send("call", new SseCall(n, a));
    public Task OnToolResult(string r, bool e) => Send("result", new SseResult(r, e));
    public Task OnDone(string m) => Send("done", m);
    public Task OnError(string m) => Send("error", m);
    public Task OnWarning(string m) => Send("warning", m);
    public Task OnDanger(string m) => Send("danger", m);
}

// ── SSE message types ──

public record SseMessage(int id, string type, object data);
public record SseStep(int n, int m);
public record SseCall(string n, string a);
public record SseResult(string r, bool e);

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(SseMessage))]
[JsonSerializable(typeof(SseStep))]
[JsonSerializable(typeof(SseCall))]
[JsonSerializable(typeof(SseResult))]
internal partial class WebJsonContext : JsonSerializerContext { }
