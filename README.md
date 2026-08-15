# CsAgentUI

A cross-platform autonomous coding agent built in C#/.NET 10. It connects to an
OpenAI-compatible LLM endpoint and can read files, search code, run shell
commands, and write files to complete coding tasks — all driven by an LLM loop.

## Features

- **Three interfaces** — terminal (CLI), web UI, and a native Windows window.
- **Autonomous agent loop** — the LLM plans and executes tool calls step by step.
- **Conversation memory** — history is persisted to a JSON file between runs.
- **Dry-run mode** — simulate tool execution without making any changes.
- **Safety** — destructive actions require confirmation; file operations are
  restricted to the current working directory.

## Requirements

- .NET 10 SDK (or a published single-file binary)
- An API key for an OpenAI-compatible endpoint

## Setup

Set your API key as an environment variable:

```
set ALBERT_API_KEY=your-key-here
```

## Usage

```
CsAgentUI [options] [memory-file]
```

### Command-line arguments (exact)

The parser (`src/Shared/ArgumentParser.cs`) recognizes the following arguments.
All flags are matched by exact string equality (`args.Contains(...)`), so they
must be spelled exactly as shown. Arguments are case-sensitive.

#### Modes (mutually exclusive flags)

| Flag         | Parsed field      | Description                                              |
|--------------|-------------------|----------------------------------------------------------|
| *(no flag)*  | —                 | CLI mode — interactive terminal session                  |
| `--ui`       | `IsUiMode`        | Web UI mode — starts a web server (default port 5050)    |
| `--native`   | `IsNativeMode`    | Native window mode — AOTrino WebView2 window (Windows)   |
| `--desktop`  | `IsDesktopMode`   | Alias for native window mode (also accepted)             |

#### Options

| Option              | Parsed field      | Description                                        |
|---------------------|-------------------|----------------------------------------------------|
| `--help`, `-h`, `/?`| `ShowHelp`        | Show help and exit                                 |
| `--version`         | `ShowVersion`     | Show version and exit                              |
| `--doc`             | `ShowDoc`         | Show full documentation in the terminal and exit   |
| `--mem <file>`      | `MemoryFile`      | Custom memory/conversation file (default: `agent_memory.json`) |
| `--model <name>`    | `ModelOverride`   | Override the LLM model (default: `LlmSettings.Model`) |
| `--port, -p <n>`    | `Port`            | Web UI port (default: `5050`)                      |
| `--dry-run`         | `IsDryRun`        | Simulate tool execution without making changes     |

#### Positional argument: `[memory-file]`

If no `--mem <file>` is given, the **first** argument that is not a recognized
flag and does not start with `-` is treated as the memory file. The recognized
non-flag tokens are `--ui`, `--native`, `--desktop`, and `--dry-run`; any other
argument starting with `-` is skipped. If none is found, the default
`agent_memory.json` is used.

#### Parsing rules (exact behaviour)

- **`--mem`** — takes the next argument as the file path. If `--mem` is the last
  argument (no value follows), it is ignored.
- **`--model`** — takes the next argument as the model name. If `--model` is the
  last argument (no value follows), it is ignored.
- **`--port` / `-p`** — takes the next argument and parses it as an integer.
  The value is accepted only if `0 < port < 65536`; otherwise the default `5050`
  is used.
- **`--help` / `-h` / `/?`** — any of these sets `ShowHelp`.
- **`--version`** — sets `ShowVersion`.
- **`--doc`** — sets `ShowDoc`.
- **`--dry-run`** — sets `IsDryRun`.
- **`--ui` / `--native` / `--desktop`** — set their respective mode flags.

### Examples

