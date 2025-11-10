# Clean CI build without unnecessary workloads.
$ErrorActionPreference = 'Stop'

Write-Host 'Restoring...'
dotnet restore

Write-Host 'Building Release...'
dotnet build -c Release --no-restore

Write-Host 'Publishing...'
dotnet publish -c Release --no-restore -o out

Write-Host 'Done.'
