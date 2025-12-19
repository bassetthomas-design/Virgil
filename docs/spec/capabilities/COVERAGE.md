# COVERAGE — Mapping du cahier des charges vers les capabilities (V3)

Date : 2025-12-16  
Auteur : template généré automatiquement

But : cartographier chaque option du cahier des charges vers une capability existante, plusieurs capabilities, ou un playbook. Indiquer l'état pour chaque item :
- ✅ = capability existante dans capabilities.v3.json
- ⚠️ = partiellement couverte (ajouts nécessaires)
- ❌ = non couverte — à ajouter au catalogue
- 🔒 = nécessite policy / confirmation (Expert, risques élevés)

Instructions :
1. Pour chaque ligne, renseigner l'ID exact (ou les IDs) si déjà présent(e).
2. Si plusieurs capabilities forment la fonctionnalité, lister les IDs dans l'ordre d'exécution prévu.
3. Si nécessaire, indiquer le rollback prévu et si la capability est dry-run.
4. Valider les entrées en PR séparée par pack/domain.

Sommaire
- [Nettoyage avancé](#nettoyage-avanc%C3%A9)
- [Navigateurs](#navigateurs)
- [Performance / optimisation](#performance--optimisation)
- [Sécurité / intégrité](#s%C3%A9curit%C3%A9--int%C3%A9grit%C3%A9)
- [Réseau](#r%C3%A9seau)
- [Updates / installations / drivers](#updates--installations--drivers)
- [Outils sysadmin](#outils-sysadmin)
- [Registry / configuration système](#registry--configuration-syst%C3%A8me)
- [Audit / reporting / historique](#audit--reporting--historique)
- [CLI / portable / déploiement](#cli--portable--d%C3%A9ploiement)
- [Playbooks transverses](#playbooks-transverses)
- [Glossaire / conventions](#glossaire--conventions)

---

## Nettoyage avancé
- Windows.old -> ❌ (TODO : capability CLEAN_WINDOWS_OLD, risk=MEDIUM, dry_run=true, rollback=NONE)
- leftovers migration -> ❌ (TODO : CLEAN_MIGRATION_LEFTOVERS)
- télémétrie / diagnostic fin -> ⚠️ (partiel : TELEMETRY_CLEAN_BASIC exists? vérifier)
- nettoyage navigateur complet (cookies, historique, IndexedDB, sessions…) -> ⚠️ (voir section Navigateurs)
- Temp utilisateur -> ✅ CLEAN_TEMP_USER (vérifier flags : dry_run=true / rollback=NONE)

Notes :
- Préciser exclusions et critères d'âge/fichiers verrouillés pour chaque capability.

## Navigateurs
- Cache global -> ✅ CLEAN_BROWSER_CACHE_ALL
- Cache par profil -> ✅ CLEAN_BROWSER_CACHE_PER_PROFILE (browsers.json)
- Cookies sélectifs -> ✅ CLEAN_BROWSER_COOKIES_SELECTIVE (browsers.json)
- Historique -> ✅ CLEAN_BROWSER_HISTORY (browsers.json)
- LocalStorage -> ✅ CLEAN_BROWSER_STORAGE_LOCAL (browsers.json)
- SessionStorage -> ✅ CLEAN_BROWSER_STORAGE_SESSION (browsers.json)
- Sessions avec préservation connexions -> ✅ CLEAN_BROWSER_SESSIONS_PRESERVE_LOGGED_IN (browsers.json)
- Extensions (liste/nettoyage) -> ✅ CLEAN_BROWSER_EXTENSIONS_LIST (browsers.json)
- Profils inactifs -> ✅ CLEAN_BROWSER_PROFILES_INACTIVE (browsers.json)
- Liste téléchargements -> ✅ CLEAN_BROWSER_DOWNLOADS_LIST (browsers.json)
- Données auto-remplissage formulaires -> ✅ CLEAN_BROWSER_FORM_AUTOFILL (browsers.json)
- Modes : Soft / Smart / Deep / Nuclear -> ⚠️ (définir playbooks combinant capabilities ci-dessus)

## Performance / optimisation
- Priorité processus -> ❌ (TODO : PERF_SET_PROCESS_PRIORITY)
- Affinité CPU -> ❌ (TODO : PERF_SET_CPU_AFFINITY)
- Timer resolution -> ❌ (TODO)
- Delayed startup intelligent -> ❌ (TODO)
- Profils services + dépendances + reset services critiques -> ⚠️ (partiel)
- Détection leaks mémoire -> ❌ (TODO : require diagnostics tooling)

## Sécurité / intégrité
- Defender scan complet / offline / reset -> ⚠️ (partiel : check EXISTENCE of DEFENDER_SCAN capabilities)
- Exclusions suspectes -> ❌ (TODO : SECURITY_LIST_SUSPICIOUS_EXCLUSIONS + REMOVE)
- Secure Boot / TPM checks -> ❌ (TODO : SECURITY_SECUREBOOT_TPM_AUDIT)
- Permissions critiques -> ❌ (TODO : SECURITY_PERMISSIONS_AUDIT)

## Réseau
- Diagnostic IP/DNS/GW -> ⚠️ (partiel : NETWORK_BASIC_DIAGNOSIS?)
- Latence / débit -> ❌ (TODO : NETWORK_PERF_TEST)
- Conflits d'IP -> ❌ (TODO)
- Optimisation DNS / MTU / QoS -> ❌ (TODO)

## Updates / installations / drivers
- Microsoft Store updates -> ❌ (TODO)
- MSI detection / management -> ❌ (TODO)
- Apps portables détectées -> ❌ (TODO)
- Drivers par catégorie -> ❌ (TODO : UPDATE_DRIVERS_ENUMERATE)
- BIOS/UEFI info + lien -> ❌ (TODO : HARDWARE_BIOS_INFO)

## Outils sysadmin
- Analyse dépendances (services/processus->fichiers) -> ❌ (TODO)
- Dump process -> ❌ (TODO)
- Crash analysis avancée -> ❌ (TODO)
- Gestion services / tâches avancée -> ⚠️ (partiel)

## Registry / configuration système
- Nettoyage clés orphelines -> ❌ (TODO)
- Export / backup registry -> ✅/⚠️ (vérifier existence)
- Correction valeurs systèmes critiques -> 🔒 (Expert)

## Audit / reporting / historique
- Génération rapports PDF -> ❌ (TODO : REPORT_PDF_GENERATE)
- Historique / undo global -> ❌ (TODO : GLOBAL_HISTORY_STORE + ROLLBACK_PLAYBOOKS)
- Logs d'audit immuables -> ⚠️ (partiel)

## CLI / portable / déploiement
- Mode CLI minimal -> ❌ (TODO)
- Portable (sans install) -> ❌ (TODO)
- Switch admin-guided -> ⚠️ (partiel)

## Playbooks transverses (exemples)
- "Clean browser deep" -> playbook : [CLEAN_BROWSER_STORAGE_LOCAL, CLEAN_BROWSER_STORAGE_SESSION, CLEAN_BROWSER_HISTORY, CLEAN_BROWSER_COOKIES_SELECTIVE, CLEAN_BROWSER_EXTENSIONS_LIST, CLEAN_BROWSER_CACHE_PER_PROFILE]
  - état : ✅ (capabilities disponibles dans browsers.json, playbook à créer)
- "System health check" -> playbook : [PERF_BASIC_CHECK, NETWORK_BASIC_DIAGNOSIS, SECURITY_PERMISSIONS_AUDIT]
  - état : ⚠️ (certains éléments manquent)

---

## Playbooks prioritaires à créer en premier (recommandé)
1. Coverage navigateur complet -> regrouper toutes les capabilities navigateur (priorité haute)
2. Coverage nettoyage avancé -> Windows.old, Temp, migration leftovers
3. Coverage security baseline -> Defender, exclusions, permissions
4. Loader spec (manifest) -> s'assurer que capabilities.v3.json référence les packs

---

## Validation & process
- Pour chaque pack ajouté : valider via JSON Schema (ai_request / ai_response si nécessaire)
- PR = 1 pack (catalog/*.json) + mise à jour capabilities.v3.json + tests schema

---

## Historique des modifications
- 2025-12-16 : Création initiale (template)
- 2025-12-19 : Ajout de 10 capabilities navigateurs dans catalog/browsers.json (CLEAN_BROWSER_COOKIES_SELECTIVE, CLEAN_BROWSER_HISTORY, CLEAN_BROWSER_STORAGE_LOCAL, CLEAN_BROWSER_STORAGE_SESSION, CLEAN_BROWSER_EXTENSIONS_LIST, CLEAN_BROWSER_CACHE_PER_PROFILE, CLEAN_BROWSER_SESSIONS_PRESERVE_LOGGED_IN, CLEAN_BROWSER_PROFILES_INACTIVE, CLEAN_BROWSER_DOWNLOADS_LIST, CLEAN_BROWSER_FORM_AUTOFILL)

---

Fin du fichier — remplir chaque ligne avec les IDs exacts et statuts, puis ouvrir PRs pack par pack.
