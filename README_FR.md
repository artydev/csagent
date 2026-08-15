# CsAgentUI 

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

## Architecture

CsAgentUI suit une architecture en couches simple, sans dépendances NuGet
externes. Le point d'entrée (`Program.cs`) analyse les arguments de ligne de
commande, puis sélectionne l'une des trois interfaces de présentation.

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
             ├── Core/Agent/ToolDispatcher → exécution des outils
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
  - `Agent/ToolDispatcher` — enregistre et exécute les outils (lecture/écriture
    de fichiers, shell, git, etc.) et identifie les actions destructives.
  - `Llm/LlmClient` — client HTTP pour le point de terminaison LLM compatible
    OpenAI (chat completions).
  - `Llm/LlmSettings` — configuration du modèle et du point de terminaison.
  - `Memory/MemoryStore` — charge et enregistre l'historique de conversation
    dans un fichier JSON.
  - `Abstractions/IAgentObserver` — interface d'observation des événements de
    l'agent (étapes, pensées, appels d'outils, résultats, erreurs).

- **`src/Presentation/`** — les trois interfaces, chacune implémentant
  `IAgentObserver` pour afficher la progression de l'agent :
  - `Tui/` — interface terminale interactive (`ConsoleObserver`,
    `ConsoleRenderer`, `TuiHost`).
  - `Web/` — serveur web avec flux SSE (`WebHost`, `ApiEndpoints`,
    `SseObserver`, `StaticAssets`).
  - `Desktop/` — fenêtre native Windows via AOTrino (`DesktopHost`,
    `DesktopAPI`, `DesktopObserver`).

### Flux d'exécution

1. `Program.cs` analyse les arguments et choisit le mode (CLI, web ou natif).
2. L'hôte de présentation crée un `CodingAgent` avec un observateur.
3. La boucle de l'agent envoie l'historique au LLM ; si le LLM demande des
   appels d'outils, chacun est exécuté via le `ToolDispatcher` (avec
   confirmation en cas d'action destructive, ou simulation en mode dry-run).
4. Les résultats sont renvoyés au LLM et l'historique est sauvegardé dans le
   fichier mémoire entre chaque étape.
5. La boucle se termine lorsque le LLM répond avec `finish_reason = "stop"` ou
   atteint le nombre maximal d'étapes.

## Prérequis

- SDK .NET 10 (ou un binaire autonome publié en un seul fichier)
- Une clé API pour un point de terminaison compatible OpenAI

## Configuration

Définissez votre clé API comme variable d'environnement :

```
set ALBERT_API_KEY=votre-cle-ici
```

## Utilisation

```
CsAgentUI [options] [fichier-memoire]
```

### Arguments de ligne de commande (exacts)

L'analyseur (`src/Shared/ArgumentParser.cs`) reconnaît les arguments suivants.
Tous les drapeaux sont comparés par égalité de chaîne exacte
(`args.Contains(...)`), ils doivent donc être orthographiés exactement comme
indiqué. Les arguments sont sensibles à la casse.

#### Modes (drapeaux mutuellement exclusifs)

| Drapeau      | Champ analysé     | Description                                              |
|--------------|-------------------|----------------------------------------------------------|
| *(aucun)*    | —                 | Mode CLI — session terminale interactive                 |
| `--ui`       | `IsUiMode`        | Mode interface web — démarre un serveur web (port 5050 par défaut) |
| `--native`   | `IsNativeMode`    | Mode fenêtre native — fenêtre AOTrino WebView2 (Windows) |
| `--desktop`  | `IsDesktopMode`   | Alias du mode fenêtre native (également accepté)         |

#### Options

| Option               | Champ analysé      | Description                                        |
|----------------------|--------------------|----------------------------------------------------|
| `--help`, `-h`, `/?` | `ShowHelp`         | Affiche l'aide et quitte                           |
| `--version`          | `ShowVersion`      | Affiche la version et quitte                       |
| `--doc`              | `ShowDoc`          | Affiche la documentation complète dans le terminal et quitte |
| `--mem <fichier>`    | `MemoryFile`       | Fichier mémoire/conversation personnalisé (défaut : `agent_memory.json`) |
| `--model <nom>`      | `ModelOverride`    | Remplace le modèle LLM (défaut : `LlmSettings.Model`) |
| `--port, -p <n>`     | `Port`             | Port de l'interface web (défaut : `5050`)          |
| `--dry-run`          | `IsDryRun`         | Simule l'exécution des outils sans apporter de modification |

#### Argument positionnel : `[fichier-memoire]`

Si aucun `--mem <fichier>` n'est fourni, le **premier** argument qui n'est pas un
drapeau reconnu et qui ne commence pas par `-` est traité comme le fichier
mémoire. Les jetons reconnus qui ne sont pas des drapeaux sont `--ui`,
`--native`, `--desktop` et `--dry-run` ; tout autre argument commençant par `-`
est ignoré. Si aucun n'est trouvé, la valeur par défaut `agent_memory.json` est
utilisée.

#### Règles d'analyse (comportement exact)

- **`--mem`** — prend l'argument suivant comme chemin de fichier. Si `--mem` est
  le dernier argument (aucune valeur ne suit), il est ignoré.
- **`--model`** — prend l'argument suivant comme nom de modèle. Si `--model` est
  le dernier argument (aucune valeur ne suit), il est ignoré.
- **`--port` / `-p`** — prend l'argument suivant et l'analyse comme un entier.
  La valeur n'est acceptée que si `0 < port < 65536` ; sinon, la valeur par
  défaut `5050` est utilisée.
- **`--help` / `-h` / `/?`** — n'importe lequel de ces drapeaux définit
  `ShowHelp`.
