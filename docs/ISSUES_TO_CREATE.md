# Guide de Création des Issues GitHub pour les TODOs

Ce document fournit un guide étape par étape pour créer les issues GitHub basées sur les TODOs identifiés dans le code.

## Vue d'ensemble

**13 TODOs** ont été identifiés dans le code source, qui doivent être convertis en **11 issues GitHub** (certains TODOs connexes sont regroupés).

Le document détaillé `TODO_TRACKING.md` contient toutes les informations nécessaires pour chaque issue.

## Issues à Créer (par priorité)

### Phase 1 - Priorité Haute

#### Issue 1: SystemMonitorService - Métriques complètes
```
Titre: [Phase 1] SystemMonitorService - Implement complete metrics
Labels: enhancement, phase-1, monitoring
Liée à: #74

Description:
Le SystemMonitorService doit collecter et exposer les métriques système réelles.

Fichiers concernés:
- src/Virgil.App/Services/SystemMonitorService.cs (lignes 11, 46)

Critères d'acceptation:
- [ ] Collecte réelle du CPU usage via PerformanceCounter
- [ ] Collecte de la RAM usage (utilisée/totale)
- [ ] Ajout de GPU usage (via LibreHardwareMonitor)
- [ ] Ajout de Disk I/O
- [ ] Intégration des températures (CPU, GPU)
- [ ] Refresh 1-2 secondes
- [ ] Performance < 1% CPU
- [ ] Pas de fuite mémoire après 30 min
```

#### Issue 2: AdvancedMonitoringService - Température disque
```
Titre: [Phase 1] AdvancedMonitoringService - Ajouter support température disque
Labels: enhancement, phase-1, monitoring, hardware
Liée à: #74

Description:
Intégrer la lecture de température des disques via smartctl ou LibreHardwareMonitor.

Fichiers concernés:
- src/Virgil.Core/Services/AdvancedMonitoringService.cs (ligne 24)

Critères d'acceptation:
- [ ] Intégrer LibreHardwareMonitor pour SMART
- [ ] Lire température du disque principal (C:)
- [ ] Gestion gracieuse si SMART indisponible
- [ ] Impact performance < 0.5% CPU
- [ ] Compatible SSD et HDD
```

#### Issue 3: Chat - Persistance et purge de l'historique
```
Titre: [Phase 1] Implémenter persistance et purge de l'historique de chat
Labels: enhancement, phase-1, chat, thanos-effect
Liée à: #75

Description:
Créer un service de persistance du chat et implémenter la purge complète (effet Thanos).

Fichiers concernés:
- src/Virgil.App/Services/SpecialActionsService.cs (ligne 30)

Critères d'acceptation:
- [ ] Service de persistance du chat
- [ ] Purge réelle de l'historique
- [ ] Coordination avec animation Thanos UI
- [ ] Respect des messages épinglés
- [ ] Confirmation utilisateur
```

#### Issue 4: Configuration - Rechargement dynamique
```
Titre: [Phase 1] Implémenter rechargement dynamique de la configuration
Labels: enhancement, phase-1, configuration
Liée à: #20

Description:
Créer un service de configuration centralisé avec rechargement sans redémarrage.

Fichiers concernés:
- src/Virgil.App/Services/SpecialActionsService.cs (ligne 40)

Critères d'acceptation:
- [ ] Service de configuration centralisé
- [ ] Rechargement sans redémarrage
- [ ] Notification des composants
- [ ] Validation avant application
- [ ] Gestion des erreurs
```

#### Issue 5: MonitoringService - Méthode RescanAsync
```
Titre: [Phase 1] MonitoringService - Exposer méthode RescanAsync
Labels: enhancement, phase-1, monitoring
Liée à: #74

Description:
Ajouter une méthode pour déclencher un refresh manuel complet des métriques.

Fichiers concernés:
- src/Virgil.App/Services/SpecialActionsService.cs (ligne 50)

Critères d'acceptation:
- [ ] Méthode RescanAsync() exposée
- [ ] Force mise à jour immédiate
- [ ] Réinitialisation des compteurs
- [ ] Notification des observateurs
- [ ] Pas de conflit avec polling normal
```

#### Issue 6: MainShell - Connexion bouton paramètres
```
Titre: [Phase 1] MainShell - Connecter bouton paramètres à SettingsWindow
Labels: enhancement, phase-1, ui, settings
Liée à: #20

Description:
Implémenter l'ouverture de la fenêtre de paramètres depuis le bouton.

Fichiers concernés:
- src/Virgil.App/Views/MainShell.xaml.cs (ligne 25)

Critères d'acceptation:
- [ ] Ouverture de SettingsWindow
- [ ] Gestion cycle de vie fenêtre
- [ ] Application temps réel si possible
- [ ] Boutons fermer/annuler/sauvegarder
- [ ] Design cohérent
```

