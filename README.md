# CsAgent 

Un agent de codage autonome multiplateforme écrit en C#/.NET 10. Il se connecte
à un point de terminaison LLM compatible OpenAI et peut lire des fichiers,
rechercher du code, exécuter des commandes shell et écrire des fichiers pour
accomplir des tâches de codage — le tout piloté par une boucle LLM.

## Fonctionnalités

- **Trois interfaces** — terminal (CLI), interface web et fenêtre Windows native.
- **Boucle d'agent autonome** — le LLM planifie et exécute les appels d'outils
  étape par étape.
- **Mémoire de conversation** — l'historique est conservé dans un fichier JSON
  entre les exécutions.
- **Mode dry-run** — simule l'exécution des outils sans apporter aucune
  modification.
- **Sécurité** — les actions destructives nécessitent une confirmation ; les
  opérations sur les fichiers sont limitées au répertoire de travail courant.
- **MCP distant** — connexion à un serveur MCP via Streamable HTTP, découverte
  automatique de ses outils et exécution des appels MCP depuis la même boucle
  d'agent que les outils natifs.

## MCP distant

CsAgent accepte un endpoint MCP Streamable HTTP avec `--mcp` ou avec la variable
d'environnement `CSAGENT_MCP_URL`.

Exemple avec le serveur Prolog :

```bash
dotnet run -- --mcp https://prolog-mcp.mcphosting.app/mcp
```

ou :

```bash
export CSAGENT_MCP_URL=https://prolog-mcp.mcphosting.app/mcp
dotnet run
```

Le client MCP effectue automatiquement `initialize`, `notifications/initialized`
et `tools/list`, puis convertit les outils MCP en définitions de fonctions
compatibles avec l'API LLM. Lorsqu'un modèle appelle un outil MCP, CsAgent utilise
`tools/call` sur le serveur distant et renvoie le résultat au modèle.

Aucune installation de Node.js ni copie locale de `prolog-mcp` n'est nécessaire.

Les outils MCP sont fusionnés avec les outils natifs. Si un serveur MCP expose
un nom qui entre en conflit avec un outil natif, l'outil natif est conservé.

## Architecture

CsAgentUI suit une architecture en couches simple, sans dépendances NuGet
externes ajoutées pour MCP. Le point d'entrée (`Program.cs`) analyse les arguments
de ligne de commande, puis sélectionne l'une des trois interfaces de présentation.

```
Program.cs  (point d'entrée — analyse des arguments + sélection du mode)
   │
   ├── Presentation/Tui      → interface terminale (CLI)
   ├── Presentation/Web      → interface web (serveur ASP.NET + SSE)
   └── Presentation/Desktop  → fenêtre native (AOTrino WebView2, Windows)
        │
        └── Core/Agent/CodingAgent   (boucle d'agent autonome)
             │
             ├── Core/Llm/LlmClient      → appels API LLM (compatible OpenAI)
             ├── Core/Agent/ToolDispatcher → outils natifs
             ├── Core/Agent/McpClient       → MCP distant (Streamable HTTP)
             └── Core/Memory/MemoryStore → persistance de la conversation (JSON)
```

### Couches principales

- **`src/Shared/`** — utilitaires partagés : analyse des arguments
  (`ArgumentParser`), affichage de l'aide (`HelpDisplay`), documentation
  (`DocDisplay`) et helpers JSON (`JsonHelpers`).

- **`src/Core/`** — la logique métier indépendante de l'interface :
  - `Agent/CodingAgent` — la boucle principale : il envoie l'historique au LLM,
    traite les appels d'outils renvoyés, exécute chaque outil via le
    `ToolDispatcher` ou `McpClient`, puis ajoute les résultats à la conversation.
  - `Agent/ToolDispatcher` — exécute les outils natifs et identifie les actions
    destructives.
  - `Agent/McpClient` — client MCP minimal basé uniquement sur `HttpClient` et
    `System.Text.Json.Nodes`; il supporte Streamable HTTP sans dépendance NuGet
    supplémentaire.
  - `Llm/LlmClient` — client HTTP pour le point de terminaison LLM compatible
    OpenAI (chat completions).
  - `Llm/LlmSettings` — configuration du modèle et du point de terminaison.
  - `Memory/MemoryStore` — charge et enregistre l'historique de conversation
    dans un fichier JSON.

- **`src/Presentation/`** — les trois interfaces, chacune implémentant
  `IAgentObserver` pour afficher la progression de l'agent.

### Flux d'exécution

1. `Program.cs` analyse les arguments et choisit le mode (CLI, web ou natif).
2. L'hôte de présentation crée un `CodingAgent` avec un observateur et, si
   demandé, l'URL MCP.
3. Au premier appel, `McpClient` initialise la session distante et découvre les
   outils.
4. Les définitions MCP sont ajoutées aux définitions natives envoyées au LLM.
5. Si le LLM demande un outil MCP, CsAgent appelle `tools/call` sur le serveur.
6. Le résultat est renvoyé au LLM comme résultat de tool call.
7. La boucle se termine lorsque le LLM répond avec `finish_reason = "stop"` ou
   atteint le nombre maximal d'étapes.
