using System.Drawing;
using System.Reflection;
using System.Text;
using CsAgentUI.Shared;
using Photino.NET;

namespace CsAgentUI.Presentation.DesktopPhotino;

/// <summary>
/// Photino window host — opens a native window and loads the CSAgent UI from
/// embedded assets served under a custom "app://" scheme.
/// Launched with the "--desktop" argument (see Task 5 for CLI integration).
/// </summary>
public static class PhotinoHost
{
    /// <summary>
    /// Runs the agent inside a native Photino window.
    /// </summary>
    public static void Run(AgentArguments args)
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

        var window = new PhotinoWindow()
            .SetTitle("CSAgent Desktop")
            .SetUseOsDefaultSize(false)
            .SetSize(new Size(1280, 800))
            .Center()
            .RegisterCustomSchemeHandler("app", ServeEmbeddedResource)
            .Load("app://index.html");

        // Wire the bridge: JS → .NET via HandleMessage, .NET → JS via SendWebMessage.
        var api = new PhotinoAPI(window, args);
        window.RegisterWebMessageReceivedHandler((sender, message) => api.HandleMessage(message));

        window.WaitForClose();
        api.Dispose();
    }

    /// <summary>
    /// Serves the Photino embedded assets (index.html, app.js, styles.css) under
    /// the custom "app://" scheme so the app is fully self-contained.
    /// </summary>
    private static Stream ServeEmbeddedResource(object sender, string scheme, string url, out string contentType)
    {
        var path = url;
        var slash = path.IndexOf('/');
        if (slash >= 0)
            path = path[(slash + 1)..];

        switch (path)
        {
            case "app.js":
                contentType = "application/javascript";
                return LoadEmbeddedResource("CsAgentUI.src.Presentation.DesktopPhotino.assets.app.js");
            case "styles.css":
                contentType = "text/css";
                return LoadEmbeddedResource("CsAgentUI.src.Presentation.DesktopPhotino.assets.styles.css");
            default:
                contentType = "text/html";
                return LoadEmbeddedResource("CsAgentUI.src.Presentation.DesktopPhotino.assets.index.html");
        }
    }

    private static Stream LoadEmbeddedResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is not null)
            return stream;

        // Fall back to an empty stream so the handler never returns null.
        return new MemoryStream(Encoding.UTF8.GetBytes(string.Empty));
    }
}
