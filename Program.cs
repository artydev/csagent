using CsAgentUI;
using CsAgentUI.Endpoints;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;

const string Version = "0.2.0";

if (args.Contains("--version"))
{
    Console.WriteLine($"CSAgent version {Version}");
    return;
}

if (args.Contains("--doc"))
{
    ShowDocumentation();
    return;
}

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
    Console.WriteLine($"  CSAgent v{Version}");
    Console.WriteLine();
    var apiKey = Environment.GetEnvironmentVariable("ALBERT_API_KEY") ?? "";
    if (string.IsNullOrEmpty(apiKey)) { Console.WriteLine("Error: ALBERT_API_KEY env var not set."); return; }

    var messages = await MemoryStore.LoadAsync(memFile);
    if (messages.Count == 0) messages.Add(CodingAgent.SystemMessage(OperatingSystem.IsWindows()));

    // Default to confirm mode for enhanced security (this is now the default in AgentOptions)
    using var agent = new CodingAgent(apiKey, "https://albert.api.etalab.gouv.fr/v1", "deepseek-v4-flash", new AgentOptions(Confirm: true), new ConsoleObserver());

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

// ── Documentation Display ──

static void ShowDocumentation()
{
    var assembly = Assembly.GetExecutingAssembly();

    // Find the embedded resource matching README.md regardless of namespace prefix
    var resourceName = assembly.GetManifestResourceNames()
        .FirstOrDefault(r => r.EndsWith("README.md", StringComparison.OrdinalIgnoreCase));

    if (string.IsNullOrEmpty(resourceName))
    {
        Console.Error.WriteLine("Error: README.md embedded resource not found.");
        return;
    }

    using var stream = assembly.GetManifestResourceStream(resourceName);
    if (stream == null)
    {
        Console.Error.WriteLine("Error: Could not read embedded README.md stream.");
        return;
    }

    using var reader = new StreamReader(stream);
    var lines = new List<string>();
    string? fileLine;
    while ((fileLine = reader.ReadLine()) != null)
    {
        lines.Add(fileLine);
    }

    // Determine terminal width safely
    var termWidth = 80;
    try
    {
        if (!Console.IsOutputRedirected)
            termWidth = Console.WindowWidth;
    }
    catch
    {
        // Fall back to default width
    }

    // Detect if terminal supports ANSI colors
    var useColor = !Console.IsOutputRedirected
                  && (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TERM"))
                      || OperatingSystem.IsLinux()
                      || OperatingSystem.IsMacOS());

    foreach (var line in lines)
    {
        var trimmed = line.Trim();

        // ── H1: Title ──
        if (trimmed.StartsWith("# ") && !trimmed.StartsWith("##"))
        {
            var title = trimmed[2..].Trim();
            var sep = new string('=', Math.Min(title.Length, termWidth - 1));
            if (useColor)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine();
                Console.WriteLine($"  {title}");
                Console.WriteLine($"  {sep}");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine($"  {title}");
                Console.WriteLine($"  {sep}");
            }
            Console.WriteLine();
            continue;
        }

        // ── H2: Section ──
        if (trimmed.StartsWith("## ") && !trimmed.StartsWith("###"))
        {
            var section = trimmed[3..].Trim();
            if (useColor)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  {section}");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($"  {section}");
            }
            Console.WriteLine();
            continue;
        }

        // ── H3: Sub-section ──
        if (trimmed.StartsWith("### "))
        {
            var sub = trimmed[4..].Trim();
            if (useColor)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  {sub}");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($"  {sub}");
            }
            continue;
        }

        // ── Horizontal rule ──
        if (trimmed == "---")
        {
            var hr = new string('─', Math.Min(60, termWidth - 1));
            if (useColor)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  {hr}");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($"  {hr}");
            }
            Console.WriteLine();
            continue;
        }

        // ── Unordered list item ──
        if (trimmed.StartsWith("- "))
        {
            var item = trimmed[2..].Trim();
            if (useColor)
            {
                var parts = SplitBold(item);
                Console.Write("  • ");
                foreach (var (text, isBold) in parts)
                {
                    if (isBold)
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write(text);
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.Write(text);
                    }
                }
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine($"  • {item}");
            }
            continue;
        }

        // ── Numbered list item ──
        if (trimmed.Length > 2 && char.IsDigit(trimmed[0]) && trimmed[1] == '.')
        {
            var idx = trimmed.IndexOf(' ');
            var num = trimmed[..idx];
            var item = trimmed[(idx + 1)..].Trim();
            if (useColor)
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.Write($"  {num}.");
                Console.ResetColor();
                Console.WriteLine($" {item}");
            }
            else
            {
                Console.WriteLine($"  {num}. {item}");
            }
            continue;
        }

        // ── Inline code (backtick) ──
        if (trimmed.StartsWith("`") && trimmed.EndsWith("`") && !trimmed.Contains(' '))
        {
            var code = trimmed.Trim('`');
            if (useColor)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"  {code}");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($"  {code}");
            }
            continue;
        }

        // ── Code block markers ──
        if (trimmed.StartsWith("```"))
        {
            continue;
        }

        // ── Table row ──
        if (trimmed.StartsWith("|") && trimmed.EndsWith("|"))
        {
            var cells = trimmed.Split('|', StringSplitOptions.RemoveEmptyEntries);
            var isHeader = cells.Length > 0 && cells.All(c => c.Trim().All(ch => ch == '-' || ch == ':'));

            if (isHeader)
            {
                if (useColor)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  {'─',-60}");
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine($"  {'─',-60}");
                }
                continue;
            }

            var formatted = string.Join(" │ ", cells.Select(c => c.Trim()));
            if (useColor)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  {formatted}");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($"  {formatted}");
            }
            continue;
        }

        // ── Bold line (surrounded by **) ──
        if (trimmed.StartsWith("**") && trimmed.EndsWith("**") && trimmed.Length > 4)
        {
            var boldText = trimmed.Trim('*');
            if (useColor)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"  {boldText}");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($"  {boldText}");
            }
            continue;
        }

        // ── Regular paragraph ──
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            if (useColor && trimmed.Contains("**"))
            {
                var parts = SplitBold(trimmed);
                foreach (var (text, isBold) in parts)
                {
                    if (isBold)
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write(text);
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.Write(text);
                    }
                }
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine($"  {trimmed}");
            }
            continue;
        }

        // ── Empty line ──
        Console.WriteLine();
    }

    Console.WriteLine();
}


// Helper to split text by **bold** markers
static List<(string text, bool isBold)> SplitBold(string input)
{
    var result = new List<(string, bool)>();
    var remaining = input;
    while (remaining.Length > 0)
    {
        var boldStart = remaining.IndexOf("**", StringComparison.Ordinal);
        if (boldStart < 0)
        {
            result.Add((remaining, false));
            break;
        }

        if (boldStart > 0)
        {
            result.Add((remaining[..boldStart], false));
        }

        var boldEnd = remaining.IndexOf("**", boldStart + 2, StringComparison.Ordinal);
        if (boldEnd < 0)
        {
            result.Add((remaining[boldStart..], false));
            break;
        }

        var boldContent = remaining[(boldStart + 2)..boldEnd];
        result.Add((boldContent, true));
        remaining = remaining[(boldEnd + 2)..];
    }
    return result;
}
