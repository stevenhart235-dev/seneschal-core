[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$baseUrl = 'http://localhost:5000'
$readyUrl = "$baseUrl/ready"
$approvalsUrl = "$baseUrl/approvals"
$statePath = Join-Path $PSScriptRoot 'artifacts/demo/state.json'
$apiLog = 'artifacts/demo/logs/api.stderr.log'
$workerOutputLog = 'artifacts/demo/logs/approval-worker.stdout.log'
$workerErrorLog = 'artifacts/demo/logs/approval-worker.stderr.log'
$approvalIdentity = 'release-approval-worker'
$approvalCapability = 'production.release.approve'
$pollSeconds = 2
$successMark = [char]0x2713

trap [System.Management.Automation.PipelineStoppedException] {
    Write-Host ''
    Write-Host 'Approval demo cancelled. The local demo is still running.' -ForegroundColor Yellow
    exit 130
}

function Write-Success {
    param([Parameter(Mandatory)][string] $Message)
    Write-Host "$successMark $Message" -ForegroundColor Green
}

function Throw-DemoNotRunning {
    throw @"
The Seneschal demo is not running.
Start it first with:
.\demo.ps1
"@
}

function Write-FailureContext {
    Write-Host ''
    Write-Host 'Relevant local logs:' -ForegroundColor Yellow
    Write-Host "  $apiLog"
    Write-Host "  $workerOutputLog"
    Write-Host "  $workerErrorLog"
    Write-Host 'The running demo was preserved.'
}

function Get-AuditEvents {
    $response = Invoke-WebRequest -UseBasicParsing -Method Get `
        -Uri "$baseUrl/audit" -Headers @{ Accept = 'application/json' } `
        -TimeoutSec 5
    $events = ConvertFrom-Json $response.Content
    foreach ($event in $events) { Write-Output $event }
}

function Get-ApprovalState {
    $response = Invoke-WebRequest -UseBasicParsing -Method Get `
        -Uri "${approvalsUrl}?handler=State" -Headers @{ Accept = 'application/json' } `
        -TimeoutSec 5
    $records = ConvertFrom-Json $response.Content
    foreach ($record in $records) { Write-Output $record }
}

function Get-ApprovalEvents {
    param([Parameter(Mandatory)][string] $ApprovalId)

    @(Get-AuditEvents | Where-Object { $_.approvalId -eq $ApprovalId } |
        Sort-Object { [datetimeoffset]$_.timestampUtc })
}

function Get-CurrentPendingApprovals {
    $pending = @(Get-ApprovalState | Where-Object {
        $_.identity -eq $approvalIdentity -and
        $_.capability -eq $approvalCapability -and
        $_.correlationMode -eq 'Operation' -and
        $_.status -eq 'Pending' -and
        -not [string]::IsNullOrWhiteSpace($_.approvalId) -and
        -not [string]::IsNullOrWhiteSpace($_.operationId)
    })
    @($pending | Sort-Object { [datetimeoffset]$_.requestedAt } | ForEach-Object {
        [pscustomobject]@{
            ApprovalId = [string]$_.approvalId
            OperationId = [string]$_.operationId
            Identity = [string]$_.identity
            Capability = [string]$_.capability
            Environment = [string]$_.environment
            Resource = [string]$_.resource
            RequestedAt = [datetimeoffset]$_.requestedAt
        }
    })
}

function Wait-ForState {
    param(
        [Parameter(Mandatory)][string] $Stage,
        [Parameter(Mandatory)][int] $TimeoutSeconds,
        [Parameter(Mandatory)][scriptblock] $Condition
    )

    $deadline = [datetime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([datetime]::UtcNow -lt $deadline) {
        $result = & $Condition
        if ($null -ne $result) { return $result }
        Start-Sleep -Seconds $pollSeconds
    }
    throw "Timed out during '$Stage' after $TimeoutSeconds seconds."
}

function Assert-RunningDemo {
    if (-not (Test-Path -LiteralPath $statePath)) { Throw-DemoNotRunning }

    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    $trackedIds = @($state.ProcessIds | ForEach-Object { [int]$_ })
    if ($trackedIds.Count -eq 0) { Throw-DemoNotRunning }

    $runningTrackedIds = @($trackedIds | Where-Object {
        $null -ne (Get-Process -Id $_ -ErrorAction SilentlyContinue)
    })
    if ($runningTrackedIds.Count -eq 0) { Throw-DemoNotRunning }

    $approvalWorkerTracked = $false
    foreach ($processId in $runningTrackedIds) {
        $process = Get-CimInstance Win32_Process -Filter "ProcessId = $processId" `
            -ErrorAction Stop
        if ($process.CommandLine -match 'ApprovalWorker[\\/]ApprovalWorker\.csproj') {
            $approvalWorkerTracked = $true
            break
        }
    }
    if (-not $approvalWorkerTracked) {
        throw 'ApprovalWorker is not present in the tracked running demo process set. Run .\stop-demo.ps1, then start a fresh demo with .\demo.ps1.'
    }

    $ready = Invoke-WebRequest -UseBasicParsing -Uri $readyUrl -TimeoutSec 5
    if ($ready.StatusCode -ne 200) { Throw-DemoNotRunning }
}

try {
    Write-Host '==================================================='
    Write-Host 'Seneschal Guided Approval Demo'
    Write-Host '==================================================='
    Write-Host ''

    Assert-RunningDemo
    Write-Success 'Local demo is running'
    Write-Success 'ApprovalWorker is running'

    Write-Host ''
    Write-Host 'Waiting for a pending production release approval...'
    $pending = Wait-ForState -Stage 'initial pending approval' -TimeoutSeconds 90 -Condition {
        @(Get-CurrentPendingApprovals) | Select-Object -First 1
    }
    Write-Success 'Pending approval detected'

    $approvalId = $pending.ApprovalId
    $operationId = $pending.OperationId
    Write-Host ''
    Write-Host "Approval ID:  $approvalId"
    Write-Host "Operation ID: $operationId"
    Write-Host "Identity:     $($pending.Identity)"
    Write-Host "Capability:   $($pending.Capability)"
    Write-Host "Environment:  $($pending.Environment)"
    Write-Host "Resource:     $($pending.Resource)"
    Write-Host "Requested:    $($pending.RequestedAt.ToString('u'))"

    Write-Host ''
    Write-Host 'Open:'
    Write-Host $approvalsUrl -ForegroundColor Cyan
    try {
        Start-Process $approvalsUrl | Out-Null
    }
    catch {
        Write-Host 'The browser could not be opened automatically; use the URL above.' -ForegroundColor Yellow
    }
    Write-Host ''
    Write-Host 'Approve or reject the request in the portal.'
    Write-Host 'Waiting for operator decision...'

    $resolution = Wait-ForState -Stage 'operator resolution' -TimeoutSeconds 300 -Condition {
        Get-ApprovalEvents -ApprovalId $approvalId | Where-Object {
            $_.approvalAction -in @('Approved', 'Rejected')
        } | Select-Object -Last 1
    }

    if ($resolution.approvalStatus -eq 'Approved') {
        Write-Success 'Approval received'
        Write-Host "Waiting for the worker to retry $operationId..."
        $consumed = Wait-ForState -Stage 'approved worker retry and consumption' -TimeoutSeconds 60 -Condition {
            Get-ApprovalEvents -ApprovalId $approvalId | Where-Object {
                $_.approvalAction -eq 'Consumed' -and
                $_.approvalStatus -eq 'Consumed' -and
                $_.approvalOperationId -eq $operationId -and
                $_.decision -eq 'Allow'
            } | Select-Object -Last 1
        }
        Write-Success 'Same operation retried'
        Write-Success 'Decision resolved to Allow'
        Write-Success 'Approval consumed'

        $traceUrl = "$baseUrl/audit/$($consumed.id)"
        Write-Host ''
        Write-Host 'Decision Trace:'
        Write-Host $traceUrl -ForegroundColor Cyan

        $nextPending = Wait-ForState -Stage 'next distinct pending operation' -TimeoutSeconds 60 -Condition {
            $candidates = @(Get-CurrentPendingApprovals | Where-Object {
                $_.ApprovalId -ne $approvalId -and
                $_.OperationId -ne $operationId
            })
            if ($candidates.Count -eq 1) { $candidates[0] }
        }
        $newPendingForOperation = @(Get-CurrentPendingApprovals | Where-Object {
            $_.OperationId -eq $nextPending.OperationId
        })
        if ($newPendingForOperation.Count -ne 1) {
            throw "Single-use proof failed: expected exactly one Pending approval for operation '$($nextPending.OperationId)', found $($newPendingForOperation.Count)."
        }

        Write-Host ''
        Write-Success 'New pending approval created'
        Write-Host "Previous operation: $operationId"
        Write-Host "New operation:      $($nextPending.OperationId)"
        Write-Host ''
        Write-Host 'Single-use approval behavior verified.' -ForegroundColor Green
        Write-Host ''
        Write-Host 'Demo complete.' -ForegroundColor Green
    }
    else {
        Write-Success 'Approval rejected'
        Write-Host "Waiting for the worker to retry $operationId..."
        $denied = Wait-ForState -Stage 'rejected worker retry' -TimeoutSeconds 60 -Condition {
            Get-ApprovalEvents -ApprovalId $approvalId | Where-Object {
                $_.approvalAction -eq 'Used' -and
                $_.approvalStatus -eq 'Rejected' -and
                $_.approvalOperationId -eq $operationId -and
                $_.decision -eq 'Deny'
            } | Select-Object -Last 1
        }
        Write-Success 'Same operation retried'
        Write-Success 'Decision resolved to Deny'
        if ($denied.enforcementMode -eq 'Enforce') {
            Write-Success 'Operation remained blocked'
        }
        else {
            Write-Success 'Deny was recorded; LogOnly allowed the simulated operation to continue'
        }

        Write-Host ''
        Write-Host 'Decision Trace:'
        Write-Host "$baseUrl/audit/$($denied.id)" -ForegroundColor Cyan
        Write-Host ''
        Write-Host 'Rejection path verified. Demo complete.' -ForegroundColor Green
    }
}
catch {
    Write-Host ''
    Write-Host "Approval demo failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-FailureContext
    exit 1
}
