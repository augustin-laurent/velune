param(
    [Parameter(Mandatory = $true)]
    [string] $RuntimeIdentifier,

    [string] $OutputRoot = "artifacts/native-tools"
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

function Get-ExecutableName {
    param([string] $Name)

    if ($RuntimeIdentifier.StartsWith("win-", [System.StringComparison]::Ordinal)) {
        return "$Name.exe"
    }

    return $Name
}

function Find-Executable {
    param([string] $Name)

    $executableName = Get-ExecutableName -Name $Name
    $candidates = [System.Collections.Generic.List[string]]::new()

    function Add-Candidate {
        param([string] $Path)

        if (-not [string]::IsNullOrWhiteSpace($Path)) {
            $candidates.Add($Path)
        }
    }

    if ($RuntimeIdentifier.StartsWith("win-", [System.StringComparison]::Ordinal)) {
        $programFiles = [Environment]::GetEnvironmentVariable("ProgramFiles")
        $programFilesX86 = [Environment]::GetEnvironmentVariable("ProgramFiles(x86)")
        $chocolateyRoot = if (-not [string]::IsNullOrWhiteSpace($env:ChocolateyInstall)) {
            $env:ChocolateyInstall
        } else {
            "C:\ProgramData\chocolatey"
        }

        if ($Name.Equals("tesseract", [System.StringComparison]::OrdinalIgnoreCase)) {
            foreach ($root in @($programFiles, $programFilesX86)) {
                if (-not [string]::IsNullOrWhiteSpace($root)) {
                    Add-Candidate -Path (Join-Path $root "Tesseract-OCR\$executableName")
                }
            }
        } elseif ($Name.Equals("qpdf", [System.StringComparison]::OrdinalIgnoreCase)) {
            $qpdfToolsRoot = Join-Path $chocolateyRoot "lib\qpdf\tools"
            foreach ($path in @(
                    (Join-Path $qpdfToolsRoot $executableName),
                    (Join-Path $qpdfToolsRoot "bin\$executableName"))) {
                Add-Candidate -Path $path
            }

            if (Test-Path -LiteralPath $qpdfToolsRoot) {
                Get-ChildItem -LiteralPath $qpdfToolsRoot -Filter $executableName -Recurse -File -ErrorAction SilentlyContinue |
                    ForEach-Object { Add-Candidate -Path $_.FullName }
            }
        }
    } elseif ($RuntimeIdentifier.StartsWith("osx-", [System.StringComparison]::Ordinal)) {
        foreach ($path in @(
                "/opt/homebrew/bin/$executableName",
                "/usr/local/bin/$executableName",
                "/usr/bin/$executableName")) {
            Add-Candidate -Path $path
        }
    } else {
        foreach ($path in @(
                "/usr/bin/$executableName",
                "/usr/local/bin/$executableName",
                "/bin/$executableName")) {
            Add-Candidate -Path $path
        }
    }

    $commands = Get-Command @($Name, $executableName) -CommandType Application -All -ErrorAction SilentlyContinue
    foreach ($command in $commands) {
        foreach ($propertyName in @("Path", "Source", "Definition")) {
            $property = $command.PSObject.Properties[$propertyName]
            if ($null -ne $property) {
                Add-Candidate -Path ([string] $property.Value)
            }
        }
    }

    $selected = $candidates |
        Where-Object { Test-Path -LiteralPath $_ } |
        Where-Object { $_ -notmatch "\\chocolatey\\bin\\" } |
        Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($selected)) {
        $checkedCandidates = ($candidates | Select-Object -Unique) -join ", "
        throw "Unable to locate $Name for $RuntimeIdentifier. Checked: $checkedCandidates"
    }

    return $selected
}

function Copy-ExecutableBundle {
    param(
        [string] $ExecutablePath,
        [string] $DestinationDirectory
    )

    New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null

    if ($RuntimeIdentifier.StartsWith("win-", [System.StringComparison]::Ordinal)) {
        $sourceDirectory = Split-Path -Parent $ExecutablePath
        Copy-Item -Path (Join-Path $sourceDirectory "*") -Destination $DestinationDirectory -Recurse -Force
    } else {
        Copy-Item -LiteralPath $ExecutablePath -Destination (Join-Path $DestinationDirectory (Split-Path -Leaf $ExecutablePath)) -Force
    }
}

