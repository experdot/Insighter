param(
    [Parameter(Mandatory = $true)]
    [string[]]$Files,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

$lines = foreach ($file in $Files) {
    $hash = Get-FileHash -Path $file -Algorithm SHA256
    "{0}  {1}" -f $hash.Hash.ToLowerInvariant(), (Split-Path -Leaf $file)
}

Set-Content -Path $OutputPath -Value $lines
