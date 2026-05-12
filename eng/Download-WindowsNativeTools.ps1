param(
    [string] $OutputRoot = "artifacts/native-tools/win-x64",
    [string] $QpdfVersion = "11.9.1",
    [string] $TesseractVersion = "5.5.0",
    [string[]] $TessdataLanguages = @("eng", "fra", "osd"),
    [switch] $Force
)

$ErrorActionPreference = "Stop"

$qpdfUrl = "https://github.com/qpdf/qpdf/releases/download/v$QpdfVersion/qpdf-$QpdfVersion-msvc64.zip"
$tesseractUrl = "https://github.com/tesseract-ocr/tesseract/releases/download/$TesseractVersion/tesseract-ocr-w64-setup-$TesseractVersion.20241111.exe"
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

    $installerPath = Join-Path $TempDirectory "tesseract-setup.exe"
    Invoke-Download -Uri $tesseractUrl -OutFile $installerPath -Description "Tesseract $TesseractVersion"

    $extractPath = Join-Path $TempDirectory "tesseract-extract"
    New-Item -ItemType Directory -Path $extractPath -Force | Out-Null

    Write-Host "  Extracting Tesseract installer (silent extract)..."
    $process = Start-Process -FilePath $installerPath `
        -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/DIR=$extractPath", "/NOICONS", "/NORESTART" `
        -Wait -PassThru -NoNewWindow

    if ($process.ExitCode -ne 0) {
        Write-Host "  Inno Setup installer returned exit code $($process.ExitCode), trying 7z extraction..."
        $sevenZip = Get-Command "7z" -CommandType Application -ErrorAction SilentlyContinue
        if ($null -ne $sevenZip) {
            & 7z x $installerPath "-o$extractPath" -y | Out-Null
        } else {
            throw "Tesseract extraction failed and 7z is not available."
        }
    }

    $binDir = Join-Path $DestinationDirectory "tesseract/bin"
    $libDir = Join-Path $DestinationDirectory "tesseract/lib"
    New-Item -ItemType Directory -Path $binDir -Force | Out-Null
    New-Item -ItemType Directory -Path $libDir -Force | Out-Null

    $tesseractExe = Get-ChildItem -LiteralPath $extractPath -Filter "tesseract.exe" -Recurse -File |
        Select-Object -First 1

    if ($null -eq $tesseractExe) {
        throw "tesseract.exe not found in extracted content."
    }

    $tesseractBinSource = $tesseractExe.DirectoryName
    Copy-Item -Path (Join-Path $tesseractBinSource "*.exe") -Destination $binDir -Force
    Copy-Item -Path (Join-Path $tesseractBinSource "*.dll") -Destination $binDir -Force -ErrorAction SilentlyContinue

    $dllFiles = Get-ChildItem -LiteralPath $extractPath -Filter "*.dll" -Recurse -File
    foreach ($dll in $dllFiles) {
        $destPath = Join-Path $binDir $dll.Name
        if (-not (Test-Path -LiteralPath $destPath)) {
            Copy-Item -LiteralPath $dll.FullName -Destination $destPath -Force
        }
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