#### Issue 7: MainWindow - Affichage métriques système
```
Titre: [Phase 1] MainWindow.Monitoring - Affichage métriques système
Labels: enhancement, phase-1, ui, monitoring
Liée à: #74, #16

Description:
Connecter l'UI aux services de monitoring et afficher les métriques en temps réel.

Fichiers concernés:
- src/Virgil.App/MainWindow.Monitoring.cs (ligne 15)

Critères d'acceptation:
- [ ] Connexion aux services monitoring
- [ ] Affichage temps réel CPU/GPU/RAM/Temp
- [ ] Refresh 1-2 secondes
- [ ] Jauges/graphiques visuels
- [ ] Seuils colorés (vert/jaune/rouge)
- [ ] Pas de freeze UI
```

#### Issue 8: ChatViewModel - Effet Thanos
```
Titre: [Phase 1] ChatViewModel - Connecter effet Thanos à chatService
Labels: enhancement, phase-1, chat, thanos-effect
Liée à: #75

Description:
Implémenter la méthode SnapAll() avec animation de désintégration.

Fichiers concernés:
- src/Virgil.App/ViewModels/ChatViewModel.Thanos.cs (ligne 5)

Critères d'acceptation:
- [ ] Méthode ClearAll() dans chatService
- [ ] Animation de désintégration
- [ ] Respect TTL et messages épinglés
- [ ] Smooth removal pipeline
- [ ] Test 1000+ messages
```

#### Issue 9: SettingsWindow - ViewModel avec databinding
```
Titre: [Phase 1] SettingsWindow - Implémenter ViewModel
Labels: enhancement, phase-1, ui, settings, architecture
Liée à: #20

Description:
Créer un SettingsViewModel pour databinding propre au lieu de JSON direct.

Fichiers concernés:
- src/Virgil.App/SettingsWindow.xaml.cs (lignes 25, 79)

Critères d'acceptation:
- [ ] SettingsViewModel créé
- [ ] INotifyPropertyChanged implémenté
- [ ] Sérialisation/désérialisation auto
- [ ] Validation des valeurs
- [ ] Support machine vs user settings
- [ ] Annulation des modifications
```

### Phase 1-2 - Priorité Moyenne

#### Issue 10: MainShell - Toggle mini HUD
```
Titre: [Phase 1-2] MainShell - Implémenter toggle du mini HUD
Labels: enhancement, phase-2, ui, hud
Liée à: #15

Description:
Définir et implémenter le mini HUD avec affichage/masquage.

Fichiers concernés:
- src/Virgil.App/Views/MainShell.xaml.cs (ligne 33)

Critères d'acceptation:
- [ ] Définir concept mini HUD
- [ ] Implémentation affichage/masquage
- [ ] Persistance état activé/désactivé
- [ ] Position/taille configurables
- [ ] Pas d'impact performances
```

#### Issue 11: MainViewModel - Indicateur de progression
```
Titre: [Phase 2] MainViewModel - Implémenter indicateur de progression UI
Labels: enhancement, phase-2, ui, progress
Liée à: #19

Description:
Créer un composant de progression pour les opérations longues.

Fichiers concernés:
- src/Virgil.App/ViewModels/MainViewModel.LegacyStubs.cs (ligne 10)

Critères d'acceptation:
- [ ] Composant barre de progression
- [ ] Affichage pourcentage et texte
- [ ] Support opérations longues
- [ ] Possibilité d'annulation
- [ ] Animation fluide
```

## Ordre de Création Recommandé

1. **Monitoring** (Issues #1, #2, #5) - Fondation du système
2. **Configuration** (Issues #4, #6, #9) - Infrastructure nécessaire
3. **Chat/Thanos** (Issues #3, #8) - Feature visible
4. **UI/Progress** (Issues #7, #10, #11) - Polish UX

## Labels à Créer (si non existants)

- `phase-1`, `phase-2` (pour tracking des phases)
- `monitoring`, `configuration`, `chat`, `ui`, `settings`
- `thanos-effect`, `hardware`, `architecture`, `progress`

## Script d'Aide

Un template est fourni ci-dessus pour chaque issue. Vous pouvez:
1. Copier le template
2. Créer l'issue via l'interface GitHub
3. Ajouter les labels appropriés
4. Lier à l'issue de phase correspondante

## Suivi

Une fois les issues créées:
- [ ] Mettre à jour TODO_TRACKING.md avec les numéros d'issues
- [ ] Créer un milestone pour Phase 1 si nécessaire
- [ ] Ajouter aux projets GitHub si utilisés
- [ ] Prioriser dans le backlog

## Références

- Document détaillé: `docs/TODO_TRACKING.md`
- Issues de phase: #73-#85 (nouvelles phases détaillées)
- Issues de lot: #15-#21 (anciennes phases)
- Statut projet: `PROJECT_STATUS.md`
