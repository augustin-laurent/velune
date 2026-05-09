param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [string] $InformationalVersion,

    [string] $RuntimeIdentifier = "win-x64",
    [string] $Configuration = "Release",
    [string] $ProjectPath = "src/Velune.Windows/Velune.Windows.csproj",
    [string] $OutputRoot = "artifacts/windows-installer",
    [string] $NativeToolsRoot = "artifacts/native-tools",
    [bool] $RequireBundledNativeTools = $true
)

$ErrorActionPreference = "Stop"

function Remove-DirectoryIfExists {
    param([string] $Path)

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function New-CleanDirectory {
    param([string] $Path)

    Remove-DirectoryIfExists -Path $Path
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Copy-DirectoryContents {
    param(
        [string] $Source,
        [string] $Destination
    )

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Copy-Item -Path (Join-Path $Source "*") -Destination $Destination -Recurse -Force
}

function Get-Sha256 {
    param([string] $Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Write-Sha256File {
    param([string] $Path)

    $hash = Get-Sha256 -Path $Path
    $fileName = Split-Path -Leaf $Path
    Set-Content -LiteralPath "$Path.sha256" -Value "$hash  $fileName" -NoNewline
}

function Invoke-LoggedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock] $Command,

        [Parameter(Mandatory = $true)]
        [string] $ErrorMessage
    )

    & $Command 2>&1 | ForEach-Object {
        Write-Host $_
    }

    if ($LASTEXITCODE -ne 0) {
        throw "$ErrorMessage Exit code: $LASTEXITCODE"
    }
}

function Assert-BundledNativeTools {
    param([string] $ToolsDirectory)

    $missing = @()
    foreach ($requiredPath in @(
            (Join-Path $ToolsDirectory "qpdf/bin/qpdf.exe"),
            (Join-Path $ToolsDirectory "tesseract/bin/tesseract.exe"),
            (Join-Path $ToolsDirectory "tesseract/tessdata/eng.traineddata"),
            (Join-Path $ToolsDirectory "tesseract/tessdata/fra.traineddata"))) {
        if (-not (Test-Path -LiteralPath $requiredPath)) {
            $missing += $requiredPath
        }
    }

    if ($missing.Count -gt 0) {
        throw "Bundled native tools are incomplete for $RuntimeIdentifier. Missing: $($missing -join ', ')"
    }
}

function Copy-BundledNativeTools {
    param([string] $DestinationBaseDirectory)

    $sourceToolsDirectory = Join-Path (Join-Path $repositoryRoot $NativeToolsRoot) $RuntimeIdentifier
    if (-not (Test-Path -LiteralPath $sourceToolsDirectory)) {
        if ($RequireBundledNativeTools) {
            throw "Bundled native tools directory not found for ${RuntimeIdentifier}: $sourceToolsDirectory"
        }

        return
    }

    $destinationToolsDirectory = Join-Path $DestinationBaseDirectory "tools"
    Copy-DirectoryContents -Source $sourceToolsDirectory -Destination $destinationToolsDirectory

    if ($RequireBundledNativeTools) {
        Assert-BundledNativeTools -ToolsDirectory $destinationToolsDirectory
    }
}

function Resolve-InnoSetupCompiler {
    $programFilesX86 = [Environment]::GetEnvironmentVariable("ProgramFiles(x86)")
    $programFiles = [Environment]::GetEnvironmentVariable("ProgramFiles")
    $candidates = @(
        $(if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) { Join-Path $programFilesX86 "Inno Setup 6\ISCC.exe" }),
        $(if (-not [string]::IsNullOrWhiteSpace($programFiles)) { Join-Path $programFiles "Inno Setup 6\ISCC.exe" })
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($candidate in $candidates) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and
            (Test-Path -LiteralPath $candidate)) {
            return $candidate
        }
    }

    $command = Get-Command "ISCC.exe" -CommandType Application -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    throw "Unable to locate Inno Setup compiler (ISCC.exe)."
}

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectFullPath = Join-Path $repositoryRoot $ProjectPath
$outputFullRoot = Join-Path $repositoryRoot $OutputRoot
$publishDir = Join-Path $outputFullRoot "publish/$RuntimeIdentifier"
$packageRoot = Join-Path $outputFullRoot "packages"

# Auto-download native tools if not already present
$nativeToolsPath = Join-Path (Join-Path $repositoryRoot $NativeToolsRoot) $RuntimeIdentifier
if ($RequireBundledNativeTools -and
    $RuntimeIdentifier.StartsWith("win-", [System.StringComparison]::Ordinal) -and
    -not (Test-Path -LiteralPath (Join-Path $nativeToolsPath "qpdf/bin/qpdf.exe"))) {
    Write-Host "Native tools not found. Downloading automatically..."
    $downloadScript = Join-Path $PSScriptRoot "Download-WindowsNativeTools.ps1"
    & $downloadScript -OutputRoot (Join-Path $NativeToolsRoot $RuntimeIdentifier)
}
$safeVersion = $Version -replace "[^0-9A-Za-z\.\-]+", "-"
$outputBaseName = "Velune-$safeVersion-$RuntimeIdentifier-setup"
$installerScriptPath = Join-Path $repositoryRoot "eng/windows-installer/Velune.iss"
$iconPath = Join-Path $repositoryRoot "src/Velune.App/Assets/Brand/Velune.ico"

New-CleanDirectory -Path $publishDir
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null

Invoke-LoggedCommand `
    -ErrorMessage "dotnet publish failed." `
    -Command {
        dotnet publish $projectFullPath `
            --configuration $Configuration `
            --runtime $RuntimeIdentifier `
            --self-contained true `
            --output $publishDir `
            /p:Version=$Version `
            /p:InformationalVersion=$InformationalVersion `
            /p:PublishSingleFile=false
    }

Copy-BundledNativeTools -DestinationBaseDirectory $publishDir

$iscc = Resolve-InnoSetupCompiler
$env:VELUNE_VERSION = $Version
$env:VELUNE_PUBLISH_DIR = $publishDir
$env:VELUNE_OUTPUT_DIR = $packageRoot
$env:VELUNE_OUTPUT_BASE_NAME = $outputBaseName
$env:VELUNE_ICON_PATH = $iconPath

Invoke-LoggedCommand `
    -ErrorMessage "Inno Setup packaging failed." `
    -Command {
        & $iscc $installerScriptPath
    }

$installerPath = Join-Path $packageRoot "$outputBaseName.exe"
if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "Installer was not created: $installerPath"
}

Write-Sha256File -Path $installerPath

[pscustomobject]@{
    RuntimeIdentifier = $RuntimeIdentifier
    Version = $Version
    InformationalVersion = $InformationalVersion
    PackagePath = $installerPath
    ChecksumPath = "$installerPath.sha256"
} | ConvertTo-Json -Compress
