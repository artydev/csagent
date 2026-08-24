extern alias PhotinoX;

using System.Drawing;
using System.Reflection;
using System.Text;
using CsAgentUI.Shared;
using PhotinoApplication = PhotinoX::Photino.NET.PhotinoApplication;
using PhotinoWindow = PhotinoX::Photino.NET.PhotinoWindow;

namespace CsAgentUI.Presentation.DesktopPhotinoX;

/// <summary>
/// PhotinoX window host — opens a native window and loads the CSAgent UI from
/// embedded assets by injecting HTML directly.
/// Launched with the "--desktopx" argument.
///
/// PhotinoX differs from Photino.NET in its hosting model: instead of calling
/// <c>WaitForClose()</c> on the window, you create a <see cref="PhotinoApplication"/>
/// and pass the main window to <c>Run(window)</c>, which pumps the message loop
/// until the window closes.
/// </summary>
public static class PhotinoXHost
{
    /// <summary>
    /// Runs the agent inside a native PhotinoX window.
    /// Called from [STAThread] Main on Windows.
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

        // Load embedded resources as strings
        Console.WriteLine("Loading embedded resources...");
        var indexHtml = LoadResourceAsString("CsAgentUI.src.Presentation.DesktopPhotinoX.assets.index.html");
        var appJs = LoadResourceAsString("CsAgentUI.src.Presentation.DesktopPhotinoX.assets.app.js");
        var stylesCss = LoadResourceAsString("CsAgentUI.src.Presentation.DesktopPhotinoX.assets.styles.css");

        if (string.IsNullOrEmpty(indexHtml))
        {
            Console.Error.WriteLine("FATAL: index.html not found!");
            PrintAvailableResources();
            return;
        }

        // Inject CSS and JS into HTML
        var htmlContent = InjectAssetsIntoHtml(indexHtml, stylesCss, appJs);

        // Create the PhotinoX application and main window.
        var app = new PhotinoApplication();

        var window = new PhotinoWindow()
        {
            Title = "CSAgent DesktopX",
            Width = 1280,
            Height = 800,
            StartString = htmlContent  // ✅ Use StartString property for HTML
        };

        Console.WriteLine("✓ PhotinoX window created");

        // Wire the bridge: JS → .NET via HandleMessage, .NET → JS via SendWebMessage.
        var api = new PhotinoXAPI(window, args);
        window.RegisterWebMessageReceivedHandler((sender, e) => api.HandleMessage(e.Message));

        // Run the message loop until the window closes (PhotinoX hosting model).
        app.Run(window);
        api.Dispose();
    }

    /// <summary>
    /// Injects CSS and JS into the HTML document.
    /// </summary>
    private static string InjectAssetsIntoHtml(string html, string? css, string? js)
    {
        var result = html;

        // Inject CSS into </head>
        if (!string.IsNullOrEmpty(css))
        {
            var headEnd = result.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
            if (headEnd > 0)
            {
                var styleTag = $"\n    <style>\n{css}\n    </style>\n    ";
                result = result.Insert(headEnd, styleTag);
            }
        }

        // Inject JS before </body>
        if (!string.IsNullOrEmpty(js))
        {
            var bodyEnd = result.IndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            if (bodyEnd > 0)
            {
                var scriptTag = $"\n    <script>\n{js}\n    </script>\n    ";
                result = result.Insert(bodyEnd, scriptTag);
            }
        }

        return result;
    }

    private static string? LoadResourceAsString(string resourceName)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading '{resourceName}': {ex.Message}");
        }

        return null;
    }

    private static void PrintAvailableResources()
    {
        Console.Error.WriteLine("\n=== Available Embedded Resources ===");
        var assembly = Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames();

        var relevant = resources
            .Where(r => r.Contains("DesktopPhotinoX") || r.Contains("assets"))
            .ToList();

        if (relevant.Any())
        {
            foreach (var res in relevant)
                Console.Error.WriteLine($"  ✓ {res}");
        }
        else
        {
            Console.Error.WriteLine("  (No DesktopPhotinoX or assets resources found)");
            Console.Error.WriteLine("\n=== First 30 Resources in Assembly ===");
            foreach (var res in resources.Take(30))
                Console.Error.WriteLine($"  - {res}");
        }
    }
}
