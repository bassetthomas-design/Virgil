Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host 'Git clean / reset'
git clean -xfd
git reset --hard

Write-Host '.NET info'
dotnet --info

Write-Host 'Install windowsdesktop workload'
dotnet workload install windowsdesktop

Write-Host 'Sanitize csproj/props'
& powershell -ExecutionPolicy Bypass -File tools/ci/Sanitize-Csproj.ps1

Write-Host 'Restore'
dotnet restore Virgil.sln --configfile NuGet.config -v minimal

Write-Host 'Build'
dotnet build --configuration Release
