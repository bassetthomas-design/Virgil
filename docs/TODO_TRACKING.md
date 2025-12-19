# TODO Tracking - GitHub Issues à Créer

Ce document liste tous les TODOs trouvés dans le code source de Virgil, organisés par composant. Chaque TODO doit être converti en une issue GitHub pour faciliter le suivi et la collaboration.

**Date de création**: 19 décembre 2024
**Nombre total de TODOs**: 13

---

## 1. SystemMonitorService - Métriques Système Complètes

**Fichier**: `src/Virgil.App/Services/SystemMonitorService.cs`

### TODOs identifiés:
1. **Ligne 11**: `TODO: GPU, Disk, Temperatures` - Ajouter les propriétés GPU, Disk et Temperatures au snapshot
2. **Ligne 46**: `TODO: replace with real system metrics (CPU, RAM, GPU, Disk, Temps)` - Implémenter la collecte réelle des métriques

### Contexte:
Le `SystemMonitorService` est le service principal de monitoring du système. Actuellement, il ne retourne que des valeurs nulles/zéro pour CPU et RAM. Il manque l'implémentation complète des métriques.

### Issue suggérée:
- **Titre**: `[Phase 1] SystemMonitorService - Implémenter métriques complètes (CPU/GPU/RAM/Disque/Températures)`
- **Label**: `enhancement`, `phase-1`, `monitoring`
- **Phase liée**: Issue #74 - System: MonitoringService (Phase 1)

### Critères d'acceptation:
- Collecte réelle du CPU usage via PerformanceCounter ou équivalent
- Collecte de la RAM usage (utilisée/totale)
- Ajout de GPU usage (si applicable via LibreHardwareMonitor)
- Ajout de Disk I/O ou usage
- Intégration des températures (CPU, GPU si disponible)
- Métriques mises à jour toutes les 1-2 secondes
- Performance < 1% CPU en moyenne
- Aucune fuite mémoire après 30 min de fonctionnement

---

## 2. AdvancedMonitoringService - Intégration smartctl/LibreHardwareMonitor

**Fichier**: `src/Virgil.Core/Services/AdvancedMonitoringService.cs`

### TODO identifié:
**Ligne 24**: `TODO: intégrer smartctl/LibreHardwareMonitor plus tard`

### Contexte:
Le `AdvancedMonitoringService` lit les températures CPU (via WMI) et GPU (via nvidia-smi). La température disque est actuellement `null` car elle nécessite des outils externes comme smartctl ou une bibliothèque comme LibreHardwareMonitor.

### Issue suggérée:
- **Titre**: `[Phase 1] AdvancedMonitoringService - Ajouter support température disque (smartctl/LibreHardwareMonitor)`
- **Label**: `enhancement`, `phase-1`, `monitoring`, `hardware`
- **Phase liée**: Issue #74 - System: MonitoringService (Phase 1)

### Critères d'acceptation:
- Intégrer LibreHardwareMonitor pour lecture SMART des disques
- Lire température du disque principal (C:)
- Gestion gracieuse si SMART non disponible (retour null)
- Pas d'impact performance (< 0.5% CPU)
- Compatible avec SSD et HDD

---

## 3. SpecialActionsService - Persistance de l'historique de chat

**Fichier**: `src/Virgil.App/Services/SpecialActionsService.cs`

### TODO identifié:
**Ligne 30**: `TODO: brancher ici un service de persistance de l'historique (si existant).`

### Contexte:
La méthode `PurgeChatHistoryAsync()` est un placeholder pour l'effet Thanos. Elle doit être connectée à un service de persistance du chat pour effacer réellement l'historique.

### Issue suggérée:
- **Titre**: `[Phase 1] SpecialActionsService - Implémenter persistance et purge de l'historique de chat`
- **Label**: `enhancement`, `phase-1`, `chat`, `thanos-effect`
- **Phase liée**: Issue #75 - UI: ChatBox with Thanos effect (Phase 1)

### Critères d'acceptation:
- Créer ou utiliser un service de persistance du chat
- Implémenter la purge réelle de l'historique
- Coordonner avec l'animation UI de l'effet Thanos
- Support des messages épinglés (ne pas purger)
- Confirmation utilisateur avant purge complète

---

## 4. SpecialActionsService - Intégration service de configuration

**Fichier**: `src/Virgil.App/Services/SpecialActionsService.cs`

### TODO identifié:
**Ligne 40**: `TODO: injecter et appeler un service de configuration si/ quand il sera disponible.`

### Contexte:
La méthode `ReloadSettingsAsync()` est un placeholder. Elle doit être connectée au système de configuration pour permettre le rechargement dynamique des paramètres.

