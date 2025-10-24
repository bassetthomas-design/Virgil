# Virgil

Virgil est une application de bureau Windows tout‑en‑un pour surveiller, nettoyer, optimiser et assister l’utilisateur. Elle est construite avec **.NET 8** et **WPF** en ciblant les plateformes **x64**.

## Projets

Ce dépôt contient plusieurs projets organisés pour séparer l’interface utilisateur, la logique métier et le fonctionnement en arrière‑plan :

* **`Virgil.App`** : application WPF affichant le tableau de bord, l’avatar et les différents modules (surveillance, nettoyage, démarrage, etc.).
* **`Virgil.Core`** : bibliothèque de services (monitoring, nettoyage, configuration), indépendante de l’interface graphique.
* **`Virgil.Agent`** : agent léger affiché dans la zone de notification (tray) qui surveille le système en arrière‑plan et affiche des notifications.
* **`Virgil.Tests`** : tests unitaires basés sur xUnit pour valider le fonctionnement du cœur.

## CI/CD

Un workflow GitHub Actions est défini dans `.github/workflows/dotnet-build-and-artifact.yml`. Il restaure les dépendances, compile tous les projets en configuration Release, publie l’application pour `win‑x64` et archive l’artefact publié.

## Script de préparation

Le dossier `tools` contient un script PowerShell (`prepare-and-push.ps1`) qui automatise la préparation et le build local : il modifie les fichiers `.csproj` pour cibler .NET 8 et x64, initialise un dépôt git, ajoute un `.gitignore` et génère le workflow CI. Vous pouvez l’exécuter à la racine de votre copie locale pour préparer l’environnement avant de pousser vers GitHub. :)
