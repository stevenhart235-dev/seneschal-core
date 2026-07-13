[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $BaseUrl,
    [Parameter(Mandatory)][string] $ApiKey,
    [Parameter(Mandatory)][string] $Identity,
    [Parameter(Mandatory)][string] $Capability,
    [Parameter(Mandatory)][string] $Environment,
    [Parameter(Mandatory)][string] $Resource
)

$ErrorActionPreference = 'Stop'

$requestBody = @{
    identity = $Identity
    capability = $Capability
    context = @{
        environment = $Environment
        resource = $Resource
    }
} | ConvertTo-Json -Depth 3

try {
    $response = Invoke-RestMethod `
        -Method Post `
        -Uri ($BaseUrl.TrimEnd('/') + '/evaluate') `
        -Headers @{ 'X-Seneschal-Api-Key' = $ApiKey } `
        -ContentType 'application/json' `
        -Body $requestBody `
        -TimeoutSec 30
}
catch {
    [Console]::Error.WriteLine('Seneschal governance evaluation failed; the gate is fail-closed.')
    exit 2
}

$decision = [string]$response.decision
$mode = [string]$response.mode
$effectiveAction = [string]$response.effectiveAction
$matchedPolicy = [string]$response.policyMatched
$reason = [string]$response.reason

Write-Host "Decision: $decision"
Write-Host "Enforcement mode: $mode"
Write-Host "Effective action: $effectiveAction"
Write-Host "Matched policy: $matchedPolicy"
Write-Host "Reason: $reason"

$shouldProceed = $decision -eq 'allow' -or $mode -eq 'LogOnly'
if ($shouldProceed) {
    if ($decision -ne 'allow') {
        Write-Host 'Governance gate: proceed (decision observed but not enforced in LogOnly)'
    }
    else {
        Write-Host 'Governance gate: proceed'
    }
    exit 0
}

[Console]::Error.WriteLine('Governance gate: blocked')
exit 1
