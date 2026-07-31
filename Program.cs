using CsAgentUI;
using CsAgentUI.Endpoints;
using System.Diagnostics;
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

// ── Argument helpers ────────────────────────────────────────────────────────

static string GetMemoryFile(string[] args)
{
    for (int i = 0; i < args.Length; i++)
        if (args[i] == "--mem" && i + 1 < args.Length) return args[i + 1];

    foreach (var arg in args)
        if (arg != "--ui" && !arg.StartsWith("-")) return arg;

    return "agent_memory.json";
}

static string? GetModelOverride(string[] args)
{
    for (int i = 0; i < args.Length; i++)
        if (args[i] == "--model" && i + 1 < args.Length) return args[i + 1];
    return null;
}

var memFile = GetMemoryFile(args);
var modelOverride = GetModelOverride(args);

// ── Web UI Mode ─────────────────────────────────────────────────────────────

if (isUiMode)
{
    var app = builder.Build();

    app.MapGet("/", () => Results.Content(StaticAssets.HtmlUI, "text/html"));
    app.MapEndpoints(memFile, modelOverride);

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        Console.WriteLine("\n--- Server started at http://localhost:5050 ---");
        try
        {
            Process.Start(new ProcessStartInfo("http://localhost:5050") { UseShellExecute = true });
        }
        catch { }
    });

    app.Run("http://localhost:5050");
}

// ── CLI Mode ────────────────────────────────────────────────────────────────

else
{
    UI.Banner();
    Console.WriteLine($"  CSAgent v{Version}");
    Console.WriteLine();

    var apiKey = Environment.GetEnvironmentVariable("ALBERT_API_KEY") ?? "";
    if (string.IsNullOrEmpty(apiKey))
    {
        Console.WriteLine("Error: ALBERT_API_KEY env var not set.");
        return;
    }

    var messages = await MemoryStore.LoadAsync(memFile);
    if (messages.Count == 0)
        messages.Add(CodingAgent.SystemMessage(OperatingSystem.IsWindows()));

    // Use unified model from LlmSettings, with optional override
    var model = modelOverride ?? LlmSettings.Model;
    Console.WriteLine($"  Model: {model}");
    Console.WriteLine();

    using var agent = new CodingAgent(
        apiKey,
        LlmSettings.Endpoint,
        model,
        new AgentOptions(Confirm: true),
        new ConsoleObserver());

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

// ── Documentation Display ───────────────────────────────────────────────────

static void ShowDocumentation()
{
    var lines = StaticAssets.ReadmeMd.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);

    var termWidth = 80;
    try
    {
        if (!Console.IsOutputRedirected)
            termWidth = Console.WindowWidth;
    }
    catch { }

    var useColor = !Console.IsOutputRedirected
                  && (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TERM"))
                      || OperatingSystem.IsLinux()
                      || OperatingSystem.IsMacOS());

    foreach (var line in lines)
    {
        var trimmed = line.Trim();

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

        if (trimmed.StartsWith("```"))
            continue;

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

        Console.WriteLine();
    }

    Console.WriteLine();
}

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
            result.Add((remaining[..boldStart], false));

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
