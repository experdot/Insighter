param(
    [Parameter(Mandatory = $true)]
    [guid]$AnchorEventId,
    [string]$BaseUrl = $env:INSIGHTER_AI_BASE_URL,
    [datetimeoffset]$FromUtc,
    [datetimeoffset]$ToUtc,
    [int]$BeforeCount = 10,
    [int]$AfterCount = 10,
    [string[]]$SelectField,
    [switch]$IncludeContent,
    [int]$TimeoutSec = 120
)

. (Join-Path $PSScriptRoot "InsighterGraphQLCommon.ps1")

$request = [ordered]@{
    context = @{
        anchorEventId = $AnchorEventId.ToString()
        beforeCount = $BeforeCount
        afterCount = $AfterCount
    }
}

if ($PSBoundParameters.ContainsKey("FromUtc")) {
    $request.fromUtc = $FromUtc.ToUniversalTime().ToString("O")
}
if ($PSBoundParameters.ContainsKey("ToUtc")) {
    $request.toUtc = $ToUtc.ToUniversalTime().ToString("O")
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
