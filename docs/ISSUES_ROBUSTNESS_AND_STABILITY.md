# Issues de Robustesse et Stabilité pour Virgil

Ce document contient 24 issues détaillées prêtes à être créées dans GitHub. Elles couvrent tous les aspects nécessaires pour garantir une release stable sans crash de l'application Virgil.

**Stack technologique**: .NET 8, C#, WPF, Windows x64

**Instructions**: Pour chaque issue ci-dessous, créez une nouvelle issue GitHub avec le titre, la description, et les labels indiqués.

---

## Table des Matières

- **Groupe A - Robustesse Critique** (Issues 1-4)
- **Groupe B - Tests & Coverage** (Issues 5-7)
- **Groupe C - CI/CD & Déploiement** (Issues 8-10)
- **Groupe D - Monitoring & Observabilité** (Issues 11-13)
- **Groupe E - Data Integrity & Backup** (Issues 14-15)
- **Groupe F - Sécurité & Secrets** (Issues 16-18)
- **Groupe G - Frontend & UX** (Issues 19-20)
- **Groupe H - Documentation & Runbooks** (Issues 21-23)
- **Bonus - Resilience Testing** (Issue 24)

---

## Groupe A — Robustesse Critique & Prévention de Crash

### Issue #1: Graceful Shutdown & Gestion des Signaux (SIGTERM/SIGINT)

**Description**:  
Implémenter l'arrêt gracieux de l'application Virgil : refuser nouvelles opérations critiques pendant le shutdown, terminer les opérations en cours, fermer proprement les ressources (fichiers, connexions, services Windows), et exit avec code approprié.

**Pourquoi**:  
Les arrêts forcés de l'application (fermeture Windows, update système) peuvent causer des corruptions de données si l'app est tuée immédiatement.

**Critères d'acceptation**:
- [ ] L'application capture SessionEnding et ProcessExit
- [ ] Nouvelles opérations bloquées pendant shutdown
- [ ] Opérations en cours terminées proprement (avec timeout)
- [ ] Toutes ressources (fichiers, mutex, services) libérées avant exit
- [ ] Test automatisé simulant arrêt forcé

**Estimation**: 0.5–1 jour  
**Labels**: `backend`, `reliability`, `high`, `enhancement`

---

### Issue #2: Circuit Breaker et Retries Intelligents pour Appels Externes

**Description**:  
Ajouter couche de résilience pour dépendances externes : retry avec backoff exponentiel, circuit breaker, timeouts stricts.

**Pourquoi**:  
Éviter crashes/blocages quand services externes (Windows services, APIs) sont lents/indisponibles.

**Critères d'acceptation**:
- [ ] Timeouts configurables par opération
- [ ] Retry avec backoff exponentiel + jitter
- [ ] Circuit breaker après X échecs consécutifs
- [ ] Logs d'état du circuit breaker
- [ ] Tests simulant latence/erreurs

**Notes d'implémentation**:  
Utiliser NuGet package **Polly** (v8.x) pour .NET.

**Estimation**: 1–2 jours  
**Labels**: `backend`, `reliability`, `medium`, `enhancement`

---

### Issue #3: Validation Forte des Entrées et Protection contre Payloads Malformés

**Description**:  
Centraliser validation des inputs utilisateur (UI, fichiers config, arguments CLI). Protéger contre chemins invalides, strings très longues, caractères spéciaux.

**Pourquoi**:  
Éviter plantages causés par entrées inattendues (path traversal, buffer overflows, injection).

**Critères d'acceptation**:
- [ ] Validation centralisée pour tous inputs
- [ ] Limites de taille sur strings et collections
- [ ] Validation chemins (longueur max, caractères interdits, path traversal)
- [ ] Messages d'erreur clairs et non techniques
- [ ] Tests couvrant cas limites (paths >260 chars, Unicode, null/empty)

**Estimation**: 1–2 jours  
**Labels**: `backend`, `security`, `high`, `enhancement`

---

### Issue #4: Gestion Centralisée d'Erreurs & Responses Uniformes

**Description**:  
Système global de gestion d'erreurs capturant toutes exceptions non gérées, avec logs enrichis et messages utilisateur appropriés.

**Pourquoi**:  
Facilite debug et évite crashes complets. Améliore UX en transformant erreurs techniques en messages actionnables.

