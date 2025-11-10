Param(
  [string[]]$Paths = @()
)

# If no explicit list provided, scan repo for *.csproj plus common props/config
if(-not $Paths -or $Paths.Count -eq 0){
  $Paths = @(Get-ChildItem -Recurse -File -Include *.csproj | ForEach-Object { $_.FullName })
  if(Test-Path 'Directory.Build.props'){ $Paths += 'Directory.Build.props' }
  if(Test-Path 'NuGet.config'){ $Paths += 'NuGet.config' }
}

Write-Host "Found files:"
$Paths | ForEach-Object { Write-Host " - $_" }

Write-Host 'HEXDUMP (first 32 bytes):'
foreach($p in $Paths){
  if(Test-Path $p){
    Write-Host "== $p =="
    $bytes = Get-Content -Encoding Byte -Path $p -TotalCount 32
    $hex = ($bytes | ForEach-Object { '{0:X2} ' -f $_ }) -join ''
    Write-Host $hex
  }
}

function Scrub-And-RewriteUtf8NoBom([string]$path){
  $bytes = Get-Content -Raw -Encoding Byte -Path $path
  # Decode as UTF8, preserving weird chars
  $text  = [System.Text.Encoding]::UTF8.GetString($bytes)
  # Force trim of any junk before first '<'
  $idx = $text.IndexOf('<')
  if($idx -gt 0){ $text = $text.Substring($idx) }
  # Normalize newlines to LF to avoid CR garbage
  $text = $text -replace '\r\n','
' -replace '\r','
'
  # Write as UTF-8 without BOM
  [System.IO.File]::WriteAllText((Resolve-Path $path), $text, (New-Object System.Text.UTF8Encoding($false)))
}

Write-Host 'Sanitize files to UTF8 without BOM and strip pre-XML junk...'
foreach($p in $Paths){
  if(Test-Path $p){ Scrub-And-RewriteUtf8NoBom $p }
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
