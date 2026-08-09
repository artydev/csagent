using CsAgentUI.Shared;

namespace CsAgentUI.Presentation.Tui;

/// <summary>
/// Terminal UI host — interactive CLI session.
/// </summary>
public static class TuiHost
{
    public static async Task RunAsync(AgentArguments args)
    {
        UI.Banner();
        Console.WriteLine($"  CSAgent v{Program.Version}");
        Console.WriteLine();

        var apiKey = Environment.GetEnvironmentVariable("ALBERT_API_KEY") ?? "";
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: ALBERT_API_KEY env var not set.");
            return;
        }

        var messages = await MemoryStore.LoadAsync(args.MemoryFile);
        if (messages.Count == 0)
            messages.Add(CodingAgent.SystemMessage(OperatingSystem.IsWindows()));

        var model = args.ModelOverride ?? LlmSettings.Model;
        Console.WriteLine($"  Model: {model}");
        if (args.IsDryRun)
            Console.WriteLine("  Dry-run: ON (no changes will be made)");
        Console.WriteLine();

        using var agent = new CodingAgent(
            apiKey,
            LlmSettings.Endpoint,
            model,
            new AgentOptions(Confirm: true, DryRun: args.IsDryRun),
            new ConsoleObserver());

        while (true)
        {
            Console.Write("\n> User (type 'exit' to quit): ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            messages.Add(JsonHelpers.Message("user", input));
            await agent.RunAsync(messages, args.MemoryFile);
        }
    }
}
