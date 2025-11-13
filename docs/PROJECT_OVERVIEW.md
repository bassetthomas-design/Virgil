# Virgil — Assistant Système Intelligent

## 🧭 Sommaire
1. [Présentation du projet](#présentation-du-projet)
2. [Modules principaux](#modules-principaux)
3. [Fonctionnalités clés](#fonctionnalités-clés)
4. [Feuille de route](#feuille-de-route)
5. [Problèmes rencontrés et solutions](#problèmes-rencontrés-et-solutions)
6. [Prochaines étapes](#prochaines-étapes)

---

## Présentation du projet
Virgil est un assistant système intelligent sous Windows, développé en C# (.NET 8 / WPF).

L'objectif : fournir un compagnon système complet capable de :
- Surveiller l’état du PC (températures, CPU, GPU, RAM).
- Réagir visuellement via un avatar animé (MoodMapper).
- Interagir avec l’utilisateur (chatbox, actions système, voix).
- Offrir un mode compagnon holographique et un effet *Thanos* pour la suppression visuelle des messages.

---

## Modules principaux
### 🧩 Noyau de l’application
- **Virgil.Core** — logique système et structures internes.
- **Virgil.Agent** — couche intermédiaire pour l’interface.
- **Virgil.App** — interface graphique principale (WPF).

### ⚙️ Services
- **MonitoringService** — collecte des métriques CPU/GPU/RAM/Disk.
- **DriverService** — gestion des pilotes système.
- **PreflightService** — initialisation et vérifications au démarrage.
- **PulseController** — gestion du rythme de l’avatar selon les événements.

### 🗣️ Interaction
- **ChatService** — gestion des messages, des types (succès, avertissement, erreur).
- **ChatViewModel** — gestion de l’affichage et effet *Thanos*.
- **ActionsService / SystemActionsService** — exécution des commandes système (nettoyage, reset explorer, maintenance réseau, etc.).

---

## Fonctionnalités clés
- **Effet Thanos** : suppression visuelle progressive des messages affichés dans le chat.
- **MoodMapper** : traduit l’état du système (ou de l’utilisateur) en humeur de l’avatar.
- **HUD mini-barre** : indicateurs discrets de performance (CPU, RAM, GPU).
- **Détection multimédia** : synchronisation du *pulse* de l’avatar avec la musique ou le son actif.
- **Mise à jour globale** : actualise tout le système sans exception (Windows Update, drivers, dépendances).

---

## Feuille de route
### ✅ Déjà implémenté
- Base du projet WPF / .NET 8
- Services système (Monitoring, Actions, etc.)
- Effet Thanos dans le chat
- MoodMapper basique
- Avatar initial (forme ronde, sans bouche)

### 🚧 En cours
- Lien entre ChatService et PulseController
- Animation dynamique de l’avatar selon le mood
- Liaison Monitoring → AvatarView (températures ↔ humeur)

### 🔮 À venir
- Expression complète du mood via les yeux et la couleur
- Effet audio (réaction à la musique)
- Mode compagnon holographique
- Intégration vocale (TTS / STT)

---

## Problèmes rencontrés et solutions
- **Erreur MSB4025 / XML invalide** — résolue par nettoyage des fichiers projet.
- **Ambiguïté Timer** — corrigée avec alias explicite (`System.Timers.Timer`).
- **Ambiguïté Forms / WPF** — suppression de `System.Windows.Forms` inutiles.
- **Conflit MoodConverters** — fichiers fusionnés et namespace unifié.
- **Crash PulseController non résolu** — ajouté via `MainViewModel` pour synchroniser les moods.

---

## Prochaines étapes
1. Terminer la liaison complète du `MonitoringViewModel` à `AvatarView`.
2. Finaliser les converters de mood (angle, scale, color).
3. Implémenter le système de réactions sonores et visuelles.
4. Ajouter le mode compagnon (mini-avatar persistant).
5. Créer une page de paramétrage visuel (thèmes, taille, transparence).

---

> Ce fichier documente l’évolution complète du projet Virgil (branche `feat/logs-thanos-startup`).
> Mis à jour automatiquement après chaque étape majeure du développement.
