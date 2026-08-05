# Spine

Spine is the feature-neutral runtime shared by CoolNether123 RimWorld mods.
It provides settings pages, contextual settings navigation, guarded Harmony
installation, opt-in tooltip stabilization, and bounded caching. Spine does not add
player-facing gameplay by itself.

## Install

Install Harmony, then install the staged Spine package as an ordinary RimWorld
mod and place Spine before mods that declare `CoolNether123.Spine` as a
dependency. Do not copy `Spine.dll` into consumer mods.

Spine does not yet have a public Workshop or download URL. The verified local
RimWorld 1.6 distribution therefore ships Spine and its gameplay consumers
together. Copy the `Spine` folder alongside any gameplay-mod folder you choose
to install. Each gameplay
mod remains independently selectable and depends on no other gameplay mod.

## Consumer rules

- Enter Spine through its runtime and capability facades. Do not bind to
  implementation types or infer support from an assembly version.
- Request only the capabilities the consumer actually uses. Pass the
  consumer's own ownership identity, including its `Harmony` instance for
  patching, so diagnostics and teardown remain attributable to that mod.
- Prefer exact utility operations with stable meaning. Do not request a public
  API for a single mod action, screen, or special case; keep that code in the
  consumer.
- Depend only on the facilities the consumer actually uses.
- Keep gameplay, compatibility adapters, and mod-specific settings definitions
  in the consumer repository.
- Treat Better Work Tab as read-only provenance. New shared changes belong in
  this standalone repository and can be adopted by BWT separately.

Architecture and provenance are documented in
[`docs/architecture.md`](docs/architecture.md) and
[`docs/research/source-provenance.md`](docs/research/source-provenance.md).
The contextual-settings public contract and BWT migration path are documented
in [`docs/contextual-settings.md`](docs/contextual-settings.md).
The final 1.6 checks are recorded in
[`docs/verification.md`](docs/verification.md).

Spine remains version 1.0 while it is under active pre-release development.
Consumers negotiate exact capabilities instead of inferring features from
incremented development version numbers.

## Quick start

For the common case, derive the mod entry point from `SpineMod<TSettings>`.
One constructor call negotiates the standard settings capabilities, loads the
persisted settings object, and owns lazy page creation plus contextual
navigation. Lazy creation keeps translation and UI work out of RimWorld's
early mod-construction phase:

```csharp
public sealed class ExampleMod : SpineMod<ExampleSettings>
{
    public ExampleMod(ModContentPack content)
        : base(
            content,
            "Author.Example",
            new SemanticVersion(1, 0, 0),
            ExampleSettingsRegistry.Definitions,
            SpineCapability.HarmonyPatching)
    {
        SpineApi.Patching
            .CreateInstaller("Author.Example", "[Example]")
            .PatchAllOnce(Assembly.GetExecutingAssembly());
    }

    protected override string SettingsCategoryLabel => "Example";
}
```

`SpineMod<TSettings>` owns standard RimWorld mod-settings plumbing and exposes
the current settings object plus contextual-settings lease through inherited
static accessors. Consumers still own their definitions, persistence fields,
gameplay startup, and Harmony owner ID. `SettingDefinitions` supplies compact
factories; omitted defaults come from a fresh settings instance and omitted
sort orders follow definition order. Mods without a settings page use
`SpineApi.Runtime` and the individual facades directly.

The lower-level `ModSettingsPages` facade remains available for unusual hosts.
It owns the localized drawer, adaptive page chrome, contextual settings lease,
and draw lifecycle without owning any setting's gameplay meaning.

Spine also advertises the versioned `ContextualSettings` capability. Consumers
bind a visible rectangle to an exact setting, settings group, or mod root.
Spine owns Alt-left-click detection, overlap arbitration, event consumption,
deferred settings-window opening, normal-page scrolling, and highlighting. It
does not filter the page or add Alt-click hints to gameplay tooltips. Its input
hook exists only while a consumer holds a lease. The settings facade also hides
search, category filters, and view filters when they would not be useful.
Simple/Advanced appears only when Advanced contributes at least four additional
settings; three or fewer advanced-only settings are shown in one unified page.

### Setting types

`SettingDefinitions` supplies `Header`, `Toggle`, `Enum`, `Colour`, `Slider`,
`Button`, and `Custom`. `Slider` binds a float field to a dragged range:

```csharp
SettingDefinitions.Slider(
    "visuals.opacity",
    nameof(ExampleSettings.Opacity),
    0.35f, 1f,
    "Label opacity",
    "Example_Settings_Opacity",
    tooltipKey: "Example_Settings_Opacity_Tip",
    step: 0.05f,
    valueFormatter: value => Mathf.RoundToInt(value * 100f) + "%",
    scribeKey: "labelOpacity")
```

`step` quantises the value so a player cannot land on 0.7431, and
`valueFormatter` controls the readout beside the slider. Both are optional.

Post-construction refinements — `Pinned`, `ShownWhen`, `AdvancedOnly` — are
extension methods in `SettingRefinements` rather than factory parameters.
That is deliberate and load-bearing: adding a parameter to an existing public
method is a binary-breaking change in C#, because a caller bakes the whole
argument list into its IL. A consumer compiled against the older Spine then
throws `MissingMethodException` against the newer assembly even though its
source would still compile. New capability arrives as a new method or a new
refinement, never as a new parameter on an existing signature.

`SpineApi.Patching` is the single public entry point for guarded patch
installation. `CreateInstaller` owns one consumer-ID Harmony instance,
install-once keys, target-specific mandatory failure diagnostics, and the
standard safety policy. Use `PatchAllOnce`, `PatchTypeOnce`, or `TryPatch` for
the narrowest required installation.

## Release payload

The public Spine mod contains the runtime metadata, languages, documentation,
and the assemblies named in the release
allowlist. Stage those paths through RimWorld-Tooling's explicit release
allowlist; never upload the repository as the package. `Developer/`, `Source/`, `Tests/`, `Engineering/`, symbols, build
logs, and intermediate outputs are development material and are not part of
the public Spine runtime.
