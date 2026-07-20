using CsAgentUI;
using System.Text.Json.Nodes;

namespace CsAgentUI.Endpoints;

public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app, string memoryFile)
    {
        // API endpoint for chat functionality
        app.MapGet("/api/chat", async (HttpContext ctx, string prompt) =>
        {
            ctx.Response.Headers.ContentType = "text/event-stream";
            var observer = new SseObserver(ctx.Response);

            var apiKey = Environment.GetEnvironmentVariable("ALBERT_API_KEY") ?? "";
            if (string.IsNullOrEmpty(apiKey)) 
            { 
                await observer.OnError("API Key not set."); 
                return; 
            }

            var msgs = await MemoryStore.LoadAsync(memoryFile);
            if (msgs.Count == 0) 
                msgs.Add(CodingAgent.SystemMessage(OperatingSystem.IsWindows()));

            msgs.Add(new JsonObject { ["role"] = "user", ["content"] = prompt });

            using var agent = new CodingAgent(
                apiKey, 
                "https://albert.api.etalab.gouv.fr/v1", 
                "Qwen/Qwen3-Coder-30B-A3B-Instruct", 
                new AgentOptions(), 
                observer);
                
            await agent.RunAsync(msgs, memoryFile);
        });

        return app;
    }
}
