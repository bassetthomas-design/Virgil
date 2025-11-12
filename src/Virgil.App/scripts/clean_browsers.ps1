# Nettoyage navigateurs (caches). Ne touche pas aux profils/sessions.
$ErrorActionPreference='Stop'
function Log([string]$m){ $ts=Get-Date -Format 'yyyy-MM-dd HH:mm:ss'; Write-Output "[$ts] $m" }
try{
  $local=$env:LOCALAPPDATA; $roam=$env:APPDATA
  # Edge (Chromium)
  $edge=Join-Path $local 'Microsoft/Edge/User Data/Default/Cache'; if(Test-Path $edge){ Log 'Edge cache'; Get-ChildItem $edge -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue }
  # Chrome
  $chrome=Join-Path $local 'Google/Chrome/User Data/Default/Cache'; if(Test-Path $chrome){ Log 'Chrome cache'; Get-ChildItem $chrome -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue }
  # Firefox
  $ffProf=Join-Path $roam 'Mozilla/Firefox/Profiles'; if(Test-Path $ffProf){
    Get-ChildItem $ffProf -Directory | ForEach-Object { $cache=Join-Path $_.FullName 'cache2'; if(Test-Path $cache){ Log "Firefox cache: $($_.Name)"; Get-ChildItem $cache -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue } }
  }
  Log 'OK'
  exit 0
}catch{ Log ("ERROR: $($_.Exception.Message)"); exit 1 }