function Get-LinkedLibraries {
    param([string] $BinaryPath)

    if ($RuntimeIdentifier.StartsWith("linux-", [System.StringComparison]::Ordinal)) {
        $lddOutput = & ldd $BinaryPath
        foreach ($line in $lddOutput) {
            $matches = [regex]::Matches($line, "=>\s+(/\S+)|^\s*(/\S+)")
            foreach ($match in $matches) {
                $libraryPath = if ($match.Groups[1].Success) { $match.Groups[1].Value } else { $match.Groups[2].Value }
                if ([string]::IsNullOrWhiteSpace($libraryPath) -or
                    $libraryPath -match "ld-linux|linux-vdso") {
                    continue
                }

                if (Test-Path -LiteralPath $libraryPath) {
                    $libraryPath
                }
            }
        }
    } elseif ($RuntimeIdentifier.StartsWith("osx-", [System.StringComparison]::Ordinal)) {
        $otoolOutput = & otool -L $BinaryPath
        foreach ($line in $otoolOutput | Select-Object -Skip 1) {
            $libraryPath = ($line.Trim() -split "\s+")[0]
            if ([string]::IsNullOrWhiteSpace($libraryPath) -or
                $libraryPath.StartsWith("/usr/lib/", [System.StringComparison]::Ordinal) -or
                $libraryPath.StartsWith("/System/Library/", [System.StringComparison]::Ordinal)) {
                continue
            }

            if (Test-Path -LiteralPath $libraryPath) {
                $libraryPath
            }
        }
    }
}

function Copy-LinkedLibraries {
    param(
        [string] $ExecutablePath,
        [string] $DestinationDirectory
    )

    if ($RuntimeIdentifier.StartsWith("win-", [System.StringComparison]::Ordinal)) {
        return
    }

    New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null

    $queue = [System.Collections.Generic.Queue[string]]::new()
    $visited = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $queue.Enqueue($ExecutablePath)

    while ($queue.Count -gt 0) {
        $binaryPath = $queue.Dequeue()
        if (-not $visited.Add($binaryPath)) {
            continue
        }

        foreach ($libraryPath in Get-LinkedLibraries -BinaryPath $binaryPath) {
            $destinationPath = Join-Path $DestinationDirectory (Split-Path -Leaf $libraryPath)
            Copy-Item -LiteralPath $libraryPath -Destination $destinationPath -Force
            $queue.Enqueue($libraryPath)
        }
    }
}

function Repair-MacToolBundle {
    param(
        [string] $ExecutablePath,
        [string] $LibraryDirectory
    )

    if (-not $RuntimeIdentifier.StartsWith("osx-", [System.StringComparison]::Ordinal)) {
        return
    }

    if (-not (Test-Path -LiteralPath $LibraryDirectory)) {
        return
    }

    $libraries = Get-ChildItem -LiteralPath $LibraryDirectory -Filter "*.dylib" -File -ErrorAction SilentlyContinue

    foreach ($binaryPath in @($ExecutablePath)) {
        foreach ($dependencyPath in Get-LinkedLibraries -BinaryPath $binaryPath) {
            $dependencyFileName = Split-Path -Leaf $dependencyPath
            if (Test-Path -LiteralPath (Join-Path $LibraryDirectory $dependencyFileName)) {
                & install_name_tool -change $dependencyPath "@executable_path/../lib/$dependencyFileName" $binaryPath
            }
        }
    }

    foreach ($library in $libraries) {
        & install_name_tool -id "@loader_path/$($library.Name)" $library.FullName

        foreach ($dependencyPath in Get-LinkedLibraries -BinaryPath $library.FullName) {
            $dependencyFileName = Split-Path -Leaf $dependencyPath
            if (Test-Path -LiteralPath (Join-Path $LibraryDirectory $dependencyFileName)) {
                & install_name_tool -change $dependencyPath "@loader_path/$dependencyFileName" $library.FullName
            }
        }
    }
}

