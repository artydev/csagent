using CsAgentUI.Presentation.Tui;
using CsAgentUI.Presentation.Web;
using CsAgentUI.Shared;

namespace CsAgentUI;

public static class Program
{
    public const string Version = "0.5";

    [STAThread]
    public static int Main(string[] args)
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
            // Web UI mode - ASP.NET server with SSE
            WebHost.Run(parsed);
        }
        else
        {
            // Default: Terminal UI mode
            TuiHost.RunAsync(parsed).GetAwaiter().GetResult();
        }

        return 0;
    }
}
