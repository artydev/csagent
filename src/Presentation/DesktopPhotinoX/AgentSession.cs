using System.Text.Json.Nodes;
using CsAgentUI.Shared;

namespace CsAgentUI.Presentation.DesktopPhotinoX;

public sealed class AgentSession : IDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cancellation = new();
    private CodingAgent? _agent;
    private bool _disposed;

    public AgentSession(string id, AgentArguments args, string apiKey, IAgentObserver observer)
    {
        Id = id;
        Args = args;
        _agent = new CodingAgent(
            apiKey,
            LlmSettings.Endpoint,
            args.ModelOverride ?? LlmSettings.Model,
            new AgentOptions(MaxSteps: 30, DryRun: args.IsDryRun, Confirm: true),
            observer,
            args.McpUrl);
    }

    public string Id { get; }
    public AgentArguments Args { get; }

    public bool IsRunning { get; private set; }

    public async Task RunAsync(string prompt)
    {
        CodingAgent agent;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsRunning)
                throw new InvalidOperationException("A chat is already running in this session.");
            IsRunning = true;
            agent = _agent ?? throw new ObjectDisposedException(nameof(AgentSession));
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
                IsRunning = false;
        }
    }

    public void Cancel()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _cancellation.Cancel();
            _agent?.Cancel();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _cancellation.Cancel();
            _agent?.Dispose();
            _agent = null;
            _cancellation.Dispose();
        }
    }
}
