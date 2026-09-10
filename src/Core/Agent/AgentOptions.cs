using CsAgentUI.Core.Tasks;

namespace CsAgentUI;

public sealed record AgentOptions(
    int MaxSteps = 30,
    bool DryRun = false,
    bool Confirm = true,
    RetryPolicy? Retry = null,
    string? ResumeTaskId = null,
    TaskTracker? Tracker = null);