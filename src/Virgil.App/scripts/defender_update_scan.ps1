# Windows Defender update + quick scan
$mp = "$Env:ProgramFiles" + '\Windows Defender\MpCmdRun.exe'
if (Test-Path $mp) {
  & $mp -SignatureUpdate
  & $mp -Scan -ScanType 1
} else { Write-Warning 'MpCmdRun not found' }
