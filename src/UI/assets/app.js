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