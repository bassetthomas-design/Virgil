# Backlog des issues à créer (phase P0/P1/P2)

Ce fichier liste les issues GitHub à ouvrir avec une checklist détaillée pour chacune, conformément au plan d'atterrissage Virgil offline.

## [P0] Startup stability + no-crash asset loading
- [ ] Vérifier et corriger les bindings TwoWay/OneWay qui peuvent créer des InvalidOperationException au chargement.
- [ ] Retarder les initialisations HUD/monitoring au `DispatcherPriority.Loaded` pour éviter les owners null.
- [ ] Encapsuler tous les chargements d'assets (avatars/modèle/prompt) dans un `try/catch` avec log et fallback.
- [ ] Assurer que l'absence d'un asset ne provoque jamais un throw bloquant (retourner un placeholder).
- [ ] Journaliser dans `%APPDATA%/Virgil/logs` les erreurs de démarrage, sans stopper l'app.

## [P0] Chat UI visible + send UX + busy indicator
- [ ] Recomposer `MainShell.xaml` pour donner une vraie place au panneau de chat (messages + input visibles sans scroll initial).
- [ ] Ajouter TextBox multi‑ligne avec Enter=Send / Shift+Enter=nouvelle ligne.
- [ ] Désactiver le bouton Send et afficher un indicateur "Virgil réfléchit…" quand `IsBusy` est vrai.
- [ ] Styler les bulles (utilisateur/assistant) et header Virgil (mini avatar + badge mood).
- [ ] Vérifier que `ChatViewModel` expose `ObservableCollection<ChatMessage>`, `InputText`, `SendCommand`, `IsBusy`.

## [P0] Theme refresh: palette plus claire centralisée
- [ ] Créer `Themes/Theme.xaml` avec brosses `AppBackgroundBrush`, `PanelBackgroundBrush`, `CardBackgroundBrush`, `TextPrimaryBrush`, `TextSecondaryBrush`, `AccentBrush`, `WarningBrush`, `CriticalBrush`.
- [ ] Remplacer les couleurs hardcodées des vues (ex: `ChatView`, `AvatarView`, `MainShell`) par des `DynamicResource`.
- [ ] Aligner App.xaml pour merger la nouvelle ResourceDictionary et alléger le rendu (moins sombre).
- [ ] Vérifier que les contrôles communs (TextBlock, Button, Border) héritent des nouvelles brosses.

## [P1] Local LLM engine offline
- [ ] Définir `IChatEngine` + `ChatContext` + `ChatEngineResult` + `ChatCommand` (JSON strict).
- [ ] Implémenter `LocalLlmChatEngine` branché sur un runner `llama.cpp` embarqué ou wrapper .NET offline.
- [ ] Charger le modèle GGUF depuis `assets/models/virgil-model.gguf` sans téléchargement runtime.
- [ ] Lire `assets/prompts/system_prompt.txt` pour le prompt système.
- [ ] Prévoir un fallback `RuleBasedChatEngine` si le modèle est absent ou corrompu (pas de crash).

## [P1] AI output schema: JSON strict + parser
- [ ] Forcer le format de sortie `{ "text": "...", "command": { "type": "none" } }` ou `{ "type": "action", ... }`.
- [ ] Parser strict JSON; en cas d'échec, fallback vers texte brut + `command.type=none` et log.
- [ ] Normaliser la sélection des commandes (nettoyage du texte, trimming) pour éviter les dérives.
- [ ] Couvrir le parser par tests unitaires (valid/invalid payloads).

## [P1] AI -> Actions bridge (whitelist + confirmations)
- [ ] Définir la whitelist: `status`, `monitor_toggle`, `monitor_rescan`, `clean_quick`, `clean_browsers`, `maintenance_full`, `open_settings`, `show_hud`, `hide_hud`.
- [ ] Dans `ChatViewModel`, router `command.type === "action"` vers `ActionOrchestrator` uniquement si whitelist.
- [ ] Ajouter confirmation (OK/Cancel ou flux chat) pour `clean_*` et `maintenance_full`.
- [ ] Afficher dans le chat le résultat succès/échec de chaque action.
- [ ] Journaliser tout refus (commande hors whitelist) sans interrompre la conversation.

## [P2] Packaging offline complet
- [ ] Préparer packaging (MSIX ou Inno Setup) incluant exe + dll + `assets/virgil`, `assets/prompts`, `assets/models`, runner `llama` éventuel.
- [ ] Vérifier que l'install ne tente aucun téléchargement et reste fonctionnelle offline.
- [ ] Documenter la taille install + prérequis CPU/RAM/AVX.

## [P2] Polish avatar + micro animations
- [ ] Garantir un affichage net (grande vignette sidebar + mini header chat) avec états normal/stress/critical.
- [ ] Ajouter éventuellement blink/glow léger (cycle 6‑12s) sans surcharge GPU/CPU.
- [ ] Documenter les assets utilisés et leur fallback (placeholder embarqué si fichier manquant).

## [P2] Tests et métriques de robustesse
- [ ] Ajouter tests d'ouverture (smoke) pour vérifier que l'app démarre sans assets ou config.
- [ ] Mesurer le temps de démarrage et tracer les erreurs d'assets manquants.
- [ ] Vérifier la résilience du logging (%APPDATA%/Virgil/logs) en cas d'accès disque restreint.
