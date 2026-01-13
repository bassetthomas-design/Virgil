$defaultZipUrl = "https://github.com/ggml-org/llama.cpp/releases/download/b7717/llama-b7717-bin-win-cpu-x64.zip"

param(
    [Parameter(Mandatory = $true)]
    [string]$OutDir,

    [string]$Version,

    [string]$ZipUrl = $defaultZipUrl
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

$resolvedOutDir = (Resolve-Path -Path $OutDir -ErrorAction SilentlyContinue)?.Path
if (-not $resolvedOutDir) {
    $resolvedOutDir = (New-Item -Path $OutDir -ItemType Directory -Force).FullName
}

$envZipUrl = $env:LLAMA_RUNTIME_ZIP_URL

if (-not [string]::IsNullOrWhiteSpace($envZipUrl)) {
    $zipSource = $envZipUrl
}
elseif ([string]::IsNullOrWhiteSpace($ZipUrl)) {
    if ([string]::IsNullOrWhiteSpace($Version)) {
        $zipSource = $defaultZipUrl
    }
    else {
        $zipSource = "https://github.com/ggerganov/llama.cpp/releases/download/$Version/llama-$Version-bin-win-x64.zip"
    }
}
else {
    $zipSource = $ZipUrl
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("llama-runtime-" + [System.Guid]::NewGuid().ToString("N"))
$zipPath = Join-Path $tempRoot "llama-runtime.zip"
$extractPath = Join-Path $tempRoot "extract"

New-Item -Path $tempRoot -ItemType Directory -Force | Out-Null

try {
    Write-Host "Downloading runtime from: $zipSource"
    Invoke-WebRequest -Uri $zipSource -OutFile $zipPath

    Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force

    $runtimeExe = Get-ChildItem -Path $extractPath -Recurse -Filter "llama-server.exe" -File | Select-Object -First 1
    if (-not $runtimeExe) {
        throw "llama-server.exe not found in extracted archive."
    }

    $destinationExe = Join-Path $resolvedOutDir "llama-server.exe"
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
