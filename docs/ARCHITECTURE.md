# VIRGIL — Architecture

## 1. Schéma global
``
[System Layer] <—events/metrics—> [Core Engine (Brain)] <—commands/state—> [Interface Layer]
``

### System Layer
- **MonitoringService**: capteurs CPU/GPU/RAM/Temp/SMART/Réseau (poll 1–5 s, adaptatif)
- **CleanupService**: simple/complet/pro + navigateurs, simulation à sec, whitelists
- **UpdateOrchestrator**:
  - Adapters: Store, Winget, WindowsUpdate, OEMDrivers, GPUDrivers, Runtimes, Defender
  - Modes: Safe / Standard / Sans Exception
  - Pré-checks: admin/secteur/espace/restore point, reboot manager
- **SecurityService**: Defender/Firewall, process suspects
- **ProcessService**: liste, kill, priorité
- **LogService**: fichiers quotidiens, rotation + compression

### Core Engine (Brain)
- **StateModel**: humeur = score pondéré (stress/fatigue/serenity)
- **Scheduler**: tâches périodiques (surveillance, punchlines, maintenance planifiée)
- **Arbitrage**: priorité alert > warn > user-locked > companion > normal
- **CompanionModule**: routine, templates contextuels, mini-mémoire
- **AudioSense Router**: isPlaying/rms/beat → mood happy + pulse, avec sécurité sur alertes
- **Policies**: SafeGuard (droits, whitelists, throttling)

### Interface Layer
- **ChatBox**: timeline, effet Thanos TTL (config), épingles, logs non impactés
- **AvatarRenderer** (Canvas/WebGL):
  - API: `setMood(mood, intensity)`, `speak(pulse)`, `notify(event)`, `setPerformanceMode(mode)`, `resize()`, `dispose()`
  - Effets: glow dynamique, pulsation, particules, flicker, aberration chromatique légère
- **Dashboard**: jauges, températures, boutons d’action
- **Themes**: clair/sombre/dynamique

## 2. Contrats d’API internes

### Brain ↔ Avatar
- `setMood(mood: "happy"|"focused"|"warn"|"alert"|"sleepy"|"proud"|"tired", intensity?: 0..1)`
- `speak(pulse?: 0..1)`
- `notify(event: "success"|"error"|"update")`
- `setPerformanceMode(mode: "auto"|"high"|"medium"|"low")`

### Brain ↔ Chat
- `post(message, {type, pinned?: bool, ttl?: ms})`
- `clearPinned(id)` / `pin(id)`
- Thanos géré côté Chat (TTL → anim → remove)

### Brain ↔ System
- `GetMetrics()` → {cpu,gpu,ram,temp,smart,net}
- `Cleanup(mode, simulate: bool)` → {summary, freedBytes, report}
- `Update(mode)` → {steps, rebootsRequired, failures}
- `SecurityScan(level)` → {issues}
- `Processes()` / `Kill(pid)` / `SetPriority(pid, level)`

## 3. SafeGuard
- Vérif admin/UAC
- Whitelists dossiers (jamais %USERPROFILE% à l’aveugle)
- Timeouts par étape + retry limité
- Point de restauration avant "Sans Exception"
- Quotas d’actions destructives

## 4. AudioSense
- Source: WASAPI loopback (mix système), GSMTC pour métadonnées
- Traitement: RMS court/long, seuil adaptatif, beats simples
- Latence cible <80 ms
- Paramètres: ON/OFF, sensibilité, style, privacy

## 5. Perf & Fallback
- Modes perf: High/Medium/Low
- Fallback PNG si pas d’accélération
- Poll adaptatif (jusqu’à 10 s si tout est calme)

## 6. Logs
- `%AppData%\Virgil\logs\YYYY-MM-DD.log`
- Rotation + ZIP > 14 jours
- Redaction sur contenu sensible (pas d’audio, pas de données privées)
