using CsAgentUI.Presentation.DesktopPhotinoX;
using CsAgentUI.Presentation.Tui;
using CsAgentUI.Presentation.Web;
using CsAgentUI.Shared;

namespace CsAgentUI;

public static class Program
{
    public const string Version = "0.3.0";

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

        if (parsed.IsDesktopXMode)
        {
            // DesktopX window mode - PhotinoX native window (cross-platform)
            PhotinoXHost.Run(parsed);
            return 0;
        }
        else if (parsed.IsUiMode)
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