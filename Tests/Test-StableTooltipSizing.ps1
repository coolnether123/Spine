param()

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$source = [System.IO.File]::ReadAllText(
    (Join-Path $root `
        'Source\Spine\UI\Tooltips\StableTooltipSizing.cs'))
$failures = [System.Collections.Generic.List[string]]::new()

if ($source -notmatch '\[StaticConstructorOnStartup\]')
{
    $failures.Add(
        'Stable tooltip sizing must install automatically with Spine.')
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

if ($failures.Count -gt 0)
{
    foreach ($failure in $failures)
    {
        Write-Error $failure
    }
    exit 1
}

Write-Output (
    'PASS: Spine measures every tooltip with its render font, restores UI ' +
    'font state, and installs the correction once.')
