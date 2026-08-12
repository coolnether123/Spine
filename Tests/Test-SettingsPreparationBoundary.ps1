$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$facadePath = Join-Path $root 'Source\Spine\UI\SettingsFramework\ModSettingsFacade.cs'
$scribePath = Join-Path $root 'Source\Spine\UI\SettingsFramework\SettingsScribe.cs'
$facade = Get-Content -LiteralPath $facadePath -Raw
$scribe = Get-Content -LiteralPath $scribePath -Raw

$facadeScribeStart = $facade.IndexOf(
    'public void Scribe(',
    [StringComparison]::Ordinal)
$facadeScribeEnd = $facade.IndexOf(
    'private static string Translate',
    $facadeScribeStart,
    [StringComparison]::Ordinal)
if ($facadeScribeStart -lt 0 -or $facadeScribeEnd -le $facadeScribeStart) {
    throw 'Could not locate ModSettingsFacade.Scribe().'
}
$facadeScribeBody = $facade.Substring(
    $facadeScribeStart,
    $facadeScribeEnd - $facadeScribeStart)
if ($facadeScribeBody -notmatch 'SettingsScribe\.ScribeAll\(') {
    throw 'ModSettingsFacade.Scribe() must delegate to SettingsScribe.ScribeAll().'
}
if ($facadeScribeBody -match 'SettingsPreparation\.Prepare\(') {
    throw 'ModSettingsFacade.Scribe() must not prepare definitions before ScribeAll().'
}

$scribeAll = [regex]::Match(
    $scribe,
    'public\s+static\s+void\s+ScribeAll\s*\([^)]*\)\s*\{(?<body>.*?)(?=\n\s*public\s+static)',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $scribeAll.Success) {
    throw 'Could not locate SettingsScribe.ScribeAll().'
}
$prepareCalls = [regex]::Matches(
    $scribeAll.Groups['body'].Value,
    'SettingsPreparation\.Prepare\(').Count
if ($prepareCalls -ne 1) {
    throw "SettingsScribe.ScribeAll() must prepare exactly once; found $prepareCalls calls."
}

$settingsSource = Get-Content -LiteralPath (
    Join-Path $root 'Source\Spine\UI\SettingsFramework\SettingDefinitions.cs') -Raw
if ($settingsSource -match 'public\s+static\s+class\s+SettingDefinitions') {
    throw 'The removed public SettingDefinitions factory surface is still present.'
}

Write-Output 'PASS: settings scribing has one preparation owner.'
