using CsAgentUI.Shared;
using System.Reflection;
using System.Runtime.InteropServices.Marshalling;

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
    public  static void Run(AgentArguments args)
    {

        var apiKey = Environment.GetEnvironmentVariable("ALBERT_API_KEY") ?? "";
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: ALBERT_API_KEY env var not set.");
            return;
        }


        var messages = Task.Run(() => MemoryStore.LoadAsync(args.MemoryFile)).Result;
        if (messages.Count == 0)
            messages.Add(CodingAgent.SystemMessage(OperatingSystem.IsWindows()));

        try
        {
            using var app = new AOTrinoApplication();
            using var window = new CsAgentWindow(args);
            window.ResizeClient(1200, 800);
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

        var html = LoadEmbeddedResource("CsAgentUI.src.Presentation.Desktop.assets.index.html");
        var script = LoadEmbeddedResource("CsAgentUI.src.Presentation.Desktop.assets.app.js");
        var style  = LoadEmbeddedResource("CsAgentUI.src.Presentation.Desktop.assets.styles.css");

        html = html.Replace("{{Version}}", version)
                   .Replace("{{Model}}", model)
                   .Replace("{{MemoryFile}}", memFile)
                   .Replace("{{DryRun}}", dryRunText)
                   .Replace("{{OS}}", os)
                   .Replace("{{Script}}", script)
                   .Replace("{{Style}}", style);

        var encoded = Uri.EscapeDataString(html);
        
        return "data:text/html;charset=utf-8," + encoded;
    }

    private static string LoadEmbeddedResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return string.Empty;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
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
