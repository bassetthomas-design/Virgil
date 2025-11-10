# Open config JSON in Notepad
$path = Join-Path $Env:APPDATA 'Virgil\settings.json'
if (!(Test-Path $path)) { New-Item -ItemType File -Path $path -Force | Out-Null }
notepad.exe $path
