# Spine

Spine is the feature-neutral runtime shared by CoolNether123 RimWorld mods.
It provides settings pages, contextual settings navigation, guarded Harmony
installation, opt-in tooltip stabilization, and bounded caching. The large,
specialized fluent-transpiler implementation is built as the separately
identified developer mod `Spine.Transpilers`, so ordinary gameplay consumers
do not load it. Spine does not add player-facing gameplay by itself.

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

`SpineApi.Patching` is the single public entry point for guarded patch
installation. `CreateInstaller` owns one consumer-ID Harmony instance,
install-once keys, target-specific mandatory failure diagnostics, and the
standard safety policy. Use `PatchAllOnce`, `PatchTypeOnce`, or `TryPatch` for
the narrowest required installation.

The optional fluent transpiler companion remains available for uncommon cases
that cannot be expressed by a prefix or postfix. Only consumers that reference
and load `Spine.Transpilers.dll` receive it. Its entry point and recipes are
documented in
[`Source/Spine/harmony/Transpilers/README.md`](Source/Spine/harmony/Transpilers/README.md).
Low-level validation, cartography, compatibility matching, and diagnostics are
internal implementation details rather than additional APIs consumers must
learn.

## Release payload

The public Spine mod contains the runtime metadata, languages, documentation,
and `1.6/Assemblies/Spine.dll`. Stage those paths through
RimWorld-Tooling's explicit release allowlist; never upload the repository as
the package. `Developer/`, `Source/`, `Tests/`, `Engineering/`, symbols, build
logs, and intermediate outputs are development material and are not part of
the public Spine runtime.

`Developer/Spine.Transpilers` is a separately identified developer mod. It is
distributed only when a modder intentionally requests that API and must never
be included in the ordinary Spine Workshop upload. Better Work Tab embeds the
transpiler implementation it owns inside `Better Work Tab.dll` and does not
load this companion.
