[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $BaseUrl,
    [Parameter(Mandatory)][string] $ApiKey,
    [Parameter(Mandatory)][string] $Identity,
    [Parameter(Mandatory)][string] $Capability,
    [Parameter(Mandatory)][string] $Environment,
    [Parameter(Mandatory)][string] $Resource,
    [string] $OperationId
)

$ErrorActionPreference = 'Stop'

$requestBodyObject = @{
    identity = $Identity
    capability = $Capability
    context = @{
        environment = $Environment
        resource = $Resource
    }
}
if ($OperationId) { $requestBodyObject.operationId = $OperationId }
$requestBody = $requestBodyObject | ConvertTo-Json -Depth 3

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
$guidance = [string]$response.executionGuidance
$approvalId = [string]$response.approvalId
$message = [string]$response.message

Write-Host "Decision: $decision"
Write-Host "Enforcement mode: $mode"
Write-Host "Effective action: $effectiveAction"
Write-Host "Matched policy: $matchedPolicy"
Write-Host "Reason: $reason"
Write-Host "Execution guidance: $guidance"
if ($approvalId) { Write-Host "Approval ID: $approvalId" }
if ($message) { Write-Host "Message: $message" }

$shouldProceed = $guidance -eq 'Proceed' -or $guidance -eq 'ContinueLogOnly'
if ($shouldProceed) {
    if ($guidance -eq 'ContinueLogOnly') {
        Write-Host 'Governance gate: proceed (decision observed but not enforced in LogOnly)'
    }
    else {
        Write-Host 'Governance gate: proceed'
    }
    exit 0
}

if ($decision -eq 'requires_approval' -or $decision -eq 'PendingApproval') {
    [Console]::Error.WriteLine('Governance gate: approval is required before retry; hosted runners are not paused automatically.')
}
else {
    [Console]::Error.WriteLine('Governance gate: blocked')
}
exit 1
