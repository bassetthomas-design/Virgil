# 📌 STATUT DE REPRISE — PROJET VIRGIL (V3)

**Date de mise à jour** : 2025-12-19  
**Repo GitHub** : [https://github.com/bassetthomas-design/Virgil](https://github.com/bassetthomas-design/Virgil)

---

## 🎯 RÉSUMÉ EXÉCUTIF

Le projet Virgil a franchi les **Étapes 1 et 2** du plan de reprise :
- ✅ **Étape 1 (Coverage)** : COVERAGE.md créé et structuré
- ✅ **Étape 2 (Structure catalog)** : 11 packs créés et 77 capabilities distribuées
- 🔄 **Étape 2 (Contenu)** : Catalogue à enrichir avec les capabilities manquantes
- ⏳ **Étape 3+** : Code C# en attente

---

## 📂 STRUCTURE ACTUELLE DU REPO

### Architecture des spécifications (✅ COMPLET)

```
docs/spec/capabilities/
├── README.md                    ✅ Règles fondamentales
├── capabilities.v3.json         ✅ 77 capabilities existantes
├── ai_request.schema.json       ✅ Contrat requête IA
├── ai_response.schema.json      ✅ Contrat réponse IA
├── COVERAGE.md                  ✅ Mapping cahier des charges → capabilities
├── REPRISE_STATUS.md            ✅ Ce fichier
└── catalog/                     ✅ Packs de capabilities
    ├── audit.json               ✅ 3 capabilities (AUDIT)
    ├── browsers.json            ✅ 11 capabilities (CLEANING)
    ├── cleaning.json            ✅ 12 capabilities (CLEANING, DISK)
    ├── network.json             ✅ 2 capabilities (NETWORK)
    ├── performance.json         ✅ 6 capabilities (PERFORMANCE, MONITORING)
    ├── registry.json            ✅ 2 capabilities (REGISTRY)
    ├── repair.json              ✅ 11 capabilities (REPAIR, CONSISTENCY, TROUBLESHOOT)
    ├── security.json            ✅ 3 capabilities (SECURITY, PRIVACY)
    ├── tools.json               ✅ 20 capabilities (multi-domaine)
    ├── uninstall.json           ✅ 2 capabilities (UNINSTALL, APPX)
    └── updates.json             ✅ 5 capabilities (UPDATES)
```

**Total distribué** : 77 capabilities (catalogue enrichi avec le pack navigateurs)

---

## 📊 ÉTAT D'AVANCEMENT PAR ÉTAPE

### ✅ Étape 1 — Coverage (TERMINÉE)

**Fichier** : `docs/spec/capabilities/COVERAGE.md`

**Contenu** :
- Mapping complet du cahier des charges vers les capabilities
- Identification claire des capabilities existantes (✅) vs manquantes (❌)
- Organisation par domaines fonctionnels
- Liste des playbooks prioritaires à créer

**Exemples de TODOs identifiés** :
- Nettoyage : Windows.old, migration leftovers
- Navigateurs : cookies sélectifs, historique, IndexedDB, sessions, extensions
- Performance : priorité processus, affinité CPU, timer resolution
- Sécurité : exclusions suspectes, Secure Boot/TPM, permissions critiques
- Réseau : tests de latence/débit, conflits IP, optimisation DNS/MTU/QoS
- Updates : Microsoft Store, MSI, apps portables, drivers par catégorie
- Outils : analyse dépendances, dump process, crash analysis
- Audit : rapports PDF, historique/undo global
- CLI : mode CLI minimal, mode portable

---

### ✅ Étape 2 — Structure catalog (TERMINÉE)

**Réalisations** :
- 11 fichiers de packs créés dans `catalog/`
- 77 capabilities distribuées par domaine
- Structure JSON validée pour tous les fichiers
- Descriptions mises à jour pour clarifier les packs multi-domaines

**Organisation des packs** :

| Pack | Domaines | Capabilities | Statut |
|------|----------|--------------|--------|
| `audit.json` | AUDIT | 3 | ✅ |
| `browsers.json` | CLEANING | 10 | ✅ |
| `cleaning.json` | CLEANING, DISK | 13 | ✅ |
| `network.json` | NETWORK | 2 | ✅ |
| `performance.json` | PERFORMANCE, MONITORING | 6 | ✅ |
| `registry.json` | REGISTRY | 2 | ✅ |
| `repair.json` | REPAIR, CONSISTENCY, TROUBLESHOOT | 11 | ✅ |
| `security.json` | SECURITY, PRIVACY | 3 | ✅ |
| `tools.json` | TOOLS, USERPROFILES, SERVICES, STARTUP, TASKSCHED, HARDWARE, LOGGING, META | 20 | ✅ |
| `uninstall.json` | UNINSTALL, APPX | 2 | ✅ |
| `updates.json` | UPDATES | 5 | ✅ |

---

### 🔄 Étape 2 — Enrichissement catalog (EN COURS)

**Objectif** : Ajouter les capabilities manquantes identifiées dans COVERAGE.md

**Prochaines actions recommandées** :

#### Priorité HAUTE
1. **Pack browsers** (browsers.json)
   - ✅ 11 capabilities ajoutées (cache global, cookies, historique, stockage, sessions, extensions)
   - Continuer l'enrichissement si de nouvelles actions navigateur sont identifiées

2. **Pack cleaning** (cleaning.json)
   - CLEAN_WINDOWS_OLD ❌
   - CLEAN_MIGRATION_LEFTOVERS ❌
   - TELEMETRY_CLEAN_ADVANCED ❌

3. **Pack security** (security.json)
   - DEFENDER_SCAN_FULL ❌
   - DEFENDER_SCAN_OFFLINE ❌
   - DEFENDER_RESET ❌
   - SECURITY_LIST_SUSPICIOUS_EXCLUSIONS ❌
   - SECURITY_REMOVE_EXCLUSIONS ❌
   - SECURITY_SECUREBOOT_TPM_AUDIT ❌
   - SECURITY_PERMISSIONS_AUDIT ❌

#### Priorité MOYENNE
4. **Pack performance** (performance.json)
   - PERF_SET_PROCESS_PRIORITY ❌
   - PERF_SET_CPU_AFFINITY ❌
   - PERF_TIMER_RESOLUTION ❌
   - STARTUP_DELAYED_INTELLIGENT ❌
   - PERF_DETECT_MEMORY_LEAKS ❌

5. **Pack network** (network.json)
   - NETWORK_BASIC_DIAGNOSIS ❌
   - NETWORK_PERF_TEST ❌
   - NETWORK_IP_CONFLICT_DETECT ❌
   - NETWORK_OPTIMIZE_DNS_MTU_QOS ❌

6. **Pack updates** (updates.json)
   - UPDATE_MICROSOFT_STORE ❌
   - UPDATE_MSI_DETECT ❌
   - UPDATE_PORTABLE_APPS_DETECT ❌
   - UPDATE_DRIVERS_ENUMERATE ❌
   - HARDWARE_BIOS_INFO ❌

#### Priorité BASSE
7. **Pack tools** (tools.json)
   - TOOLS_ANALYZE_DEPENDENCIES ❌
   - TOOLS_DUMP_PROCESS ❌
   - TOOLS_CRASH_ANALYSIS ❌

8. **Pack audit** (audit.json)
   - REPORT_PDF_GENERATE ❌
   - GLOBAL_HISTORY_STORE ❌
   - ROLLBACK_PLAYBOOKS ❌

9. **Meta capabilities**
   - META_CLI_MINIMAL ❌
   - META_PORTABLE_FULL ❌

---

### ⏳ Étape 3 — Loader C# (À FAIRE)

**Objectif** : Premier code C# pour charger et valider le catalogue

**Tâches** :
- [ ] Créer `CapabilityLoader.cs` dans `Virgil.Core`
- [ ] Lire `capabilities.v3.json` et tous les fichiers `catalog/*.json`
- [ ] Valider contre les schémas JSON
- [ ] Exposer au runtime via service
- [ ] **Aucune exécution** à ce stade

---

### ⏳ Étape 4 — Policy Layer (À FAIRE)

**Objectif** : Implémenter les niveaux de risque et confirmations

**Tâches** :
- [ ] Implémenter les niveaux CORE / ADVANCED / EXPERT / BONUS
- [ ] Verrouiller EXPERT par défaut
- [ ] Système de confirmations utilisateur
- [ ] Règles de politique par niveau de risque

---

### ⏳ Étape 5 — Dry-run Engine (À FAIRE)

**Objectif** : Simulation des actions

**Tâches** :
- [ ] Implémenter `simulate()` pour chaque capability
- [ ] Logs détaillés de ce qui serait fait
- [ ] Tests unitaires de dry-run

---

### ⏳ Étape 6 — Première exécution réelle (À FAIRE)

**Objectif** : Implémenter UNE capability complète

**Exemple** : CLEAN_TEMP_USER
- [ ] Implémentation réelle
- [ ] Dry-run fonctionnel
- [ ] Logs
- [ ] Tests

---

### ⏳ Étape 7 — IA locale COPILOT (À FAIRE)

**Objectif** : Intégrer l'IA en mode copilote

**Tâches** :
- [ ] Analyse de l'état système
- [ ] Recommandations textuelles
- [ ] Génération de plans (liste d'IDs)
- [ ] **JAMAIS d'exécution directe**

---

## 🚀 POUR REPRENDRE LE TRAVAIL

### Si vous voulez continuer l'enrichissement du catalogue :

```
Je travaille sur Virgil.
Repo : https://github.com/bassetthomas-design/Virgil

État actuel :
- 77 capabilities existantes distribuées dans 11 packs
- COVERAGE.md identifie encore des capabilities manquantes
- Structure catalog/ prête

Objectif : ajouter les capabilities manquantes dans les packs appropriés.
Commencer par les priorités hautes restantes (cleaning/security/performance).
```

### Si vous voulez passer au code C# :

```
Je travaille sur Virgil.
Repo : https://github.com/bassetthomas-design/Virgil

État actuel :
- Catalogue V3 complet (docs/spec/capabilities/)
- 77 capabilities existantes + structure pour enrichissement
- Aucun code C# d'intégration encore

Objectif : créer le CapabilityLoader (Étape 3) pour charger
et valider le catalogue JSON en C#.
```

---

## 📋 CHECKLIST DE VALIDATION

Avant de passer à l'étape suivante, vérifier :

### Étape 1 ✅
- [x] COVERAGE.md existe
- [x] Tous les domaines du cahier des charges sont mappés
- [x] Capabilities manquantes identifiées avec ❌

### Étape 2 (Structure) ✅
- [x] Dossier catalog/ existe
- [x] 11 packs JSON créés
- [x] Tous les JSON sont valides
- [x] 77 capabilities distribuées (100%)

### Étape 2 (Contenu) 🔄
- [x] Pack browsers complété
- [ ] Capabilities de nettoyage avancé ajoutées
- [ ] Capabilities de sécurité complètes
- [ ] Capabilities de performance ajoutées
- [ ] Capabilities réseau ajoutées
- [ ] Capabilities updates complétées
- [ ] Outils sysadmin ajoutés
- [ ] Features audit/reporting ajoutées
- [ ] Meta capabilities (CLI/portable) ajoutées

### Étape 3 ⏳
- [ ] CapabilityLoader.cs créé
- [ ] Validation JSON fonctionnelle
- [ ] Tests unitaires pour le loader
- [ ] Documentation du loader

---

## 🎓 PRINCIPES À RESPECTER

1. **Aucune action libre** : Toutes les actions système sont des capabilities IDées
2. **IA en cage** : L'IA propose, le moteur exécute
3. **Dry-run d'abord** : Toute capability supportant dry-run doit l'implémenter
4. **Rollback prévu** : Les actions à risque ont un plan de retour arrière
5. **Niveaux de risque** : LOW / MEDIUM / HIGH / CRITICAL bien définis
6. **Expert verrouillé** : Les capabilities EXPERT nécessitent confirmation

---

## 📞 CONTACTS ET RESSOURCES

- **Repo GitHub** : https://github.com/bassetthomas-design/Virgil
- **Doc principale** : `docs/spec/capabilities/README.md`
- **Coverage** : `docs/spec/capabilities/COVERAGE.md`
- **Catalogue** : `docs/spec/capabilities/capabilities.v3.json`
- **Packs** : `docs/spec/capabilities/catalog/*.json`

---

**Dernière mise à jour** : 2025-12-19
**Prochaine action recommandée** : Poursuivre l'enrichissement des packs cleaning/security/performance après l'ajout du pack navigateurs
