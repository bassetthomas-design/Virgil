<#
prepare-and-push.ps1
But : préparer le projet localement, appliquer réglages .NET8/x64/WindowsAppSDK si détecté,
initialiser git, ajouter workflow GitHub Actions et builder localement.

Usage :
  Ouvrir PowerShell à la racine du projet puis :
    .\prepare-and-push.ps1 -Root "C:\Chemin\Vers\Virgil"

Ce script est optionnel ; il automatise certaines tâches courantes pour préparer et publier Virgil.
#>

param(
  [Parameter(Mandatory=$true)]
  [string]$Root
)

Set-StrictMode -Version Latest
Write-Host "==> Préparation du projet à : $Root"

if (-not (Test-Path $Root)) {
  Write-Error "Chemin introuvable : $Root"
  exit 1
}

Set-Location $Root

# 1) trouver les csproj
$csprojFiles = Get-ChildItem -Path $Root -Recurse -Filter *.csproj -ErrorAction SilentlyContinue
if ($csprojFiles.Count -eq 0) {
  Write-Warning "Aucun fichier .csproj trouvé sous $Root"
} else {
  Write-Host "Fichiers csproj trouvés :"
  $csprojFiles | ForEach-Object { Write-Host " - $($_.FullName)" }
}

# 2) fonctions utilitaires pour modifier csproj (xml)
function Update-CsprojProperty {
  param(
    [string]$Path,
    [string]$PropertyName,
    [string]$Value
  )
  try {
    [xml]$xml = Get-Content -Raw -Path $Path
  } catch {
    Write-Warning "Impossible de lire $Path : $_"
    return
  }

  # s'assurer qu'il y a un Project/PropertyGroup
  $pg = $xml.Project.PropertyGroup | Select-Object -First 1
  if ($null -eq $pg) {
    $pg = $xml.CreateElement("PropertyGroup")
    $xml.Project.AppendChild($pg) | Out-Null
  }

  $node = $pg.SelectSingleNode($PropertyName)
  if ($null -eq $node) {
    $node = $xml.CreateElement($PropertyName)
    $node.InnerText = $Value
    $pg.AppendChild($node) | Out-Null
    Write-Host "Ajouté <$PropertyName>$Value</$PropertyName> dans $Path"
  } else {
    $node.InnerText = $Value
    Write-Host "Mis à jour <$PropertyName> -> $Value dans $Path"
  }

  $xml.Save($Path)
}

# 3) appliquer réglages conseillés (NET8, x64) aux csproj détectés
foreach ($f in $csprojFiles) {
  # Forcer TargetFramework à net8.0 si possible
  Update-CsprojProperty -Path $f.FullName -PropertyName "TargetFramework" -Value "net8.0"
  # Ajouter RuntimeIdentifier pour build x64 windows
  Update-CsprojProperty -Path $f.FullName -PropertyName "RuntimeIdentifier" -Value "win-x64"
  # Option : PlatformTarget
  Update-CsprojProperty -Path $f.FullName -PropertyName "PlatformTarget" -Value "x64"
  # Si projet WinUI / WindowsAppSDK, définir une version par défaut (1.8.*)
  Update-CsprojProperty -Path $f.FullName -PropertyName "WindowsAppSDKPackageVersion" -Value "1.8.*"
}

# 4) créer .gitignore minimal si absent
$gitignorePath = Join-Path $Root ".gitignore"
if (-not (Test-Path $gitignorePath)) {
  @"
# Visual Studio / .NET
bin/
obj/
.vs/
*.user
*.suo
*.userosscache
*.sln.docstates
*.db
*.log
TestResults/
.nuget/
packages/
publish/
# Rider
.idea/
"@ | Out-File -FilePath $gitignorePath -Encoding UTF8
  Write-Host "Création de .gitignore"
} else {
  Write-Host ".gitignore déjà présent"
}

# 5) initialiser git si nécessaire
if (-not (Test-Path (Join-Path $Root ".git"))) {
  git init 2>$null
  git add -A
  git commit -m "Initial commit — préparation automatique par prepare-and-push.ps1" 2>$null
  Write-Host "Git initialisé et premier commit créé"
} else {
  Write-Host "Dépôt git déjà initialisé"
}

# 6) ajouter workflow GitHub Actions (build + artifact)
$workflowDir = Join-Path $Root ".github\workflows"
if (-not (Test-Path $workflowDir)) {
  New-Item -ItemType Directory -Path $workflowDir -Force | Out-Null
}
$workflowPath = Join-Path $workflowDir "dotnet-build-and-artifact.yml"
$workflowContent = @"
name: Build .NET and produce artifact

on:
  push:
    branches: [ main, master ]
  pull_request:

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 8
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore

      - name: Build (Release)
        run: dotnet build --configuration Release --no-restore

      - name: Publish (win-x64)
        run: dotnet publish -c Release -r win-x64 --self-contained false -o publish-output

      - name: Archive artifact
        uses: actions/upload-artifact@v4
        with:
          name: published-app
          path: publish-output
"@
$workflowContent | Out-File -FilePath $workflowPath -Encoding UTF8
Write-Host "Workflow GitHub Actions écrit dans : $workflowPath"

# 7) tentative de restore/build local pour diagnostiquer maintenant
Write-Host "=== dotnet restore ==="
$restore = dotnet restore 2>&1
$restore | Tee-Object -Variable restoreLog
if ($LASTEXITCODE -ne 0) {
  Write-Warning "dotnet restore a rencontré des erreurs. Voir le fichier restore.log"
  $restore | Out-File -FilePath (Join-Path $Root "restore.log") -Encoding UTF8
} else {
  Write-Host "restore OK"
}

Write-Host "=== dotnet build (Debug) ==="
$build = dotnet build -v minimal 2>&1
$build | Tee-Object -Variable buildLog
$build | Out-File -FilePath (Join-Path $Root "build.log") -Encoding UTF8
if ($LASTEXITCODE -ne 0) {
  Write-Warning "dotnet build a échoué. Logs enregistrés dans build.log"
} else {
  Write-Host "Build local OK"
  # créer un zip des binaires publish si possible
  Write-Host "Tentative de publish pour créer un artefact local..."
  dotnet publish -c Release -r win-x64 --self-contained false -o publish-output 2>&1 | Out-File -FilePath (Join-Path $Root "publish.log") -Encoding UTF8
  if (Test-Path (Join-Path $Root "publish-output")) {
    $zipPath = Join-Path $Root "publish-output.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $Root "publish-output"), $zipPath)
    Write-Host "Artefact zip créé : $zipPath"
  }
}

Write-Host "`n=== Résumé ==="
Write-Host " - Fichiers .csproj modifiés (TargetFramework -> net8.0, PlatformTarget -> x64, RuntimeIdentifier -> win-x64)"
Write-Host " - Workflow GitHub Actions ajouté dans .github/workflows/dotnet-build-and-artifact.yml"
Write-Host " - Logs : restore.log, build.log, publish.log (si publish tenté) dans la racine"
Write-Host "Prochaine étape : créer un repo distant (GitHub) et pousser. Exemple :"
Write-Host "`n   git remote add origin https://github.com/TON_USER/TON_REPO.git"
Write-Host "   git push -u origin main"
