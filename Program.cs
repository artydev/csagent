using CsAgentUI;
using CsAgentUI.Endpoints;
using System.Diagnostics;
using System.Text.Json.Nodes;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Critical);

var isUiMode = args.Contains("--ui");

// Helper to find memory file from args
static string GetMemoryFile(string[] args)
{
    for (int i = 0; i < args.Length; i++)
        if (args[i] == "--mem" && i + 1 < args.Length) return args[i + 1];

    foreach (var arg in args)
        if (arg != "--ui" && !arg.StartsWith("-")) return arg;

    return "agent_memory.json";
}

var memFile = GetMemoryFile(args);

// ── UI Mode (Web Server) ──
if (isUiMode)
{
    var app = builder.Build();

    // Serve the UI from the embedded string (AOT-safe)
    app.MapGet("/", () => Results.Content(StaticAssets.HtmlUI, "text/html"));

    // Map API endpoints
    app.MapEndpoints(memFile);

    // Register browser launch after server starts
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        Console.WriteLine("\n--- Server started at http://localhost:5050 ---");
        try
        {
            // Attempt to open the default browser
            Process.Start(new ProcessStartInfo("http://localhost:5050") { UseShellExecute = true });
        }
        catch { /* Fail silently if browser cannot be launched */ }
    });

    app.Run("http://localhost:5050");
}

// ── CLI Mode ──
else
{
    UI.Banner();
    var apiKey = Environment.GetEnvironmentVariable("ALBERT_API_KEY") ?? "";
    if (string.IsNullOrEmpty(apiKey)) { Console.WriteLine("Error: ALBERT_API_KEY env var not set."); return; }

    var messages = await MemoryStore.LoadAsync(memFile);
    if (messages.Count == 0) messages.Add(CodingAgent.SystemMessage(OperatingSystem.IsWindows()));

    // Default to confirm mode for enhanced security (this is now the default in AgentOptions)
    using var agent = new CodingAgent(apiKey, "https://albert.api.etalab.gouv.fr/v1", "Qwen/Qwen3-Coder-30B-A3B-Instruct", new AgentOptions(Confirm: true), new ConsoleObserver());

    while (true)
    {
        Console.Write("\n> User (type 'exit' to quit): ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input)) continue;
        if (input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

        messages.Add(new JsonObject { ["role"] = "user", ["content"] = input });
        await agent.RunAsync(messages, memFile);
    }
}
