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

    /* Code block styling for syntax highlighting */
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
    // Initialize Prism with explicit component loading for better reliability
    document.addEventListener('DOMContentLoaded', function() {
        // Ensure Prism is loaded and ready
        if (typeof Prism !== 'undefined') {
            // Load common languages explicitly to avoid autoloader issues
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

                const content=
                typeof message.data==="string"
                ?message.data
                :JSON.stringify(message.data,null,2);

                // For result messages, we'll wrap content in pre/code tags for syntax highlighting
                if(message.type === "result") {
                    // Create a pre element with appropriate class for Prism.js to highlight
                    const preElement = document.createElement('pre');
                    const codeElement = document.createElement('code');
                    codeElement.className = 'language-javascript'; // Default to JavaScript
                    codeElement.textContent = content;
                    preElement.appendChild(codeElement);
                    div.appendChild(preElement);
                } else {
                    div.innerText=`[${message.type}] ${content}`;
                }

                log.appendChild(div);
                
                // After adding content, trigger syntax highlighting for the new content
                if(message.type === "result") {
                    // Use setTimeout to ensure DOM is updated before highlighting
                    setTimeout(function() {
                        try {
                            if (typeof Prism !== 'undefined' && Prism.highlightAllUnder) {
                                // Highlight only within the newly added div
                                Prism.highlightAllUnder(div);
                            }
                        } catch(e) {
                            console.error('Highlighting error:', e);
                        }
                    }, 10); // Small delay to ensure DOM update
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
}