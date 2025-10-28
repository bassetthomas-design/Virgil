# Virgil — Roadmap d’implémentation

> Cette checklist découpe la spec en lots livrables. Coche au fur et à mesure et/OU crée une issue par case.

## Lot 0 — Base & Qualité
- [ ] Activer journalisation centralisée `%AppData%\Virgil\logs`
- [ ] Gestion erreurs globales (App.xaml.cs) + crash log `Virgil_crash.log`
- [ ] Vérif UAC/droits admin avant opérations sensibles + messages clairs

## Lot 1 — UI/UX squelette
- [ ] Barre du haut (horloge temps réel, toggle Surveillance)
- [ ] Panneau gauche (avatar + jauges CPU/GPU/RAM/Disque)
- [ ] Températures (CPU/GPU/Disque) avec seuils colorés
- [ ] Zone Actions (6 boutons)
- [ ] Zone Chat (list + couleurs selon humeur)

## Lot 2 — Monitoring & Humeurs
- [ ] Refresh 1–2 s usages/temperatures
- [ ] Humeurs dynamiques (happy/focused/warn/alert/sleepy/…)
- [ ] Punchlines auto toutes 1–6 min en surveillance
- [ ] Alerte seuils (chat + color UI)

## Lot 3 — Nettoyage Intelligent
- [ ] Heuristique choix Simple/Complet/Pro
- [ ] Nettoyages: Corbeille, TEMP, Prefetch, logs
- [ ] Caches navigateurs (Chrome/Edge/Brave/Opera/OperaGX/Vivaldi/Firefox)
- [ ] Caches système (DX Shader, WER, Adobe, etc.)
- [ ] Statistiques (trouvés/supprimés) + chat
- [ ] **Effet Thanos**: compte à rebours 60 s + anim UI

## Lot 4 — Mises à jour Totales
- [ ] Winget upgrade `--all --include-unknown`
- [ ] Pilotes (best effort via winget/OEM)
- [ ] Windows Update (UsoClient: scan/download/install)
- [ ] Defender: MAJ signatures + scan rapide
- [ ] Rendu chat étape par étape (journal)

## Lot 5 — Maintenance complète
- [ ] Scénario qui enchaîne: Nettoyage Intelligent → Navigateurs → Mises à jour
- [ ] Rapport final + stats

## Lot 6 — Configuration & Personnalisation
- [ ] Fichiers config machine/user + fusion (user override)
- [ ] Seuils CPU/RAM/temp + fréquences + style/humeur + thème
- [ ] Commande “Ouvrir configuration” (édition guidée)

## Lot 7 — Tests & Stabilisation
- [ ] Tests unitaires cœur (services de nettoyage, update, heuristique)
- [ ] Tests intégration (scénarios)
- [ ] Optimisations perfs + UI polish
