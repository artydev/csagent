namespace CsAgentUI.Shared;

/// <summary>
/// Parsed CLI arguments — clean record, no parsing logic mixed in.
/// </summary>
public sealed record AgentArguments(
    string MemoryFile,
    string? ModelOverride,
    string? McpUrl,
    int Port,
    bool IsUiMode,
    bool IsNativeMode,
    bool IsDesktopMode,
    bool IsDryRun,
    bool ShowHelp,
    bool ShowVersion,
    bool ShowDoc);

/// <summary>
/// Pure argument parsing — no side effects, no console output.
/// </summary>
public static class ArgumentParser
{
    public static AgentArguments Parse(string[] args)
    {
        var isUiMode = args.Contains("--ui");
        var isNativeMode = args.Contains("--native");
        var isDesktopMode = args.Contains("--desktop");
        var isDryRun = args.Contains("--dry-run");
        var showHelp = args.Contains("--help") || args.Contains("-h") || args.Contains("/?");
        var showVersion = args.Contains("--version");
        var showDoc = args.Contains("--doc");
        var memFile = GetMemoryFile(args);
        var modelOverride = GetModelOverride(args);
        var mcpUrl = GetValue(args, "--mcp", "--mcp-url")
                     ?? Environment.GetEnvironmentVariable("CSAGENT_MCP_URL");
        var port = GetPort(args);

        return new AgentArguments(memFile, modelOverride, mcpUrl, port, isUiMode, isNativeMode, isDesktopMode, isDryRun, showHelp, showVersion, showDoc);
    }

    private static string GetMemoryFile(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "--mem" && i + 1 < args.Length) return args[i + 1];

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "--model" or "--mcp" or "--mcp-url" or "--port" or "-p")
            {
                i++;
                continue;
            }

            if (args[i] != "--ui" && args[i] != "--native" && args[i] != "--desktop" && args[i] != "--dry-run" && !args[i].StartsWith("-"))
                return args[i];
        }

        return "agent_memory.json";
    }

    private static string? GetModelOverride(string[] args) => GetValue(args, "--model");

    private static string? GetValue(string[] args, params string[] names)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (names.Contains(args[i], StringComparer.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static int GetPort(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
            if ((args[i] == "--port" || args[i] == "-p") && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out var p) && p > 0 && p < 65536)
                    return p;
            }
        return 5050;
    }
}
