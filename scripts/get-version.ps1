param(
    [Parameter(Mandatory = $true)]
    [string]$GitRef
)

$ErrorActionPreference = 'Stop'

$version = $GitRef
if ($version.StartsWith('refs/tags/')) {
    $version = $version.Substring('refs/tags/'.Length)
}

if ($version.StartsWith('v')) {
    $version = $version.Substring(1)
}

if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Unsupported version format '$GitRef'. Expected vMajor.Minor.Patch."
}

Write-Output $version
