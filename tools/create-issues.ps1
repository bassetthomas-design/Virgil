# Script pour créer automatiquement toutes les issues de robustesse dans GitHub
# Usage: .\create-issues.ps1
# Prérequis: GitHub CLI (gh) installé et authentifié

$repo = "bassetthomas-design/Virgil"

# Définition de toutes les issues
$issues = @(
    @{
        title = "Graceful Shutdown & Gestion des Signaux (SIGTERM/SIGINT)"
        labels = @("backend", "reliability", "high", "enhancement")
        body = @"
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
**Groupe**: A - Robustesse Critique
"@
    },
    @{
        title = "Circuit Breaker et Retries Intelligents pour Appels Externes"
        labels = @("backend", "reliability", "medium", "enhancement")
        body = @"
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
**Groupe**: A - Robustesse Critique
"@
    },
    @{
        title = "Validation Forte des Entrées et Protection contre Payloads Malformés"
        labels = @("backend", "security", "high", "enhancement")
        body = @"
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
**Groupe**: A - Robustesse Critique
"@
    },
    @{
        title = "Gestion Centralisée d'Erreurs & Responses Uniformes"
        labels = @("backend", "observability", "high", "enhancement")
        body = @"
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
**Groupe**: A - Robustesse Critique
"@
    },
    @{
        title = "Couverture Minimale 80% des Modules Critiques"
        labels = @("tests", "backend", "high", "enhancement")
        body = @"
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
**Groupe**: B - Tests & Coverage
"@
    },
    @{
        title = "E2E Automatisés (Flows Critiques) en CI"
        labels = @("tests", "e2e", "high", "enhancement")
        body = @"
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
**Groupe**: B - Tests & Coverage
"@
    },
    @{
        title = "Test de Charge Baseline & Soak"
        labels = @("perf", "testing", "medium", "enhancement")
        body = @"
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
**Groupe**: B - Tests & Coverage
"@
    },
    @{
        title = "Pipeline CI Complet avec Staging"
        labels = @("ci", "infra", "high", "enhancement")
        body = @"
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
**Groupe**: C - CI/CD & Déploiement
"@
    },
    @{
        title = "Déploiement Progressif ou Feature Flags"
        labels = @("infra", "release", "high", "enhancement")
        body = @"
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
**Groupe**: C - CI/CD & Déploiement
"@
    },
    @{
        title = "Rollback & Runbook de Déploiement"
        labels = @("docs", "release", "high", "documentation")
        body = @"
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
**Groupe**: C - CI/CD & Déploiement
"@
    },
    @{
        title = "Health Checks + Readiness + Liveness"
        labels = @("infra", "monitoring", "high", "enhancement")
        body = @"
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
**Groupe**: D - Monitoring & Observabilité
"@
    },
    @{
        title = "Logs Structurés + Traces Distribuées"
        labels = @("observability", "infra", "medium", "enhancement")
        body = @"
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
**Groupe**: D - Monitoring & Observabilité
"@
    },
    @{
        title = "Alerting SLO/SLI"
        labels = @("monitoring", "infra", "high", "enhancement")
        body = @"
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
**Groupe**: D - Monitoring & Observabilité
"@
    },
    @{
        title = "Transactions et Idempotence"
        labels = @("backend", "data", "high", "enhancement")
        body = @"
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
**Groupe**: E - Data Integrity & Backup
"@
    },
    @{
        title = "Backup et Recovery DB + Tests"
        labels = @("infra", "data", "high", "enhancement")
        body = @"
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
- Backup configs/state dans %APPDATA%\\Virgil
- Copy vers backup location (network, cloud)
- Script restauration automatisé

**Estimation**: 1–2 jours  
**Groupe**: E - Data Integrity & Backup
"@
    },
    @{
        title = "Gestion Secrets + Rotation"
        labels = @("security", "infra", "high", "enhancement")
        body = @"
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
**Groupe**: F - Sécurité & Secrets
"@
    },
    @{
        title = "Scan Dépendances + Vulnérabilités"
        labels = @("security", "maintenance", "medium", "enhancement")
        body = @"
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
**Groupe**: F - Sécurité & Secrets
"@
    },
    @{
        title = "Least Privilege & Review Accès"
        labels = @("security", "ops", "medium", "enhancement")
        body = @"
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
**Groupe**: F - Sécurité & Secrets
"@
    },
    @{
        title = "Gestion Erreurs Réseau Côté Client"
        labels = @("frontend", "ux", "medium", "enhancement")
        body = @"
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
**Groupe**: G - Frontend & UX
"@
    },
    @{
        title = "Performance et Memory Leaks Frontend"
        labels = @("frontend", "perf", "medium", "enhancement")
        body = @"
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
**Groupe**: G - Frontend & UX
"@
    },
    @{
        title = "README + Guide Dev Local"
        labels = @("docs", "documentation")
        body = @"
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
**Groupe**: H - Documentation & Runbooks
"@
    },
    @{
        title = "Runbook Incidents & Communication"
        labels = @("docs", "ops", "high", "documentation")
        body = @"
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
**Groupe**: H - Documentation & Runbooks
"@
    },
    @{
        title = "Checklist Pré-Release Automatisée"
        labels = @("release", "ci", "high", "enhancement")
        body = @"
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
**Groupe**: H - Documentation & Runbooks
"@
    },
    @{
        title = "Chaos/Resilience Testing"
        labels = @("testing", "reliability", "enhancement")
        body = @"
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
**Groupe**: Bonus - Resilience Testing
"@
    }
)

