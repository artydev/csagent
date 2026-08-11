namespace CsAgentUI.src.Presentation.Desktop
    
{
    [System.Runtime.InteropServices.Marshalling.GeneratedComClass]
    public partial class DesktopAPI : DispatchObject
    {
        private readonly WebViewWindow _window;

        public DesktopAPI(WebViewWindow window)
        {
            _window = window;
        }

        public string? MachineName => Environment.MachineName;

        public string UserName => Environment.UserName;


        public string? ExePath => Environment.ProcessPath?.Substring(0, Math.Min(Environment.ProcessPath.Length, 100));


    }
}