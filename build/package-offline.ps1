[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputRoot = "dist/offline"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$appProject = Join-Path $repoRoot "src/Virgil.App/Virgil.App.csproj"
$packageRoot = Join-Path $repoRoot $OutputRoot
$appOutput = Join-Path $packageRoot "app"

Write-Host "🔧 Preparing offline package in '$packageRoot'" -ForegroundColor Cyan

if (Test-Path $packageRoot) {
    Remove-Item $packageRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $appOutput | Out-Null

$publishArgs = @(
    "publish",
    $appProject,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained",
    "-o", $appOutput
)

Write-Host "📦 dotnet $($publishArgs -join ' ')" -ForegroundColor Yellow
dotnet @publishArgs

# Garantit que tous les assets offline sont présents (prompts, modèles, runner local)
$assetSource = Join-Path $repoRoot "src/Virgil.App/assets"
$assetTarget = Join-Path $appOutput "assets"
if (Test-Path $assetSource) {
    Write-Host "📁 Copying assets" -ForegroundColor Green
    Copy-Item -Path $assetSource -Destination $assetTarget -Recurse -Force
}

# Copie la configuration par défaut pour permettre une exécution offline out-of-the-box
$configSource = Join-Path $repoRoot "config"
$configTarget = Join-Path $appOutput "config"
if (Test-Path $configSource) {
    Copy-Item -Path $configSource -Destination $configTarget -Recurse -Force
}

# Génère un manifeste lisible pour l'équipe packaging
$manifestPath = Join-Path $packageRoot "offline-manifest.txt"
$sizeBytes = (Get-ChildItem -Path $appOutput -Recurse | Measure-Object -Property Length -Sum).Sum
$sizeMb = [Math]::Round($sizeBytes / 1MB, 2)

$manifest = @()
$manifest += "Virgil offline package"
$manifest += "Configuration: $Configuration"
$manifest += "Runtime: $Runtime"
$manifest += "Self-contained: True"
$manifest += "Build timestamp: $(Get-Date -Format "yyyy-MM-ddTHH:mm:ss")"
$manifest += "Approximate size (MiB): $sizeMb"
$manifest += "Included asset folders:"
$manifest += "- assets/activity"
$manifest += "- assets/avatar"
$manifest += "- assets/voice"
$manifest += "- assets/virgil"
$manifest += "- assets/prompts"
$manifest += "- assets/models"
$manifest += "- assets/llama"
$manifest += "Notes: add the GGUF model and llama runner binaries before building the installer."

Set-Content -Path $manifestPath -Value $manifest -Encoding UTF8

Write-Host "✅ Offline package ready at $appOutput" -ForegroundColor Cyan
Write-Host "📝 Manifest generated at $manifestPath" -ForegroundColor Cyan