### Issue suggérée:
- **Titre**: `[Phase 1] SpecialActionsService - Implémenter rechargement dynamique de la configuration`
- **Label**: `enhancement`, `phase-1`, `configuration`
- **Phase liée**: Issue #20 - [Lot 6] Config & Personnalisation

### Critères d'acceptation:
- Créer ou utiliser un service de configuration centralisé
- Implémenter le rechargement des paramètres sans redémarrage
- Notifier les composants concernés des changements
- Valider la configuration avant application
- Gestion des erreurs de lecture/parsing

---

## 5. SpecialActionsService - Méthode RescanAsync du MonitoringService

**Fichier**: `src/Virgil.App/Services/SpecialActionsService.cs`

### TODO identifié:
**Ligne 50**: `TODO: brancher ici MonitoringService.RescanAsync() quand il sera exposé.`

### Contexte:
La méthode `RescanMonitoringAsync()` doit déclencher un re-scan complet du système de monitoring.

### Issue suggérée:
- **Titre**: `[Phase 1] MonitoringService - Exposer méthode RescanAsync pour refresh manuel`
- **Label**: `enhancement`, `phase-1`, `monitoring`
- **Phase liée**: Issue #74 - System: MonitoringService (Phase 1)

### Critères d'acceptation:
- Ajouter méthode `RescanAsync()` au MonitoringService
- Forcer une mise à jour immédiate de toutes les métriques
- Réinitialiser les compteurs si nécessaire
- Notifier les observateurs du refresh
- Pas de conflit avec le polling normal

---

## 6. MainShell - Finalisation du flux UX des paramètres

**Fichier**: `src/Virgil.App/Views/MainShell.xaml.cs`

### TODO identifié:
**Ligne 25**: `TODO: wire to settings window when the UX flow is finalized again.`

### Contexte:
Le bouton des paramètres dans `MainShell` n'ouvre pas encore la fenêtre de paramètres. Le handler `OnOpenSettings` est un placeholder en attendant la finalisation de l'UX.

### Issue suggérée:
- **Titre**: `[Phase 1] MainShell - Connecter bouton paramètres à SettingsWindow`
- **Label**: `enhancement`, `phase-1`, `ui`, `settings`
- **Phase liée**: Issue #20 - [Lot 6] Config & Personnalisation

### Critères d'acceptation:
- Implémenter l'ouverture de la fenêtre de paramètres
- Gestion du cycle de vie de la fenêtre (singleton ou multiple instances)
- Application des paramètres en temps réel si possible
- Bouton de fermeture/annulation/sauvegarde fonctionnel
- Design cohérent avec le reste de l'application

---

## 7. MainShell - Logique de toggle du HUD

**Fichier**: `src/Virgil.App/Views/MainShell.xaml.cs`

### TODO identifié:
**Ligne 33**: `TODO: implement HUD toggle logic (show/hide mini HUD) if required.`

### Contexte:
Un bouton de toggle HUD existe dans l'UI mais son handler `OnHudToggled` ne fait rien. Il doit afficher/masquer un mini HUD.

### Issue suggérée:
- **Titre**: `[Phase 1-2] MainShell - Implémenter toggle du mini HUD`
- **Label**: `enhancement`, `phase-1`, `ui`, `hud`
- **Phase liée**: Issue #15 - [Lot 1] UI/UX squelette

### Critères d'acceptation:
- Définir ce qu'est le "mini HUD" (overlay, floating window, etc.)
- Implémenter l'affichage/masquage du HUD
- Persistance de l'état (activé/désactivé)
- Position et taille configurables
- Pas d'impact sur les performances

---

## 8. MainWindow.Monitoring - Lecture CPU/GPU/RAM/Températures

**Fichier**: `src/Virgil.App/MainWindow.Monitoring.cs`

### TODO identifié:
**Ligne 15**: `TODO : lecture CPU/GPU/RAM/Températures`

### Contexte:
Partie partielle de `MainWindow` dédiée au monitoring. Nécessite l'implémentation de la lecture des métriques système.

### Issue suggérée:
- **Titre**: `[Phase 1] MainWindow.Monitoring - Implémenter affichage des métriques système`
- **Label**: `enhancement`, `phase-1`, `ui`, `monitoring`
- **Phase liée**: Issue #74 - System: MonitoringService (Phase 1) et Issue #16 - [Lot 2] Monitoring & Humeurs

### Critères d'acceptation:
- Connecter aux services de monitoring
- Afficher CPU/GPU/RAM/Températures en temps réel
- Refresh visuel toutes les 1-2 secondes
- Jauges/graphiques visuels cohérents
- Seuils colorés (vert/jaune/rouge)
- Pas de freeze de l'UI

---

## 9. MainViewModel.LegacyStubs - Câblage du progress à l'UI

