param(
    [string]$BaseUrl = $env:INSIGHTER_AI_BASE_URL,
    [string]$RequestJson,
    [string]$RequestFile,
    [int]$TimeoutSec = 120
)

. (Join-Path $PSScriptRoot "InsighterGraphQLCommon.ps1")

if ([string]::IsNullOrWhiteSpace($RequestJson)) {
    $RequestJson = Read-InsighterText -Path $RequestFile -Label "RequestFile"
}

$request = $RequestJson | ConvertFrom-InsighterJson
$response = Invoke-InsighterAiQuery -BaseUrl $BaseUrl -Request $request -TimeoutSec $TimeoutSec
$response.data.aiQuery | ConvertTo-InsighterJson
