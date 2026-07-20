#Requires -Version 7.0
<#
.SYNOPSIS
    Fails the build when application source or build inputs contain authentication
    flows, Microsoft Graph surfaces, permissions, or UI elements prohibited by
    IMPLEMENTATION_CONTRACT.md and docs/GRAPH_ENDPOINT_PERMISSION_MATRIX.md.

.DESCRIPTION
    Scans src/, the root solution file, and shared MSBuild props. Test sources and this
    script are intentionally outside the scanned set because they define the patterns.

    Prohibited in version 1: Graph beta endpoints, application permissions, Microsoft 365
    write permissions, resource-owner password credentials, device-code flow, client
    secrets, certificates, and any employee-password UI surface.
#>
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

$prohibitedPatterns = @(
    'graph\.microsoft\.com/beta',
    'Microsoft\.Graph\.Beta',
    'AcquireTokenByUsernamePassword',
    'AcquireTokenWithDeviceCode',
    'AcquireTokenForClient',
    'ConfidentialClientApplication',
    'ClientSecret',
    'X509Certificate',
    'PasswordBox',
    'Files\.ReadWrite',
    'Sites\.ReadWrite',
    'Mail\.ReadWrite',
    'Directory\.ReadWrite',
    'ROPC',
    'DeviceCode'
)

$scanRoots = @(
    (Join-Path $RepositoryRoot 'src'),
    (Join-Path $RepositoryRoot 'OneDriveServerTransfer.sln'),
    (Join-Path $RepositoryRoot 'Directory.Build.props'),
    (Join-Path $RepositoryRoot 'Directory.Packages.props')
)

$files = New-Object System.Collections.Generic.List[System.IO.FileInfo]
foreach ($root in $scanRoots) {
    if (Test-Path -LiteralPath $root -PathType Container) {
        Get-ChildItem -LiteralPath $root -Recurse -File |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
            ForEach-Object { $files.Add($_) }
    }
    elseif (Test-Path -LiteralPath $root -PathType Leaf) {
        $files.Add((Get-Item -LiteralPath $root))
    }
}

$violations = New-Object System.Collections.Generic.List[string]
foreach ($file in $files) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($pattern in $prohibitedPatterns) {
        if ($content -match $pattern) {
            $violations.Add("Prohibited pattern '$pattern' found in $($file.FullName)")
        }
    }
}

if ($violations.Count -gt 0) {
    foreach ($violation in $violations) {
        Write-Host "ERROR: $violation"
    }
    exit 1
}

Write-Host "Prohibited-content check passed. Scanned $($files.Count) files for $($prohibitedPatterns.Count) patterns."
exit 0
