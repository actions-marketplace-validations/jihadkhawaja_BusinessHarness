[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $repositoryRoot
try {
    # Updates are deliberate: normal builds always use committed versions and lock files.
    for ($iteration = 0; $iteration -lt 8; $iteration++) {
        & dotnet restore BusinessHarness.slnx
        if ($LASTEXITCODE -ne 0) { throw 'Restore failed. Resolve audit/package errors before updating.' }
        $resultText = & dotnet package list --project BusinessHarness.slnx --outdated --include-transitive --format json --output-version 1
        if ($LASTEXITCODE -ne 0) { throw 'Could not query NuGet package updates.' }
        $result = ($resultText -join "`n") | ConvertFrom-Json
        $updates = @($result.projects | ForEach-Object { $_.frameworks } | ForEach-Object {
            $_.topLevelPackages
            $_.transitivePackages
        } | Where-Object { $_ -and $_.latestVersion } | Sort-Object id -Unique)
        if ($updates.Count -eq 0) {
            Write-Output 'All resolved NuGet dependencies are current stable versions. Review the changes and run tests.'
            return
        }
        $versionsPath = Join-Path $repositoryRoot 'Directory.Packages.props'
        [xml]$versions = Get-Content -LiteralPath $versionsPath -Raw
        foreach ($package in $updates) {
            $entry = $versions.SelectSingleNode("/Project/ItemGroup/PackageVersion[@Include='$($package.id)']")
            if ($null -eq $entry) {
                $entry = $versions.CreateElement('PackageVersion')
                $entry.SetAttribute('Include', $package.id)
                [void]$versions.Project.ItemGroup.AppendChild($entry)
            }
            $entry.SetAttribute('Version', $package.latestVersion)
            Write-Output "$($package.id): $($package.resolvedVersion) -> $($package.latestVersion)"
        }
        $versions.Save($versionsPath)
    }
    throw 'Update graph did not converge after eight iterations. Inspect package compatibility manually.'
}
finally {
    Pop-Location
}
