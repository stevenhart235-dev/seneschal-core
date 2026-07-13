[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$statePath = Join-Path $PSScriptRoot 'artifacts/demo/state.json'

if (-not (Test-Path -LiteralPath $statePath)) {
    Write-Host 'No Seneschal demo is running.'
    exit 0
}

try {
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
}
catch {
    Write-Error "The demo state file could not be read. No processes were stopped: $($_.Exception.Message)"
    exit 1
}

$stopped = 0
foreach ($processId in @($state.ProcessIds)) {
    $process = Get-Process -Id ([int]$processId) -ErrorAction SilentlyContinue
    if (-not $process -or $process.ProcessName -ne 'dotnet') {
        continue
    }

    & taskkill.exe /PID ([int]$processId) /T /F 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0 -or -not (Get-Process -Id ([int]$processId) -ErrorAction SilentlyContinue)) {
        $stopped++
    }
}

Remove-Item -LiteralPath $statePath -Force
Write-Host "Seneschal demo stopped. Tracked processes stopped: $stopped."
