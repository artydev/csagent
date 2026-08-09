namespace CsAgentUI.src.Presentation.Desktop
{
    
    
    
    public class DesktopObserver : IAgentObserver
    {
        Task IAgentObserver.OnDanger(string message)
        {
            throw new NotImplementedException();
        }

        Task IAgentObserver.OnDone(string message)
        {
            throw new NotImplementedException();
        }

        Task IAgentObserver.OnError(string message)
        {
            throw new NotImplementedException();
        }

        Task IAgentObserver.OnStep(int n, int max)
        {
            throw new NotImplementedException();
        }

        Task IAgentObserver.OnThought(string text)
        {
            throw new NotImplementedException();
        }

        Task IAgentObserver.OnToolCall(string name, string args)
        {
            throw new NotImplementedException();
        }

        Task IAgentObserver.OnToolResult(string result, bool isError)
        {
            throw new NotImplementedException();
        }

        Task IAgentObserver.OnWarning(string message)
        {
            throw new NotImplementedException();
        }
    }
}
