param(
    [string] $RefName = $env:GITHUB_REF_NAME,
    [string] $RunNumber = $env:GITHUB_RUN_NUMBER,
    [string] $Sha = $env:GITHUB_SHA,
    [string] $PropsPath = "Directory.Build.props"
)

$ErrorActionPreference = "Stop"

function Get-VersionPrefix {
    param([string] $Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Version props file not found: $Path"
    }

    [xml] $props = Get-Content -LiteralPath $Path
    $versionPrefix = $props.Project.PropertyGroup.VersionPrefix | Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($versionPrefix)) {
        throw "VersionPrefix is missing from $Path"
    }

    return $versionPrefix.Trim()
}

function Convert-ToChannelName {
    param([string] $Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return "local"
    }

    $channel = $Value.ToLowerInvariant() -replace "[^0-9a-z]+", "."
    $channel = $channel.Trim(".")

    if ([string]::IsNullOrWhiteSpace($channel)) {
        return "local"
    }

    return $channel
}

function Convert-ToShortSha {
    param([string] $Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return "local"
    }

    return $Value.Substring(0, [Math]::Min(7, $Value.Length))
}

$baseVersion = Get-VersionPrefix -Path $PropsPath
$channel = Convert-ToChannelName -Value $RefName

if ($RefName -in @("main", "master")) {
    $channel = "dev"
} elseif ($RefName -like "maintenance/*") {
    $channel = "maintenance"
    $maintenanceTag = $RefName.Substring("maintenance/".Length).Trim()
    $maintenanceTag = $maintenanceTag -replace "^[vV]", ""

    if ($maintenanceTag -match "^\d+\.\d+\.\d+$") {
        $baseVersion = $maintenanceTag
    }
}

if ([string]::IsNullOrWhiteSpace($RunNumber)) {
    $RunNumber = [DateTimeOffset]::UtcNow.ToString("yyyyMMddHHmmss")
}

$shortSha = Convert-ToShortSha -Value $Sha
$version = "$baseVersion-$channel.$RunNumber"
$informationalVersion = "$version+$shortSha"

[pscustomobject]@{
    BaseVersion = $baseVersion
    Channel = $channel
    Version = $version
    InformationalVersion = $informationalVersion
    ShortSha = $shortSha
} | ConvertTo-Json -Compress
