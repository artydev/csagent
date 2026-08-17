namespace CsAgentUI.Shared;

/// <summary>
/// Renders the help text to the console.
/// </summary>
public static class HelpDisplay
{
    public static void Show(string version)
    {
        var useColor = !Console.IsOutputRedirected
                      && (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TERM"))
                          || OperatingSystem.IsLinux()
                          || OperatingSystem.IsMacOS());

        void C(string? color = null)
        {
            if (useColor && color is not null)
                Console.ForegroundColor = color switch
                {
                    "cyan"    => ConsoleColor.Cyan,
                    "green"   => ConsoleColor.Green,
                    "yellow"  => ConsoleColor.Yellow,
                    "magenta" => ConsoleColor.Magenta,
                    "dark"    => ConsoleColor.DarkGray,
                    _         => ConsoleColor.Gray
                };
            else
                Console.ResetColor();
        }

        C("magenta");
        Console.WriteLine();
        Console.WriteLine(@"   ██████╗███████╗ █████╗  ██████╗ ███████╗███╗   ██╗████████╗");
        Console.WriteLine(@"  ██╔════╝██╔════╝██╔══██╗██╔════╝ ██╔════╝████╗  ██║╚══██╔══╝");
        Console.WriteLine(@"  ██║     ███████╗███████║██║  ███╗█████╗  ██╔██╗ ██║   ██║   ");
        Console.WriteLine(@"  ██║     ╚════██║██╔══██║██║   ██║██╔══╝  ██║╚██╗██║   ██║   ");
        Console.WriteLine(@"  ╚██████╗███████║██║  ██║╚██████╔╝███████╗██║ ╚████║   ██║   ");
        Console.WriteLine(@"   ╚═════╝╚══════╝╚═╝  ╚═╝ ╚═════╝ ╚══════╝╚═╝  ╚═══╝   ╚═══╝  ");
        Console.ResetColor();
        C("dark");
        Console.WriteLine("  Cross-platform autonomous coding agent  |  zero NuGet deps");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"  USAGE:  CsAgentUI [options] [memory-file]");
        Console.WriteLine();
        Console.WriteLine($"  VERSION:  {version}");
        Console.WriteLine();

        C("green");
        Console.WriteLine("  MODES");
        Console.ResetColor();
        Console.WriteLine("    (no flag)     CLI mode — interactive terminal session");
        Console.WriteLine("    --ui          Web UI mode — starts a web server");
        Console.WriteLine();

        C("green");
        Console.WriteLine("  OPTIONS");
        Console.ResetColor();
        Console.WriteLine("    --help, -h, /?    Show this help message and exit");
        Console.WriteLine("    --version         Show version number and exit");
        Console.WriteLine("    --doc             Display full documentation in terminal and exit");
        Console.WriteLine("    --mem <file>      Use a custom memory/conversation file");
        Console.WriteLine("                       (default: agent_memory.json)");
        Console.WriteLine("    --model <name>    Override the LLM model for the current mode");
        Console.WriteLine($"                       (default: {LlmSettings.Model})");
        Console.WriteLine("    --port, -p <n>    Web UI port number (default: 5050)");
        Console.WriteLine("    --dry-run         Simulate tool execution without making changes");
        Console.WriteLine();

        C("green");
        Console.WriteLine("  ENVIRONMENT");
        Console.ResetColor();
        Console.WriteLine("    ALBERT_API_KEY    API key for the OpenAI-compatible endpoint (required)");
        Console.WriteLine();

        C("green");
        Console.WriteLine("  EXAMPLES");
        Console.ResetColor();
        Console.WriteLine("    csagent                                       CLI mode");
        Console.WriteLine("    csagent --ui                                  Web UI mode (port 5050)");
        Console.WriteLine("    csagent --ui --port 8080                      Web UI on port 8080");
        Console.WriteLine("    csagent --model gpt-4o                        CLI with custom model");
        Console.WriteLine("    csagent --ui --model gpt-4o                   Web UI with custom model");
        Console.WriteLine("    csagent --mem my_history.json                 CLI with custom memory file");
        Console.WriteLine("    csagent --ui --mem my_history.json            Web UI with custom memory file");
        Console.WriteLine("    csagent --dry-run                             Dry-run mode (no changes)");
        Console.WriteLine("    csagent --doc                                 Show documentation");
        Console.WriteLine("    csagent --version                             Show version");
        Console.WriteLine("    csagent --help                                Show this help");
        Console.WriteLine();

        C("dark");
        Console.WriteLine("  NOTE: All destructive actions (write_file) require user confirmation.");
        Console.WriteLine("  File operations are restricted to the current working directory.");
        Console.ResetColor();
        Console.WriteLine();
    }
}
