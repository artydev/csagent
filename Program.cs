using System.Text;
using CsAgentUI.Infrastructure.Clipboard;
using CsAgentUI.Presentation.LeanUI;
using CsAgentUI.Presentation.Tui;
using CsAgentUI.Presentation.Web;
using CsAgentUI.Shared;

namespace CsAgentUI;

public static class Program
{
    public const string Version = "0.5.4";

    [STAThread]
    public static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; }
        catch { }

        var parsed = ArgumentParser.Parse(args);

        if (parsed.ShowHelp)    { HelpDisplay.Show(Version); return 0; }
        if (parsed.ShowVersion) { Console.WriteLine($"CSAgent version {Version}"); return 0; }
        if (parsed.ShowDoc)     { DocDisplay.Show(); return 0; }

        using var clipboard = new WindowsClipboardMonitor();
        clipboard.Start();

        if (parsed.IsLeanUiMode)
            LeanUIHost.Run(parsed, clipboard);
        else if (parsed.IsUiMode)
            WebHost.Run(parsed, clipboard);
        else
            TuiHost.RunAsync(parsed, clipboard).GetAwaiter().GetResult();

        return 0;
    }
}
