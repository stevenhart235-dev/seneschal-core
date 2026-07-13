[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $BaseUrl,
    [Parameter(Mandatory)][string] $ApiKey,
    [Parameter(Mandatory)][string] $Identity,
    [Parameter(Mandatory)][string] $Capability,
    [Parameter(Mandatory)][string] $Environment,
    [Parameter(Mandatory)][string] $Resource,
    [string] $PlanFile
)

$ErrorActionPreference = 'Stop'

if ($PlanFile) {
    if (-not (Test-Path -LiteralPath $PlanFile -PathType Leaf)) {
        [Console]::Error.WriteLine("Plan file not found: $PlanFile")
        exit 3
    }

    $plan = Get-Item -LiteralPath $PlanFile
    Write-Host "Plan file: $($plan.Name)"
    Write-Host "Plan size: $($plan.Length) bytes"
}

$sharedGate = Join-Path $PSScriptRoot '../github-actions/invoke-seneschal-gate.ps1'
& $sharedGate `
    -BaseUrl $BaseUrl `
    -ApiKey $ApiKey `
    -Identity $Identity `
    -Capability $Capability `
    -Environment $Environment `
    -Resource $Resource
exit $LASTEXITCODE