- **`--version`** — définit `ShowVersion`.
- **`--doc`** — définit `ShowDoc`.
- **`--dry-run`** — définit `IsDryRun`.
- **`--ui` / `--native` / `--desktop`** — définissent leurs drapeaux de mode
  respectifs.

### Exemples

```
csagent                                    Mode CLI
csagent --ui                               Mode interface web (port 5050)
csagent --native                           Mode fenêtre native
csagent --desktop                          Mode fenêtre native (alias)
csagent --ui --port 8080                   Interface web sur le port 8080
csagent --model gpt-4o                     CLI avec un modèle personnalisé
csagent --ui --model gpt-4o                Interface web avec modèle personnalisé
csagent --native --model gpt-4o            Fenêtre native avec modèle personnalisé
csagent --mem my_history.json              CLI avec un fichier mémoire personnalisé
csagent --ui --mem my_history.json         Interface web avec fichier mémoire personnalisé
csagent --dry-run                          Mode dry-run (aucune modification)
csagent --doc                              Affiche la documentation
csagent --version                          Affiche la version
csagent --help                             Affiche l'aide
```

## Outils disponibles

L'agent peut appeler les outils suivants pour accomplir ses tâches :

```
CsAgentUI
├── Opérations sur les fichiers
│   ├── write_file      Écrire/écraser un fichier texte
│   ├── read_file       Lire un fichier texte
│   ├── read_json       Lire un fichier JSON (avec requête dot-path)
│   ├── edit_file       Modifications rechercher-remplacer (atomiques)
│   ├── copy_file       Copier un fichier
│   ├── move_file       Déplacer/renommer un fichier  ⚠ destructif
│   ├── delete_file     Supprimer un fichier          ⚠ destructif
│   ├── zip             Créer une archive zip
│   └── unzip           Extraire une archive zip      ⚠ destructif
├── Inspection et recherche
│   ├── list_dir        Lister les fichiers/sous-répertoires
│   ├── tree            Arborescence de répertoires visuelle
│   ├── search_files    Recherche grep récursive
│   └── parse_output    Analyser la sortie en JSON structuré
├── Git
│   ├── git_status      État de l'arbre de travail
│   ├── git_diff        Modifications non validées
│   ├── git_log         Historique des validations
│   ├── git_branch      Branches courante/locales
│   └── git_commit      Mettre en scène et valider  ⚠ destructif
├── Shell et réseau
│   ├── sh              Exécuter une commande shell
│   ├── run_terminal    Session shell persistante
│   ├── close_terminal  Fermer une session shell
│   ├── http_request    Effectuer une requête HTTP
│   ├── web_search      Rechercher sur le web
│   └── fetch_url       Récupérer le texte d'une page web
└── Modèle
    └── switch_model    Changer le modèle LLM actif
```

### Opérations sur les fichiers

| Outil | Description |
|-------|-------------|
| `write_file` | Écrit (ou écrase) un fichier texte ; crée les répertoires parents |
| `read_file` | Lit un fichier texte et renvoie son contenu |
| `read_json` | Lit un fichier JSON (mis en forme), en extrayant éventuellement une sous-valeur via une requête dot-path |
| `edit_file` | Applique des modifications rechercher-remplacer précises à un fichier (atomique) |
| `copy_file` | Copie un fichier de la source vers la destination |
| `move_file` | Déplace (renomme) un fichier *(destructif)* |
| `delete_file` | Supprime définitivement un fichier *(destructif)* |
| `zip` | Crée une archive zip à partir d'un fichier ou d'un répertoire |
| `unzip` | Extrait une archive zip dans un répertoire *(destructif)* |

### Inspection et recherche

| Outil | Description |
|-------|-------------|
| `list_dir` | Liste les fichiers et sous-répertoires (éventuellement de manière récursive) |
| `tree` | Affiche une arborescence de répertoires visuelle et indentée |
| `search_files` | Recherche récursive grep d'un motif texte, renvoyant les chemins de fichiers et les numéros de ligne |
| `parse_output` | Analyse la sortie de commande en JSON structuré (json / keyvalue / csv / auto) |

### Git

| Outil | Description |
|-------|-------------|
| `git_status` | Affiche l'état de l'arbre de travail |
| `git_diff` | Affiche les modifications non validées (éventuellement mises en scène) |
| `git_log` | Affiche l'historique récent des validations |
| `git_branch` | Affiche la branche courante et les branches locales |
| `git_commit` | Met en scène toutes les modifications et crée une validation *(destructif)* |

### Shell et réseau

| Outil | Description |
|-------|-------------|
| `sh` | Exécute une commande shell (cmd.exe sous Windows, /bin/sh ailleurs) |
| `run_terminal` | Exécute une commande dans une session shell persistante et avec état |
| `close_terminal` | Ferme et termine une session shell persistante |
| `http_request` | Effectue une requête HTTP et renvoie le statut, les en-têtes et le corps |
| `web_search` | Recherche sur le web des documentations, erreurs ou solutions |
| `fetch_url` | Récupère le contenu textuel lisible d'une page web |

### Modèle

| Outil | Description |
|-------|-------------|
| `switch_model` | Change le modèle LLM actif pour la session courante |

## Remarques

- Toutes les actions destructives (par ex. `write_file`, `edit_file`,
  `git_commit`, `move_file`, `delete_file`, `unzip`) nécessitent une
  confirmation de l'utilisateur.
- Les opérations sur les fichiers sont limitées au répertoire de travail
  courant.
- Les commandes shell sont filtrées pour les opérations potentiellement
  dangereuses.

## Téléchargement
[CsAgent](https://raw.githubusercontent.com/artydev/csagent-ui/main/download/csagent.exe)
