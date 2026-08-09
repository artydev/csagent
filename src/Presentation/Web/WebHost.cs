using System.Diagnostics;
using CsAgentUI.Endpoints;
using CsAgentUI.Shared;

namespace CsAgentUI.Presentation.Web;

/// <summary>
/// Web UI host — starts an ASP.NET server with SSE-based chat.
/// </summary>
public static class WebHost
{
    public static void Run(AgentArguments args)
    {
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.Logging.SetMinimumLevel(LogLevel.Critical);

        var app = builder.Build();

        app.MapGet("/", () => Results.Content(StaticAssets.HtmlUI, "text/html"));
        app.MapGet("/app.js", () => Results.Content(StaticAssets.JsUI, "application/javascript"));
        app.MapGet("/styles.css", () => Results.Content(StaticAssets.CssUI, "text/css"));

        app.MapEndpoints(args.MemoryFile, args.ModelOverride);

        var url = $"http://localhost:{args.Port}";

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            Console.WriteLine($"\n--- Server started at {url} ---");
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        });

        app.Run(url);
    }
}
