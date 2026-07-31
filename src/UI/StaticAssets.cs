namespace CsAgentUI;

public static class StaticAssets
{
    public const string HtmlUI = """
    <!DOCTYPE html>
    <html lang="en">
    <head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>CSAgent Console</title>

    <!-- Prism.js for syntax highlighting -->
    <link href="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/themes/prism.min.css" rel="stylesheet" />
    <link href="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/plugins/toolbar/prism-toolbar.min.css" rel="stylesheet" />

    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap" rel="stylesheet">

    <style>
    :root{
        --bg:#070b14;
        --panel:rgba(18,25,38,.85);
        --border:rgba(255,255,255,.10);
        --primary:#4da3ff;
        --primary-glow:rgba(77,163,255,.35);
        --text:#f8fafc;
        --muted:#94a3b8;
        --thought:#fbbf24;
        --call:#38bdf8;
        --result:#4ade80;
        --done:#22c55e;
        --warning:#f59e0b;
        --danger:#ef4444;
    }

    *{
        box-sizing:border-box;
    }

    body{
        margin:0;
        height:100vh;
        display:flex;
        align-items:center;
        justify-content:center;
        padding:25px;
        background:radial-gradient(circle at top,#172033,#070b14 65%);
        color:var(--text);
        font-family:Inter,system-ui,sans-serif;
    }

    .container{
        width:100%;
        max-width:1280px;
        height:90vh;
        display:flex;
        flex-direction:column;
        background:linear-gradient(145deg,rgba(255,255,255,.06),rgba(255,255,255,.01)),var(--panel);
        backdrop-filter:blur(20px);
        border:1px solid var(--border);
        border-radius:22px;
        overflow:hidden;
        box-shadow:0 35px 90px rgba(0,0,0,.65);
    }

    header{
        height:75px;
        padding:0 30px;
        display:flex;
        align-items:center;
        justify-content:space-between;
        border-bottom:1px solid var(--border);
        background:rgba(255,255,255,.03);
    }

    .brand{
        display:flex;
        align-items:center;
        gap:14px;
    }

    h2{
        margin:0;
        display:flex;
        align-items:center;
        gap:10px;
        font-family:"JetBrains Mono",monospace;
        font-size:15px;
        letter-spacing:1px;
    }

    .version{
        color:var(--muted);
        font-size:12px;
    }

    .status{
        display:flex;
        align-items:center;
        gap:8px;
        padding:7px 14px;
        border-radius:20px;
        font-family:"JetBrains Mono",monospace;
        font-size:12px;
        color:#86efac;
        background:rgba(34,197,94,.1);
        border:1px solid rgba(34,197,94,.25);
    }

    .status-dot{
        width:9px;
        height:9px;
        background:var(--done);
        border-radius:50%;
        box-shadow:0 0 15px var(--done);
        animation:pulse 2s infinite;
    }

    @keyframes pulse{
        50%{opacity:.35;}
    }

    #log{
        flex:1;
        overflow-y:auto;
        padding:25px;
        font-family:"JetBrains Mono",monospace;
        font-size:14px;
    }

    #log div{
        margin-bottom:15px;
        padding:14px 18px;
        border-radius:12px;
        line-height:1.7;
        white-space:pre-wrap;
        animation:appear .25s ease;
    }

    @keyframes appear{
        from{
            opacity:0;
            transform:translateY(8px);
        }
        to{
            opacity:1;
            transform:translateY(0);
        }
    }

    .user-msg{
        background:linear-gradient(135deg,rgba(77,163,255,.18),rgba(77,163,255,.05));
        border-left:4px solid var(--primary);
        color:#dbeafe;
    }

    .thought{
        background:rgba(251,191,36,.08);
        border-left:4px solid var(--thought);
        color:var(--thought);
    }

    .call{
        background:rgba(56,189,248,.08);
        border-left:4px solid var(--call);
        color:var(--call);
    }

    .result{
        background:rgba(74,222,128,.08);
        border-left:4px solid var(--result);
        color:var(--result);
    }

    .warning{
        background:rgba(245,158,11,.08);
        border-left:4px solid var(--warning);
        color:var(--warning);
    }

    .danger{
        background:rgba(239,68,68,.08);
        border-left:4px solid var(--danger);
        color:var(--danger);
    }

    .done{
        text-align:center;
        color:var(--done);
        font-weight:600;
    }

    .input-area{
        padding:20px 25px;
        background:rgba(0,0,0,.25);
        border-top:1px solid var(--border);
    }

    input{
        width:100%;
        padding:16px 20px;
        background:rgba(15,23,42,.9);
        border:1px solid rgba(255,255,255,.12);
        border-radius:14px;
        color:white;
        font-family:"JetBrains Mono",monospace;
        font-size:15px;
        outline:none;
        transition:.25s;
    }

    input::placeholder{
        color:#64748b;
    }

    input:focus{
        border-color:var(--primary);
        box-shadow:0 0 0 4px var(--primary-glow);
    }

    .result pre {
        background: rgba(0, 0, 0, 0.25);
        border-radius: 8px;
        padding: 12px 16px;
        overflow-x: auto;
        margin: 10px 0;
        font-size: 13px !important;
    }

    .result code[class*="language-"] {
        background: transparent !important;
        font-size: 13px !important;
    }

    #log::-webkit-scrollbar{
        width:8px;
    }

    #log::-webkit-scrollbar-thumb{
        background:#334155;
        border-radius:20px;
    }

    @media(max-width:700px){
        body{
            padding:10px;
        }

        .container{
            height:95vh;
            border-radius:15px;
        }

        header{
            padding:0 15px;
        }

        .status{
            display:none;
        }
    }
    </style>
    </head>

    <body>

    <div class="container">

    <header>
    <div class="brand">
    <h2>
    <span class="status-dot"></span>
    CSAgent
    <span class="version">DUAL v1.0</span>
    </h2>
    </div>

    <div class="status">
    <span class="status-dot"></span>
    SYSTEM READY
    </div>
    </header>

    <div id="log"></div>

    <div class="input-area">
    <input
    id="in"
    autocomplete="off"
    placeholder="Ask CSAgent anything... (All destructive actions require approval)"
    onkeypress="if(event.key==='Enter'){run();}">
    </div>

    </div>

    <script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/components/prism-core.min.js"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/plugins/autoloader/prism-autoloader.min.js"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/plugins/toolbar/prism-toolbar.min.js"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/plugins/copy-to-clipboard/prism-copy-to-clipboard.min.js"></script>

    <script>
    document.addEventListener('DOMContentLoaded', function() {
        if (typeof Prism !== 'undefined') {
            Prism.plugins.autoloader.languages_path = 'https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/components/';
        }
    });

    function run(){
        const input=document.getElementById("in");
        const prompt=input.value.trim();
        if(!prompt)return;

        const log=document.getElementById("log");
        const user=document.createElement("div");
        user.className="user-msg";
        user.innerHTML=`<strong>> User:</strong> ${prompt}`;
        log.appendChild(user);
        input.value="";

        const stream=new EventSource(
            `/api/chat?prompt=${encodeURIComponent(prompt)}`
        );

        stream.onmessage=function(event){
            const message=JSON.parse(event.data);
            const div=document.createElement("div");

            if(message.type==="done"){
                div.className="done";
                div.innerText="✓ Task completed successfully";
                stream.close();
            }else if(message.type==="warning"){
                div.className="warning";
                div.innerText="⚠ " + message.data;
            }else if(message.type==="danger"){
                div.className="danger";
                div.innerText="✗ " + message.data;
            }else{
                div.className=message.type;
                const content=typeof message.data==="string"
                    ?message.data
                    :JSON.stringify(message.data,null,2);

                if(message.type === "result") {
                    const preElement = document.createElement('pre');
                    const codeElement = document.createElement('code');
                    codeElement.className = 'language-javascript';
                    codeElement.textContent = content;
                    preElement.appendChild(codeElement);
                    div.appendChild(preElement);
                } else {
                    div.innerText=`[${message.type}] ${content}`;
                }
                log.appendChild(div);

                if(message.type === "result") {
                    setTimeout(function() {
                        try {
                            if (typeof Prism !== 'undefined' && Prism.highlightAllUnder) {
                                Prism.highlightAllUnder(div);
                            }
                        } catch(e) { console.error('Highlighting error:', e); }
                    }, 10);
                }
            }
        };

        stream.onerror=function(){
            stream.close();
        };
    }
    </script>

    </body>
    </html>
    """;

