$ErrorActionPreference = 'Stop'

$modsRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$consumers = @(
    'FilterSignals',
    'PrisonerInteractionTimer',
    'SOS2WeaponReadouts',
    'FactionLens',
    'MechMuster',
    'FilterByExample',
    'CaravanReadiness',
    'TaskBreak'
)
$settingsConsumers = @(
    'FilterSignals',
    'PrisonerInteractionTimer',
    'SOS2WeaponReadouts',
    'FactionLens',
    'MechMuster',
    'TaskBreak'
)

function SourceText([string]$consumer)
{
    $source = Join-Path (Join-Path $modsRoot $consumer) 'Source'
    if (-not (Test-Path -LiteralPath $source))
    {
        throw "Missing consumer source: $source"
    }

    return (Get-ChildItem -LiteralPath $source -Filter '*.cs' -Recurse |
        ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
}

foreach ($consumer in $consumers)
{
    $text = SourceText $consumer
    if ($text -notmatch 'SpineApi\.|SpineMod<')
    {
        throw "$consumer does not enter Spine through its public facade."
    }
    if ($text -match '\bharmony\.Patch\(' -or
        $text -match 'new\s+Harmony\([^\r\n]+\)\.PatchAll\(' -or
        $text -match 'HarmonyUtil\.Patch(All|Type)\(' -or
        $text -match 'HarmonyUtil\.PatchOptions' -or
        $text -match 'HarmonyHelper\.(TryPatchMethod|AddPrefix|AddPostfix)\(')
    {
        throw "$consumer bypasses SpineApi.Patching."
    }
    if ($text -match 'new\s+SettingsListDrawer\(' -or
        $text -match 'SpineApi\.ContextualSettings\.Acquire\(' -or
        $text -match 'SettingsScribe\.ScribeAll\(')
    {
        throw "$consumer recreates settings infrastructure owned by Spine."
    }
}

foreach ($consumer in $settingsConsumers)
{
    if ((SourceText $consumer) -notmatch 'SpineMod<')
    {
        throw "$consumer has settings but does not use SpineMod<TSettings>."
    }
}

Write-Output 'PASS: all eight consumers use Spine facades without duplicating settings or patch-installation plumbing.'
