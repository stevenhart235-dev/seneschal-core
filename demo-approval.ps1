[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$baseUrl = 'http://localhost:5000'
$statePath = Join-Path $PSScriptRoot 'artifacts/demo/state.json'
$readyUrl = "$baseUrl/ready"
$approvalsUrl = "$baseUrl/approvals"
$approvalIdentity = 'release-approval-worker'
$approvalCapability = 'production.release.approve'
$pollSeconds = 2
$timeoutSeconds = 300

function Stop-WithDemoInstruction {
    Write-Error 'The Seneschal local demo is not running. Start it with .\demo.ps1, then run .\demo-approval.ps1 again.'
    exit 1
}

function Get-AuditEvents {
    $response = Invoke-WebRequest -UseBasicParsing -Method Get `
        -Uri "$baseUrl/audit" -Headers @{ Accept = 'application/json' } `
        -TimeoutSec 5
    $events = ConvertFrom-Json $response.Content
    foreach ($event in $events) {
        Write-Output $event
    }
}

function Get-ApprovalEvents {
    param([Parameter(Mandatory)][string] $ApprovalId)

    @(Get-AuditEvents | Where-Object { $_.approvalId -eq $ApprovalId } |
        Sort-Object { [datetimeoffset]$_.timestampUtc })
}

function Find-CurrentPendingApproval {
    $events = Get-AuditEvents | Where-Object {
        $_.identityId -eq $approvalIdentity -and
        $_.capabilityId -eq $approvalCapability -and
        -not [string]::IsNullOrWhiteSpace($_.approvalId)
    } | Sort-Object { [datetimeoffset]$_.timestampUtc } -Descending

    foreach ($event in $events) {
        $latest = @(Get-ApprovalEvents -ApprovalId $event.approvalId)[-1]
        if ($latest.approvalStatus -eq 'Pending') {
            return $latest
        }
    }
    return $null
}

function Wait-Until {
    param(
        [Parameter(Mandatory)][scriptblock] $Condition,
        [Parameter(Mandatory)][string] $TimeoutMessage
    )

    $deadline = [datetime]::UtcNow.AddSeconds($timeoutSeconds)
    while ([datetime]::UtcNow -lt $deadline) {
        $result = & $Condition
        if ($null -ne $result) { return $result }
        Start-Sleep -Seconds $pollSeconds
    }
    throw $TimeoutMessage
}

if (-not (Test-Path -LiteralPath $statePath)) {
    Stop-WithDemoInstruction
}

try {
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    $trackedProcesses = @($state.ProcessIds | Where-Object {
        Get-Process -Id ([int]$_) -ErrorAction SilentlyContinue
    })
    if ($trackedProcesses.Count -eq 0) { Stop-WithDemoInstruction }

    $ready = Invoke-WebRequest -UseBasicParsing -Uri $readyUrl -TimeoutSec 5
    if ($ready.StatusCode -ne 200) { Stop-WithDemoInstruction }
}
catch {
    Stop-WithDemoInstruction
}

Write-Host ''
Write-Host '[1/4] Detect the application operation' -ForegroundColor Cyan
Write-Host 'Presenter: The worker owns a stable operation ID and reuses it while approval is pending.'
$pending = Wait-Until -TimeoutMessage 'No pending ApprovalWorker operation appeared within five minutes.' -Condition {
    Find-CurrentPendingApproval
}

$approvalId = [string]$pending.approvalId
$operationId = [string]$pending.approvalOperationId
Write-Host "  Approval ID: $approvalId"
Write-Host "  Operation ID: $operationId"
Write-Host "  Identity: $($pending.identityId)"
Write-Host "  Capability: $($pending.capabilityId)"
Write-Host "  Resource: $($pending.resourceId)"
Write-Host 'Expected: One Pending record scoped to this exact operation.' -ForegroundColor DarkGray

Write-Host ''
Write-Host '[2/4] Resolve the request' -ForegroundColor Cyan
Write-Host "Open: $approvalsUrl"
Write-Host 'Point out:'
Write-Host '  - Operation correlation is explicit.'
Write-Host '  - Approval is single-use.'
try { Start-Process $approvalsUrl | Out-Null } catch { }
[void](Read-Host 'Approve or reject the request in the portal, then press Enter')

Write-Host ''
Write-Host '[3/4] Observe the same operation retry' -ForegroundColor Cyan
$resolution = Wait-Until -TimeoutMessage "Approval $approvalId was not resolved within five minutes." -Condition {
    $events = Get-ApprovalEvents -ApprovalId $approvalId
    $events | Where-Object { $_.approvalAction -in @('Approved', 'Rejected') } |
        Select-Object -Last 1
}

if ($resolution.approvalStatus -eq 'Approved') {
    Write-Host 'Resolution: Approved'
    $consumed = Wait-Until -TimeoutMessage "Approved operation $operationId did not retry as Allow and become Consumed." -Condition {
        Get-ApprovalEvents -ApprovalId $approvalId | Where-Object {
            $_.approvalAction -eq 'Consumed' -and
            $_.approvalStatus -eq 'Consumed' -and
            $_.approvalOperationId -eq $operationId -and
            $_.decision -eq 'Allow'
        } | Select-Object -Last 1
    }
    Write-Host "Confirmed: $operationId retried as Allow and approval $approvalId was Consumed." -ForegroundColor Green
    $traceUrl = "$baseUrl/audit/$($consumed.id)"

    Write-Host ''
    Write-Host '[4/4] Observe the next distinct operation' -ForegroundColor Cyan
    $nextPending = Wait-Until -TimeoutMessage 'The worker did not create one Pending approval for its next operation.' -Condition {
        $candidate = Find-CurrentPendingApproval
        if ($null -ne $candidate -and
            $candidate.approvalId -ne $approvalId -and
            $candidate.approvalOperationId -ne $operationId) { $candidate }
    }
    Write-Host "New operation ID: $($nextPending.approvalOperationId)"
    Write-Host "New approval ID: $($nextPending.approvalId)"
    Write-Host 'Expected: The consumed approval did not authorize the next operation.' -ForegroundColor DarkGray
}
else {
    Write-Host 'Resolution: Rejected'
    $denied = Wait-Until -TimeoutMessage "Rejected operation $operationId did not retry as Deny." -Condition {
        Get-ApprovalEvents -ApprovalId $approvalId | Where-Object {
            $_.approvalAction -eq 'Used' -and
            $_.approvalStatus -eq 'Rejected' -and
            $_.approvalOperationId -eq $operationId -and
            $_.decision -eq 'Deny'
        } | Select-Object -Last 1
    }
    Write-Host "Confirmed: $operationId retried as Deny. The rejected approval remains scoped to that operation." -ForegroundColor Yellow
    $traceUrl = "$baseUrl/audit/$($denied.id)"
    Write-Host ''
    Write-Host '[4/4] Rejection outcome' -ForegroundColor Cyan
    Write-Host 'Expected: The worker keeps the same operation ID; rejection does not authorize execution.' -ForegroundColor DarkGray
}

Write-Host ''
Write-Host 'Approval demo complete.' -ForegroundColor Green
Write-Host "Decision Trace: $traceUrl"
Write-Host "Approvals: $approvalsUrl"
