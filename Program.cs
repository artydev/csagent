using System.Text;
using CsAgentUI.Presentation.LeanUI;
using CsAgentUI.Presentation.Tui;
using CsAgentUI.Presentation.Web;
using CsAgentUI.Shared;

namespace CsAgentUI;

public static class Program
{
    public const string Version = "0.5.1";

    [STAThread]
    public static int Main(string[] args)
    {
        // Ensure the console can render Unicode visual characters (✓, ⚠, →, box
        // drawing, etc.). On Windows the default legacy code page (CP437/CP850)
        // cannot display these; UTF-8 is required. Safe no-op on Unix terminals.
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch
        {
            // Some hosts (e.g. redirected output) may reject this; fall back to
            // the default encoding rather than crashing.
        }

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

        if (parsed.IsLeanUiMode)
        {
            // Lean UI mode - lightweight duplicate of the Web UI
            LeanUIHost.Run(parsed);
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
