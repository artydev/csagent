using CsAgentUI.Shared;

namespace CsAgentUI.Endpoints;

public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app, string memoryFile, string? modelOverride = null)
    {
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

            msgs.Add(JsonHelpers.Message("user", prompt));

            var model = modelOverride ?? LlmSettings.Model;

            using var agent = new CodingAgent(
                apiKey,
                LlmSettings.Endpoint,
                model,
                new AgentOptions(),
                observer);

            await agent.RunAsync(msgs, memoryFile);
        });

        return app;
    }
}
