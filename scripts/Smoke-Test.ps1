[CmdletBinding()]
param([string]$ContainerImage)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runRoot = Join-Path $repositoryRoot "artifacts/smoke/$([Guid]::NewGuid().ToString('N'))"
$fixture = Join-Path $runRoot 'repository'
$reports = Join-Path $runRoot 'reports'
[void](New-Item -ItemType Directory -Path $fixture -Force)
[void](New-Item -ItemType Directory -Path $reports -Force)

function Invoke-FixtureGit {
    param([string[]]$GitArguments)
    $result = & git -C $fixture @GitArguments
    if ($LASTEXITCODE -ne 0) { throw 'Fixture Git operation failed.' }
    return $result
}

Invoke-FixtureGit @('init', '--initial-branch=main') | Out-Null
Invoke-FixtureGit @('config', 'user.name', 'BusinessHarness smoke test') | Out-Null
Invoke-FixtureGit @('config', 'user.email', 'test@example.invalid') | Out-Null
Invoke-FixtureGit @('config', 'core.autocrlf', 'false') | Out-Null
$source = Join-Path $fixture 'example.cs'
Set-Content -LiteralPath $source -Value '// baseline' -Encoding utf8
Invoke-FixtureGit @('add', '.') | Out-Null
Invoke-FixtureGit @('commit', '-m', 'baseline') | Out-Null
$baseCommit = Invoke-FixtureGit @('rev-parse', 'HEAD')
Set-Content -LiteralPath $source -Value '// clean change' -Encoding utf8
Invoke-FixtureGit @('add', '.') | Out-Null
Invoke-FixtureGit @('commit', '-m', 'clean change') | Out-Null
$cleanCommit = Invoke-FixtureGit @('rev-parse', 'HEAD')
# Synthetic fixture only; never a real credential.
Add-Content -LiteralPath $source -Value 'string password = "synthetic-fixture-value";' -Encoding utf8
Invoke-FixtureGit @('add', '.') | Out-Null
Invoke-FixtureGit @('commit', '-m', 'synthetic finding') | Out-Null
$secretCommit = Invoke-FixtureGit @('rev-parse', 'HEAD')

function Invoke-Scan {
    param([string]$BaseRef, [string]$HeadRef)
    if ($ContainerImage) {
        $userArguments = @()
        if (-not [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
            $userArguments = @('--user', "$(id -u):$(id -g)")
        }
        & docker run --rm --read-only --network none --cap-drop=ALL --security-opt=no-new-privileges `
            @userArguments `
            --mount "type=bind,source=$fixture,target=/repo,readonly" `
            --mount "type=bind,source=$reports,target=/reports" `
            $ContainerImage scan --repo /repo --base $BaseRef --head $HeadRef --output /reports
    }
    else {
        & dotnet (Join-Path $repositoryRoot 'src/BusinessHarness.Cli/bin/Release/net10.0/BusinessHarness.Cli.dll') `
            scan --repo $fixture --base $BaseRef --head $HeadRef --output $reports
    }
    return $LASTEXITCODE
}

# Collect native stdout separately from the exit code returned by the function.
$clean = @(Invoke-Scan $baseCommit $cleanCommit)
if ($clean[-1] -ne 0) { throw 'Clean scan did not return exit 0.' }
$finding = @(Invoke-Scan $baseCommit $secretCommit)
if ($finding[-1] -ne 1) { throw 'Secret scan did not return gate exit 1.' }
$review = Get-Content -LiteralPath (Join-Path $reports 'review.json') -Raw | ConvertFrom-Json
if ($review.passed -or $review.findings[0].ruleId -ne 'BH001') { throw 'Finding report was not correct.' }
$reviewCopy = Join-Path $runRoot 'finding-review.json'
Copy-Item -LiteralPath (Join-Path $reports 'review.json') -Destination $reviewCopy
Copy-Item -LiteralPath (Join-Path $reports 'review.sarif') -Destination (Join-Path $runRoot 'finding-review.sarif')
$failure = @(Invoke-Scan 'missing-reference' $secretCommit)
if ($failure[-1] -ne 3) { throw 'Missing history did not return Git exit 3.' }
if (Test-Path -LiteralPath (Join-Path $reports 'review.json')) { throw 'A stale success report survived failure.' }
$errorReport = Get-Content -LiteralPath (Join-Path $reports 'error.json') -Raw | ConvertFrom-Json
if ($errorReport.errorCode -ne 'BH1003') { throw 'Expected structured Git failure.' }
Write-Output "Smoke checks passed: clean=0, finding=1, missing-ref=3. Artifacts: $runRoot"
