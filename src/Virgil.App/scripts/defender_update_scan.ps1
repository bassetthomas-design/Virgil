# Defender: Update signatures + Quick scan
$ErrorActionPreference='Stop'
function Log([string]$m){ $ts=Get-Date -Format 'yyyy-MM-dd HH:mm:ss'; Write-Output "[$ts] $m" }
try{
  Log 'Update-MpSignature'
  Update-MpSignature | Out-Null
  Log 'Start-MpScan QuickScan'
  Start-MpScan -ScanType QuickScan | Out-Null
  Log 'OK'
  exit 0
}catch{ Log ("ERROR: $($_.Exception.Message)"); exit 1 }
