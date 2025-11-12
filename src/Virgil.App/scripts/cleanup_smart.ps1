# Nettoyage intelligent: Temp, Prefetch, Recycle Bin (safe)
$ErrorActionPreference='Stop'
function Log([string]$m){ $ts=Get-Date -Format 'yyyy-MM-dd HH:mm:ss'; Write-Output "[$ts] $m" }
try{
  Log 'Purge Temp utilisateur'
  $u=$env:TEMP; if(Test-Path $u){ Get-ChildItem $u -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue }
  Log 'Purge Temp Windows'
  $w=Join-Path $env:WINDIR 'Temp'; if(Test-Path $w){ Get-ChildItem $w -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue }
  Log 'Nettoyage Prefetch (non critique)'
  $p=Join-Path $env:WINDIR 'Prefetch'; if(Test-Path $p){ Get-ChildItem $p -Force -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue }
  Log 'Vider Corbeille'
  try{ Clear-RecycleBin -Force -ErrorAction SilentlyContinue }catch{}
  Log 'OK'
  exit 0
}catch{ Log ("ERROR: $($_.Exception.Message)"); exit 1 }
