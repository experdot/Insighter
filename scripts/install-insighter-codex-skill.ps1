param(
    [string]$TargetRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$skillName = "insighter-log-explorer"
$skillSource = Join-Path $repoRoot ".codex\\skills\\insighter-log-explorer"
$defaultCodexHome = if (-not [string]::IsNullOrWhiteSpace($env:CODEX_HOME)) {
    $env:CODEX_HOME
}
else {
    Join-Path $HOME ".codex"
}

if ([string]::IsNullOrWhiteSpace($TargetRoot)) {
    $TargetRoot = Join-Path $defaultCodexHome "skills"
}

$skillTarget = Join-Path $TargetRoot $skillName

function Resolve-ExternalValidatorPath {
    $candidateRoots = [System.Collections.Generic.List[string]]::new()

    if (-not [string]::IsNullOrWhiteSpace($env:CODEX_HOME)) {
        $candidateRoots.Add($env:CODEX_HOME)
    }

    if (-not [string]::IsNullOrWhiteSpace($HOME)) {
        $candidateRoots.Add((Join-Path $HOME ".codex"))
    }

    foreach ($root in $candidateRoots | Select-Object -Unique) {
        $candidate = Join-Path $root "skills\\.system\\skill-creator\\scripts\\quick_validate.py"
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    return $null
}

function Test-BundledSkillStructure {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SkillPath
    )

    $requiredRelativePaths = @(
        "SKILL.md",
        "agents\\openai.yaml",
        "scripts",
        "references"
    )

    foreach ($relativePath in $requiredRelativePaths) {
        $fullPath = Join-Path $SkillPath $relativePath
        if (-not (Test-Path -LiteralPath $fullPath)) {
            throw "Skill validation failed. Required path was not found: $fullPath"
        }
    }

    $skillFile = Join-Path $SkillPath "SKILL.md"
    $skillText = Get-Content -Path $skillFile -Raw -Encoding UTF8

    if ($skillText -notmatch "(?m)^name:\s*$([regex]::Escape($skillName))\s*$") {
        throw "Skill validation failed. SKILL.md must declare name: $skillName"
    }

    if ($skillText -notmatch "(?m)^description:\s*\S.*$") {
        throw "Skill validation failed. SKILL.md must declare a non-empty description."
    }
}

function Invoke-Validation {
    param(
        [string]$ValidatorPath,
        [string]$SkillPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ValidatorPath) -and (Test-Path -LiteralPath $ValidatorPath)) {
        foreach ($launcher in @("python", "py")) {
            $command = Get-Command -Name $launcher -ErrorAction SilentlyContinue
            if ($null -eq $command) {
                continue
            }

            $arguments = if ($launcher -eq "py") {
                @("-3", $ValidatorPath, $SkillPath)
            }
            else {
                @($ValidatorPath, $SkillPath)
            }

            & $command.Source @arguments | Out-Null
            if ($LASTEXITCODE -eq 0) {
                return
            }

            throw "Skill validation failed via external validator: $ValidatorPath"
        }

        Write-Warning "External skill validator was found at $ValidatorPath, but no Python launcher was available. Falling back to built-in validation."
    }

    Test-BundledSkillStructure -SkillPath $SkillPath
}

if (-not (Test-Path -LiteralPath $skillSource)) {
    throw "Skill source path was not found: $skillSource"
}

Invoke-Validation -ValidatorPath (Resolve-ExternalValidatorPath) -SkillPath $skillSource
New-Item -ItemType Directory -Path $TargetRoot -Force | Out-Null

if (Test-Path -LiteralPath $skillTarget) {
    Remove-Item -LiteralPath $skillTarget -Recurse -Force
}

Copy-Item -LiteralPath $skillSource -Destination $skillTarget -Recurse -Force
Write-Output "Installed insighter-log-explorer to $skillTarget"
