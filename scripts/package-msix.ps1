param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$Publisher,

    [Parameter(Mandatory = $true)]
    [string]$PublishDir,

    [Parameter(Mandatory = $true)]
    [string]$StartupPublishDir,

    [Parameter(Mandatory = $true)]
    [string]$OutputDir,

    [Parameter(Mandatory = $true)]
    [string]$CertificatePath,

    [Parameter(Mandatory = $true)]
    [string]$CertificatePassword
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$packageRoot = Join-Path $repoRoot 'src\Insighter.Agent.Package'
$stagingDir = Join-Path $OutputDir 'msix-staging'
$manifestTemplate = Join-Path $packageRoot 'AppxManifest.xml'
$manifestPath = Join-Path $stagingDir 'AppxManifest.xml'
$packagePath = Join-Path $OutputDir ("Insighter.Agent_{0}_x64.msix" -f $Version)
$manifestVersion = if ($Version -match '^\d+\.\d+\.\d+$') { "$Version.0" } else { $Version }

function Resolve-SdkToolPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ToolName
    )

    $command = Get-Command $ToolName -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (-not (Test-Path $sdkRoot)) {
        throw "Windows SDK bin directory not found: $sdkRoot"
    }

    $candidate = Get-ChildItem -Path $sdkRoot -Filter $ToolName -Recurse -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName

    if (-not $candidate) {
        throw "Unable to locate $ToolName in PATH or Windows SDK bin directory."
    }

    return $candidate
}

Remove-Item -LiteralPath $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $stagingDir | Out-Null
New-Item -ItemType Directory -Path (Join-Path $stagingDir 'Insighter.Agent') | Out-Null
New-Item -ItemType Directory -Path (Join-Path $stagingDir 'Assets') | Out-Null

Copy-Item -Path (Join-Path $PublishDir '*') -Destination (Join-Path $stagingDir 'Insighter.Agent') -Recurse -Force
Copy-Item -Path (Join-Path $StartupPublishDir '*') -Destination (Join-Path $stagingDir 'Insighter.Agent') -Recurse -Force
Copy-Item -Path (Join-Path $packageRoot 'Assets\*') -Destination (Join-Path $stagingDir 'Assets') -Recurse -Force

$manifest = Get-Content -Path $manifestTemplate -Raw
$manifest = $manifest.Replace('$version$', $manifestVersion)
$manifest = $manifest.Replace('$publisher$', $Publisher)
Set-Content -Path $manifestPath -Value $manifest -NoNewline

$makeAppx = Resolve-SdkToolPath -ToolName 'makeappx.exe'
$signTool = Resolve-SdkToolPath -ToolName 'signtool.exe'

if (Test-Path $packagePath) {
    Remove-Item -LiteralPath $packagePath -Force
}

& $makeAppx pack /d $stagingDir /p $packagePath /o
if ($LASTEXITCODE -ne 0) {
    throw "makeappx failed with exit code $LASTEXITCODE"
}

& $signTool sign /fd SHA256 /f $CertificatePath /p $CertificatePassword $packagePath
if ($LASTEXITCODE -ne 0) {
    throw "signtool failed with exit code $LASTEXITCODE"
}

Write-Host "Created MSIX package at $packagePath"
