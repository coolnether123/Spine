[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AssemblyPath,

    [Parameter(Mandatory = $true)]
    [string]$ManagedDir,

    [Parameter(Mandatory = $true)]
    [string]$HarmonyPath,

    [Parameter(Mandatory = $true)]
    [string]$CecilPath
)

$ErrorActionPreference = 'Stop'

foreach ($path in @($AssemblyPath, $ManagedDir, $HarmonyPath, $CecilPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required legacy API-surface input does not exist: $path"
    }
}

Add-Type -Path $CecilPath

$resolver = New-Object Mono.Cecil.DefaultAssemblyResolver
$resolver.AddSearchDirectory((Split-Path -Parent $AssemblyPath))
$resolver.AddSearchDirectory($ManagedDir)
$resolver.AddSearchDirectory((Split-Path -Parent $HarmonyPath))

$reader = New-Object Mono.Cecil.ReaderParameters
$reader.AssemblyResolver = $resolver
$reader.ReadSymbols = $false
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($AssemblyPath, $reader)

function Get-TypeDefinition([string]$fullName) {
    $type = $assembly.MainModule.GetType($fullName)
    if ($null -eq $type) {
        throw "Missing public Spine type: $fullName"
    }
    if (-not $type.IsPublic) {
        throw "Consumer-required Spine type is not public: $fullName"
    }
    return $type
}

function Require-Method([Mono.Cecil.TypeDefinition]$type, [string]$name) {
    $method = @($type.Methods | Where-Object { $_.IsPublic -and $_.Name -eq $name })
    if ($method.Count -eq 0) {
        throw "Missing public method $name on $($type.FullName)"
    }
}

$requiredMethods = @{
    'Spine.Api.SpineApi' = @('get_Runtime', 'get_Settings', 'get_ContextualSettings', 'get_Patching', 'get_Tooltips')
    'Spine.UI.SettingsFramework.SettingsSchema`1' = @('get_Definitions')
    'Spine.UI.ContextualSettings.ContextualSettingsTarget' = @('Exact', 'Group', 'Root')
    'Spine.UI.ContextualSettings.ContextualSettingsBindingOptions' = @('HintOnly', 'WithTooltip')
    'Spine.UI.ContextualSettings.IContextualSettingsLease' = @('Bind')
    'Spine.Harmony.IHarmonyPatchingFacade' = @('CreateInstaller')
    'Spine.Harmony.IHarmonyPatchInstaller' = @('TryPatch', 'PatchAllOnce', 'PatchTypeOnce')
    'Spine.Caching.BoundedLruCache`2' = @('TryGet', 'AddOrUpdate', 'Remove', 'Reset')
    'Spine.UI.SettingsFramework.SpineMod`1' = @('SettingsCategory', 'DoSettingsWindowContents')
}

foreach ($entry in $requiredMethods.GetEnumerator()) {
    $type = Get-TypeDefinition $entry.Key
    foreach ($methodName in $entry.Value) {
        Require-Method $type $methodName
    }
}

foreach ($typeName in @(
    'Spine.UI.SettingsFramework.ModSettingsPageOptions',
    'Spine.UI.SettingsFramework.SettingDefinition',
    'Spine.UI.SettingsFramework.SettingsFilterDefinition',
    'Spine.UI.SettingsFramework.SettingsHierarchy',
    'Spine.UI.SettingsFramework.SettingsImportExportActions',
    'Spine.UI.SettingsFramework.SettingsListDrawer',
    'Spine.UI.SettingsFramework.SettingSuppression',
    'Spine.UI.SettingsFramework.SettingSupersession',
    'Spine.UI.ColourPicker.Dialog_ColourPicker',
    'Spine.UI.ColourPicker.TextField`1',
    'Spine.Harmony.HarmonyPatchOptions'
)) {
    [void]$assembly.MainModule.GetType($typeName)
    if ($null -eq $assembly.MainModule.GetType($typeName)) {
        throw "Missing resolved Spine capability type: $typeName"
    }
}

$forbidden = @(
    'System.Collections.Generic.IReadOnlyList`1',
    'System.Collections.Generic.IReadOnlyCollection`1',
    'System.Func`6',
    'System.Runtime.CompilerServices.IteratorStateMachineAttribute',
    'System.Runtime.CompilerServices.IsReadOnlyAttribute',
    'System.Runtime.Versioning.TargetFrameworkAttribute'
)
$references = @($assembly.MainModule.GetTypeReferences() | ForEach-Object { $_.FullName })
foreach ($name in $forbidden) {
    if ($references -contains $name) {
        throw "1.0 payload retains forbidden old-CLR reference: $name"
    }
}

$assemblyRefs = @($assembly.MainModule.AssemblyReferences | ForEach-Object { $_.Name })
foreach ($requiredReference in @('mscorlib', 'System', 'System.Core', 'Assembly-CSharp', 'UnityEngine', '0Harmony')) {
    if ($assemblyRefs -notcontains $requiredReference) {
        throw "1.0 payload is missing authoritative runtime reference: $requiredReference"
    }
}

Write-Output ("PASS: Spine 1.0 API surface and old-runtime references resolve " +
    "against managed dir '$ManagedDir' and Harmony '$HarmonyPath'")
