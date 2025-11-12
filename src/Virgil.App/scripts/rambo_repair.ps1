# Rambo Repair: restart Explorer and rebuild icon/thumbnail cache
# Requires: Admin for some operations. Run elevated for best results.
param([switch]$Force)

$ErrorActionPreference = 'Stop'

function Write-Log([string]$msg){
  $ts = Get-Date -Format 'yyyy-MM-dd HH:mm:ss';
  Write-Output "[$ts] $msg"
}

try{
  Write-Log 'Stopping Explorer.exe'
  Get-Process explorer -ErrorAction SilentlyContinue | Stop-Process -Force

  $local = $env:LOCALAPPDATA
  $iconDir = Join-Path $local 'Microsoft\Windows\Explorer'
  $thumbPattern = 'thumbcache_*'
  $iconPattern = 'iconcache*'

  Write-Log "Clearing thumbnail cache in $iconDir"
  Get-ChildItem -Path $iconDir -Filter $thumbPattern -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

  Write-Log "Clearing icon cache in $iconDir"
  Get-ChildItem -Path $iconDir -Filter $iconPattern -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

  if($Force){
    # Optional deeper clean: shell icon cache in user profile (safe)
    $userProfile = $env:USERPROFILE
    $db = Join-Path $userProfile 'AppData\Local\IconCache.db'
    if(Test-Path $db){ Write-Log 'Removing IconCache.db (legacy)'; Remove-Item $db -Force -ErrorAction SilentlyContinue }
  }

  Start-Sleep -Milliseconds 500
  Write-Log 'Restarting Explorer.exe'
  Start-Process explorer.exe

  Write-Log 'Done'
  exit 0
}
catch{
  Write-Log ("ERROR: $($_.Exception.Message)")
  exit 1
}
