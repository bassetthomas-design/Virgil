# Microsoft Store reset + re-register (si nécessaire)
$ErrorActionPreference='Stop'
function Log([string]$m){ $ts=Get-Date -Format 'yyyy-MM-dd HH:mm:ss'; Write-Output "[$ts] $m" }
try{
  Log 'wsreset.exe'
  Start-Process wsreset.exe -Wait
  Log 'Re-enregistrement Microsoft Store (AllUsers)'
  Get-AppxPackage -AllUsers Microsoft.WindowsStore | Foreach { Add-AppxPackage -DisableDevelopmentMode -Register "$($_.InstallLocation)\AppxManifest.xml" }
  Log 'OK'
  exit 0
}catch{ Log ("ERROR: $($_.Exception.Message)"); exit 1 }
