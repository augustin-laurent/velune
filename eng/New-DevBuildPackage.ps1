param(
    [Parameter(Mandatory = $true)]
    [string] $RuntimeIdentifier,

    [Parameter(Mandatory = $true)]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [string] $InformationalVersion,

    [string] $Configuration = "Release",
    [string] $ProjectPath = "src/Velune.App/Velune.App.csproj",
    [string] $OutputRoot = "artifacts/dev-builds",
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

function New-ZipArchive {
    param(
        [string] $SourceDirectory,
        [string] $DestinationPath
    )

    if (Test-Path -LiteralPath $DestinationPath) {
        Remove-Item -LiteralPath $DestinationPath -Force
    }

    Compress-Archive -Path (Join-Path $SourceDirectory "*") -DestinationPath $DestinationPath -Force
}

function New-TarGzArchive {
    param(
        [string] $SourceDirectory,
        [string] $DestinationPath,
        [string] $EntryName
    )

    if (Test-Path -LiteralPath $DestinationPath) {
        Remove-Item -LiteralPath $DestinationPath -Force
    }

    $sourceParent = Split-Path -Parent $SourceDirectory
    & tar -czf $DestinationPath -C $sourceParent $EntryName

    if ($LASTEXITCODE -ne 0) {
        throw "tar failed with exit code $LASTEXITCODE"
    }
}

function Set-ExecutableBit {
    param([string] $Path)

    if (-not $IsWindows -and (Test-Path -LiteralPath $Path)) {
        & chmod +x $Path
    }
}

function Get-PlatformExecutableName {
    param([string] $Name)

    if ($RuntimeIdentifier.StartsWith("win-", [System.StringComparison]::Ordinal)) {
        return "$Name.exe"
    }

    return $Name
}

function Assert-BundledNativeTools {
    param([string] $ToolsDirectory)

    $qpdfPath = Join-Path $ToolsDirectory (Join-Path "qpdf/bin" (Get-PlatformExecutableName -Name "qpdf"))
    $tesseractPath = Join-Path $ToolsDirectory (Join-Path "tesseract/bin" (Get-PlatformExecutableName -Name "tesseract"))
    $englishDataPath = Join-Path $ToolsDirectory "tesseract/tessdata/eng.traineddata"
    $frenchDataPath = Join-Path $ToolsDirectory "tesseract/tessdata/fra.traineddata"
    $missing = @()

    foreach ($requiredPath in @($qpdfPath, $tesseractPath, $englishDataPath, $frenchDataPath)) {
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

    Set-ExecutableBit -Path (Join-Path $destinationToolsDirectory (Join-Path "qpdf/bin" (Get-PlatformExecutableName -Name "qpdf")))
    Set-ExecutableBit -Path (Join-Path $destinationToolsDirectory (Join-Path "tesseract/bin" (Get-PlatformExecutableName -Name "tesseract")))
}

function Write-MacInfoPlist {
    param(
        [string] $Path,
        [string] $Version
    )

    $shortVersion = ($Version -split "-", 2)[0]
    $bundleVersion = $shortVersion

    $content = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDisplayName</key>
    <string>Velune</string>
    <key>CFBundleExecutable</key>
    <string>Velune.App</string>
    <key>CFBundleIconFile</key>
    <string>Velune</string>
    <key>CFBundleIdentifier</key>
    <string>app.velune.desktop</string>
    <key>CFBundleName</key>
    <string>Velune</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>$shortVersion</string>
    <key>CFBundleVersion</key>
    <string>$bundleVersion</string>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
"@

    Set-Content -LiteralPath $Path -Value $content -NoNewline
}

function Write-LinuxDesktopFile {
    param(
        [string] $Path,
        [string] $Version
    )

    $content = @"
[Desktop Entry]
Type=Application
Name=Velune
Comment=PDF and image viewer
Exec=Velune.App %F
Icon=velune
Terminal=false
Categories=Office;Viewer;Graphics;
MimeType=application/pdf;image/png;image/jpeg;image/webp;
X-Velune-Version=$Version
"@

    Set-Content -LiteralPath $Path -Value $content -NoNewline
}

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectFullPath = Join-Path $repositoryRoot $ProjectPath
$outputFullRoot = Join-Path $repositoryRoot $OutputRoot
$publishDir = Join-Path $outputFullRoot "publish/$RuntimeIdentifier"
$stagingRoot = Join-Path $outputFullRoot "staging/$RuntimeIdentifier"
$packageRoot = Join-Path $outputFullRoot "packages"
$safeVersion = $Version -replace "[^0-9A-Za-z\.\-]+", "-"

New-CleanDirectory -Path $publishDir
New-CleanDirectory -Path $stagingRoot
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null

dotnet publish $projectFullPath `
    --configuration $Configuration `
    --runtime $RuntimeIdentifier `
    --self-contained true `
    --output $publishDir `
    /p:Version=$Version `
    /p:InformationalVersion=$InformationalVersion `
    /p:PublishSingleFile=false

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$packageName = "Velune-$safeVersion-$RuntimeIdentifier"

if ($RuntimeIdentifier.StartsWith("osx-", [System.StringComparison]::Ordinal)) {
    $appBundle = Join-Path $stagingRoot "Velune.app"
    $contents = Join-Path $appBundle "Contents"
    $macOs = Join-Path $contents "MacOS"
    $resources = Join-Path $contents "Resources"

    New-Item -ItemType Directory -Path $macOs -Force | Out-Null
    New-Item -ItemType Directory -Path $resources -Force | Out-Null
    Copy-DirectoryContents -Source $publishDir -Destination $macOs
    Copy-BundledNativeTools -DestinationBaseDirectory $macOs
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "src/Velune.App/Assets/Brand/Velune.icns") -Destination (Join-Path $resources "Velune.icns") -Force
    Write-MacInfoPlist -Path (Join-Path $contents "Info.plist") -Version $Version
    Set-ExecutableBit -Path (Join-Path $macOs "Velune.App")

    $packagePath = Join-Path $packageRoot "$packageName.tar.gz"
    New-TarGzArchive -SourceDirectory $appBundle -DestinationPath $packagePath -EntryName "Velune.app"
} elseif ($RuntimeIdentifier.StartsWith("linux-", [System.StringComparison]::Ordinal)) {
    $linuxBundle = Join-Path $stagingRoot "Velune"
    Copy-DirectoryContents -Source $publishDir -Destination $linuxBundle
    Copy-BundledNativeTools -DestinationBaseDirectory $linuxBundle
    Set-ExecutableBit -Path (Join-Path $linuxBundle "Velune.App")
    Write-LinuxDesktopFile -Path (Join-Path $linuxBundle "velune.desktop") -Version $Version
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "src/Velune.App/Assets/Brand/velune-app-icon.png") -Destination (Join-Path $linuxBundle "velune.png") -Force

    $packagePath = Join-Path $packageRoot "$packageName.tar.gz"
    New-TarGzArchive -SourceDirectory $linuxBundle -DestinationPath $packagePath -EntryName "Velune"
} elseif ($RuntimeIdentifier.StartsWith("win-", [System.StringComparison]::Ordinal)) {
    $windowsBundle = Join-Path $stagingRoot "Velune"
    Copy-DirectoryContents -Source $publishDir -Destination $windowsBundle
    Copy-BundledNativeTools -DestinationBaseDirectory $windowsBundle

    $packagePath = Join-Path $packageRoot "$packageName.zip"
    New-ZipArchive -SourceDirectory $windowsBundle -DestinationPath $packagePath
} else {
    throw "Unsupported runtime identifier: $RuntimeIdentifier"
}

Write-Sha256File -Path $packagePath

[pscustomobject]@{
    RuntimeIdentifier = $RuntimeIdentifier
    Version = $Version
    InformationalVersion = $InformationalVersion
    PackagePath = $packagePath
    ChecksumPath = "$packagePath.sha256"
} | ConvertTo-Json -Compress