**Critères d'acceptation**:
- [ ] Tous throws non catchés capturés au niveau Application
- [ ] Logs structurés avec stack trace complète
- [ ] UI affiche message clair sans stack trace
- [ ] Option "Rapport d'erreur" avec consentement
- [ ] Tests de régression prouvant aucun crash complet

**Estimation**: 0.5–1 jour  
**Labels**: `backend`, `observability`, `high`, `enhancement`

---

## Groupe B — Test Coverage & E2E

### Issue #5: Couverture Minimale 80% des Modules Critiques

**Description**:  
Augmenter couverture tests unitaires à ≥80% sur modules critiques : MonitoringService, CleaningService, StartupManager, ProcessService.

**Pourquoi**:  
Couverture actuelle ~6/10. Une couverture élevée réduit régressions non détectées.

**Critères d'acceptation**:
- [ ] Couverture ≥80% pour modules critiques
- [ ] Rapport couverture généré dans CI (Coverlet/dotnet-coverage)
- [ ] CI bloque PRs si couverture < seuil
- [ ] Tests indépendants avec mocks

**Tests à créer**:
- MonitoringService: mocks PerformanceCounter, calculs métriques, alerting
- CleaningService: détection fichiers temp, calcul espace, rollback
- StartupManager: CRUD registre, détection programmes
- ProcessService: détection processus, kill, permissions

**Estimation**: 2–5 jours  
**Labels**: `tests`, `backend`, `high`, `enhancement`

---

### Issue #6: E2E Automatisés (Flows Critiques) en CI

**Description**:  
Scénarios E2E couvrant flows critiques : démarrage, navigation, cleaning, settings. Exécution automatique en CI.

**Pourquoi**:  
Tests unitaires ne capturent pas problèmes d'intégration UI/backend.

**Critères d'acceptation**:
- [ ] Suite E2E en CI (GitHub Actions Windows)
- [ ] Framework: WinAppDriver ou Appium
- [ ] Tests: démarrage, navigation, cleaning, startup mgmt, settings
- [ ] Temps < 15 min
- [ ] Screenshots en cas d'échec
- [ ] Tests isolés (fresh start)

**Scénarios**:
1. Démarrage et navigation modules
2. Flow nettoyage complet
3. Gestion démarrage Windows
4. Cas d'erreur (fichier verrouillé, permissions)

**Estimation**: 3–5 jours  
**Labels**: `tests`, `e2e`, `high`, `enhancement`

---

### Issue #7: Test de Charge Baseline & Soak

**Description**:  
Tests performance validant que Virgil reste responsive sous charge prolongée. Documenter métriques attendues.

**Pourquoi**:  
Memory leaks et dégradations souvent visibles qu'après utilisation prolongée.

**Critères d'acceptation**:
- [ ] Objectifs documentés (CPU <5% idle, RAM <200MB steady)
- [ ] Test soak 2–4h sans fuite mémoire
- [ ] Monitoring: RAM, handles, threads, CPU
- [ ] Tests simulant usage normal
- [ ] Rapport avec graphiques métriques

**Scénarios**:
1. Soak idle 4h → pas de croissance mémoire
2. Soak avec activité 2h → CPU <10% moyen
3. Stress navigation UI → pas de ralentissement

**Estimation**: 2–3 jours  
**Labels**: `perf`, `testing`, `medium`, `enhancement`

---

## Groupe C — CI/CD & Déploiement Sûr

### Issue #8: Pipeline CI Complet avec Staging

**Description**:  
Renforcer pipeline CI pour bloquer merges si tests échouent. Automatiser déploiement staging après merge main.

**Pourquoi**:  
Garantir code mergé toujours fonctionnel et testé.

**Critères d'acceptation**:
- [ ] Pipeline sur PR et push main
- [ ] Étapes: Restore → Build → Unit Tests → E2E Tests
- [ ] PR non mergeable si pipeline échoue (branch protection)
- [ ] Auto-deploy staging après merge main
- [ ] Artifacts archivés
- [ ] Notifications en cas d'échec

**Estimation**: 2–3 jours  
**Labels**: `ci`, `infra`, `high`, `enhancement`

---

### Issue #9: Déploiement Progressif ou Feature Flags

**Description**:  
Mécanisme de déploiement progressif ou feature flags pour limiter blast radius.

**Pourquoi**:  
Bugs critiques en prod affectent tous utilisateurs. Déploiement progressif permet détection rapide.

