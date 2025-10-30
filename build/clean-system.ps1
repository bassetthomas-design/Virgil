param(
    [ValidateSet("Simple","Complete","Pro")]
    [string]$Level = "Simple"
)

$ErrorActionPreference = "SilentlyContinue"

function Clear-Folder {
    param($Path)
    if (Test-Path $Path) {
        Get-ChildItem -Path $Path -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "=== Virgil Clean System ==="
$totalFreed = 0

function Add-Freed($size) { $script:totalFreed += $size }

# Corbeille
try {
    Write-Host "Vidage de la corbeille..."
    $recycle = (New-Object -ComObject Shell.Application).NameSpace(0xA)
    $size = 0
    foreach ($item in $recycle.Items()) { $size += $item.Size }
    Clear-RecycleBin -Force -ErrorAction SilentlyContinue
    Add-Freed $size
} catch {}

# TEMP dossiers
$temps = @(
    "$env:TEMP",
    "$env:WINDIR\Temp",
    "$env:LOCALAPPDATA\Temp"
)
foreach ($t in $temps) {
    Write-Host "Nettoyage $t"
    $size = (Get-ChildItem $t -Recurse -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
    Clear-Folder $t
    Add-Freed $size
}

# Prefetch
if ($Level -ne "Simple") {
    Write-Host "Nettoyage Prefetch..."
    Clear-Folder "$env:WINDIR\Prefetch"
}

# Windows logs et caches
if ($Level -eq "Pro") {
    Write-Host "Nettoyage caches système..."
    Clear-Folder "$env:ProgramData\Microsoft\Windows\WER\ReportQueue"
    Clear-Folder "$env:SystemRoot\SoftwareDistribution\Download"
    Clear-Folder "$env:SystemRoot\Temp"
}

# Navigateurs
$browsers = @(
    "$env:LOCALAPPDATA\Google\Chrome\User Data\Default\Cache",
    "$env:LOCALAPPDATA\Microsoft\Edge\User Data\Default\Cache",
    "$env:APPDATA\Mozilla\Firefox\Profiles"
)
foreach ($b in $browsers) {
    Write-Host "Nettoyage navigateur: $b"
    Clear-Folder $b
}

Write-Host "Total Freed: $totalFreed"
Write-Host "=== Nettoyage terminé ==="
exit 0
