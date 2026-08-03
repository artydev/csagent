# CSAgent — Cross-Platform Autonomous Coding Agent

**CSAgent** is a zero-NuGet-dependency autonomous coding agent that runs on Windows, Linux, and macOS. It uses an OpenAI-compatible API (e.g., [Albert API](https://albert.api.etalab.gouv.fr)) to understand natural-language instructions and autonomously perform coding tasks by reading, writing, and listing files, as well as executing shell commands.

---

## Quick Start

### Prerequisites

- .NET 10.0 SDK or later (for building from source)
- An API key for an OpenAI-compatible endpoint (e.g., [Albert API](https://albert.api.etalab.gouv.fr))

### Run with the Web UI

```bash
# Set your API key
set ALBERT_API_KEY=your-api-key-here

# Run the web server
csagent --ui
```

Then open your browser to **http://localhost:5050**.

### Run in CLI Mode

```bash
set ALBERT_API_KEY=your-api-key-here
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
1. Think about the task
2. Execute tools (write files, run shell commands)
3. Report results
4. Continue until the task is complete

Type `exit` to quit the session.

### Web UI Mode

In Web UI mode (`--ui` flag), CSAgent starts a local web server with a modern, dark-themed interface featuring:

- Real-time streaming of agent thoughts, tool calls, and results via Server-Sent Events (SSE)
- Syntax highlighting for code blocks (via Prism.js)
- Responsive design for desktop and mobile
- A clean, terminal-inspired aesthetic

The web UI is served at **http://localhost:5050**.

---

## LLM Models

CSAgent uses a **single unified model** for both CLI and Web UI modes. The default model is `deepseek-v4-flash`, defined in `LlmSettings.cs`. This means both modes behave identically in terms of LLM behaviour.

You can override the model in either mode using the `--model` argument.

### Examples

```bash
# CLI mode with a different model
csagent --model gpt-4o

# Web UI mode with a different model
csagent --ui --model gpt-4o
```

---

## Environment Variables

| Variable | Required | Description |
|---|---|---|
| `ALBERT_API_KEY` | Yes | Your API key for the OpenAI-compatible endpoint |

---

## Command-Line Arguments

| Argument | Description |
|---|---|
| `--ui` | Start in Web UI mode (starts a web server at http://localhost:5050) |
| `--mem <file>` | Specify a custom memory/conversation file (default: `agent_memory.json`) |
| `--model <model>` | Override the default LLM model for the current mode |
| `--dry-run` | Simulate tool execution without making changes |
| `--version` | Display the current version of CSAgent and exit |
| `--doc` | Display full documentation in a formatted terminal view and exit |
| `--help`, `-h`, `/?` | Show this help message and exit |
| `<file>` | Positional argument: specify a memory file without `--mem` flag |

### Examples

```bash
# Web UI with custom memory file
csagent --ui --mem my_project_memory.json

# CLI mode with a specific memory file
csagent my_memory.json

# Dry run mode (simulate without making changes)
csagent --dry-run

# Display version
csagent --version

# Display full documentation in terminal
csagent --doc

# Show help message
csagent --help

# Override the LLM model in CLI mode
csagent --model gpt-4o-mini

# Override the LLM model in Web UI mode
csagent --ui --model gpt-4o-mini
```

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
set ALBERT_API_KEY=your-key
csagent

# Web UI mode
set ALBERT_API_KEY=your-key
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

*CSAgent — Zero dependencies, maximum autonomy.*
