param(
    [string] $TagName = $env:GITHUB_REF_NAME,
    [string] $Sha = $env:GITHUB_SHA
)

$ErrorActionPreference = "Stop"

function Convert-ToShortSha {
    param([string] $Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return "local"
    }

    return $Value.Substring(0, [Math]::Min(7, $Value.Length))
}

if ([string]::IsNullOrWhiteSpace($TagName)) {
    throw "Release tag is required."
}

if ($TagName.StartsWith("refs/tags/", [System.StringComparison]::Ordinal)) {
    $TagName = $TagName.Substring("refs/tags/".Length)
}

$tag = $TagName.Trim()

if ($tag -notmatch "^v(?<version>\d+\.\d+\.\d+-beta\.\d+)$") {
    throw "Beta releases must use tags like v1.0.0-beta.1. Received: $tag"
}

$version = $Matches["version"]
$baseVersion = ($version -split "-", 2)[0]
$shortSha = Convert-ToShortSha -Value $Sha
$informationalVersion = "$version+$shortSha"

[pscustomobject]@{
    TagName = $tag
    BaseVersion = $baseVersion
    Version = $version
    InformationalVersion = $informationalVersion
    ShortSha = $shortSha
} | ConvertTo-Json -Compress
