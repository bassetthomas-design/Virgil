param(
    [string]$VersionFile = (Join-Path $PSScriptRoot ".." "src" "Virgil.App" "AI" "Runtime" "llama-runtime.version.txt"),
    [string]$DestinationDir = (Join-Path $PSScriptRoot ".." "src" "Virgil.App" "AI" "Runtime")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Test-PeAmd64 {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
    try {
        $reader = New-Object System.IO.BinaryReader($stream)
        $stream.Seek(0x3C, [System.IO.SeekOrigin]::Begin) | Out-Null
        $peOffset = $reader.ReadInt32()
        $stream.Seek($peOffset, [System.IO.SeekOrigin]::Begin) | Out-Null
        $signature = $reader.ReadUInt32()
        if ($signature -ne 0x00004550) {
            return $false
        }

        $machine = $reader.ReadUInt16()
        return $machine -eq 0x8664
    }
    finally {
        if ($reader) {
            $reader.Close()
        }
        $stream.Close()
    }
}

if (-not (Test-Path -Path $VersionFile)) {
    throw "Version file not found: $VersionFile"
}

$versionLines = Get-Content -Path $VersionFile | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" }
if ($versionLines.Count -lt 2) {
    throw "Version file must contain a tag and sha256: $VersionFile"
}

$versionTag = $versionLines[0]
$expectedSha = $versionLines[1]
if ($expectedSha -match "sha256[:=]\s*(.+)") {
    $expectedSha = $Matches[1].Trim()
}

$zipUrl = "https://github.com/ggml-org/llama.cpp/releases/download/$versionTag/llama-$versionTag-bin-win-cpu-x64.zip"
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("llama-runtime-" + [System.Guid]::NewGuid().ToString("N"))
$zipPath = Join-Path $tempRoot "llama-runtime.zip"
$extractPath = Join-Path $tempRoot "extract"

New-Item -Path $tempRoot -ItemType Directory -Force | Out-Null

try {
    Write-Host "Downloading runtime from: $zipUrl"
    Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath

    $hash = (Get-FileHash -Algorithm SHA256 -Path $zipPath).Hash.ToLowerInvariant()
    if (-not [string]::IsNullOrWhiteSpace($expectedSha) -and $expectedSha -ne "UNKNOWN") {
        if ($hash -ne $expectedSha.ToLowerInvariant()) {
            throw "SHA256 mismatch for $zipUrl. Expected $expectedSha but got $hash"
        }
    }
    else {
        Write-Host "WARNING: SHA256 not set in $VersionFile; downloaded hash is $hash"
    }

    Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force

    $runtimeExe = Get-ChildItem -Path $extractPath -Recurse -Filter "llama-server.exe" -File |
        Sort-Object Length -Descending |
        Select-Object -First 1
    if (-not $runtimeExe) {
        throw "llama-server.exe not found in extracted archive."
    }

    $resolvedOutDir = (Resolve-Path -Path $DestinationDir -ErrorAction SilentlyContinue)?.Path
    if (-not $resolvedOutDir) {
        $resolvedOutDir = (New-Item -Path $DestinationDir -ItemType Directory -Force).FullName
    }

    $destinationExe = Join-Path $resolvedOutDir "llama-server.exe"
    if (Test-Path -Path $destinationExe) {
        Remove-Item -Path $destinationExe -Force -ErrorAction SilentlyContinue
    }

    $existingDlls = Get-ChildItem -Path $resolvedOutDir -Filter "*.dll" -File -ErrorAction SilentlyContinue
    foreach ($dll in $existingDlls) {
        Remove-Item -Path $dll.FullName -Force -ErrorAction SilentlyContinue
    }

    Copy-Item -Path $runtimeExe.FullName -Destination $destinationExe -Force

    $runtimeDir = Split-Path -Path $runtimeExe.FullName -Parent
    $runtimeDlls = Get-ChildItem -Path $runtimeDir -Filter "*.dll" -File -ErrorAction SilentlyContinue
    foreach ($dll in $runtimeDlls) {
        Copy-Item -Path $dll.FullName -Destination $resolvedOutDir -Force
    }

    if (-not (Test-Path -Path $destinationExe)) {
        throw "Runtime copy failed. Expected $destinationExe."
    }

    $fileInfo = Get-Item -Path $destinationExe
    if ($fileInfo.Length -le 1MB) {
        throw "Runtime size validation failed. Size was $($fileInfo.Length) bytes."
    }

    if (-not (Test-PeAmd64 -Path $destinationExe)) {
        throw "Runtime architecture validation failed. Expected AMD64 (0x8664)."
    }

    Write-Host "Runtime staged at: $destinationExe"
    Write-Host ("Runtime size: {0} bytes" -f $fileInfo.Length)
    Write-Host "PE x64 OK"
}
finally {
    if (Test-Path -Path $tempRoot) {
        Remove-Item -Path $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
