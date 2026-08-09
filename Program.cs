using System.Runtime.InteropServices;
using CsAgentUI.Presentation.Desktop;
using CsAgentUI.Presentation.Tui;
using CsAgentUI.Presentation.Web;
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

        if (parsed.IsDesktopMode)
        {
            // Desktop window mode — AOTrino native window (Windows only)
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("Error: --desktop mode is only supported on Windows.");
                return 1;
            }

            // AOTrino requires STA thread — run on a new STA thread
            int exitCode = 0;
            var thread = new Thread(() =>
            {
                DesktopHost.Run(parsed);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return exitCode;
        }
        else if (parsed.IsUiMode)
        {
            // Web UI mode — ASP.NET server with SSE
            WebHost.Run(parsed);
        }
        else
        {
            // Default: Terminal UI mode
            await TuiHost.RunAsync(parsed);
        }

        return 0;
    }
}