```
csagent                                    CLI mode
csagent --ui                               Web UI mode (port 5050)
csagent --native                           Native window mode
csagent --desktop                          Native window mode (alias)
csagent --ui --port 8080                   Web UI on port 8080
csagent --model gpt-4o                     CLI with a custom model
csagent --ui --model gpt-4o                Web UI with custom model
csagent --native --model gpt-4o            Native window with custom model
csagent --mem my_history.json              CLI with a custom memory file
csagent --ui --mem my_history.json         Web UI with custom memory file
csagent --dry-run                          Dry-run mode (no changes)
csagent --doc                              Show documentation
csagent --version                          Show version
csagent --help                             Show help
```

## Available Tools

The agent can call the following tools to complete tasks:

```
CsAgentUI
├── File operations
│   ├── write_file      Write/overwrite a text file
│   ├── read_file       Read a text file
│   ├── read_json       Read a JSON file (with dot-path query)
│   ├── edit_file       Find-and-replace edits (atomic)
│   ├── copy_file       Copy a file
│   ├── move_file       Move/rename a file  ⚠ destructive
│   ├── delete_file     Delete a file       ⚠ destructive
│   ├── zip             Create a zip archive
│   └── unzip           Extract a zip archive ⚠ destructive
├── Inspection & search
│   ├── list_dir        List files/subdirectories
│   ├── tree            Visual directory tree
│   ├── search_files    Recursive grep search
│   └── parse_output    Parse output into structured JSON
├── Git
│   ├── git_status      Working tree status
│   ├── git_diff        Uncommitted changes
│   ├── git_log         Commit history
│   ├── git_branch      Current/local branches
│   └── git_commit      Stage & commit  ⚠ destructive
├── Shell & network
│   ├── sh              Run a shell command
│   ├── run_terminal    Persistent shell session
│   ├── close_terminal  Close a shell session
│   ├── http_request    Make an HTTP request
│   ├── web_search      Search the web
│   └── fetch_url       Fetch a webpage's text
└── Model
    └── switch_model    Switch the active LLM model
```

### File operations

| Tool | Description |
|------|-------------|
| `write_file` | Write (or overwrite) a text file; creates parent directories |
| `read_file` | Read a text file and return its content |
| `read_json` | Read a JSON file (pretty-printed), optionally extracting a sub-value via a dot-path query |
| `edit_file` | Apply precise find-and-replace edits to a file (atomic) |
| `copy_file` | Copy a file from source to destination |
| `move_file` | Move (rename) a file *(destructive)* |
| `delete_file` | Permanently delete a file *(destructive)* |
| `zip` | Create a zip archive from a file or directory |
| `unzip` | Extract a zip archive into a directory *(destructive)* |

### Inspection & search

| Tool | Description |
|------|-------------|
| `list_dir` | List files and subdirectories (optionally recursive) |
| `tree` | Display a visual, indented directory tree |
| `search_files` | Recursively grep for a text pattern, returning file paths and line numbers |
| `parse_output` | Parse command output into structured JSON (json / keyvalue / csv / auto) |

### Git

| Tool | Description |
|------|-------------|
| `git_status` | Show working tree status |
| `git_diff` | Show uncommitted changes (optionally staged) |
| `git_log` | Show recent commit history |
| `git_branch` | Show current branch and local branches |
| `git_commit` | Stage all changes and create a commit *(destructive)* |

### Shell & network

| Tool | Description |
|------|-------------|
| `sh` | Execute a shell command (cmd.exe on Windows, /bin/sh elsewhere) |
| `run_terminal` | Run a command in a persistent, stateful shell session |
| `close_terminal` | Close and terminate a persistent shell session |
| `http_request` | Make an HTTP request and return status, headers, and body |
| `web_search` | Search the web for docs, errors, or solutions |
| `fetch_url` | Retrieve a webpage's readable text content |

### Model

| Tool | Description |
|------|-------------|
| `switch_model` | Switch the active LLM model for the current session |

## Notes

- All destructive actions (e.g. `write_file`, `edit_file`, `git_commit`, `move_file`, `delete_file`, `unzip`) require user confirmation.
- File operations are restricted to the current working directory.
- Shell commands are filtered for potentially dangerous operations.
