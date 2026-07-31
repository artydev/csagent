namespace CsAgentUI;

public sealed record AgentOptions(
    int MaxSteps = 30,
    bool DryRun = false,
    bool Confirm = true);
