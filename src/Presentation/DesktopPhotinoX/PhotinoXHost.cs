extern alias PhotinoX;

using System.Reflection;
using System.Text;
using CsAgentUI.Shared;
using PhotinoApplication = PhotinoX::Photino.NET.PhotinoApplication;
using PhotinoWindow = PhotinoX::Photino.NET.PhotinoWindow;

namespace CsAgentUI.Presentation.DesktopPhotinoX;

public static class PhotinoXHost
{
    public static void Run(AgentArguments args)
    {
        var apiKey = Environment.GetEnvironmentVariable("ALBERT_API_KEY") ?? "";
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: ALBERT_API_KEY env var not set.");
            return;
        }

        Console.WriteLine("Loading embedded PhotinoX resources...");
        var indexHtml = LoadResourceAsString("CsAgentUI.src.Presentation.DesktopPhotinoX.assets.index.html");
        var bridgeJs = LoadResourceAsString("CsAgentUI.src.Presentation.DesktopPhotinoX.assets.bridge.js");
        var appJs = LoadResourceAsString("CsAgentUI.src.Presentation.DesktopPhotinoX.assets.app.js");
        var stylesCss = LoadResourceAsString("CsAgentUI.src.Presentation.DesktopPhotinoX.assets.styles.css");

        if (string.IsNullOrEmpty(indexHtml))
        {
            Console.Error.WriteLine("FATAL: index.html not found!");
            PrintAvailableResources();
            return;
        }

        var htmlContent = InjectAssetsIntoHtml(indexHtml, stylesCss, bridgeJs, appJs);

        var app = new PhotinoApplication();
        var window = new PhotinoWindow
        {
            Title = "CSAgent DesktopX",
            Width = 1280,
            Height = 800,
            StartString = htmlContent
        };

        Console.WriteLine("✓ PhotinoX window created");

        var api = new PhotinoXAPI(window, args);
        window.RegisterWebMessageReceivedHandler((sender, e) => api.HandleMessage(e.Message));

        app.Run(window);
        api.Dispose();
    }

    private static string InjectAssetsIntoHtml(string html, string? css, string? bridgeJs, string? appJs)
    {
        var result = html;

        if (!string.IsNullOrEmpty(css))
        {
            var headEnd = result.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
            if (headEnd > 0)
                result = result.Insert(headEnd, $"\n<style>\n{css}\n</style>\n");
        }

        var bodyEnd = result.IndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (bodyEnd > 0)
        {
            var scripts = new StringBuilder();
            if (!string.IsNullOrEmpty(bridgeJs))
                scripts.Append("\n<script>\n").Append(bridgeJs).Append("\n</script>\n");
            if (!string.IsNullOrEmpty(appJs))
                scripts.Append("\n<script>\n").Append(appJs).Append("\n</script>\n");
            result = result.Insert(bodyEnd, scripts.ToString());
        }

        return result;
    }

    private static string? LoadResourceAsString(string resourceName)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null) return null;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading '{resourceName}': {ex.Message}");
            return null;
        }
    }

    private static void PrintAvailableResources()
    {
        Console.Error.WriteLine("\n=== Available Embedded Resources ===");
        var resources = Assembly.GetExecutingAssembly().GetManifestResourceNames();
        var relevant = resources.Where(r => r.Contains("DesktopPhotinoX") || r.Contains("assets")).ToList();

        if (relevant.Count > 0)
        {
            foreach (var resource in relevant)
                Console.Error.WriteLine($"  ✓ {resource}");
        }
        else
        {
            Console.Error.WriteLine("  (No DesktopPhotinoX or assets resources found)");
        }
    }
}
