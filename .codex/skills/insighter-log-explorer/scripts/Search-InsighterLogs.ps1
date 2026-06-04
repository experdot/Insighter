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
    [string[]]$SelectField,
    [switch]$IncludeContent,
    [ValidateSet("asc", "desc")]
    [string]$SortDirection = "desc",
    [int]$Limit = 50,
    [int]$Offset = 0,
    [int]$TimeoutSec = 120
)

. (Join-Path $PSScriptRoot "InsighterGraphQLCommon.ps1")

$request = [ordered]@{
    sort = @{
        field = "occurredAtUtc"
        direction = $SortDirection
    }
    limit = $Limit
    offset = $Offset
}

if ($PSBoundParameters.ContainsKey("FromUtc")) {
    $request.fromUtc = $FromUtc.ToUniversalTime().ToString("O")
}
if ($PSBoundParameters.ContainsKey("ToUtc")) {
    $request.toUtc = $ToUtc.ToUniversalTime().ToString("O")
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

if ($IncludeContent.IsPresent -or ($null -ne $SelectField -and $SelectField.Count -gt 0)) {
    $request.select = [ordered]@{
        includeContent = $IncludeContent.IsPresent
    }

    if ($null -ne $SelectField -and $SelectField.Count -gt 0) {
        $request.select.fields = @($SelectField)
    }
}

$response = Invoke-InsighterAiQuery -BaseUrl $BaseUrl -Request $request -TimeoutSec $TimeoutSec
$response.data.aiQuery | ConvertTo-InsighterJson
