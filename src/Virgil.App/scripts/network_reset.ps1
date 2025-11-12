# Network Reset: flush DNS, reset Winsock, reset IP interfaces, renew DHCP
# Requires: Admin
$ErrorActionPreference = 'Stop'
function Write-Log([string]$msg){ $ts = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'; Write-Output "[$ts] $msg" }

try{
  Write-Log 'Flushing DNS cache'
  ipconfig /flushdns | Out-Null

  Write-Log 'Resetting Winsock'
  netsh winsock reset | Out-Null

  Write-Log 'Resetting IP stack (IPv4/IPv6)'
  netsh int ip reset | Out-Null
  netsh int ipv6 reset | Out-Null

  Write-Log 'Releasing DHCP leases'
  ipconfig /release | Out-Null
  Start-Sleep -Seconds 1
  Write-Log 'Renewing DHCP leases'
  ipconfig /renew | Out-Null

  Write-Log 'Done (a reboot may be required for full effect)'
  exit 0
}
catch{
  Write-Log ("ERROR: $($_.Exception.Message)")
  exit 1
}
