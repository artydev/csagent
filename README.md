# CSAgent — Cross-Platform Autonomous Coding Agent

**CSAgent** is a cross-platform autonomous coding agent that runs on Windows, Linux, and macOS. It uses an OpenAI-compatible API (e.g., [Albert API](https://albert.api.etalab.gouv.fr)) to understand natural-language instructions and autonomously perform coding tasks by reading, writing, and listing files, as well as executing shell commands.

It ships with three presentation modes — a terminal UI (TUI), a web UI, and a native window — and can optionally connect to external **MCP (Model Context Protocol)** servers to extend its toolset.

---

## Table of Contents

- [Quick Start](#quick-start)
- [Modes of Operation](#modes-of-operation)
  - [CLI Mode (Default)](#cli-mode-default)
  - [Web UI Mode](#web-ui-mode)
  - [Native Window Mode](#native-window-mode)
- [LLM Models](#llm-models)
- [MCP Integration](#mcp-integration)
- [Environment Variables](#environment-variables)
- [Command-Line Arguments](#command-line-arguments)
- [Safety Features](#safety-features)
- [Available Tools](#available-tools)
- [Memory & Conversation Persistence](#memory--conversation-persistence)
- [Building from Source](#building-from-source)
- [AOT Publishing](#aot-publishing)
- [Troubleshooting](#troubleshooting)

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

Then open your browser to **http://localhost:5050** (or the port you chose with `--port`).

### Run in CLI Mode

```bash
set ALBERT_API_KEY=your-api-key-here
dotnet run
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

The web UI is served at **http://localhost:5050** by default. Use `--port <n>` (or `-p <n>`) to change the port.

### Native Window Mode

In Native window mode (`--native` flag), CSAgent opens a dedicated desktop window (AOTrino WebView2) instead of a terminal or browser tab. This mode is **Windows-only**.

```bash
csagent --native
```

---

## LLM Models

CSAgent uses different LLM models depending on the mode of operation. This is intentional — each model is chosen for its strengths in the specific context.

| Mode | Default Model | Rationale |
|---|---|---|
| **CLI** | `deepseek-v4-flash` | Fast, lightweight, ideal for interactive terminal sessions where quick turnarounds matter |
| **Web UI** | `Qwen/Qwen3-Coder-30B-A3B-Instruct` | More capable for complex multi-step coding tasks; the Web UI is designed for longer, more involved sessions |

You can override the default model in any mode using the `--model` argument (see [Command-Line Arguments](#command-line-arguments)).

### Examples

```bash
# CLI mode with a different model
csagent --model gpt-4o

# Web UI mode with a different model
csagent --ui --model deepseek-v4-flash

# Native window mode with a different model
csagent --native --model gpt-4o
```

---

## MCP Integration

CSAgent can connect to external **Model Context Protocol (MCP)** servers over Streamable HTTP. MCP tools discovered on the server are exposed to the LLM alongside the built-in tools, letting you extend the agent's capabilities without changing its code.

### Connecting to an MCP server

Pass the endpoint URL via the `--mcp` (or `--mcp-url`) argument, or set the `CSAGENT_MCP_URL` environment variable:

```bash
# Via command line
csagent --ui --mcp https://example.com/mcp

# Via environment variable
set CSAGENT_MCP_URL=https://example.com/mcp
csagent --ui
```

### Supported MCP operations

The built-in MCP client supports the core Streamable HTTP flow:

- `initialize` — protocol negotiation (protocol version `2025-06-18`)
- `notifications/initialized` — session handshake
- `tools/list` — discover available tools
- `tools/call` — invoke a tool with JSON arguments

MCP tool definitions are converted to OpenAI-style function definitions and offered to the LLM alongside the built-in tools. When the agent calls an MCP tool, the request is forwarded to the server and the text result is returned.

---

## Environment Variables

| Variable | Required | Description |
|---|---|---|
| `ALBERT_API_KEY` | Yes | Your API key for the OpenAI-compatible endpoint |
| `CSAGENT_MCP_URL` | No | Default MCP server endpoint (overridden by `--mcp`/`--mcp-url`) |

---

## Command-Line Arguments

| Argument | Description |
|---|---|
| `--ui` | Start in Web UI mode (starts a web server) |
| `--native` | Start in Native window mode (AOTrino WebView2, Windows only) |
| `--mem <file>` | Specify a custom memory/conversation file (default: `agent_memory.json`) |
| `--model <model>` | Override the default LLM model for the current mode |
| `--mcp`, `--mcp-url <url>` | Connect to an MCP server at the given endpoint |
| `--port`, `-p <n>` | Web UI port number (default: `5050`) |
| `--dry-run` | Simulate tool execution without making changes |
| `--help`, `-h`, `/?` | Display help and exit |
| `--version` | Display the current version of CSAgent and exit |
| `--doc` | Display this documentation in a nicely formatted terminal view and exit |
| `<file>` | Positional argument: specify a memory file without `--mem` flag |

### Examples

```bash
# Web UI with custom memory file
csagent --ui --mem my_project_memory.json

# Web UI on a custom port
csagent --ui --port 8080

# CLI mode with a specific memory file
dotnet run my_memory.json

# Dry run mode
csagent --dry-run

# Display version
csagent --version

# Display documentation in terminal
csagent --doc

# Override the LLM model in CLI mode
csagent --model gpt-4o-mini

# Override the LLM model in Web UI mode
csagent --ui --model deepseek-v4-flash

# Connect to an MCP server
csagent --ui --mcp https://example.com/mcp
```

---

## Safety Features

CSAgent includes multiple layers of safety to prevent accidental damage to your system:

### 1. Destructive Action Confirmation

The `write_file` tool is classified as **destructive** because it modifies files on disk. Before executing, the agent will prompt for confirmation:

```
[?] Allow destructive action 'write_file'? [Y/n]
```

Shell commands (`sh`) are **not** classified as destructive by default, but they are still filtered for dangerous operations (see below).

### 2. Path Restriction

File operations (`write_file`, `read_file`, `list_dir`) are **restricted to the current working directory** and its subdirectories. Attempts to access files outside this scope are blocked:

```
Error: write_file - Path 'C:\Windows\System32\config' is not allowed for writing.
```

### 3. Dangerous Command Filtering

Shell commands are scanned for potentially dangerous patterns before execution. The filter is **platform-aware**:

#### Windows (cmd.exe)
Blocked patterns include:
- `format` — Format drives
- `del /f` / `del /s` — Force/recursive deletion
- `rd /s` / `rmdir /s` — Recursive directory removal
- `reg delete` / `reg add` / `reg import` — Registry manipulation
- `net user` / `net localgroup` / `net share` — System administration
- `takeown` / `icacls` / `cacls` — Permission/ownership changes
- `bcdedit` / `diskpart` — Boot/disk configuration
- `runas` / `powershell start-process -verb runas` — Privilege escalation
- `shutdown` / `reboot` — System control
- `\windows\system32\` / `\windows\system\` — System directory access
- `\program files\` — Protected directory access

#### Unix/Linux/macOS (bash/sh)
Blocked patterns include:
- `sudo` — Privilege escalation
- `chmod` — Permission changes
- `shutdown` / `reboot` — System control
- `dd` — Low-level disk operations
- `mkfs` — File system creation
- `/etc/` / `/usr/bin/` / `/bin/` — System directory access

### 4. Command Timeout

All shell commands have a **60-second timeout**. If a command takes longer, it is automatically killed:

```
Error: command timed out (60s).
```

### 5. File Size Limit

Reading files larger than **500 KB** is blocked to prevent memory issues:

```
Error: file too large (1024 KB). Use sh to grep/head.
```

---

## Available Tools

The agent has access to four built-in tools, plus any tools exposed by a connected MCP server:

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

### MCP tools
When connected to an MCP server, its `tools/list` results are exposed to the LLM as additional callable tools. Invocations are forwarded to the server via `tools/call`.

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

### Run

```bash
# CLI mode
set ALBERT_API_KEY=your-key
csagent

# Web UI mode
set ALBERT_API_KEY=your-key
csagent --ui

# Native window mode (Windows)
set ALBERT_API_KEY=your-key
csagent --native
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

### "API Key not set"
Ensure the `ALBERT_API_KEY` environment variable is set before running.

### "API 401: ..."
Your API key is invalid or expired. Check your credentials.

### "API 429: ..."
You've hit the rate limit. Wait a moment and try again.

### "command timed out (60s)"
The shell command took longer than 60 seconds. Try breaking the task into smaller steps.

### "file too large"
The file exceeds the 500 KB read limit. Use `sh` with tools like `grep`, `head`, or `find` to inspect specific parts.

### "Path is not allowed"
File operations are restricted to the current working directory. Change to the target directory before running the agent, or use shell commands to copy files into the workspace.

### "MCP server returned no tools list"
The MCP server did not respond to `tools/list`. Verify the endpoint URL is correct and reachable, and that it implements the Streamable HTTP transport.

### Browser doesn't open automatically
Navigate manually to **http://localhost:5050** in your browser (or the port you chose with `--port`).

---

## License

This project is provided as-is. It is built on the .NET base class library with a single NuGet dependency (`ModelContextProtocol`) used for MCP integration.

---

*CSAgent — Maximum autonomy, minimal dependencies.*
