param(
    [string]$Ref = "",
    [string]$Repo = "",
    [string]$Workflow = "actions.yml",
    [string]$ArtifactName = "azookey-setup",
    [string]$OutputDir = "",
    [switch]$NoDownload
)

$ErrorActionPreference = "Stop"

function Invoke-Native {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

function Invoke-GhJson {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $output = & gh @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "gh $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
    if (!$output) {
        return $null
    }
    return (($output -join "`n") | ConvertFrom-Json)
}

function Convert-GitHubRemoteToRepo {
    param([Parameter(Mandatory = $true)][string]$Url)

    if ($Url -match "github\.com[:/](?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?$") {
        return "$($Matches.owner)/$($Matches.repo)"
    }

    throw "could not derive GitHub owner/repo from remote URL: $Url"
}

function Resolve-Repository {
    if ($Repo) {
        return $Repo
    }

    $upstream = & git rev-parse --abbrev-ref --symbolic-full-name "@{u}" 2>$null
    if ($LASTEXITCODE -eq 0 -and $upstream -match "^(?<remote>[^/]+)/") {
        $remoteName = $Matches.remote
    } else {
        $remoteName = (& git remote | Select-Object -First 1)
    }

    if (!$remoteName) {
        throw "no git remote found; pass -Repo owner/name"
    }

    $remoteUrl = (& git remote get-url $remoteName).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "failed to read git remote URL for $remoteName"
    }

    return Convert-GitHubRemoteToRepo -Url $remoteUrl
}

function Resolve-Ref {
    if ($Ref) {
        return $Ref
    }

    $branch = (& git branch --show-current).Trim()
    if ($LASTEXITCODE -eq 0 -and $branch) {
        return $branch
    }

    $sha = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or !$sha) {
        throw "could not resolve current git ref; pass -Ref explicitly"
    }
    return $sha
}

function Convert-ToSafePathSegment {
    param([Parameter(Mandatory = $true)][string]$Value)

    return ($Value -replace "[^A-Za-z0-9._-]", "_")
}

$repoRoot = (& git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or !$repoRoot) {
    throw "this script must be run inside a git repository"
}

$resolvedRepo = Resolve-Repository
$resolvedRef = Resolve-Ref
$startedAt = (Get-Date).ToUniversalTime().AddSeconds(-10)

if (!$OutputDir) {
    $safeRef = Convert-ToSafePathSegment -Value $resolvedRef
    $OutputDir = Join-Path $repoRoot ".local/artifacts/github-actions/$safeRef"
}

Write-Host "triggering workflow $Workflow on $resolvedRepo ref $resolvedRef"
Invoke-Native gh workflow run $Workflow --repo $resolvedRepo --ref $resolvedRef

$deadline = (Get-Date).AddMinutes(3)
$run = $null
do {
    Start-Sleep -Seconds 5
    $runs = Invoke-GhJson -Arguments @(
        "run", "list",
        "--repo", $resolvedRepo,
        "--workflow", $Workflow,
        "--branch", $resolvedRef,
        "--limit", "10",
        "--json", "databaseId,createdAt,headBranch,headSha,status,conclusion,url"
    )

    $run = @($runs | Where-Object {
            [DateTime]::Parse($_.createdAt).ToUniversalTime() -ge $startedAt
        } | Sort-Object { [DateTime]::Parse($_.createdAt) } -Descending | Select-Object -First 1)
} while (!$run -and (Get-Date) -lt $deadline)

if (!$run) {
    throw "workflow run did not appear within 3 minutes for $resolvedRepo ref $resolvedRef"
}

$runId = [string]$run.databaseId
Write-Host "watching run $runId"
Invoke-Native gh run watch $runId --repo $resolvedRepo --exit-status

if ($NoDownload) {
    Write-Host "download skipped: $($run.url)"
    exit 0
}

New-Item -ItemType Directory -Force $OutputDir | Out-Null
Write-Host "downloading artifact $ArtifactName to $OutputDir"
Invoke-Native gh run download $runId --repo $resolvedRepo -n $ArtifactName -D $OutputDir
Write-Host "artifact downloaded from $($run.url)"
