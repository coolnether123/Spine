[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Phase,
    [Parameter(Mandatory = $true)][string]$Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$toolingRoot = $env:RWT_CASCADE_TOOLING_ROOT
if ([string]::IsNullOrWhiteSpace($toolingRoot))
{
    throw 'Spine validation must run through the Cascade executor.'
}

Import-Module (Join-Path $toolingRoot 'modules\RimWorld.Tooling.Build\RimWorld.Tooling.Build.psd1') -Force
$result = Test-RwtPackage `
    -ModRoot (Resolve-Path (Join-Path $PSScriptRoot '..')).Path `
    -Version $Version `
    -ExpectedAssemblyName 'Spine.dll'
if (-not $result.Succeeded)
{
    throw "Spine package validation failed for ${Version}: $($result.Code) $($result.Message)"
}
