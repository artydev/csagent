"# csagent-ui" 
Ce projet est un agent de codage IA croisée-platforme qui peut effectuer automatiquement des tâches en interagissant avec le système de fichiers et en exécutant des commandes shell via un LLM (Modèle de Langage à Grande Échelle).
|
| ## Fonctionnalités principales :
|
| 1. Modes d'interface dual :
| - Mode UI Web : Exécution comme serveur local avec interface navigateur
| - Mode CLI : Interface en ligne de commande pour utilisation terminal
|
| 2. Capacités principales :
| - Lecture/écriture de fichiers
| - Liste des répertoires
| - Exécution de commandes shell (multiplateforme - utilise cmd.exe sur Windows, /bin/sh sur Unix)
| - Interaction avec les API LLM (configuré pour fonctionner avec l'API Albert d'Etalab)
|
| 3. Fonctionnement autonome :
| - L'agent réfléchit étape par étape avant d'agir
| - Il peut inspecter l'espace de travail avec list_dir et read_file
| - Il peut créer/modifier des fichiers avec write_file
| - Il peut exécuter des commandes système avec sh
| - Gestion des erreurs et tentatives de correction
|
| 4. Fonctions de sécurité :
| - Opérations destructrices (sh et write_file) nécessitent une confirmation en mode interactif
| - Mode "dry-run" pour tester sans faire de changements
| - Mémorisation de l'historique pour gérer la longueur de la conversation
|
| 5. Interface utilisateur :
| - Interface web avec streaming de réponses en temps réel
| - Journalisation codée par couleurs pour différents types d'interactions (pensées, appels d'outils, résultats)
| - Design moderne et responsive
|
| ### Comment ça marche :
| L'agent reçoit des demandes des utilisateurs, les traite via un LLM, puis utilise des outils intégrés pour exécuter des actions sur le système local. Il maintient un historique de conversation dans la mémoire et peut persister cet état entre les sessions.
|
| Pour l'exécuter, il faut définir la variable d'environnement ALBERT_API_KEY et peut être lancé en mode web (--ui) ou en mode CLI.
