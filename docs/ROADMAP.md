# VIRGIL — Feuille de route officielle

## Vision
Virgil = compagnon système vivant : surveille, maintient, met à jour, diagnostique, optimise, et papote intelligemment dans une chatbox éphémère, avec un avatar holographique animé.

## Architecture (3 couches)
- **Core Engine (Brain)** : humeurs, logique, scheduler, arbitrage.
- **System Layer** : monitoring, nettoyage, mises à jour, sécurité, logs.
- **Interface Layer (UI)** : chatbox avec effet Thanos, avatar holographique, dashboard.

## Modules
- **Surveillance** : CPU/GPU/RAM/Temp/Disque SMART/Réseau, états normal/warn/alert.
- **Nettoyage & Maintenance** : simple/complet/pro + navigateurs, simulation à sec, SafeGuard.
- **Mises à jour (Orchestrator)** :
  - Modes: Safe / Standard / Sans Exception
  - Store, Winget, Windows Update, OEM/GPU, Runtimes, Defender
  - Pré-checks: admin, secteur, espace, point de restau; Reboot manager; Logs
- **Sécurité** : Defender/Firewall, processus suspects, UAC.
- **Optimisation** : énergie (Éco/Normal/Turbo), startup manager, RAM/cache.
- **Diagnostic** : analyse système, services/périphériques, recommandations.
- **Process Manager** : vue live, kill/prio via chat.
- **Automatisation** : tâches planifiées, centre de notifications.
- **Mode compagnon** : routine, punchlines contextuelles, mini-mémoire, styles Pro/Détendu/Humoristique, silence/bavard.
- **AudioSense** : détection musique/vidéo via loopback, pulsation rythmée, mood happy, arbitrage avec alertes.
- **Chatbox** : effet Thanos sur tous les messages (45–120 s), épingles possibles, logs intacts.
- **Avatar holographique** : WebGL/Canvas, rond à deux yeux, glow/pulsation/particules/flicker, API `setMood/speak/notify`.

## Phases
1) **Core MVP**: Monitoring + Chatbox + Thanos + humeurs basiques  
2) **Maintenance Engine**: Nettoyage + SafeGuard + Logs  
3) **Update & Security**: Orchestrator + Defender  
4) **Companion Mode**: personnalité + mini-mémoire  
5) **Holographic UI**: Avatar temps réel + réactions  
6) **Diagnostic Pro**: analyse + conseils  
7) **Optimisation+**: RAM, startup, énergie  
8) **Expérience+**: thèmes, météo/actus, plugins

## Cibles techniques
- Perf: <3% CPU, 60 FPS si GPU ok, modes High/Medium/Low
- Logs: rotation + compression, rétention 14 jours
- Zéro blocage UI (async), gestion erreurs robuste

## Valeurs par défaut (peuvent changer)
- Thanos TTL message: 60 s
- Punchlines: 1 toutes 3–6 min en surveillance ON
- Seuils warn/alert: CPU 80/90%, Temp 80/90°C, RAM 75/85%
- Orchestrator: mode Standard par défaut
