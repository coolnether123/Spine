[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Configuration
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$toolingRoot = $env:RWT_CASCADE_TOOLING_ROOT
$outputRoot = $env:RWT_CASCADE_BUILD_OUTPUT_ROOT
if ([string]::IsNullOrWhiteSpace($toolingRoot) -or
    [string]::IsNullOrWhiteSpace($outputRoot))
{
    throw 'Spine build must run through the Cascade executor.'
}

Import-Module (Join-Path $toolingRoot 'modules\RimWorld.Tooling.Depot\RimWorld.Tooling.Depot.psd1') -Force
Import-Module (Join-Path $toolingRoot 'modules\RimWorld.Tooling.Build\RimWorld.Tooling.Build.psd1') -Force

$environment = Resolve-RwtEnvironment `
    -Version $Configuration `
    -Purpose Compile `
    -Dependency @('harmony') `
    -VersionManifestPath (Join-Path $toolingRoot 'manifests\rimworld-versions.json') `
    -DependencyManifestPath (Join-Path $toolingRoot 'manifests\dependencies.json')

function Invoke-SpineProjectBuild
{
    param([Parameter(Mandatory = $true)][string]$ProjectPath)

    $result = Invoke-RwtBuild `
        -Project $ProjectPath `
        -Configuration $Configuration `
        -Environment $environment `
        -OutputRoot $outputRoot `
        -Engine DotNet
    if (-not $result.Succeeded)
    {
        throw "Spine build failed for ${Configuration}: $($result.ExitCode)."
    }
}

Invoke-SpineProjectBuild (Join-Path $repoRoot 'Source\Mod.csproj')
Invoke-SpineProjectBuild (Join-Path $repoRoot 'Source\Spine.Transpilers.csproj')

$buildRoot = Join-Path $outputRoot 'build'
$payloadRoot = Join-Path $repoRoot "$Configuration\Assemblies"
New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null
foreach ($assemblyName in @('Spine.dll', 'Spine.Transpilers.dll'))
{
    $artifact = Join-Path $buildRoot $assemblyName
    if (-not (Test-Path -LiteralPath $artifact -PathType Leaf))
    {
        throw "Expected Spine artifact is missing: $artifact"
    }
    Copy-Item -LiteralPath $artifact -Destination (Join-Path $payloadRoot $assemblyName) -Force
}
