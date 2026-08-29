using System.Text.Json.Nodes;
using CsAgentUI.Shared;

namespace CsAgentUI.Presentation.DesktopPhotinoX;

public sealed class AgentSession : IDisposable
{
    private readonly object _gate = new();
    private readonly string _apiKey;
    private CodingAgent? _agent;
    private bool _disposed;

    public AgentSession(string id, AgentArguments args, string apiKey)
    {
        Id = id;
        Args = args;
        _apiKey = apiKey;
    }

    public string Id { get; }
    public AgentArguments Args { get; }
    public bool IsRunning { get; private set; }

    public async Task RunAsync(string prompt, IAgentObserver observer)
    {
        CodingAgent agent;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsRunning)
                throw new InvalidOperationException("A chat is already running in this session.");

            IsRunning = true;
            agent = new CodingAgent(
                _apiKey,
                LlmSettings.Endpoint,
                Args.ModelOverride ?? LlmSettings.Model,
                new AgentOptions(MaxSteps: 30, DryRun: Args.IsDryRun, Confirm: true),
                observer,
                Args.McpUrl);
            _agent = agent;
        }

        try
        {
            var messages = await MemoryStore.LoadAsync(Args.MemoryFile);
            if (messages.Count == 0)
                messages.Add(CodingAgent.SystemMessage(OperatingSystem.IsWindows()));

            messages.Add(JsonHelpers.Message("user", prompt));
            await agent.RunAsync(messages, Args.MemoryFile);
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_agent, agent))
                    _agent = null;
                IsRunning = false;
            }
            agent.Dispose();
        }
    }

    public void Cancel()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _agent?.Cancel();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _agent?.Cancel();
            _agent?.Dispose();
            _agent = null;
        }
    }
}
