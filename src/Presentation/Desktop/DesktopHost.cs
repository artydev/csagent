using System.Runtime.InteropServices.Marshalling;
using CsAgentUI.Shared;

namespace CsAgentUI.Presentation.Desktop;

/// <summary>
/// Desktop window host — runs the CSAgent inside an AOTrino native window.
/// Launched with the "--desktop" argument.
/// </summary>
internal static class DesktopHost
{
    /// <summary>
    /// Runs the agent with a native AOTrino desktop window.
    /// </summary>
    public static void Run(AgentArguments args)
    {
        try
        {
            using var app = new AOTrinoApplication();
            using var window = new CsAgentWindow(args);
            window.ResizeClient(1000, 700);
            window.Center();
            window.Show();
            app.Run();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error launching desktop window: {ex.Message}");
            Console.Error.WriteLine(ex.ToString());
        }
    }
}

/// <summary>
/// The main AOTrino window for the CSAgent desktop application.
/// </summary>
[GeneratedComClass]
internal partial class CsAgentWindow : AOTrinoWindow
{
    private readonly AgentArguments _args;

    public CsAgentWindow(AgentArguments args)
        : base("CSAgent")
    {
        _args = args;
    }

    /// <summary>
    /// Navigate to an inline HTML page with agent information.
    /// </summary>
    protected override string? StartUrl => BuildDataUri();

    private string BuildDataUri()
    {
        var model = _args.ModelOverride ?? LlmSettings.Model;
        var version = Program.Version;
        var memFile = _args.MemoryFile;
        var dryRunText = _args.IsDryRun ? "ON" : "OFF";
        var os = OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsLinux() ? "Linux" : "macOS";

        var html = "<!DOCTYPE html>" +
        "<html lang=\"en\">" +
        "<head>" +
        "<meta charset=\"UTF-8\">" +
        "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1.0\">" +
        "<title>CSAgent</title>" +
        "<style>" +
        "*{margin:0;padding:0;box-sizing:border-box}" +
        "body{font-family:'Segoe UI',system-ui,sans-serif;background:#1e1e2e;color:#cdd6f4;display:flex;flex-direction:column;height:100vh;overflow:hidden}" +
        "header{background:#181825;padding:12px 20px;border-bottom:1px solid #313244;display:flex;align-items:center;gap:12px}" +
        "header h1{font-size:18px;font-weight:600}" +
        "header .badge{background:#45475a;padding:2px 10px;border-radius:10px;font-size:12px}" +
        "main{flex:1;padding:20px;overflow-y:auto}" +
        ".info-grid{display:grid;grid-template-columns:auto 1fr;gap:8px 16px;font-size:14px}" +
        ".info-grid .label{color:#a6adc8}" +
        ".info-grid .value{color:#cdd6f4}" +
        "footer{background:#181825;padding:8px 20px;border-top:1px solid #313244;font-size:12px;color:#6c7086;text-align:center}" +
        "</style>" +
        "</head>" +
        "<body>" +
        "<header>" +
        "<h1>CSAgent</h1>" +
        "<span class=\"badge\">v" + version + "</span>" +
        "<span class=\"badge\">Desktop</span>" +
        "</header>" +
        "<main>" +
        "<h2 style=\"margin-bottom:16px\">Agent Configuration</h2>" +
        "<div class=\"info-grid\">" +
        "<span class=\"label\">Model</span><span class=\"value\">" + model + "</span>" +
        "<span class=\"label\">Memory File</span><span class=\"value\">" + memFile + "</span>" +
        "<span class=\"label\">Dry Run</span><span class=\"value\">" + dryRunText + "</span>" +
        "<span class=\"label\">OS</span><span class=\"value\">" + os + "</span>" +
        "</div>" +
        "</main>" +
        "<footer>CSAgent v" + version + " &mdash; Powered by AOTrino</footer>" +
        "</body>" +
        "</html>";

        var encoded = Uri.EscapeDataString(html);
        
        return "data:text/html;charset=utf-8," + encoded;
    }

    /// <summary>
    /// Allow navigation only to data URIs and about:blank.
    /// </summary>
    protected override bool IsNavigationAllowed(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return uri.Scheme is "about" or "data" or "blob";
    }
}