**Critères d'acceptation**:
- [ ] Système feature flags (local config ou service externe)
- [ ] Flags contrôlent fonctionnalités critiques
- [ ] UI admin pour toggle flags
- [ ] Configuration par utilisateur ou pourcentage
- [ ] Metrics par flag
- [ ] Documentation développeurs

**Options**:
- Feature flags locaux (appsettings.json)
- Service externe (LaunchDarkly)
- Canary channel (stable vs canary)

**Estimation**: 2–3 jours  
**Labels**: `infra`, `release`, `high`, `enhancement`

---

### Issue #10: Rollback & Runbook de Déploiement

**Description**:  
Documenter et tester procédures de rollback. Créer runbook détaillant déploiement, vérifications, rollback.

**Pourquoi**:  
Procédure rapide et testée essentielle pour minimiser impact en cas de bug.

**Critères d'acceptation**:
- [ ] Runbook déploiement avec toutes étapes
- [ ] Procédure rollback documentée et testée
- [ ] Rollback possible en <15 min
- [ ] Drill rollback effectué au moins 1x
- [ ] Checklist pré-déploiement
- [ ] Contacts d'escalation

**Contenu runbook**:
- Pré-déploiement: checklist, backup
- Déploiement: étapes détaillées
- Post-déploiement: smoke tests
- Rollback: trigger conditions, procédure
- Communication: équipe, utilisateurs
- Postmortem template

**Estimation**: 0.5–1 jour  
**Labels**: `docs`, `release`, `high`, `documentation`

---

## Groupe D — Monitoring & Observabilité

### Issue #11: Health Checks + Readiness + Liveness

**Description**:  
Mécanismes health check pour surveiller état Virgil. Readiness (prêt), Liveness (pas deadlock).

**Pourquoi**:  
Monitoring centralisé, détection proactive, auto-healing potentiel.

**Critères d'acceptation**:
- [ ] Health check vérifie: services actifs, ressources, config
- [ ] Readiness false pendant startup/shutdown
- [ ] Liveness détecte deadlocks
- [ ] API locale (HTTP localhost ou named pipe)
- [ ] Logs structurés health checks
- [ ] Tests automatisés

**Implémentation**:
- API HTTP localhost:8080/health
- Endpoints: /health, /health/ready, /health/live
- Watchdog service pour liveness

**Estimation**: 0.5–1 jour  
**Labels**: `infra`, `monitoring`, `high`, `enhancement`

---

### Issue #12: Logs Structurés + Traces Distribuées

**Description**:  
Logs structurés (JSON) vers log store centralisé. Instrumentation traces pour requêtes/opérations.

**Pourquoi**:  
Facilite debug en production, visualisation flows, correlation erreurs.

**Critères d'acceptation**:
- [ ] Logs JSON structurés avec contexte enrichi
- [ ] Envoi vers log store (file, Seq, Sentry)
- [ ] Traces montrent latence par opération
- [ ] Logs contiennent correlation ID
- [ ] Dashboard exemple pour flow principal

**Implémentation**:
- Serilog configuré avec sinks appropriés
- Enrichisseurs: machine, user, version
- Structured logging: @Property au lieu de string interpolation
- Optional: OpenTelemetry pour traces

**Estimation**: 2–3 jours  
**Labels**: `observability`, `infra`, `medium`, `enhancement`

---

### Issue #13: Alerting SLO/SLI

**Description**:  
Définir SLOs et configurer alertes (errors, latency, saturation ressources) avec playbooks.

**Pourquoi**:  
Détection proactive problèmes avant impact utilisateur.

**Critères d'acceptation**:
- [ ] SLOs définis (ex: <5% error rate, p95 latency <500ms)
- [ ] Alertes configurées (email/Slack/PagerDuty)
- [ ] Seuils d'alerte documentés
- [ ] Playbook pour chaque type d'alerte
- [ ] Tests alertes (déclenchement manuel)

