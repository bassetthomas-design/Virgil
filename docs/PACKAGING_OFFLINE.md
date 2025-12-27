# Packaging offline complet (P2)

Ce guide détaille la préparation d'un package d'installation 100 % offline pour Virgil afin de respecter les exigences de la backlog P2.

## Objectifs
- Inclure l'exécutable et toutes les dépendances .NET sans téléchargement runtime.
- Embarquer l'intégralité des assets (`assets/virgil`, `assets/avatar`, `assets/activity`, `assets/voice`, `assets/prompts`, `assets/models`) et le runner LLM local.
- Documenter les prérequis CPU/RAM/AVX et la taille approximative du package.
- Produire un artefact unique consommable par un installeur (MSIX/Inno Setup).

## Pré-requis matériels
- Windows 10/11 x64 avec AVX/AVX2 (pour le runner `llama.cpp`).
- 8 Go RAM minimum (16 Go recommandés pour les modèles >7B).
- Espace disque : ~3 Go libres (dont ~2 Go pour le modèle GGUF hors debug symbols).

## Structure d'assets attendue
```
src/Virgil.App/assets/
├── activity/
├── avatar/
├── voice/
├── virgil/
├── prompts/
│   └── system_prompt.sample.txt
├── models/
│   └── README.md (dépôt du modèle GGUF)
└── llama/
    └── README.md (binaire/runner local)
```

## Génération d'un package offline (script PowerShell)
1. **Publier en self-contained** (inclut le runtime .NET) et copier les assets :
   ```powershell
   pwsh build/package-offline.ps1 -Configuration Release -Runtime win-x64
   ```
2. L'artefact est produit dans `dist/offline/app` avec un manifeste `offline-manifest.txt` résumant la build et la liste des assets.
3. Vérifier que le modèle GGUF (`assets/models/*.gguf`) et les binaires du runner (`assets/llama/*`) sont présents **avant** de créer l'installeur.

## Emballage MSIX ou Inno Setup
- **MSIX** :
  - Importer le contenu `dist/offline/app` dans le packaging tool Microsoft.
  - Désactiver tout téléchargement dynamique ; fournir uniquement des chemins locaux.
  - Déclarer les capacités nécessaires (PerformanceCounter, accès fichiers) dans l'AppxManifest.
- **Inno Setup** :
  - Copier récursivement `dist/offline/app` dans la section `Files`.
  - Ajouter un prérequis AVX/AVX2 dans la section `Check` pour le runner LLM.
  - Prévoir une option de désinstallation complète des assets (modèle, prompts, logs dans `%APPDATA%/Virgil`).

## Vérifications offline
- Tester l'installation sur une VM sans connexion réseau :
  - Lancer l'application et vérifier qu'aucun téléchargement n'est tenté (observer les logs Serilog).
  - Débrancher le réseau puis lancer une requête chat : l'engine doit rester fonctionnel en local.
  - Supprimer temporairement les assets (prompts/modèle) pour confirmer la présence de fallbacks.
- Archiver le rapport de test avec la taille finale de l'installeur.

## Notes
- Le script n'inclut pas de modèle par défaut pour limiter la taille du dépôt ; déposer le fichier GGUF correspondant dans `src/Virgil.App/assets/models/` avant publication.
- Les prompts peuvent être personnalisés dans `assets/prompts/`; le script copie l'intégralité du dossier dans le package.
