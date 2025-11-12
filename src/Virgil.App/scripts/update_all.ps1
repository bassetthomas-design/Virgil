# Mise à jour applications via winget + note Store
$ErrorActionPreference='Stop'
function Log([string]$m){ $ts=Get-Date -Format 'yyyy-MM-dd HH:mm:ss'; Write-Output "[$ts] $m" }
try{
  Log 'Winget upgrade --all'
  winget source update | Out-Null
  winget upgrade --all --accept-source-agreements --accept-package-agreements --silent | Write-Output
  Log 'Note: mises à jour Microsoft Store non garanties par ce script (voir store_repair.ps1 si souci)'.
  exit 0
}catch{ Log ("ERROR: $($_.Exception.Message)"); exit 1 }
