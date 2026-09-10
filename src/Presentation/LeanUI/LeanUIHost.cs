using System.Diagnostics;
using CsAgentUI.Endpoints;
using CsAgentUI.Shared;

namespace CsAgentUI.Presentation.LeanUI;

/// <summary>Lean UI host — lightweight Web UI, launched via --leanui.</summary>
public static class LeanUIHost
{
    public static void Run(AgentArguments args)
    {
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.Logging.SetMinimumLevel(LogLevel.Critical);

        var app = builder.Build();

        app.MapGet("/", () => Results.Content(LeanStaticAssets.HtmlUI, "text/html"));
        app.MapGet("/app.js", () => Results.Content(LeanStaticAssets.JsUI, "application/javascript"));
        app.MapGet("/styles.css", () => Results.Content(LeanStaticAssets.CssUI, "text/css"));

        app.MapEndpoints(
            args.MemoryFile, args.ModelOverride, args.McpUrl,
            new RetryPolicy(args.MaxRetries, args.RetryDelayMs),
            args.TaskSlug);

        var url = $"http://localhost:{args.Port}";

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            Console.WriteLine($"\n--- LeanUI server started at {url} ---");
            if (!string.IsNullOrWhiteSpace(args.McpUrl))
                Console.WriteLine($"--- MCP endpoint: {args.McpUrl} ---");
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { }
        });

        app.Run(url);
    }
}