**Métriques à surveiller**:
- Crash rate (# crashes / # starts)
- Erreurs non gérées par jour
- CPU/RAM moyens et peaks
- Temps réponse opérations
- Handle/Thread leaks

**Estimation**: 1–2 jours  
**Labels**: `monitoring`, `infra`, `high`, `enhancement`

---

## Groupe E — Data Integrity & Backup

### Issue #14: Transactions et Idempotence

**Description**:  
Opérations critiques utilisent transactions/compensations. Endpoints idempotents.

**Pourquoi**:  
Garantir intégrité données même en cas de failures/retries.

**Critères d'acceptation**:
- [ ] Opérations critiques dans transactions
- [ ] Rollback automatique si échec partiel
- [ ] Idempotence: retry n'a pas effet secondaire
- [ ] Tests démontrant pas de duplications sur retry
- [ ] Documentation patterns transactionnels

**Implémentation**:
- TransactionScope pour opérations multiples
- Compensation manuelle si transactions distribuées
- Idempotency keys pour opérations critiques

**Estimation**: 1–2 jours  
**Labels**: `backend`, `data`, `high`, `enhancement`

---

### Issue #15: Backup et Recovery DB + Tests

**Description**:  
Automatiser backups avec retention. Procédure restauration testée.

**Pourquoi**:  
Protection contre corruption/perte données. RTO/RPO documentés.

**Critères d'acceptation**:
- [ ] Backup automatique (quotidien ou avant operations critiques)
- [ ] Retention policy (ex: 7 jours local, 30 jours archive)
- [ ] Restauration testée sur environnement test
- [ ] RTO/RPO documentés (ex: RTO <1h, RPO <24h)
- [ ] Monitoring succès backups

**Implémentation**:
- Backup configs/state dans %APPDATA%\Virgil
- Copy vers backup location (network, cloud)
- Script restauration automatisé

**Estimation**: 1–2 jours  
**Labels**: `infra`, `data`, `high`, `enhancement`

---

## Groupe F — Sécurité & Secrets

### Issue #16: Gestion Secrets + Rotation

**Description**:  
Centraliser secrets, interdiction commit secrets, rotation automatique si possible.

**Pourquoi**:  
Éviter fuite credentials, conformité sécurité.

**Critères d'acceptation**:
- [ ] Aucune clé secrète en clair dans repo
- [ ] Secrets stockés dans store sécurisé (ProtectedData, Azure KeyVault)
- [ ] Variables CI via secret store
- [ ] Procédure rotation documentée
- [ ] Scan git history pour secrets existants

**Implémentation**:
- ProtectedData API pour secrets locaux
- Environment variables pour CI
- Pre-commit hook détection secrets (git-secrets)

**Estimation**: 1 jour  
**Labels**: `security`, `infra`, `high`, `enhancement`

---

### Issue #17: Scan Dépendances + Vulnérabilités

**Description**:  
Activer scans automatiques dépendances. Process remédiation vulnérabilités critiques.

**Pourquoi**:  
Dépendances vulnérables = vecteur d'attaque.

**Critères d'acceptation**:
- [ ] Dependabot activé sur repo
- [ ] CI bloque/alerte vulnérabilités critiques
- [ ] Process défini: review → patch → test → deploy
- [ ] SLA: vulns critiques patchées sous 7 jours
- [ ] Dashboard vulnérabilités

**Configuration**:
- GitHub Dependabot alerts
- dotnet list package --vulnerable
- NuGet package analysis dans CI

**Estimation**: 0.5–1 jour  
**Labels**: `security`, `maintenance`, `medium`, `enhancement`

---

### Issue #18: Least Privilege & Review Accès

**Description**:  
Revoir permissions, retirer accès excessifs, MFA activé, revues mensuelles.

**Pourquoi**:  
Principe least privilege réduit surface d'attaque.

**Critères d'acceptation**:
- [ ] Liste accès validée et réduite
- [ ] App exécutée avec permissions minimales (pas admin par défaut)
- [ ] Elevation UAC uniquement quand nécessaire
- [ ] MFA activé pour comptes développeurs
- [ ] Review permissions mensuelle

**Actions**:
- Audit permissions actuelles
- Documenter quelles opérations nécessitent admin
- Implémenter UAC elevation granulaire

**Estimation**: 1–2 jours  
**Labels**: `security`, `ops`, `medium`, `enhancement`

---

## Groupe G — Frontend & UX

### Issue #19: Gestion Erreurs Réseau Côté Client

**Description**:  
Gestion échecs réseau visible (toasts), retry optionnel, fallback UI au lieu de crash.

**Pourquoi**:  
Améliorer UX en cas de problèmes réseau/API.

**Critères d'acceptation**:
- [ ] UI ne devient jamais non-responsive
- [ ] Erreurs affichées avec call-to-action
- [ ] Retry automatique configurable
- [ ] Mode offline dégradé si applicable
- [ ] Tests E2E simulant perte réseau

**Implémentation**:
- Toast notifications pour erreurs
- Retry UI avec feedback visuel
- Disable features nécessitant réseau si offline

**Estimation**: 1–2 jours  
**Labels**: `frontend`, `ux`, `medium`, `enhancement`

---

### Issue #20: Performance et Memory Leaks Frontend

**Description**:  
Audit mémoire (profiling), corriger fuites (listeners), lazy load composants lourds.

**Pourquoi**:  
UI qui ralentit au fil du temps = mauvaise UX.

**Critères d'acceptation**:
- [ ] Profiling montre pas de croissance mémoire après navigation répétée
- [ ] Event handlers proprement unsubscribed
- [ ] Weak references pour caches
- [ ] Lazy loading vues lourdes
- [ ] Tests performance automatisés

**Outils**:
- Visual Studio Diagnostic Tools
- dotMemory profiler
- XAML Binding debugging

**Estimation**: 2 jours  
**Labels**: `frontend`, `perf`, `medium`, `enhancement`

---

## Groupe H — Documentation & Runbooks

### Issue #21: README + Guide Dev Local

**Description**:  
Documenter comment lancer localement, variables, seeds, commandes tests, debug.

**Pourquoi**:  
Onboarding rapide nouveaux développeurs.

**Critères d'acceptation**:
- [ ] Fresh dev peut lancer en 15–30 min
- [ ] Prérequis listés (.NET SDK, Visual Studio, etc.)
- [ ] Commandes build/test/run documentées
- [ ] Troubleshooting section
- [ ] Architecture overview avec diagrammes

**Contenu**:
- Prerequisites
- Installation steps
- Configuration
- Running the app
- Running tests
- Common issues
- Contributing guidelines

**Estimation**: 0.5–1 jour  
**Labels**: `docs`, `low`, `documentation`

---

### Issue #22: Runbook Incidents & Communication

**Description**:  
Documenter procédure incident: triage, on-call, escalation, postmortem template.

**Pourquoi**:  
Réponse coordonnée et rapide aux incidents.

**Critères d'acceptation**:
- [ ] Runbook incident complet
- [ ] Rôles et responsabilités définis
- [ ] On-call rotation si applicable
- [ ] Escalation path claire
- [ ] Postmortem template
- [ ] Drill incident effectué 1x

**Contenu runbook**:
- Détection et triage
- Severity levels
- Communication (qui notifier, comment)
- Investigation steps
- Mitigation et fix
- Postmortem process

**Estimation**: 0.5 jour  
**Labels**: `docs`, `ops`, `high`, `documentation`

---

### Issue #23: Checklist Pré-Release Automatisée

**Description**:  
Checklist automatisée vérifiant: tests OK, scans OK, backups, monitoring, smoke tests.

**Pourquoi**:  
Éviter oublis critiques avant release.

**Critères d'acceptation**:
- [ ] Checklist automatisée dans CI
- [ ] Items: tests passed, coverage OK, scans passed, backup done, staging validated
- [ ] Release non disponible si checklist rouge
- [ ] Override documenté avec approbation
- [ ] Rapport checklist archivé

**Implémentation**:
- Script PowerShell/CI job
- Valide tous critères
- Output markdown checklist
- Block release si non-green

**Estimation**: 1 jour  
**Labels**: `release`, `ci`, `high`, `enhancement`

---

## Bonus - Resilience Testing

### Issue #24: Chaos/Resilience Testing

**Description**:  
Plan simple pour injecter latence/erreurs et vérifier comportement système.

**Pourquoi**:  
Valider que système gère gracefully failures partiels.

**Critères d'acceptation**:
- [ ] Scénarios définis: latency, errors, resource exhaustion
- [ ] Tests montrent dégradation contrôlée
- [ ] Documentation comportement attendu
- [ ] Exécution périodique (mensuelle)

**Scénarios**:
1. Simuler disk full → cleaning échoue gracefully
2. Simuler service Windows indisponible → circuit breaker
3. Simuler latence extrême → timeout approprié
4. Kill processus aléatoires → app reste stable

**Outils**:
- Manual injection (mocks)
- Chaos Monkey for Windows (custom)

**Estimation**: 1–2 jours  
**Labels**: `testing`, `reliability`, `low`, `enhancement`

---

## Priorisation et Sprints Suggérés

### Sprint 1 (2 semaines) - Fondations Critiques
**Objectif**: Prévenir crashes et améliorer stabilité immédiate

Issues à adresser:
- Issue #1: Graceful Shutdown
- Issue #3: Validation forte entrées
- Issue #4: Gestion centralisée erreurs
- Issue #5: Couverture tests 80%
- Issue #11: Health checks

**Valeur**: Réduction immédiate taux de crash

---

### Sprint 2 (2 semaines) - Tests & Résilience
**Objectif**: Augmenter confiance dans le code

Issues à adresser:
- Issue #2: Circuit breakers
- Issue #6: Tests E2E
- Issue #7: Tests de charge
- Issue #8: Pipeline CI complet

**Valeur**: Détection précoce régressions

---

### Sprint 3 (1-2 semaines) - Production Readiness
**Objectif**: Préparation release stable

Issues à adresser:
- Issue #9: Feature flags
- Issue #10: Runbook rollback
- Issue #12: Logs structurés
- Issue #13: Alerting
- Issue #23: Checklist pré-release

**Valeur**: Déploiements sûrs et rapides

---

### Sprint 4 (1 semaine) - Sécurité & Data
**Objectif**: Hardening sécurité

Issues à adresser:
- Issue #14: Transactions
- Issue #15: Backups
- Issue #16: Gestion secrets
- Issue #17: Scan vulnérabilités
- Issue #18: Least privilege

**Valeur**: Conformité et protection données

---

### Sprint 5 (1 semaine) - Polish & Documentation
**Objectif**: Excellence opérationnelle

Issues à adresser:
- Issue #19: Erreurs réseau UI
- Issue #20: Performance frontend
- Issue #21: README dev
- Issue #22: Runbook incidents
- Issue #24: Chaos testing

**Valeur**: Excellence opérationnelle continue

---

## Métriques de Succès

Après implémentation de ces issues, vous devriez observer:

### Stabilité
- ✅ Crash rate < 1% (target: 0.1%)
- ✅ 99.9% uptime pour monitoring service
- ✅ 0 corruptions de données

### Qualité
- ✅ Couverture tests ≥ 80%
- ✅ 0 vulnérabilités critiques/high
- ✅ Tous tests E2E passent

### Déploiement
- ✅ Déploiements < 15 min
- ✅ Rollback < 15 min
- ✅ 0 rollbacks sur 10 déploiements

### Observabilité
- ✅ MTTD (Mean Time To Detect) < 5 min
- ✅ MTTR (Mean Time To Resolve) < 1h
- ✅ 100% incidents avec postmortem

---

## Guide de Création des Issues

### Méthode 1: Création Manuelle

Pour chaque issue:
1. Aller sur https://github.com/bassetthomas-design/Virgil/issues/new
2. Copier le titre
3. Copier toute la section comme description
4. Ajouter les labels recommandés
5. Assigner selon disponibilité
6. Ajouter à milestone approprié (Sprint 1, 2, etc.)

### Méthode 2: GitHub CLI (Automatique)

Si vous avez GitHub CLI installé:

```bash
# Créer toutes les issues d'un coup
cd /path/to/Virgil

# Issue 1
gh issue create --repo bassetthomas-design/Virgil \
  --title "Graceful Shutdown & Gestion des Signaux (SIGTERM/SIGINT)" \
  --label "backend,reliability,high,enhancement" \
  --body "$(cat <<EOF
Implémenter l'arrêt gracieux de l'application Virgil...
[copier contenu complet issue #1]
EOF
)"

# Répéter pour chaque issue...
```

### Méthode 3: Script Automatisé

Un script PowerShell/Python peut créer toutes les issues:

```powershell
# create-issues.ps1
$issues = @(
    @{
        title = "Graceful Shutdown & Gestion des Signaux"
        labels = "backend,reliability,high,enhancement"
        body = "..."
    },
    # ... autres issues
)

foreach ($issue in $issues) {
    gh issue create --repo bassetthomas-design/Virgil `
        --title $issue.title `
        --label $issue.labels `
        --body $issue.body
}
```

---

## Support et Questions

Si vous avez besoin de:
- **Adapter ces issues à des besoins spécifiques**: Modifiez les critères d'acceptation
- **Prioriser différemment**: Ajustez selon vos contraintes business
- **Aide technique**: Consultez la documentation .NET, Polly, WinAppDriver, etc.

**Bonne implémentation! 🚀**

