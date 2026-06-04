param(
    [string]$BaseUrl = $env:INSIGHTER_AI_BASE_URL,
    [string]$Query,
    [string]$QueryFile,
    [string]$VariablesJson,
    [string]$VariablesFile,
    [string]$OperationName,
    [int]$TimeoutSec = 120
)

. (Join-Path $PSScriptRoot "InsighterGraphQLCommon.ps1")

if ([string]::IsNullOrWhiteSpace($Query)) {
    $Query = Read-InsighterText -Path $QueryFile -Label "QueryFile"
}

$variables = @{}
if (-not [string]::IsNullOrWhiteSpace($VariablesFile)) {
    $VariablesJson = Read-InsighterText -Path $VariablesFile -Label "VariablesFile"
}
if (-not [string]::IsNullOrWhiteSpace($VariablesJson)) {
    $variables = $VariablesJson | ConvertFrom-InsighterJson
}

$response = Invoke-InsighterGraphQLRequest `
    -BaseUrl $BaseUrl `
    -Query $Query `
    -Variables $variables `
    -OperationName $OperationName `
    -TimeoutSec $TimeoutSec

$response | ConvertTo-InsighterJson
