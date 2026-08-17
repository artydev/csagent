namespace CsAgentUI.Shared;

/// <summary>
/// Parsed CLI arguments — clean record, no parsing logic mixed in.
/// </summary>
public sealed record AgentArguments(
    string MemoryFile,
    string? ModelOverride,
    int Port,
    bool IsUiMode,
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
        var isDryRun = args.Contains("--dry-run");
        var showHelp = args.Contains("--help") || args.Contains("-h") || args.Contains("/?");
        var showVersion = args.Contains("--version");
        var showDoc = args.Contains("--doc");
        var memFile = GetMemoryFile(args);
        var modelOverride = GetModelOverride(args);
        var port = GetPort(args);

        return new AgentArguments(memFile, modelOverride, port, isUiMode, isDryRun, showHelp, showVersion, showDoc);
    }

    private static string GetMemoryFile(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "--mem" && i + 1 < args.Length) return args[i + 1];

        foreach (var arg in args)
            if (arg != "--ui" && arg != "--dry-run" && !arg.StartsWith("-")) return arg;

        return "agent_memory.json";
    }

    private static string? GetModelOverride(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "--model" && i + 1 < args.Length) return args[i + 1];
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
