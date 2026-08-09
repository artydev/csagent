using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CsAgentUI;

public static class MemoryStore
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    public static async Task<JsonArray> LoadAsync(string path)
    {
        if (!File.Exists(path)) return [];
        try
        {
            var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
            return JsonNode.Parse(json)?.AsArray() ?? [];
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[MemoryStore] {ex.Message}");
            return [];
        }
    }

    public static async Task SaveAsync(string path, JsonArray messages)
    {
        var json = messages.ToJsonString(Pretty);
        await File.WriteAllTextAsync(path, json, Encoding.UTF8);
    }
}
