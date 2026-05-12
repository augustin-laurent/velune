param(
    [string] $OutputRoot = "artifacts/native-tools/win-x64",
    [string] $QpdfVersion = "11.9.1",
    [string] $TesseractVersion = "5.5.0.20241111",
    [string[]] $TessdataLanguages = @("eng", "fra", "osd"),
    [switch] $Force
)

$ErrorActionPreference = "Stop"

$qpdfUrl = "https://github.com/qpdf/qpdf/releases/download/v$QpdfVersion/qpdf-$QpdfVersion-msvc64.zip"
$tessdataBaseUrl = "https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main"

function New-CleanDirectory {
    param([string] $Path)

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Get-TemporaryDirectory {
    $tempPath = Join-Path ([System.IO.Path]::GetTempPath()) "velune-native-tools-$(New-Guid)"
    New-Item -ItemType Directory -Path $tempPath -Force | Out-Null
    return $tempPath
}

function Invoke-Download {
    param(
        [string] $Uri,
        [string] $OutFile,
        [string] $Description
    )

    Write-Host "Downloading $Description..."
    Write-Host "  URL: $Uri"

    $progressPreference = 'SilentlyContinue'
    Invoke-WebRequest -Uri $Uri -OutFile $OutFile -UseBasicParsing
    $progressPreference = 'Continue'

    if (-not (Test-Path -LiteralPath $OutFile)) {
        throw "Download failed: $OutFile does not exist after download."
    }

    $size = (Get-Item -LiteralPath $OutFile).Length
    Write-Host "  Downloaded: $([math]::Round($size / 1MB, 1)) MB"
}

function Install-Qpdf {
    param(
        [string] $DestinationDirectory,
        [string] $TempDirectory
    )

    $zipPath = Join-Path $TempDirectory "qpdf.zip"
    Invoke-Download -Uri $qpdfUrl -OutFile $zipPath -Description "qpdf $QpdfVersion"

    $extractPath = Join-Path $TempDirectory "qpdf-extract"
    Expand-Archive -LiteralPath $zipPath -DestinationPath $extractPath -Force

    $qpdfRoot = Get-ChildItem -LiteralPath $extractPath -Directory | Select-Object -First 1
    if ($null -eq $qpdfRoot) {
        throw "Failed to locate extracted qpdf directory."
    }

    $binDir = Join-Path $DestinationDirectory "qpdf/bin"
    New-Item -ItemType Directory -Path $binDir -Force | Out-Null

    $sourceBin = Join-Path $qpdfRoot.FullName "bin"
    Copy-Item -Path (Join-Path $sourceBin "*") -Destination $binDir -Recurse -Force

    $sourceLib = Join-Path $qpdfRoot.FullName "lib"
    if (Test-Path -LiteralPath $sourceLib) {
        $libDir = Join-Path $DestinationDirectory "qpdf/lib"
        New-Item -ItemType Directory -Path $libDir -Force | Out-Null
        Copy-Item -Path (Join-Path $sourceLib "*") -Destination $libDir -Recurse -Force
    }

    $qpdfExe = Join-Path $binDir "qpdf.exe"
    if (-not (Test-Path -LiteralPath $qpdfExe)) {
        throw "qpdf.exe not found after extraction at: $qpdfExe"
    }

    Write-Host "  qpdf $QpdfVersion installed to: $binDir"
}

function Install-Tesseract {
    param(
        [string] $DestinationDirectory,
        [string] $TempDirectory
    )

    Write-Host "Installing Tesseract $TesseractVersion via Chocolatey..."
    & choco install tesseract --version=$TesseractVersion --yes --no-progress
    if ($LASTEXITCODE -ne 0) {
        throw "Chocolatey install of Tesseract failed with exit code $LASTEXITCODE."
    }

    $chocoInstallDir = "C:\Program Files\Tesseract-OCR"
    if (-not (Test-Path -LiteralPath $chocoInstallDir)) {
        $chocoInstallDir = (Get-Command tesseract -CommandType Application -ErrorAction Stop).Source | Split-Path
    }

    $binDir = Join-Path $DestinationDirectory "tesseract/bin"
    New-Item -ItemType Directory -Path $binDir -Force | Out-Null

    Copy-Item -Path (Join-Path $chocoInstallDir "tesseract.exe") -Destination $binDir -Force
    Copy-Item -Path (Join-Path $chocoInstallDir "*.dll") -Destination $binDir -Force -ErrorAction SilentlyContinue

    $tesseractExe = Join-Path $binDir "tesseract.exe"
    if (-not (Test-Path -LiteralPath $tesseractExe)) {
        throw "tesseract.exe not found after Chocolatey install at: $tesseractExe"
    }

    Write-Host "  Tesseract $TesseractVersion installed to: $binDir"
}

function Install-Tessdata {
    param([string] $DestinationDirectory)

    $tessdataDir = Join-Path $DestinationDirectory "tesseract/tessdata"
    New-Item -ItemType Directory -Path $tessdataDir -Force | Out-Null

    foreach ($language in $TessdataLanguages) {
        $targetPath = Join-Path $tessdataDir "$language.traineddata"

        if ((Test-Path -LiteralPath $targetPath) -and -not $Force) {
            Write-Host "  $language.traineddata already exists, skipping."
            continue
        }

        $url = "$tessdataBaseUrl/$language.traineddata"
        Invoke-Download -Uri $url -OutFile $targetPath -Description "$language.traineddata"
    }
}

function Assert-NativeTools {
    param([string] $ToolsDirectory)

    $required = @(
        (Join-Path $ToolsDirectory "qpdf/bin/qpdf.exe"),
        (Join-Path $ToolsDirectory "tesseract/bin/tesseract.exe"),
        (Join-Path $ToolsDirectory "tesseract/tessdata/eng.traineddata"),
        (Join-Path $ToolsDirectory "tesseract/tessdata/fra.traineddata")
    )

    $missing = @()
    foreach ($path in $required) {
        if (-not (Test-Path -LiteralPath $path)) {
            $missing += $path
        }
    }

    if ($missing.Count -gt 0) {
        throw "Native tools validation failed. Missing:`n$($missing -join "`n")"
    }

    Write-Host ""
    Write-Host "Validation passed. All required native tools present."
}

# Main

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$toolsDirectory = Join-Path $repositoryRoot $OutputRoot

if ((Test-Path -LiteralPath (Join-Path $toolsDirectory "qpdf/bin/qpdf.exe")) -and
    (Test-Path -LiteralPath (Join-Path $toolsDirectory "tesseract/bin/tesseract.exe")) -and
    (Test-Path -LiteralPath (Join-Path $toolsDirectory "tesseract/tessdata/eng.traineddata")) -and
    -not $Force) {
    Write-Host "Native tools already present at: $toolsDirectory"
    Write-Host "Use -Force to re-download."
    Assert-NativeTools -ToolsDirectory $toolsDirectory

    [pscustomobject]@{
        ToolsRoot         = $toolsDirectory
        QpdfVersion       = $QpdfVersion
        TesseractVersion  = $TesseractVersion
        Status            = "already-present"
    } | ConvertTo-Json -Compress
    return
}

Write-Host "=== Downloading Windows native tools ==="
Write-Host "  qpdf:      $QpdfVersion"
Write-Host "  Tesseract: $TesseractVersion"
Write-Host "  Languages: $($TessdataLanguages -join ', ')"
Write-Host "  Output:    $toolsDirectory"
Write-Host ""

New-CleanDirectory -Path $toolsDirectory
$tempDirectory = Get-TemporaryDirectory

try {
    Install-Qpdf -DestinationDirectory $toolsDirectory -TempDirectory $tempDirectory
    Install-Tesseract -DestinationDirectory $toolsDirectory -TempDirectory $tempDirectory
    Install-Tessdata -DestinationDirectory $toolsDirectory
    Assert-NativeTools -ToolsDirectory $toolsDirectory
} finally {
    if (Test-Path -LiteralPath $tempDirectory) {
        Remove-Item -LiteralPath $tempDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Write-Host "=== Native tools ready ==="

[pscustomobject]@{
    ToolsRoot         = $toolsDirectory
    QpdfVersion       = $QpdfVersion
    TesseractVersion  = $TesseractVersion
    Status            = "downloaded"
} | ConvertTo-Json -Compress
