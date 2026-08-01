param()

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$source = [System.IO.File]::ReadAllText(
    (Join-Path $root `
        'Source\Spine\UI\Tooltips\StableTooltipSizing.cs'))
$api = [System.IO.File]::ReadAllText(
    (Join-Path $root `
        'Source\Spine\Core\Diagnostics\SpineApi.cs'))
$failures = [System.Collections.Generic.List[string]]::new()

if ($source -match '\[StaticConstructorOnStartup\]' -or
    $source -match 'static StableTooltipSizing\s*\(')
{
    $failures.Add(
        'Tooltip sizing must not install merely because Spine loaded.')
}
if ($source -notmatch 'ITooltipSizingFacade' -or
    $source -notmatch 'IDisposable Acquire\(string consumerId\)')
{
    $failures.Add(
        'Tooltip sizing must be requested through the exact opt-in facade.')
}
if ($api -notmatch 'public static ITooltipSizingFacade Tooltips')
{
    $failures.Add(
        'SpineApi must expose the tooltip capability facade.')
}
if ($source -notmatch
    'AccessTools\.PropertyGetter\(\s*typeof\(ActiveTip\),\s*' +
    'nameof\(ActiveTip\.TipRect\)\)')
{
    $failures.Add(
        'The correction must target only ActiveTip.TipRect measurement.')
}
if ($source -notmatch
    '(?s)BeforeMeasure\(out GameFont __state\).*?' +
    '__state = Text\.Font;.*?Text\.Font = GameFont\.Small;')
{
    $failures.Add(
        'Tooltip measurement must save the caller font and use Small.')
}
if ($source -notmatch
    '(?s)AfterMeasure\(GameFont __state\).*?Text\.Font = __state;')
{
    $failures.Add(
        'Normal tooltip measurement must restore the caller font.')
}
if ($source -notmatch
    '(?s)AfterMeasureFailure\(.*?GameFont __state\).*?' +
    'Text\.Font = __state;.*?return __exception;')
{
    $failures.Add(
        'Exceptional tooltip measurement must restore the caller font.')
}
if ($source -notmatch 'if \(installed\)' -or
    $source -notmatch 'private static bool installed')
{
    $failures.Add(
        'Shared tooltip installation must be idempotent.')
}
if ($source -notmatch 'HarmonyPatchType\.All' -or
    $source -notmatch 'activeLeaseCount != 0')
{
    $failures.Add(
        'The final facade lease must remove the shared Harmony patch.')
}

if ($failures.Count -gt 0)
{
    foreach ($failure in $failures)
    {
        Write-Error $failure
    }
    exit 1
}

Write-Output (
    'PASS: Spine tooltip sizing is opt-in, lease-scoped, idempotent, and ' +
    'restores UI font state.')
