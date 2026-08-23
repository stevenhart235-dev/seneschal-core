[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Tag,

    [Parameter(Mandatory)]
    [ValidatePattern('^\d{12}$')]
    [string] $AwsAccountId,

    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z]{2}(?:-gov)?-[a-z]+(?:-[a-z]+)*-\d$')]
    [string] $AwsRegion,

    [switch] $AllowDirtyWorkingTree
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory)]
        [string] $Command,

        [Parameter()]
        [string[]] $Arguments = @()
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $Command $($Arguments -join ' ')"
    }
}

function Get-EcrImageDigest {
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryName
    )

    $digest = & aws ecr describe-images `
        --region $AwsRegion `
        --repository-name $RepositoryName `
        --image-ids "imageTag=$Tag" `
        --query 'imageDetails[0].imageDigest' `
        --output text
    if ($LASTEXITCODE -ne 0) {
        throw "Could not inspect ${RepositoryName}:$Tag in ECR."
    }

    return $digest.Trim()
}

function Assert-SourceUnchanged {
    param(
        [Parameter(Mandatory)]
        [string] $Stage
    )

    $currentSha = (& git rev-parse HEAD).Trim()
    $currentStatus = (& git status --porcelain=v1 --untracked-files=normal) -join "`n"
    if ($LASTEXITCODE -ne 0 -or $currentSha -ne $commitSha -or
        $currentStatus -ne $initialStatus) {
        throw "The working-tree revision changed $Stage; refusing to publish mismatched images."
    }
}

function Assert-ImageRevision {
    param(
        [Parameter(Mandatory)]
        [string] $Image
    )

    $revision = (& docker image inspect $Image `
        --format '{{index .Config.Labels "org.opencontainers.image.revision"}}').Trim()
    if ($LASTEXITCODE -ne 0 -or $revision -ne $commitSha) {
        throw "Image '$Image' does not carry expected source revision '$commitSha'."
    }
}

$Tag = $Tag.Trim()
if ($Tag.Length -eq 0) {
    throw 'Tag must not be blank.'
}
if ($Tag.Length -gt 128 -or $Tag -notmatch '^[A-Za-z0-9_][A-Za-z0-9_.-]*$') {
    throw "Tag '$Tag' is not a valid OCI image tag."
}
if ($Tag -ieq 'latest') {
    throw "Tag 'latest' is not allowed. Supply an immutable release tag."
}

foreach ($requiredCommand in @('aws', 'docker', 'git')) {
    if (-not (Get-Command $requiredCommand -ErrorAction SilentlyContinue)) {
        throw "Required command '$requiredCommand' was not found on PATH."
    }
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot
try {
    $gitRoot = (& git rev-parse --show-toplevel).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw 'The script must be run from a Git worktree.'
    }
    if ([IO.Path]::GetFullPath($gitRoot) -ne [IO.Path]::GetFullPath($repositoryRoot)) {
        throw "Expected repository root '$repositoryRoot', but Git reported '$gitRoot'."
    }

    $commitSha = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $commitSha -notmatch '^[0-9a-f]{40}$') {
        throw 'Could not determine the current Git commit SHA.'
    }

    $initialStatus = (& git status --porcelain=v1 --untracked-files=normal) -join "`n"
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not inspect the Git working tree.'
    }
    if ($initialStatus -and -not $AllowDirtyWorkingTree) {
        throw 'The working tree is dirty. Commit or remove changes, or use -AllowDirtyWorkingTree for a development-only publication.'
    }
    if ($initialStatus) {
        Write-Warning 'Publishing from a dirty working tree. The reported Git SHA does not identify uncommitted content.'
    }

    $callerAccount = (& aws sts get-caller-identity --query Account --output text).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw 'AWS identity validation failed.'
    }
    if ($callerAccount -ne $AwsAccountId) {
        throw "The active AWS identity belongs to account '$callerAccount', not requested account '$AwsAccountId'."
    }

    $registry = "$AwsAccountId.dkr.ecr.$AwsRegion.amazonaws.com"
    $runtimeRepository = 'seneschal/core'
    $migrationRepository = 'seneschal/migrations'
    $runtimeImage = "${registry}/${runtimeRepository}:$Tag"
    $migrationImage = "${registry}/${migrationRepository}:$Tag"

    foreach ($repositoryName in @(
        $runtimeRepository,
        $migrationRepository)) {
        $existingDigest = & aws ecr describe-images `
            --region $AwsRegion `
            --repository-name $repositoryName `
            --image-ids "imageTag=$Tag" `
            --query 'imageDetails[0].imageDigest' `
            --output text 2>$null
        if ($LASTEXITCODE -eq 0 -and $existingDigest -and $existingDigest.Trim() -ne 'None') {
            throw "Immutable tag already exists: ${registry}/${repositoryName}:$Tag"
        }
        if ($LASTEXITCODE -ne 0) {
            $errorCode = & aws ecr batch-get-image `
                --region $AwsRegion `
                --repository-name $repositoryName `
                --image-ids "imageTag=$Tag" `
                --query 'failures[0].failureCode' `
                --output text
            if ($LASTEXITCODE -ne 0 -or $errorCode.Trim() -ne 'ImageNotFound') {
                throw "Could not verify that ${registry}/${repositoryName}:$Tag is unused."
            }
        }
    }

    Write-Host "Publishing Seneschal images from commit $commitSha"
    Write-Host "Building runtime image: $runtimeImage"
    Invoke-NativeCommand docker @(
        'build', '--file', 'Dockerfile', '--tag', $runtimeImage,
        '--label', "org.opencontainers.image.revision=$commitSha", '.'
    )
    Assert-SourceUnchanged 'after the runtime build'

    Write-Host "Building migration image: $migrationImage"
    Invoke-NativeCommand docker @(
        'build', '--file', 'Dockerfile.migrations', '--tag', $migrationImage,
        '--label', "org.opencontainers.image.revision=$commitSha", '.'
    )
    Assert-SourceUnchanged 'after the migration build'

    Assert-ImageRevision $runtimeImage
    Assert-ImageRevision $migrationImage

    Write-Host "Authenticating Docker to $registry"
    $ecrPassword = & aws ecr get-login-password --region $AwsRegion
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($ecrPassword)) {
        throw 'ECR login-password retrieval failed.'
    }
    $ecrPassword | & docker login --username AWS --password-stdin $registry
    $ecrPassword = $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Docker authentication to ECR failed.'
    }

    Write-Host "Pushing runtime image: $runtimeImage"
    Invoke-NativeCommand docker @('push', $runtimeImage)

    Write-Host "Pushing migration image: $migrationImage"
    Invoke-NativeCommand docker @('push', $migrationImage)

    $runtimeDigest = Get-EcrImageDigest $runtimeRepository
    $migrationDigest = Get-EcrImageDigest $migrationRepository

    Write-Host 'Release-matched images published successfully:'
    Write-Output "Git commit: $commitSha"
    Write-Output "Runtime: $runtimeImage"
    Write-Output "Runtime digest: ${registry}/${runtimeRepository}@$runtimeDigest"
    Write-Output "Migrations: $migrationImage"
    Write-Output "Migrations digest: ${registry}/${migrationRepository}@$migrationDigest"
}
finally {
    Pop-Location
}