# Fonction pour créer les issues
function Create-Issues {
    param (
        [switch]$DryRun
    )
    
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host "  Création des Issues de Robustesse - Virgil" -ForegroundColor Cyan
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Repository: $repo" -ForegroundColor Yellow
    Write-Host "Nombre d'issues: $($issues.Count)" -ForegroundColor Yellow
    Write-Host ""
    
    if ($DryRun) {
        Write-Host "[MODE DRY-RUN] Aucune issue ne sera créée" -ForegroundColor Yellow
        Write-Host ""
    }
    
    # Vérifier que gh CLI est installé
    try {
        $ghVersion = gh --version 2>&1
        Write-Host "✓ GitHub CLI détecté: $($ghVersion[0])" -ForegroundColor Green
    } catch {
        Write-Host "✗ GitHub CLI (gh) n'est pas installé ou n'est pas dans le PATH" -ForegroundColor Red
        Write-Host "  Installez-le depuis: https://cli.github.com/" -ForegroundColor Yellow
        exit 1
    }
    
    Write-Host ""
    Write-Host "Création des issues..." -ForegroundColor Cyan
    Write-Host ""
    
    $created = 0
    $failed = 0
    
    foreach ($issue in $issues) {
        $number = $created + $failed + 1
        Write-Host "[$number/$($issues.Count)] $($issue.title)" -ForegroundColor White
        
        if ($DryRun) {
            Write-Host "  [DRY-RUN] Labels: $($issue.labels -join ', ')" -ForegroundColor Gray
            $created++
        } else {
            try {
                $labelString = $issue.labels -join ','
                $result = gh issue create --repo $repo `
                    --title $issue.title `
                    --body $issue.body `
                    --label $labelString 2>&1
                
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "  ✓ Créée: $result" -ForegroundColor Green
                    $created++
                } else {
                    Write-Host "  ✗ Erreur: $result" -ForegroundColor Red
                    $failed++
                }
            } catch {
                Write-Host "  ✗ Exception: $_" -ForegroundColor Red
                $failed++
            }
        }
        
        # Pause pour ne pas surcharger l'API
        Start-Sleep -Milliseconds 500
    }
    
    Write-Host ""
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host "  Résumé" -ForegroundColor Cyan
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host "✓ Issues créées avec succès: $created" -ForegroundColor Green
    
    if ($failed -gt 0) {
        Write-Host "✗ Issues en échec: $failed" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "Voir toutes les issues: https://github.com/$repo/issues" -ForegroundColor Yellow
}

# Point d'entrée principal
Write-Host ""
Write-Host "Ce script va créer 24 issues dans le repository $repo" -ForegroundColor Yellow
Write-Host ""
Write-Host "Options:" -ForegroundColor Cyan
Write-Host "  1. Dry-run (tester sans créer)" -ForegroundColor White
Write-Host "  2. Créer toutes les issues" -ForegroundColor White
Write-Host "  3. Annuler" -ForegroundColor White
Write-Host ""

$choice = Read-Host "Votre choix (1-3)"

switch ($choice) {
    "1" {
        Create-Issues -DryRun
    }
    "2" {
        Write-Host ""
        Write-Host "Confirmation: Voulez-vous vraiment créer 24 issues? (O/N)" -ForegroundColor Yellow
        $confirm = Read-Host
        if ($confirm -eq "O" -or $confirm -eq "o") {
            Create-Issues
        } else {
            Write-Host "Annulé." -ForegroundColor Yellow
        }
    }
    "3" {
        Write-Host "Annulé." -ForegroundColor Yellow
    }
    default {
        Write-Host "Choix invalide. Annulé." -ForegroundColor Red
    }
}
