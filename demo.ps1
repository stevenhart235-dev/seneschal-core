[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repositoryRoot = $PSScriptRoot
$demoDirectory = Join-Path $repositoryRoot 'artifacts/demo'
$logDirectory = Join-Path $demoDirectory 'logs'
$statePath = Join-Path $demoDirectory 'state.json'
$dashboardUrl = 'http://localhost:5000/dashboard'
$readinessUrl = 'http://localhost:5000/ready'
$readinessTimeoutSeconds = 60
$startedProcessIds = [System.Collections.Generic.List[int]]::new()

function Save-DemoState {
    @{ ProcessIds = @($startedProcessIds) } |
        ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8
}

function Stop-TrackedProcesses {
    foreach ($processId in @($startedProcessIds)) {
        if (Get-Process -Id $processId -ErrorAction SilentlyContinue) {
            & taskkill.exe /PID $processId /T /F 2>$null | Out-Null
        }
    }
    Remove-Item -LiteralPath $statePath -Force -ErrorAction SilentlyContinue
}

function Start-DemoProcess {
    param(
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][string[]] $Arguments
    )

    $stdoutPath = Join-Path $logDirectory "$Name.stdout.log"
    $stderrPath = Join-Path $logDirectory "$Name.stderr.log"
    $process = Start-Process dotnet `
        -ArgumentList $Arguments `
        -WorkingDirectory $repositoryRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -PassThru
    $startedProcessIds.Add($process.Id)
    Save-DemoState
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'The dotnet CLI was not found. Install a compatible .NET SDK and ensure dotnet is on PATH.'
    exit 1
}

if (Test-Path -LiteralPath $statePath) {
    try {
        $existingState = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
        $running = @($existingState.ProcessIds | Where-Object {
            $process = Get-Process -Id ([int]$_) -ErrorAction SilentlyContinue
            $process -and $process.ProcessName -eq 'dotnet'
        })
        if ($running.Count -gt 0) {
            Write-Host 'The Seneschal demo is already running. Run .\stop-demo.ps1 before starting it again.'
            exit 1
        }
    }
    catch {
        Write-Warning "Ignoring unreadable stale demo state: $($_.Exception.Message)"
    }
}

New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
Remove-Item -Path (Join-Path $logDirectory '*.log') -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $statePath -Force -ErrorAction SilentlyContinue

Push-Location $repositoryRoot
try {
    & dotnet pack 'Seneschal.Client/Seneschal.Client.csproj' -c Release --nologo --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        throw "Packing Seneschal.Client failed with exit code $LASTEXITCODE."
    }

    Start-DemoProcess -Name 'api' -Arguments @(
        'run', '--project', 'Seneschal.Api/Seneschal.Api.csproj', '--urls', 'http://localhost:5000')

    $deadline = [datetime]::UtcNow.AddSeconds($readinessTimeoutSeconds)
    $ready = $false
    while ([datetime]::UtcNow -lt $deadline) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $readinessUrl -TimeoutSec 3
            if ($response.StatusCode -eq 200) {
                $ready = $true
                break
            }
        }
        catch {
            Start-Sleep -Milliseconds 750
        }
    }

    if (-not $ready) {
        throw "Seneschal did not become ready at $readinessUrl within $readinessTimeoutSeconds seconds. See artifacts/demo/logs/api.*.log."
    }

    Start-DemoProcess -Name 'deployment-worker' -Arguments @(
        'run', '--project', 'labs/multi-application-adoption/DeploymentWorker/DeploymentWorker.csproj')
    Start-DemoProcess -Name 'database-migration-worker' -Arguments @(
        'run', '--project', 'labs/multi-application-adoption/DatabaseMigrationWorker/DatabaseMigrationWorker.csproj')
    Start-DemoProcess -Name 'refund-worker' -Arguments @(
        'run', '--project', 'labs/multi-application-adoption/RefundWorker/RefundWorker.csproj')
    Start-DemoProcess -Name 'approval-worker' -Arguments @(
        'run', '--project', 'labs/multi-application-adoption/ApprovalWorker/ApprovalWorker.csproj')

    Start-Process $dashboardUrl
    Write-Host "Seneschal local demo is running. Logs: artifacts/demo/logs/"
}
catch {
    Stop-TrackedProcesses
    Write-Error "Unable to start the Seneschal demo: $($_.Exception.Message)"
    exit 1
}
finally {
    Pop-Location
}
