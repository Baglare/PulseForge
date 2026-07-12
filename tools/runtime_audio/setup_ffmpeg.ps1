[CmdletBinding()]
param(
    [string]$SourceExecutable = "",
    [string]$DownloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$targetDirectory = Join-Path $repoRoot "Assets\StreamingAssets\PulseForge"
$targetExecutable = Join-Path $targetDirectory "ffmpeg.exe"

New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null

if ((Test-Path -LiteralPath $targetExecutable) -and -not $Force) {
    Write-Host "FFmpeg is already ready at: $targetExecutable"
    Write-Host "Use -Force to replace it."
    exit 0
}

$resolvedSource = $null
$temporaryRoot = $null

try {
    if (-not [string]::IsNullOrWhiteSpace($SourceExecutable)) {
        $resolvedSource = (Resolve-Path -LiteralPath $SourceExecutable).Path
    }
    else {
        $installedCommand = Get-Command ffmpeg.exe -ErrorAction SilentlyContinue
        if ($null -ne $installedCommand) {
            $resolvedSource = $installedCommand.Source
            Write-Host "Using installed FFmpeg: $resolvedSource"
        }
    }

    if ([string]::IsNullOrWhiteSpace($resolvedSource)) {
        $temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("PulseForge-FFmpeg-" + [Guid]::NewGuid().ToString("N"))
        $archivePath = Join-Path $temporaryRoot "ffmpeg-release-essentials.zip"
        $checksumPath = $archivePath + ".sha256"
        $extractDirectory = Join-Path $temporaryRoot "extract"

        New-Item -ItemType Directory -Force -Path $temporaryRoot | Out-Null
        Write-Host "Downloading FFmpeg release essentials..."
        Invoke-WebRequest -Uri $DownloadUrl -OutFile $archivePath
        Invoke-WebRequest -Uri ($DownloadUrl + ".sha256") -OutFile $checksumPath

        $expectedHash = ((Get-Content -LiteralPath $checksumPath -Raw).Trim() -split "\s+")[0].ToUpperInvariant()
        $actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToUpperInvariant()
        if ($actualHash -ne $expectedHash) {
            throw "FFmpeg archive SHA256 verification failed."
        }

        Expand-Archive -LiteralPath $archivePath -DestinationPath $extractDirectory -Force
        $downloadedExecutable = Get-ChildItem -LiteralPath $extractDirectory -Recurse -Filter ffmpeg.exe | Select-Object -First 1
        if ($null -eq $downloadedExecutable) {
            throw "The downloaded archive did not contain ffmpeg.exe."
        }

        $resolvedSource = $downloadedExecutable.FullName
    }

    Copy-Item -LiteralPath $resolvedSource -Destination $targetExecutable -Force
    $versionLine = (& $targetExecutable -version | Select-Object -First 1)

    Write-Host "FFmpeg is ready for Unity builds:"
    Write-Host "  $targetExecutable"
    Write-Host "  $versionLine"
    Write-Warning "Before distributing a tester build, review FFmpeg licensing and provide the matching notices/source obligations for the binary you ship."
}
finally {
    if ($null -ne $temporaryRoot -and (Test-Path -LiteralPath $temporaryRoot)) {
        $resolvedTemporaryRoot = [System.IO.Path]::GetFullPath($temporaryRoot)
        $resolvedSystemTemp = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        $temporaryLeaf = Split-Path -Leaf $resolvedTemporaryRoot
        $isUnderSystemTemp = $resolvedTemporaryRoot.StartsWith($resolvedSystemTemp, [System.StringComparison]::OrdinalIgnoreCase)
        $hasExpectedPrefix = $temporaryLeaf.StartsWith("PulseForge-FFmpeg-", [System.StringComparison]::Ordinal)
        if ($isUnderSystemTemp -and $hasExpectedPrefix) {
            Remove-Item -LiteralPath $resolvedTemporaryRoot -Recurse -Force
        }
    }
}
