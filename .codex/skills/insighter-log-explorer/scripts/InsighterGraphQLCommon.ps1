Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-InsighterDefaultStatePaths {
    $paths = [System.Collections.Generic.List[string]]::new()

    if (-not [string]::IsNullOrWhiteSpace($env:INSIGHTER_AGENT_ROOT)) {
        $paths.Add((Join-Path $env:INSIGHTER_AGENT_ROOT "state\\agent-state.json"))
    }

    $localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    $paths.Add((Join-Path $localAppData "Insighter\\state\\agent-state.json"))
    return $paths
}

function Resolve-InsighterBaseUrl {
    param(
        [string]$ExplicitBaseUrl
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitBaseUrl)) {
        return $ExplicitBaseUrl.TrimEnd("/")
    }

    if (-not [string]::IsNullOrWhiteSpace($env:INSIGHTER_AI_BASE_URL)) {
        return $env:INSIGHTER_AI_BASE_URL.TrimEnd("/")
    }

    foreach ($statePath in Get-InsighterDefaultStatePaths) {
        if (-not (Test-Path -LiteralPath $statePath)) {
            continue
        }

        try {
            $state = Get-Content -Path $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($null -ne $state -and -not [string]::IsNullOrWhiteSpace($state.ApiBaseUrl)) {
                return $state.ApiBaseUrl.TrimEnd("/")
            }
        }
        catch {
        }
    }

    return "http://127.0.0.1:5078"
}

function Read-InsighterText {
    param(
        [string]$Path,
        [string]$Label
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "$Label is required."
    }

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Label path was not found: $Path"
    }

    return (Get-Content -Path $Path -Raw -Encoding UTF8)
}

function ConvertTo-InsighterJson {
    param(
        [Parameter(ValueFromPipeline = $true)]
        [object]$InputObject
    )

    process {
        $InputObject | ConvertTo-Json -Depth 50 -Compress
    }
}

function ConvertFrom-InsighterJson {
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [string]$InputText
    )

    process {
        $command = Get-Command -Name ConvertFrom-Json -ErrorAction Stop
        if ($command.Parameters.ContainsKey("Depth")) {
            return $InputText | ConvertFrom-Json -Depth 50
        }

        return $InputText | ConvertFrom-Json
    }
}

function Get-InsighterAiQueryDocument {
    return @"
query InsighterAiQuery(`$input: AiLogQueryRequestInput!) {
  aiQuery(input: `$input) {
    mode
    meta {
      fromUtc
      toUtc
      totalMatched
      returnedCount
      contentIncluded
      warnings
    }
    items {
      id
      occurredAtUtc
      eventType
      collectorId
      severity
      userSessionId
      machineId
      platform
      fields {
        name
        value
      }
      contentJson
    }
    groups {
      key {
        name
        value
      }
      count
      firstOccurredAtUtc
      lastOccurredAtUtc
    }
    anchor {
      id
      occurredAtUtc
      eventType
      collectorId
      severity
      userSessionId
      machineId
      platform
      fields {
        name
        value
      }
      contentJson
    }
    before {
      id
      occurredAtUtc
      eventType
      collectorId
      severity
      userSessionId
      machineId
      platform
      fields {
        name
        value
      }
      contentJson
    }
    after {
      id
      occurredAtUtc
      eventType
      collectorId
      severity
      userSessionId
      machineId
      platform
      fields {
        name
        value
      }
      contentJson
    }
  }
}
"@
}

