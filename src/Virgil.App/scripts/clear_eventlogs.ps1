# Purge journaux Windows (attention). Utiliser -Force pour bypass confirm.
param([switch]$Force)
$ErrorActionPreference='Stop'
function Log([string]$m){ $ts=Get-Date -Format 'yyyy-MM-dd HH:mm:ss'; Write-Output "[$ts] $m" }
try{
  $logs = & wevtutil el
  foreach($name in $logs){
    if($Force){ Log "Clear $name"; & wevtutil cl $name }
    else{ Write-Output "Would clear: $name (use -Force)" }
  }
  Log 'Terminé'
  exit 0
}catch{ Log ("ERROR: $($_.Exception.Message)"); exit 1 }
