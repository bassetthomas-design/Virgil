Write-Host "=== Virgil System Update ==="

$ErrorActionPreference = "SilentlyContinue"
$totalSteps = 4
$step = 1

function Step($msg) {
    Write-Host "[$step/$totalSteps] $msg"
    $script:step++
}

# 1️⃣ Windows Update
Step "Scan + téléchargement Windows Update..."
try {
    UsoClient StartScan
    UsoClient StartDownload
    UsoClient StartInstall
} catch {
    Write-Host "Windows Update non disponible (probablement déjà à jour)."
}

# 2️⃣ Winget (Applications + Jeux + Pilotes OEM)
Step "Mises à jour via Winget..."
try {
    winget upgrade --all --include-unknown --accept-source-agreements --accept-package-agreements
} catch {
    Write-Host "Erreur Winget : $_"
}

# 3️⃣ Microsoft Defender
Step "Mise à jour Microsoft Defender..."
try {
    Update-MpSignature
    Start-MpScan -ScanType QuickScan
} catch {
    Write-Host "Defender non accessible (droits administrateur nécessaires)."
}

# 4️⃣ Vérifications finales
Step "Vérification des composants .NET et Redistribuables..."
try {
    dotnet --info | Out-Null
} catch {
    Write-Host ".NET SDK non détecté."
}

Write-Host "=== Mises à jour terminées ==="
exit 0
