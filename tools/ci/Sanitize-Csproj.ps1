Param(
  [string[]]$Paths = @('src/Virgil.App/Virgil.App.csproj','src/Virgil.Core/Virgil.Core.csproj','src/Virgil.Agent/Virgil.Agent.csproj','tests/Virgil.Tests/Virgil.Tests.csproj','Directory.Build.props','NuGet.config')
)

Write-Host 'HEXDUMP (first 32 bytes):'
foreach($p in $Paths){
  if(Test-Path $p){
    Write-Host "== $p =="
    $bytes = Get-Content -Encoding Byte -Path $p -TotalCount 32
    $hex = ($bytes | ForEach-Object { '{0:X2} ' -f $_ }) -join ''
    Write-Host $hex
  }
}

Write-Host 'Sanitize files to UTF8 without BOM...'
foreach($p in $Paths){
  if(Test-Path $p){
    $bytes = Get-Content -Raw -Encoding Byte -Path $p
    $text  = [System.Text.Encoding]::UTF8.GetString($bytes)
    [System.IO.File]::WriteAllText((Resolve-Path $p), $text, (New-Object System.Text.UTF8Encoding($false)))
  }
}

Write-Host 'Validate XML...'
$failed = $false
foreach($p in $Paths){
  if(Test-Path $p -and ([IO.Path]::GetExtension($p) -in '.csproj','.props','.config')){
    try{ [void][xml](Get-Content -Raw -Path $p); Write-Host "OK XML -> $p" }
    catch{ Write-Host "XML FAIL -> $p"; $failed = $true }
  }
}
if($failed){ throw 'XML validation failed for one or more files.' }
