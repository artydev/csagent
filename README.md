# CSAgent — Cross-Platform Autonomous Coding Agent

**CSAgent** is a zero-NuGet-dependency autonomous coding agent that runs on Windows, Linux, and macOS. It uses an OpenAI-compatible API (e.g., [Albert API](https://albert.api.etalab.gouv.fr)) to understand natural-language instructions and autonomously perform coding tasks by reading, writing, and listing files, as well as executing shell commands.

```
   ██████╗███████╗ █████╗  ██████╗ ███████╗███╗   ██╗████████╗
  ██╔════╝██╔════╝██╔══██╗██╔════╝ ██╔════╝████╗  ██║╚══██╔══╝
  ██║     ███████╗███████║██║  ███╗█████╗  ██╔██╗ ██║   ██║   
  ██║     ╚════██║██╔══██║██║   ██║██╔══╝  ██║╚██╗██║   ██║   
  ╚██████╗███████║██║  ██║╚██████╔╝███████╗██║ ╚████║   ██║   
   ╚═════╝╚══════╝╚═╝  ╚═╝ ╚═════╝ ╚══════╝╚═╝  ╚═══╝   ╚═╝  
  Cross-platform autonomous coding agent  |  zero NuGet deps
```

---

## Table of Contents

- [Quick Start](#quick-start)
- [Modes of Operation](#modes-of-operation)
- [Architecture: Presentation Layer is Swappable](#architecture-presentation-layer-is-swappable)
- [Command-Line Arguments](#command-line-arguments)
- [Environment Variables](#environment-variables)
- [LLM Models](#llm-models)
- [Available Tools](#available-tools)
- [Safety Features](#safety-features)
- [Memory & Conversation Persistence](#memory--conversation-persistence)
- [Building from Source](#building-from-source)
- [AOT Publishing](#aot-publishing)
- [Project Structure (for Developers)](#project-structure-for-developers)
- [How to Add a New Interface (Desktop, Slack, etc.)](#how-to-add-a-new-interface-desktop-slack-etc)
- [Troubleshooting](#troubleshooting)
- [License](#license)

---

## Quick Start

### Prerequisites

- .NET 10.0 SDK or later (for building from source)
- An API key for an OpenAI-compatible endpoint (e.g., [Albert API](https://albert.api.etalab.gouv.fr))

### Run with the Web UI

```bash
# Set your API key
export ALBERT_API_KEY=your-api-key-here   # Linux/macOS
set ALBERT_API_KEY=your-api-key-here      # Windows

# Run the web server
csagent --ui
```

Then open your browser to **http://localhost:5050** (or the port you specified with `--port`).

### Run in CLI Mode

```bash
export ALBERT_API_KEY=your-api-key-here   # Linux/macOS
set ALBERT_API_KEY=your-api-key-here      # Windows

csagent
```

---

## Modes of Operation

### CLI Mode (Default)

In CLI mode, CSAgent presents a text-based interactive session. You type instructions, and the agent autonomously works through them step by step.

```
> User: Create a new C# console project that prints "Hello, World!"
```

The agent will:
1. **Think** about the task and explain its plan
2. **Execute tools** (write files, run shell commands)
3. **Report results** of each action
4. **Continue** until the task is complete

Type `exit` to quit the session.

### Web UI Mode

In Web UI mode (`--ui` flag), CSAgent starts a local web server with a modern, dark-themed interface featuring:

- **Real-time streaming** of agent thoughts, tool calls, and results via Server-Sent Events (SSE)
- **Syntax highlighting** for code blocks (via Prism.js)
- **Responsive design** for desktop and mobile
- A clean, terminal-inspired aesthetic

The web UI is served at **http://localhost:5050** by default. Use `--port` to change:

```bash
csagent --ui --port 8080
```

---

## Architecture: Presentation Layer is Swappable

One of CSAgent's core design principles is **clean separation between the agent logic and the user interface**. The entire agent engine lives in `src/Core/` and has **zero knowledge** of how it is being presented — it doesn't import `Console`, `HttpContext`, or any UI framework.

### How it works

The agent communicates with the outside world exclusively through the **`IAgentObserver`** interface:

```csharp
public interface IAgentObserver
{
    Task OnStep(int n, int max);
    Task OnThought(string text);
    Task OnToolCall(string name, string args);
    Task OnToolResult(string result, bool isError);
    Task OnDone(string message);
    Task OnError(string message);
    Task OnWarning(string message);
    Task OnDanger(string message);
}
```

The `CodingAgent` calls these methods at each stage of its loop. **It doesn't care what implements them** — it could be a terminal, a web page, a desktop app, a Slack bot, or a CI/CD pipeline.

### Currently available interfaces

| Interface | File | Observer | Description |
|-----------|------|----------|-------------|
| **Terminal (TUI)** | `src/Interfaces/Tui/` | `ConsoleObserver` | Interactive CLI session with colored output |
| **Web** | `src/Interfaces/Web/` | `SseObserver` | Web server with SSE streaming and HTML/JS frontend |

Both interfaces use the **exact same `CodingAgent`** — the agent doesn't know or care which one is active.

### Diagram

```
┌─────────────────────────────────────────┐
│              User Input                  │
└──────────┬──────────────────┬────────────┘
           │                  │
     ┌─────▼─────┐      ┌────▼────┐
     │  TUI Host  │      │ Web Host│
     │ (Console)  │      │(ASP.NET)│
     └─────┬─────┘      └────┬────┘
           │                  │
     ┌─────▼──────────────────▼─────┐
     │       IAgentObserver          │
     │ (ConsoleObserver / SseObserver)│
     └─────────────────┬──────────────┘
                       │
          ┌────────────▼────────────┐
          │      CodingAgent        │
          │     (src/Core/)         │
          │  - LlmClient            │
          │  - ToolDispatcher       │
          │  - MemoryStore          │
          └─────────────────────────┘
```

### Why this matters

- **You can add a new interface without touching a single line of agent code.**
- The agent's behaviour is identical across all interfaces — same model, same tools, same safety rules.
- Testing is easier: you can write a `TestObserver` that captures events and assert on them.
- Future-proof: Desktop (Avalonia, Terminal.GUI), Slack bot, Discord bot, or CI mode — all just need a new observer.

---

## Command-Line Arguments

| Argument | Description |
|---|---|
| `--ui` | Start in Web UI mode (starts a web server) |
| `--port`, `-p <n>` | Web UI port number (default: 5050) |
| `--mem <file>` | Specify a custom memory/conversation file (default: `agent_memory.json`) |
| `--model <model>` | Override the default LLM model for the current mode |
| `--dry-run` | Simulate tool execution without making changes |
| `--version` | Display the current version of CSAgent and exit |
| `--doc` | Display full documentation in a formatted terminal view and exit |
| `--help`, `-h`, `/?` | Show this help message and exit |
| `<file>` | Positional argument: specify a memory file without `--mem` flag |

### Examples

```bash
# Web UI with custom port
csagent --ui --port 8080

# Web UI with custom memory file
csagent --ui --mem my_project_memory.json

# CLI mode with a specific memory file
csagent my_memory.json

# CLI mode with a different model
csagent --model gpt-4o

# Web UI mode with a different model
csagent --ui --model gpt-4o

# Dry run mode (simulate without making changes)
csagent --dry-run

# Display version
csagent --version

# Display full documentation in terminal
csagent --doc

# Show help message
csagent --help
```

---

## Environment Variables

| Variable | Required | Description |
|---|---|---|
| `ALBERT_API_KEY` | Yes | Your API key for the OpenAI-compatible endpoint |

---

## LLM Models

CSAgent uses a **single unified model** for both CLI and Web UI modes. The default model is `deepseek-v4-flash`, defined in `LlmSettings.cs`. This means both modes behave identically in terms of LLM behaviour.

You can override the model in either mode using the `--model` argument.

---

## Available Tools

The agent has access to four tools:

### `write_file`
Write (or overwrite) a text file. Parent directories are created automatically.

**Parameters:**
- `path` (string, required) — File path
- `content` (string, required) — UTF-8 content to write

### `read_file`
Read a text file and return its content.

**Parameters:**
- `path` (string, required) — File path

### `list_dir`
List files and subdirectories in a directory.

**Parameters:**
- `path` (string, optional, default: `.`) — Directory to list
- `recursive` (boolean, optional, default: `false`) — Whether to list recursively

### `sh`
Execute a shell command. Uses `cmd.exe` on Windows, `/bin/sh` elsewhere.

**Parameters:**
- `cmd` (string, required) — Shell command to run

---

## Safety Features

CSAgent includes multiple layers of safety to prevent accidental damage to your system:

### 1. Destructive Action Confirmation

The `write_file` tool is classified as **destructive** because it modifies files on disk. Before executing, the agent will prompt for confirmation:

```
[?] Allow destructive action 'write_file'? [Y/n]
```

### 2. Path Restriction

File operations (`write_file`, `read_file`, `list_dir`) are **restricted to the current working directory** and its subdirectories. Attempts to access files outside this scope are blocked.

### 3. Dangerous Command Filtering

Shell commands are scanned for potentially dangerous patterns before execution. The filter is **platform-aware** and blocks operations like formatting drives, registry manipulation, privilege escalation, and system shutdown.

### 4. Command Timeout

All shell commands have a **60-second timeout**. If a command takes longer, it is automatically killed.

### 5. File Size Limit

Reading files larger than **500 KB** is blocked to prevent memory issues.

### 6. Dry-Run Mode

The `--dry-run` flag simulates all tool executions without making any actual changes to the filesystem. Useful for testing and reviewing what the agent intends to do.

---

## Memory & Conversation Persistence

CSAgent saves the conversation history to a JSON file (default: `agent_memory.json`). This allows the agent to maintain context across sessions.

- The memory file is automatically loaded when the agent starts
- It is saved after each step
- Old messages are trimmed when the total content exceeds ~96 KB to keep context manageable
- You can specify a custom memory file with `--mem <file>` or as a positional argument

---

## Building from Source

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later

### Build

```bash
dotnet build
```

### Run (after build)

```bash
# CLI mode
export ALBERT_API_KEY=your-key   # Linux/macOS
set ALBERT_API_KEY=your-key      # Windows
csagent

# Web UI mode
csagent --ui
```

---

## AOT Publishing

CSAgent supports **Ahead-of-Time (AOT) compilation** for fast startup and single-file deployment:

```bash
# Publish as a single-file AOT binary
dotnet publish -c Release -r win-x64   # Windows
dotnet publish -c Release -r linux-x64 # Linux
dotnet publish -c Release -r osx-x64   # macOS
```

The AOT build produces a self-contained executable with no runtime dependencies.

---

## Project Structure (for Developers)

```
src/
├── Core/                              ← Domain logic, zero UI dependencies
│   ├── Abstractions/
│   │   └── IAgentObserver.cs          ← The only interface between agent and UI
│   ├── Agent/
│   │   ├── AgentOptions.cs            ← Configuration record (max steps, dry-run, confirm)
│   │   ├── CodingAgent.cs             ← Main agent loop (calls observer, dispatches tools)
│   │   └── ToolDispatcher.cs          ← Tool definitions, dispatch, safety checks
│   ├── Llm/
│   │   ├── LlmClient.cs               ← HTTP client for OpenAI-compatible API
│   │   └── LlmSettings.cs             ← Endpoint and model configuration
│   └── Memory/
│       └── MemoryStore.cs             ← JSON file persistence for conversation history
│
├── Interfaces/                         ← One folder per presentation layer
│   ├── Tui/                            ← Terminal UI
│   │   ├── TuiHost.cs                 ← CLI entry point (reads input, runs agent)
│   │   ├── ConsoleObserver.cs         ← IAgentObserver → Console.WriteLine
│   │   └── ConsoleRenderer.cs         ← Colored console output helpers
│   └── Web/                            ← Web UI
│       ├── WebHost.cs                 ← ASP.NET server entry point
│       ├── SseObserver.cs             ← IAgentObserver → SSE stream
│       ├── ApiEndpoints.cs            ← HTTP API routes
│       ├── StaticAssets.cs            ← Embedded HTML/JS/CSS loader
│       └── assets/                    ← Frontend files (index.html, app.js, styles.css)
│
├── Shared/                             ← Shared utilities (no UI dependency)
│   ├── ArgumentParser.cs              ← CLI argument parsing
│   ├── HelpDisplay.cs                 ← Renders --help output
│   ├── DocDisplay.cs                  ← Renders --doc output
│   └── JsonHelpers.cs                 ← AOT-safe JSON helpers (Message, ToolResult, PrettyJson, TrimHistory)
│
└── Program.cs                          ← Thin entry point (~20 lines)
```

### Key design principles

| Principle | How it's applied |
|-----------|-----------------|
| **KISS** | Each file has one clear responsibility. `Program.cs` is ~20 lines. |
| **DRY** | Tool definitions, safety checks, argument parsing, and JSON helpers are each defined once. |
| **Separation of concerns** | `Core/` has zero knowledge of `Interfaces/`. No `Console.WriteLine` or `HttpContext` in agent code. |
| **Observer pattern** | `IAgentObserver` is the only bridge. Add a new UI by implementing this interface. |
| **AOT-safe** | All JSON operations use `JsonValue.Create` and source generators — no trimming warnings. |

---

## How to Add a New Interface (Desktop, Slack, etc.)

Adding a new presentation layer requires **zero changes to `src/Core/`**. Here's the recipe:

### Step 1: Create your interface folder

```
src/Interfaces/Desktop/
├── DesktopHost.cs
├── DesktopObserver.cs
└── MainWindow.axaml
```

### Step 2: Implement `IAgentObserver`

```csharp
// src/Interfaces/Desktop/DesktopObserver.cs
public class DesktopObserver : IAgentObserver
{
    public Task OnStep(int n, int max) { /* update progress bar */ return Task.CompletedTask; }
    public Task OnThought(string text) { /* append to chat log */ return Task.CompletedTask; }
    public Task OnToolCall(string name, string args) { /* show tool call */ return Task.CompletedTask; }
    public Task OnToolResult(string result, bool isError) { /* show result */ return Task.CompletedTask; }
    public Task OnDone(string message) { /* show completion */ return Task.CompletedTask; }
    public Task OnError(string message) { /* show error */ return Task.CompletedTask; }
    public Task OnWarning(string message) { /* show warning */ return Task.CompletedTask; }
    public Task OnDanger(string message) { /* show danger */ return Task.CompletedTask; }
}
```

### Step 3: Create your host

```csharp
// src/Interfaces/Desktop/DesktopHost.cs
public static class DesktopHost
{
    public static async Task RunAsync(AgentArguments args)
    {
        var apiKey = Environment.GetEnvironmentVariable("ALBERT_API_KEY") ?? "";
        var messages = await MemoryStore.LoadAsync(args.MemoryFile);
        if (messages.Count == 0)
            messages.Add(CodingAgent.SystemMessage(OperatingSystem.IsWindows()));

        using var agent = new CodingAgent(
            apiKey,
            LlmSettings.Endpoint,
            args.ModelOverride ?? LlmSettings.Model,
            new AgentOptions(),
            new DesktopObserver());

        // Your desktop UI loop here
        // Call agent.RunAsync(messages, args.MemoryFile) when user sends a message
    }
}
```

### Step 4: Wire it up in `Program.cs`

```csharp
if (parsed.IsDesktopMode)  // add a --desktop flag in ArgumentParser
    DesktopHost.RunAsync(parsed);
else if (parsed.IsUiMode)
    WebHost.Run(parsed);
else
    await TuiHost.RunAsync(parsed);
```

**That's it.** The agent, tools, safety checks, memory persistence, and LLM client are all reused unchanged.

---

## Troubleshooting

| Problem | Solution |
|---|---|
| "API Key not set" | Ensure the `ALBERT_API_KEY` environment variable is set before running |
| "API 401: ..." | Your API key is invalid or expired. Check your credentials |
| "API 429: ..." | You've hit the rate limit. Wait a moment and try again |
| "command timed out (60s)" | The shell command took longer than 60 seconds. Try breaking the task into smaller steps |
| "file too large" | The file exceeds the 500 KB read limit. Use `sh` with tools like `grep`, `head`, or `find` to inspect specific parts |
| "Path is not allowed" | File operations are restricted to the current working directory. Change to the target directory before running the agent, or use shell commands to copy files into the workspace |
| Browser doesn't open automatically | Navigate manually to **http://localhost:5050** in your browser |

---

## License

This project is provided as-is. No external NuGet packages are required — everything is built with the .NET base class library.

---

*CSAgent — Zero dependencies, maximum autonomy. Swap the UI, keep the brain.*
