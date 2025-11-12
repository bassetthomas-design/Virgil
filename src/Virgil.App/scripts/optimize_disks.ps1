# Optimisation disques: TRIM SSD, defrag HDD
# Requires: Admin
$ErrorActionPreference='Stop'
function Log([string]$m){ $ts=Get-Date -Format 'yyyy-MM-dd HH:mm:ss'; Write-Output "[$ts] $m" }
try{
  $drives = Get-Volume | Where-Object { $_.DriveLetter -ne $null }
  foreach($d in $drives){
    $dl = $d.DriveLetter + ':'
    if($d.DriveType -eq 'Fixed'){
      try{
        $media = (Get-PhysicalDisk | Where FriendlyName -Like '*').MediaType
      }catch{}
      Log "Optimize-Volume $dl (TRIM)"
      Optimize-Volume -DriveLetter $d.DriveLetter -ReTrim -ErrorAction SilentlyContinue | Out-Null
      Log "Defrag $dl (si applicable)"
      defrag $dl /O | Out-Null
    }
  }
  Log 'OK'
  exit 0
}catch{ Log ("ERROR: $($_.Exception.Message)"); exit 1 }
