

# Agent de codage IA multiplateforme

Ce projet est un agent de codage IA multiplateforme capable d'effectuer automatiquement des tâches en interagissant avec le système de fichiers et en exécutant des commandes shell via un LLM (Large Language Model).

## Fonctionnalités principales

### 1. Modes d'interface

- **Mode UI Web** : exécution comme serveur local avec une interface accessible via navigateur.
- **Mode CLI** : interface en ligne de commande pour une utilisation depuis le terminal.

### 2. Capacités principales

- Lecture et écriture de fichiers.
- Liste des répertoires.
- Exécution de commandes shell multiplateformes :
  - `cmd.exe` sur Windows.
  - `/bin/sh` sur Unix.
- Interaction avec les API LLM (configuré pour fonctionner avec l'API Albert d'Etalab).

### 3. Fonctionnement autonome

- L'agent raisonne étape par étape avant d'agir.
- Inspection de l'espace de travail avec :
  - `list_dir` pour parcourir les dossiers.
  - `read_file` pour lire les fichiers.
- Création et modification de fichiers avec :
  - `write_file`.
- Exécution de commandes système avec :
  - `sh`.
- Gestion des erreurs et mécanismes de correction automatique.

### 4. Fonctions de sécurité

- Les opérations destructrices (`sh` et `write_file`) nécessitent une confirmation en mode interactif.
- Mode **dry-run** permettant de tester les actions sans appliquer de modifications.
- Mémorisation de l'historique pour gérer la longueur des conversations.

### 5. Interface utilisateur

- Interface web avec streaming des réponses en temps réel.
- Journalisation avec code couleur pour différencier :
  - Les pensées de l'agent.
  - Les appels aux outils.
  - Les résultats d'exécution.
- Design moderne et responsive.

## Fonctionnement

L'agent reçoit les demandes des utilisateurs, les traite via un LLM, puis utilise des outils intégrés pour exécuter des actions sur le système local.

Il conserve un historique des conversations en mémoire et peut persister cet état entre plusieurs sessions.

## Exécution

Avant de lancer l'agent, définir la variable d'environnement :

```bash
export ALBERT_API_KEY="votre_clé_api"