**Fichier**: `src/Virgil.App/ViewModels/MainViewModel.LegacyStubs.cs`

### TODO identifié:
**Ligne 10**: `TODO: wire to UI progress`

### Contexte:
Méthode `Progress(int percent, string? text = null)` est un stub. Elle doit être connectée à un composant UI de progression.

### Issue suggérée:
- **Titre**: `[Phase 1-2] MainViewModel - Implémenter indicateur de progression UI`
- **Label**: `enhancement`, `phase-2`, `ui`, `progress`
- **Phase liée**: Issue #19 - [Lot 5] Maintenance complète

### Critères d'acceptation:
- Créer ou utiliser un composant de barre de progression
- Afficher le pourcentage et le texte
- Supporter les opérations longues (nettoyage, updates)
- Possible d'annuler l'opération
- Animation fluide

---

## 10. ChatViewModel.Thanos - Connexion à chatService.ClearAll()

**Fichier**: `src/Virgil.App/ViewModels/ChatViewModel.Thanos.cs`

### TODO identifié:
**Ligne 5**: `TODO: call chatService.ClearAll() once exposed here`

### Contexte:
Méthode `SnapAll()` pour l'effet Thanos sur le chat. Doit appeler une méthode du chatService pour effacer tous les messages.

### Issue suggérée:
- **Titre**: `[Phase 1] ChatViewModel - Connecter effet Thanos à chatService.ClearAll()`
- **Label**: `enhancement`, `phase-1`, `chat`, `thanos-effect`
- **Phase liée**: Issue #75 - UI: ChatBox with Thanos effect (Phase 1)

### Critères d'acceptation:
- Exposer méthode `ClearAll()` dans le chatService
- Implémenter l'animation de désintégration avant suppression
- Respect du TTL et des messages épinglés
- Smooth removal pipeline (anim → DOM remove)
- Test de charge avec 1000+ messages

---

## 11. SettingsWindow - Sérialisation/Désérialisation du ViewModel

**Fichier**: `src/Virgil.App/SettingsWindow.xaml.cs`

### TODOs identifiés:
1. **Ligne 25**: `TODO: désérialiser vers un ViewModel si tu en as un`
2. **Ligne 79**: `TODO: si tu as un ViewModel (DataContext), sérialise-le ici :`

### Contexte:
La fenêtre de paramètres charge/sauvegarde la configuration en JSON mais ne passe pas par un ViewModel. Il faut implémenter le databinding propre avec un ViewModel.

### Issue suggérée:
- **Titre**: `[Phase 1] SettingsWindow - Implémenter ViewModel pour databinding propre`
- **Label**: `enhancement`, `phase-1`, `ui`, `settings`, `architecture`
- **Phase liée**: Issue #20 - [Lot 6] Config & Personnalisation

### Critères d'acceptation:
- Créer un `SettingsViewModel` avec toutes les propriétés de configuration
- Implémenter `INotifyPropertyChanged` pour le databinding
- Sérialisation/désérialisation automatique
- Validation des valeurs avant sauvegarde
- Support des paramètres machine vs user
- Annulation des modifications

---

## Résumé des Issues à Créer

### Par Phase:

**Phase 1 (Priorité Haute)**:
1. SystemMonitorService - Métriques complètes
2. AdvancedMonitoringService - Température disque
3. SpecialActionsService - Persistance chat
4. SpecialActionsService - Configuration service
5. MonitoringService - RescanAsync
6. MainShell - Settings window
7. MainWindow.Monitoring - Affichage métriques
8. ChatViewModel - Thanos effect
9. SettingsWindow - ViewModel

**Phase 1-2 (Priorité Moyenne)**:
10. MainShell - HUD toggle
11. MainViewModel - Progress UI

### Par Composant:

- **Monitoring** (5 issues): #1, #2, #5, #8, #10
- **Configuration/Settings** (3 issues): #4, #6, #9
- **Chat** (2 issues): #3, #10
- **UI/UX** (3 issues): #7, #8, #11

---

## Actions Recommandées

1. **Créer les issues GitHub** dans l'ordre de priorité (Phase 1 d'abord)
2. **Ajouter les labels** appropriés pour faciliter le filtrage
3. **Lier aux issues de phase** existantes (#73-#85, #15-#21)
4. **Assigner à des milestones** selon la roadmap
5. **Documenter les dépendances** entre issues (ex: #1 dépend de #2)

---

## Notes

- Certains TODOs peuvent être regroupés en une seule issue si ils concernent le même composant et peuvent être résolus ensemble
- Les critères d'acceptation sont basés sur les spécifications existantes dans les issues #73-#85
- Les estimations de performance sont alignées avec le PROJECT_STATUS.md