function Invoke-InsighterGraphQLRequest {
    param(
        [string]$BaseUrl,
        [string]$Query,
        [object]$Variables,
        [string]$OperationName,
        [int]$TimeoutSec = 120
    )

    $resolvedBaseUrl = Resolve-InsighterBaseUrl -ExplicitBaseUrl $BaseUrl
    $uri = "$resolvedBaseUrl/graphql"
    $payload = [ordered]@{
        query = $Query
        variables = $Variables
    }

    if (-not [string]::IsNullOrWhiteSpace($OperationName)) {
        $payload.operationName = $OperationName
    }

    try {
        $response = Invoke-RestMethod `
            -Method Post `
            -Uri $uri `
            -ContentType "application/json; charset=utf-8" `
            -Body ($payload | ConvertTo-Json -Depth 50 -Compress) `
            -TimeoutSec $TimeoutSec
    }
    catch {
        $checkedPaths = (Get-InsighterDefaultStatePaths) -join ", "
        throw "Failed to reach Insighter GraphQL endpoint at $uri. Checked explicit URL, INSIGHTER_AI_BASE_URL, and agent state files: $checkedPaths. $($_.Exception.Message)"
    }

    $errorsProperty = $response.PSObject.Properties["errors"]
    if ($null -ne $errorsProperty) {
        $errors = @($errorsProperty.Value)
    }
    else {
        $errors = @()
    }

    if ($errors.Count -gt 0) {
        $messages = @($errors | ForEach-Object { $_.message }) -join " | "
        throw "Insighter GraphQL query failed: $messages"
    }

    return $response
}

function Invoke-InsighterAiQuery {
    param(
        [string]$BaseUrl,
        [object]$Request,
        [int]$TimeoutSec = 120
    )

    return Invoke-InsighterGraphQLRequest `
        -BaseUrl $BaseUrl `
        -Query (Get-InsighterAiQueryDocument) `
        -Variables @{ input = $Request } `
        -OperationName "InsighterAiQuery" `
        -TimeoutSec $TimeoutSec
}

function Add-InsighterBasicFilter {
    param(
        [System.Collections.Generic.List[object]]$Target,
        [string]$Field,
        [string]$Operator,
        [string]$Value,
        [string[]]$Values
    )

    if ([string]::IsNullOrWhiteSpace($Field)) {
        return
    }

    $hasValues = $null -ne $Values -and $Values.Count -gt 0
    $hasValue = -not [string]::IsNullOrWhiteSpace($Value)
    $normalizedOperator = if ([string]::IsNullOrWhiteSpace($Operator)) { "eq" } else { $Operator }

    if ($normalizedOperator -eq "exists") {
    }
    elseif (-not $hasValue -and -not $hasValues) {
        return
    }

    $filter = [ordered]@{
        field = $Field
        operator = $normalizedOperator
    }

    if ($hasValues) {
        $filter.values = @($Values)
    }
    elseif ($hasValue) {
        $filter.value = $Value
    }

    $Target.Add($filter)
}

function New-InsighterFilterList {
    param(
        [string]$EventType,
        [string]$CollectorId,
        [string]$Severity,
        [string]$UserSessionId,
        [string]$MachineId,
        [string]$Field,
        [string]$Operator,
        [string]$Value,
        [string[]]$Values,
        [string]$FiltersJson
    )

    $filters = [System.Collections.Generic.List[object]]::new()
    Add-InsighterBasicFilter -Target $filters -Field "eventType" -Operator "eq" -Value $EventType -Values @()
    Add-InsighterBasicFilter -Target $filters -Field "collectorId" -Operator "eq" -Value $CollectorId -Values @()
    Add-InsighterBasicFilter -Target $filters -Field "severity" -Operator "eq" -Value $Severity -Values @()
    Add-InsighterBasicFilter -Target $filters -Field "userSessionId" -Operator "eq" -Value $UserSessionId -Values @()
    Add-InsighterBasicFilter -Target $filters -Field "machineId" -Operator "eq" -Value $MachineId -Values @()
    Add-InsighterBasicFilter -Target $filters -Field $Field -Operator $Operator -Value $Value -Values $Values

    if (-not [string]::IsNullOrWhiteSpace($FiltersJson)) {
        $parsed = $FiltersJson | ConvertFrom-InsighterJson
        foreach ($item in @($parsed)) {
            $filters.Add($item)
        }
    }

    return @($filters)
}
