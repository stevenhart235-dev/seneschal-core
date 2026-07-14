[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repositoryRoot = $PSScriptRoot
$baseUrl = 'http://localhost:5000'
$statePath = Join-Path $repositoryRoot 'artifacts/demo/state.json'
$githubGate = Join-Path $repositoryRoot 'integrations/github-actions/invoke-seneschal-gate.ps1'
$terraformGate = Join-Path $repositoryRoot 'integrations/terraform/invoke-seneschal-gate.ps1'
$runtimeAvailable = $false
$demoSucceeded = $false

function Write-Stage {
    param(
        [Parameter(Mandatory)][int] $Number,
        [Parameter(Mandatory)][string] $Title,
        [Parameter(Mandatory)][string] $Cue,
        [Parameter(Mandatory)][string] $PortalUrl,
        [Parameter(Mandatory)][string[]] $Points,
        [Parameter(Mandatory)][string] $Expected
    )

    Write-Host ''
    Write-Host "[$Number/6] $Title" -ForegroundColor Cyan
    Write-Host "Presenter: $Cue"
    Write-Host "Open: $PortalUrl"
    Write-Host 'Point out:'
    foreach ($point in $Points) {
        Write-Host "  - $point"
    }
    Write-Host "Expected: $Expected" -ForegroundColor DarkGray
}

function Wait-ForPresenter {
    [void](Read-Host 'Press Enter to run this stage')
}

function Set-RuntimeMode {
    param([ValidateSet('LogOnly', 'Enforce')][string] $Mode)

    Invoke-WebRequest -UseBasicParsing -Method Post `
        -Uri "$baseUrl/governance?handler=SetMode" `
        -ContentType 'application/x-www-form-urlencoded' `
        -Body "mode=$Mode" | Out-Null
}

function Set-GovernanceWindow {
    param(
        [Parameter(Mandatory)][bool] $Enabled,
        [ValidateSet('Observe', 'Enforce')][string] $Mode
    )

    $body = "mode=$Mode"
    if ($Enabled) {
        $body = "enabled=true&$body"
    }

    Invoke-WebRequest -UseBasicParsing -Method Post `
        -Uri "$baseUrl/governance-windows?handler=SetState" `
        -ContentType 'application/x-www-form-urlencoded' `
        -Body $body | Out-Null
}

function Invoke-DemoGate {
    param(
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][string] $Script,
        [Parameter(Mandatory)][string] $ApiKey,
        [Parameter(Mandatory)][string] $Identity,
        [Parameter(Mandatory)][string] $Capability,
        [Parameter(Mandatory)][string] $Resource,
        [Parameter(Mandatory)][string] $ExpectedDecision,
        [Parameter(Mandatory)][string] $ExpectedMode,
        [Parameter(Mandatory)][string] $ExpectedAction,
        [Parameter(Mandatory)][int] $ExpectedExitCode
    )

    $originalError = [Console]::Error
    $suppressedError = [System.IO.StringWriter]::new()
    try {
        [Console]::SetError($suppressedError)
        $output = @(& $Script `
            -BaseUrl $baseUrl `
            -ApiKey $ApiKey `
            -Identity $Identity `
            -Capability $Capability `
            -Environment production `
            -Resource $Resource 2>&1 6>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        [Console]::SetError($originalError)
        $suppressedError.Dispose()
    }
    $text = ($output | ForEach-Object { $_.ToString() }) -join "`n"

    $decision = if ($text -match '(?m)^Decision: (.+)$') { $Matches[1].Trim() } else { '' }
    $mode = if ($text -match '(?m)^Enforcement mode: (.+)$') { $Matches[1].Trim() } else { '' }
    $action = if ($text -match '(?m)^Effective action: (.+)$') { $Matches[1].Trim() } else { '' }
    $reason = if ($text -match '(?m)^Reason: (.+)$') { $Matches[1].Trim() } else { '' }

    Write-Host ''
    Write-Host $Name -ForegroundColor Yellow
    Write-Host "  Decision: $decision"
    Write-Host "  Mode: $mode"
    Write-Host "  Effective action: $action"
    Write-Host "  Reason: $reason"
    Write-Host "  Exit code: $exitCode"

    $mismatches = [System.Collections.Generic.List[string]]::new()
    if ($decision -ne $ExpectedDecision) { $mismatches.Add("decision '$decision' (expected '$ExpectedDecision')") }
    if ($mode -ne $ExpectedMode) { $mismatches.Add("mode '$mode' (expected '$ExpectedMode')") }
    if ($action -ne $ExpectedAction) { $mismatches.Add("action '$action' (expected '$ExpectedAction')") }
    if ($exitCode -ne $ExpectedExitCode) { $mismatches.Add("exit code '$exitCode' (expected '$ExpectedExitCode')") }

    if ($mismatches.Count -gt 0) {
        throw "$Name did not match the scripted outcome: $($mismatches -join '; ')."
    }
}

function Invoke-ProductionGates {
    param(
        [Parameter(Mandatory)][string] $ExpectedDecision,
        [Parameter(Mandatory)][string] $ExpectedMode,
        [Parameter(Mandatory)][string] $ExpectedAction,
        [Parameter(Mandatory)][int] $ExpectedExitCode
    )

    $expectation = @{
        ExpectedDecision = $ExpectedDecision
        ExpectedMode = $ExpectedMode
        ExpectedAction = $ExpectedAction
        ExpectedExitCode = $ExpectedExitCode
    }

    Invoke-DemoGate -Name 'GitHub Actions deployment' -Script $githubGate `
        -ApiKey 'dev-github-actions-key' -Identity 'github-actions-production' `
        -Capability 'production.deployment.execute' -Resource 'checkout-api' @expectation

    Invoke-DemoGate -Name 'Terraform/OpenTofu apply' -Script $terraformGate `
        -ApiKey 'dev-terraform-production-key' -Identity 'terraform-production' `
        -Capability 'infrastructure.production.apply' -Resource 'prod-subscription' @expectation
}

function Assert-DemoRunning {
    if (-not (Test-Path -LiteralPath $statePath -PathType Leaf)) {
        throw 'The demo is not running. Run .\demo.ps1 first.'
    }

    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    $runningProcesses = @($state.ProcessIds | Where-Object {
        $process = Get-Process -Id ([int]$_) -ErrorAction SilentlyContinue
        $process -and $process.ProcessName -eq 'dotnet'
    })
    if ($runningProcesses.Count -eq 0) {
        throw 'Demo state is stale. Run .\stop-demo.ps1, then .\demo.ps1.'
    }

    $ready = Invoke-WebRequest -UseBasicParsing -Uri "$baseUrl/ready" -TimeoutSec 5
    if ($ready.StatusCode -ne 200) {
        throw "Readiness returned HTTP $($ready.StatusCode)."
    }
}

Push-Location $repositoryRoot
try {
    Assert-DemoRunning
    $runtimeAvailable = $true

    Write-Host 'Seneschal: Production Freeze' -ForegroundColor Green
    Write-Host 'A guided runtime-governance story using the existing demo gates.'
    Write-Host 'No deployment or terraform apply is performed.'

    Set-RuntimeMode LogOnly
    Set-GovernanceWindow -Enabled $false -Mode Observe

    Write-Stage -Number 1 -Title 'Baseline' `
        -Cue 'Establish normal production automation before a freeze.' `
        -PortalUrl "$baseUrl/dashboard" `
        -Points @('Runtime mode is LogOnly', 'Production Freeze is inactive', 'Four workers provide live activity') `
        -Expected 'GitHub and Terraform return Allow and exit 0.'
    Wait-ForPresenter
    Invoke-ProductionGates -ExpectedDecision allow -ExpectedMode LogOnly -ExpectedAction allow -ExpectedExitCode 0

    Write-Stage -Number 2 -Title 'Observe the Production Freeze' `
        -Cue 'Introduce the freeze safely before it changes decisions.' `
        -PortalUrl "$baseUrl/governance-windows" `
        -Points @('Production Freeze is enabled', 'Window mode is Observe', 'Affected capabilities are explicit') `
        -Expected 'Requests remain Allow while Governance Window participation is audited.'
    Wait-ForPresenter
    Set-GovernanceWindow -Enabled $true -Mode Observe
    Invoke-ProductionGates -ExpectedDecision allow -ExpectedMode LogOnly -ExpectedAction allow -ExpectedExitCode 0

    Write-Stage -Number 3 -Title 'Governed, but still allowed' `
        -Cue 'Show evidence that the runtime saw the window without changing policy outcomes.' `
        -PortalUrl "$baseUrl/audit" `
        -Points @('GitHub and Terraform evaluations are present', 'Governance Window matched: Production Freeze', 'Effective action remains Allow') `
        -Expected 'Audit evidence exists and both automation gates have proceeded.'
    Wait-ForPresenter

    Write-Stage -Number 4 -Title 'Make the window enforce decisions' `
        -Cue 'Turn the active window into a decision override while runtime remains LogOnly.' `
        -PortalUrl "$baseUrl/governance-windows" `
        -Points @('Window mode changes to Enforce', 'Global runtime remains LogOnly', 'Allow is overridden with Deny for affected capabilities') `
        -Expected 'Both gates report Deny and logged_only, but still exit 0.'
    Wait-ForPresenter
    Set-GovernanceWindow -Enabled $true -Mode Enforce
    Invoke-ProductionGates -ExpectedDecision deny -ExpectedMode LogOnly -ExpectedAction logged_only -ExpectedExitCode 0

    Write-Stage -Number 5 -Title 'Enforce the production freeze' `
        -Cue 'Make the denied decisions consequential for integrated automation.' `
        -PortalUrl "$baseUrl/governance" `
        -Points @('Runtime Governance changes to Enforce', 'Policy configuration is unchanged', 'The next GitHub and Terraform attempts stop before execution') `
        -Expected 'Both gates report Deny, effective action deny, and exit 1.'
    Wait-ForPresenter
    Set-RuntimeMode Enforce
    Invoke-ProductionGates -ExpectedDecision deny -ExpectedMode Enforce -ExpectedAction deny -ExpectedExitCode 1

    Write-Stage -Number 6 -Title 'Investigate and restore' `
        -Cue 'Connect the blocked automation back to operational evidence.' `
        -PortalUrl "$baseUrl/dashboard" `
        -Points @('Dashboard shows current posture', 'Capability Activity shows governed evaluations', 'Audit Trail shows the window, reason, mode, and decision') `
        -Expected 'Evidence is available across the portal; cleanup restores LogOnly and disables the window.'
    Write-Host "  Capability Activity: $baseUrl/capability-activity"
    Write-Host "  Audit Trail: $baseUrl/audit"
    Wait-ForPresenter

    $demoSucceeded = $true
}
catch {
    Write-Host ''
    Write-Host "Demo failed: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    if ($runtimeAvailable) {
        Write-Host ''
        Write-Host 'Restoring runtime state...' -ForegroundColor DarkGray
        try { Set-RuntimeMode LogOnly } catch { Write-Warning "Could not restore LogOnly: $($_.Exception.Message)" }
        try { Set-GovernanceWindow -Enabled $false -Mode Observe } catch { Write-Warning "Could not disable Production Freeze: $($_.Exception.Message)" }
    }
    Pop-Location
}

if (-not $demoSucceeded) {
    exit 1
}

Write-Host ''
Write-Host 'Demo complete' -ForegroundColor Green
Write-Host '  - Baseline gates allowed.'
Write-Host '  - Observe recorded Governance Window participation.'
Write-Host '  - Window Enforce produced Deny decisions.'
Write-Host '  - Runtime Enforce blocked GitHub and Terraform.'
Write-Host '  - Audit evidence is available.'
Write-Host '  - Runtime restored to LogOnly; Production Freeze disabled.'
