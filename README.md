# CsAgent

Un agent de codage autonome multiplateforme écrit en C#/.NET 10. Il se connecte
à un point de terminaison LLM compatible OpenAI et peut lire des fichiers,
rechercher du code, exécuter des commandes shell et écrire des fichiers pour
accomplir des tâches de codage — le tout piloté par une boucle LLM.

## Fonctionnalités

- **Trois interfaces** — terminal (CLI), interface web et fenêtre de bureau
  native (PhotinoX, multiplateforme).
- **Boucle d'agent autonome** — le LLM planifie et exécute les appels d'outils
  étape par étape.
- **Mémoire de conversation** — l'historique est conservé dans un fichier JSON
  entre les exécutions.
- **Mode dry-run** — simule l'exécution des outils sans apporter aucune
  modification.
- **Sécurité** — les actions destructives nécessitent une confirmation ; les
  opérations sur les fichiers sont limitées au répertoire de travail courant.
- **Zéro dépendance NuGet** — le projet ne dépend d'aucun paquet externe ; la
  fenêtre de bureau utilise PhotinoX, fourni en tant que projet local vendored.

## Démarrage rapide

```bash
# Mode CLI (terminal interactif)
dotnet run

# Interface web (port 5050 par défaut)
dotnet run -- --ui

# Fenêtre de bureau native (PhotinoX, multiplateforme)
dotnet run -- --desktop
```

L'endpoint LLM compatible OpenAI est configuré dans `LlmSettings` (point de
terminaison et modèle par défaut). La clé d'API est fournie via la variable
d'environnement `ALBERT_API_KEY`.

## Modes

| Mode | Commande | Description |
|------|----------|-------------|
| CLI | `(aucun drapeau)` | Session terminale interactive |
| Web | `--ui` | Serveur web ASP.NET avec SSE (port 5050 par défaut) |
| Bureau | `--desktop` | Fenêtre native PhotinoX (multiplateforme) |

## Options

| Option | Description |
|--------|-------------|
| `--help`, `-h`, `/?` | Affiche l'aide et quitte |
| `--version` | Affiche le numéro de version et quitte |
| `--doc` | Affiche la documentation complète dans le terminal |
| `--mem <fichier>` | Fichier de mémoire/conversation personnalisé (défaut : `agent_memory.json`) |
| `--model <nom>` | Remplace le modèle LLM pour le mode courant |
| `--port`, `-p <n>` | Port de l'interface web (défaut : 5050) |
| `--dry-run` | Simule l'exécution des outils sans apporter de modification |

## Architecture

CsAgentUI suit une architecture en couches simple, sans dépendances NuGet
externes. Le point d'entrée (`Program.cs`) analyse les arguments de ligne de
commande, puis sélectionne l'une des trois interfaces de présentation.

```
Program.cs  (point d'entrée — analyse des arguments + sélection du mode)
   │
   ├── Presentation/Tui            → interface terminale (CLI)
   ├── Presentation/Web            → interface web (serveur ASP.NET + SSE)
   └── Presentation/DesktopPhotinoX → fenêtre native (PhotinoX, multiplateforme)
        │
        └── Core/Agent/CodingAgent   (boucle d'agent autonome)
             │
             ├── Core/Llm/LlmClient      → appels API LLM (compatible OpenAI)
             ├── Core/Agent/ToolDispatcher → outils natifs
             └── Core/Memory/MemoryStore → persistance de la conversation (JSON)
```

### Couches principales

- **`src/Shared/`** — utilitaires partagés : analyse des arguments
  (`ArgumentParser`), affichage de l'aide (`HelpDisplay`), documentation
  (`DocDisplay`) et helpers JSON (`JsonHelpers`).

- **`src/Core/`** — la logique métier indépendante de l'interface :
  - `Agent/CodingAgent` — la boucle principale : il envoie l'historique au LLM,
    traite les appels d'outils renvoyés, exécute chaque outil via le
    `ToolDispatcher`, puis ajoute les résultats à la conversation.
  - `Agent/ToolDispatcher` — exécute les outils natifs et identifie les actions
    destructives.
  - `Llm/LlmClient` — client HTTP pour le point de terminaison LLM compatible
    OpenAI (chat completions).
  - `Llm/LlmSettings` — configuration du modèle et du point de terminaison.
  - `Memory/MemoryStore` — charge et enregistre l'historique de conversation
    dans un fichier JSON.

- **`src/Presentation/`** — les trois interfaces, chacune implémentant
  `IAgentObserver` pour afficher la progression de l'agent.

### Flux d'exécution

1. `Program.cs` analyse les arguments et choisit le mode (CLI, web ou bureau).
2. L'hôte de présentation crée un `CodingAgent` avec un observateur.
3. L'agent envoie l'historique de conversation au LLM.
4. Si le LLM demande un appel d'outil, l'agent l'exécute via le
   `ToolDispatcher` et renvoie le résultat au LLM.
5. La boucle se termine lorsque le LLM répond avec `finish_reason = "stop"` ou
   atteint le nombre maximal d'étapes.

## Évolutions futures

- **MCP (Model Context Protocol)** — prise en charge planifiée d'un serveur MCP
  distant via Streamable HTTP, avec découverte automatique de ses outils et
  exécution des appels MCP depuis la même boucle d'agent que les outils natifs.

- **Scripting Python** — piloter CsAgent depuis des scripts Python : lancer des
  sessions, envoyer des prompts, récupérer les réponses et les événements de
  l'agent (étapes, appels d'outils, résultats) de manière programmatique, par
  exemple via un module `csagent` ou un client de l'interface web (SSE).

- **Mémoire vectorielle basée sur SQLite** — architecture de mémoire à long terme
  fondée sur SQLite avec indexation vectorielle : stockage des conversations et
  des connaissances dans une base locale, recherche sémantique par similarité
  d'embedding, et récupération des contextes pertinents pour enrichir les prompts
  de l'agent.

  <img width="853" height="694" alt="image" src="https://github.com/user-attachments/assets/c0bb113d-b97b-42c0-b8d4-bd952c75ee93" />
