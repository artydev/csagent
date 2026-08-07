using CsAgentUI.Interfaces.Tui;
using CsAgentUI.Interfaces.Web;
using CsAgentUI.Shared;

namespace CsAgentUI;

public static class Program
{
    public const string Version = "0.3.0";

    public static async Task<int> Main(string[] args)
    {
        var parsed = ArgumentParser.Parse(args);

        if (parsed.ShowHelp)
        {
            HelpDisplay.Show(Version);
            return 0;
        }

        if (parsed.ShowVersion)
        {
            Console.WriteLine($"CSAgent version {Version}");
            return 0;
        }

        if (parsed.ShowDoc)
        {
            DocDisplay.Show();
            return 0;
        }

        if (parsed.IsUiMode)
        {
            WebHost.Run(parsed);
        }
        else
        {
            await TuiHost.RunAsync(parsed);
        }

        return 0;
    }
}
