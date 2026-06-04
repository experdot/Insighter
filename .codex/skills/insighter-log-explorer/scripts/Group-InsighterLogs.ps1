param(
    [string]$BaseUrl = $env:INSIGHTER_AI_BASE_URL,
    [datetimeoffset]$FromUtc,
    [datetimeoffset]$ToUtc,
    [string]$EventType,
    [string]$CollectorId,
    [string]$Severity,
    [string]$UserSessionId,
    [string]$MachineId,
    [string]$Field,
    [ValidateSet("eq", "in", "exists", "contains", "gt", "gte", "lt", "lte")]
    [string]$Operator = "eq",
    [string]$Value,
    [string[]]$Values,
    [string]$FiltersJson,
    [string[]]$GroupBy,
    [ValidateSet("hour", "day")]
    [string]$TimeBucket,
    [int]$Limit = 50,
    [int]$Offset = 0,
    [int]$TimeoutSec = 120
)

. (Join-Path $PSScriptRoot "InsighterGraphQLCommon.ps1")

if (($null -eq $GroupBy -or $GroupBy.Count -eq 0) -and [string]::IsNullOrWhiteSpace($TimeBucket)) {
    throw "Specify -GroupBy, -TimeBucket, or both."
}

$request = [ordered]@{
    limit = $Limit
    offset = $Offset
}

if ($PSBoundParameters.ContainsKey("FromUtc")) {
    $request.fromUtc = $FromUtc.ToUniversalTime().ToString("O")
}
if ($PSBoundParameters.ContainsKey("ToUtc")) {
    $request.toUtc = $ToUtc.ToUniversalTime().ToString("O")
}
if ($null -ne $GroupBy -and $GroupBy.Count -gt 0) {
    $request.groupBy = @($GroupBy)
}
if (-not [string]::IsNullOrWhiteSpace($TimeBucket)) {
    $request.timeBucket = $TimeBucket
}

$filters = @(New-InsighterFilterList `
    -EventType $EventType `
    -CollectorId $CollectorId `
    -Severity $Severity `
    -UserSessionId $UserSessionId `
    -MachineId $MachineId `
    -Field $Field `
    -Operator $Operator `
    -Value $Value `
    -Values $Values `
    -FiltersJson $FiltersJson)

if ($filters.Count -gt 0) {
    $request.filters = @($filters)
}

$response = Invoke-InsighterAiQuery -BaseUrl $BaseUrl -Request $request -TimeoutSec $TimeoutSec
$response.data.aiQuery | ConvertTo-InsighterJson