function Find-TessdataDirectory {
    $candidateDirectories = @()

    if ($RuntimeIdentifier.StartsWith("win-", [System.StringComparison]::Ordinal)) {
        $candidateDirectories += @(
            "C:\Program Files\Tesseract-OCR\tessdata",
            "C:\Program Files (x86)\Tesseract-OCR\tessdata"
        )
    } elseif ($RuntimeIdentifier.StartsWith("osx-", [System.StringComparison]::Ordinal)) {
        $candidateDirectories += @(
            "/opt/homebrew/share/tessdata",
            "/usr/local/share/tessdata"
        )
    } else {
        $candidateDirectories += @(
            "/usr/share/tesseract-ocr/5/tessdata",
            "/usr/share/tesseract-ocr/4.00/tessdata",
            "/usr/share/tessdata"
        )
    }

    if (-not [string]::IsNullOrWhiteSpace($env:TESSDATA_PREFIX)) {
        $candidateDirectories = @($env:TESSDATA_PREFIX) + $candidateDirectories
    }

    foreach ($candidateDirectory in $candidateDirectories) {
        if (Test-Path -LiteralPath (Join-Path $candidateDirectory "eng.traineddata")) {
            return $candidateDirectory
        }
    }

    throw "Unable to locate tessdata for $RuntimeIdentifier."
}

function Copy-Tessdata {
    param(
        [string] $SourceDirectory,
        [string] $DestinationDirectory
    )

    New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null

    foreach ($language in @("eng", "fra", "osd")) {
        $sourcePath = Join-Path $SourceDirectory "$language.traineddata"
        if (Test-Path -LiteralPath $sourcePath) {
            Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $DestinationDirectory "$language.traineddata") -Force
        }
    }
}

function Assert-NativeTools {
    param([string] $ToolsDirectory)

    $qpdfExecutable = Join-Path $ToolsDirectory (Join-Path "qpdf/bin" (Get-ExecutableName -Name "qpdf"))
    $tesseractExecutable = Join-Path $ToolsDirectory (Join-Path "tesseract/bin" (Get-ExecutableName -Name "tesseract"))
    $engData = Join-Path $ToolsDirectory "tesseract/tessdata/eng.traineddata"
    $fraData = Join-Path $ToolsDirectory "tesseract/tessdata/fra.traineddata"
    $missing = @()

    foreach ($requiredPath in @($qpdfExecutable, $tesseractExecutable, $engData, $fraData)) {
        if (-not (Test-Path -LiteralPath $requiredPath)) {
            $missing += $requiredPath
        }
    }

    if ($missing.Count -gt 0) {
        throw "Native tool collection is incomplete. Missing: $($missing -join ', ')"
    }
}

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$toolsRoot = Join-Path (Join-Path $repositoryRoot $OutputRoot) $RuntimeIdentifier

New-CleanDirectory -Path $toolsRoot

$qpdfExecutable = Find-Executable -Name "qpdf"
$tesseractExecutable = Find-Executable -Name "tesseract"
$tessdataDirectory = Find-TessdataDirectory

$qpdfBin = Join-Path $toolsRoot "qpdf/bin"
$qpdfLib = Join-Path $toolsRoot "qpdf/lib"
$tesseractBin = Join-Path $toolsRoot "tesseract/bin"
$tesseractLib = Join-Path $toolsRoot "tesseract/lib"
$tessdataDestination = Join-Path $toolsRoot "tesseract/tessdata"

Copy-ExecutableBundle -ExecutablePath $qpdfExecutable -DestinationDirectory $qpdfBin
Copy-LinkedLibraries -ExecutablePath $qpdfExecutable -DestinationDirectory $qpdfLib
Copy-ExecutableBundle -ExecutablePath $tesseractExecutable -DestinationDirectory $tesseractBin
Copy-LinkedLibraries -ExecutablePath $tesseractExecutable -DestinationDirectory $tesseractLib
Repair-MacToolBundle -ExecutablePath (Join-Path $qpdfBin (Split-Path -Leaf $qpdfExecutable)) -LibraryDirectory $qpdfLib
Repair-MacToolBundle -ExecutablePath (Join-Path $tesseractBin (Split-Path -Leaf $tesseractExecutable)) -LibraryDirectory $tesseractLib
Copy-Tessdata -SourceDirectory $tessdataDirectory -DestinationDirectory $tessdataDestination
Assert-NativeTools -ToolsDirectory $toolsRoot

[pscustomobject]@{
    RuntimeIdentifier = $RuntimeIdentifier
    ToolsRoot = $toolsRoot
    QpdfExecutable = $qpdfExecutable
    TesseractExecutable = $tesseractExecutable
    TessdataDirectory = $tessdataDirectory
} | ConvertTo-Json -Compress
