# Maintenance complète: DISM, SFC, purge Temp, résumé
# Requires: Admin
$ErrorActionPreference = 'Stop'
function Log([string]$m){ $ts=Get-Date -Format 'yyyy-MM-dd HH:mm:ss'; Write-Output "[$ts] $m" }
try{
  Log 'DISM /Online /Cleanup-Image /RestoreHealth'
  DISM /Online /Cleanup-Image /RestoreHealth | Out-Null
  Log 'SFC /scannow'
  sfc /scannow | Out-Null
  Log 'Purge Temp (user + Windows Temp)'
  $u=$env:TEMP; if(Test-Path $u){ Get-ChildItem $u -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue }
  $w=Join-Path $env:WINDIR 'Temp'; if(Test-Path $w){ Get-ChildItem $w -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue }
  Log 'Fini. Un redémarrage peut être recommandé.'
  exit 0
}catch{ Log ("ERROR: $($_.Exception.Message)"); exit 1 }