    public const string ReadmeMd = """
# CSAgent — Cross-Platform Autonomous Coding Agent

**CSAgent** is a zero-NuGet-dependency autonomous coding agent that runs on Windows, Linux, and macOS. It uses an OpenAI-compatible API (e.g., [Albert API](https://albert.api.etalab.gouv.fr)) to understand natural-language instructions and autonomously perform coding tasks by reading, writing, and listing files, as well as executing shell commands.

---

## Table of Contents

- [Quick Start](#quick-start)
- [Modes of Operation](#modes-of-operation)
  - [CLI Mode (Default)](#cli-mode-default)
  - [Web UI Mode](#web-ui-mode)
- [LLM Models](#llm-models)
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
dotnet run -- --ui
```

Then open your browser to **http://localhost:5050**.

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

The web UI is served at **http://localhost:5050**.

---

## LLM Models

CSAgent uses a **single unified model** for both CLI and Web UI modes. The default model is `deepseek-v4-flash`, defined in `LlmSettings.cs`. This means both modes behave identically in terms of LLM behaviour — there is no longer a separate model per mode.

You can override the model in either mode using the `--model` argument (see [Command-Line Arguments](#command-line-arguments)).

### Examples

```bash
# CLI mode with a different model
dotnet run -- --model gpt-4o

# Web UI mode with a different model
dotnet run -- --ui --model gpt-4o
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
| `--ui` | Start in Web UI mode (starts a web server) |
| `--mem <file>` | Specify a custom memory/conversation file (default: `agent_memory.json`) |
| `--model <model>` | Override the default LLM model for the current mode |
| `--dry-run` | Simulate tool execution without making changes |
| `--version` | Display the current version of CSAgent and exit |
| `--doc` | Display this documentation in a nicely formatted terminal view and exit |
| `<file>` | Positional argument: specify a memory file without `--mem` flag |

### Examples

```bash
# Web UI with custom memory file
dotnet run -- --ui --mem my_project_memory.json

# CLI mode with a specific memory file
dotnet run my_memory.json

# Dry run mode
dotnet run -- --dry-run

# Display version
dotnet run -- --version

# Display documentation in terminal
dotnet run -- --doc

# Override the LLM model in CLI mode
dotnet run -- --model gpt-4o-mini

# Override the LLM model in Web UI mode
dotnet run -- --ui --model gpt-4o-mini
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

### Run

```bash
# CLI mode
set ALBERT_API_KEY=your-key
dotnet run

# Web UI mode
set ALBERT_API_KEY=your-key
dotnet run -- --ui
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

### Browser doesn't open automatically
Navigate manually to **http://localhost:5050** in your browser.

---

## License

This project is provided as-is. No external NuGet packages are required — everything is built with the .NET base class library.

---

*CSAgent — Zero dependencies, maximum autonomy.*
""";
}
