# SpineLib

SpineLib is the public name of the feature-neutral runtime shared by CoolNether123
RimWorld mods. Its package ID, assemblies, namespaces, and API identifiers remain
`Spine` for compatibility with existing consumers.
It provides settings pages, contextual settings navigation, guarded Harmony
installation, opt-in tooltip stabilization, and bounded caching. SpineLib does not add
player-facing gameplay by itself.

## Alt-click to settings

Every setting a SpineLib mod exposes is reachable from the thing it affects.
Alt-click a mod's button, command or label and SpineLib opens that mod's settings
page with the matching option highlighted, rather than leaving you to hunt
through a list for it.

This is a guarantee, not a feature that comes and goes: every CoolNether123 mod
built on SpineLib supports it, and new ones are expected to. The dev-mode settings
audit exists partly to keep that true.

Any other modder can have the same behaviour. Bind a visible rectangle to a
setting id through the `ContextualSettings` capability and SpineLib owns the rest -
Alt-left-click detection, overlap arbitration between competing rectangles,
event consumption, opening the window, scrolling to the row and highlighting
it. The input hook only exists while a consumer holds a lease, so a mod that
never asks for it pays nothing.
## Install
Install Harmony first, then copy the `Spine` folder into RimWorld's `Mods`
directory and load SpineLib before any mod that declares `CoolNether123.Spine` as
a dependency. The folder and dependency ID intentionally remain `Spine`.

Never copy `Spine.dll` into a consumer mod. Two copies of the same types loaded
at once is precisely the situation a shared runtime exists to prevent.

SpineLib is available on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3778463813). Each
consumer mod stays independently selectable and depends on no other gameplay
mod.

## Consumer rules

- Enter SpineLib through its runtime and capability facades. Do not bind to
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

SpineLib remains version 1.0 while it is under active pre-release development.
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
gameplay startup, and Harmony owner ID. `SettingsSchema<TSettings>` is the
single settings-definition API; omitted defaults come from a fresh settings
instance and omitted sort orders follow definition order. The schema
(Spine API 1.1.0, `SpineCapability.SettingsSchema`) owns ordered
definitions and scopes them under optional headers. Mods without a settings page use
`SpineApi.Runtime` and the individual facades directly.

The lower-level `ModSettingsPages` facade remains available for unusual hosts.
It owns the localized drawer, adaptive page chrome, contextual settings lease,
and draw lifecycle without owning any setting's gameplay meaning.

SpineLib also advertises the versioned `ContextualSettings` capability. Consumers
bind a visible rectangle to an exact setting, settings group, or mod root.
SpineLib owns Alt-left-click detection, overlap arbitration, event consumption,
deferred settings-window opening, normal-page scrolling, and highlighting. It
does not filter the page or add Alt-click hints to gameplay tooltips. Its input
hook exists only while a consumer holds a lease. The settings facade also hides
search, category filters, and view filters when they would not be useful.
Simple/Advanced appears only when Advanced contributes at least four additional
settings; three or fewer advanced-only settings are shown in one unified page.

### Setting types

`SettingsScope<TSettings>` supplies `Toggle`, `Int`, `Float`, `NumericInt`,
`Slider`, `Enum`, `Colour`, `Button`, `Custom`, and the low-level `Define`
escape hatch. `SettingsSchema<TSettings>.Section` supplies headers.

For typed field-backed settings, the schema API removes repeated field-name and
parent wiring while keeping the existing definition and scribing runtime:

```csharp
var schema = new SettingsSchema<MySettings>(
    SettingsSchemaConventions.LowerCamelCase);
var section = schema.Section("display", "Display");
section.Toggle("enabled", s => s.Enabled, "Enabled", "Tooltip")
    .DefaultTo(true)
    .ControlsChildren()
    .ScribeAs("legacyEnabled");
section.Slider("size", s => s.Size, "Size", onChanged: s => Apply(s))
    .Range(0f, 1f);
section.Enum("mode", s => s.Mode, "Mode", labelProvider: value => value.ToString());
section.Colour("color", s => s.Color, "Color");

// Re-enter any existing parent without creating another header.
schema.Root.Under("display").Toggle("preview", s => s.Preview, "Preview");

SpineApi.Settings.Acquire(
    "MyMod", mod, settings, schema.Definitions, SaveSettings);
```

Selectors must be direct fields (`s => s.Enabled`); properties, nested members,
and method calls are rejected so the runtime continues to use its established
field-based preparation and scribing behavior. Passing no convention keeps
`ScribeKey` null, preserving the existing `FieldName` fallback.

Most settings need nothing more than that. A checkbox bound to a bool field:

```csharp
schema.Root.Toggle(
        "visuals.legend", settings => settings.ShowLegend, "Show legend")
    .Localized("Example_Settings_Legend", "Example_Settings_Legend_Tip")
    .ScribeAs("showLegend");
```

And a button, which runs an action instead of storing a value:

```csharp
schema.Root.Button(
        "colors.reset", "Reset all",
        tooltip: "Restore color defaults",
        onChanged: settings => settings.ApplyDefaults())
    .Localized("Example_Settings_Reset", "Example_Settings_Reset_Tip");
```

That is the whole API for the common case. The rest of this section is for the
settings that need shaping beyond a label and a field.

#### Refinements

Anything specific to one widget is a **refinement**: a chained method that says
its own name. A slider, for instance, needs bounds, and bounds passed as bare
arguments would be two anonymous floats:

```csharp
schema.Root.Slider(
        "visuals.opacity", settings => settings.Opacity, "Label opacity")
    .Localized("Example_Settings_Opacity", "Example_Settings_Opacity_Tip")
    .ScribeAs("labelOpacity")
    .Range(0.35f, 1f)
    .Step(0.05f)
    .ShowsPercent()
```

Written positionally, a reader meeting `0.35f, 1f` mid-call learns nothing
about which is the minimum, what units it is in, or whether a third number is
coming. Refinements cost one line each and remove the guessing.

Available refinements, all in `SettingRefinements`, all chainable in any order:

| Refinement | Applies to | Effect |
| --- | --- | --- |
| `Range(min, max)` | Slider | Bounds the value. Defaults to 0..1. |
| `Step(size)` | Slider | Quantises, so a player lands on 0.75 not 0.7431. |
| `ShowsPercent()` | Slider | Reads a 0..1 value out as a whole percentage. |
| `ShowsValue(fn)` | Slider | Any other readout: units, counts, a word. |
| `AdvancedOnly()` | any | Hides the entry from the Simple view. |
| `Localized(labelKey, tooltipKey)` | any | Sets the localized label and tooltip keys. |
| `WithCustomReset(test, reset)` | Custom | Supplies the dirty check and reset action for a custom-rendered row. |
| `ShownWhen(pred)` | any | Runtime visibility, evaluated against the settings object. |
| `Pinned(pin)` | any | Holds the entry outside the scrolling region. |

Refinements are not a style preference. Adding a parameter to an existing
public method is a binary-breaking change in C#: a caller bakes the whole
argument list into its IL, so a consumer compiled against the older SpineLib
throws `MissingMethodException` against the newer assembly even though its
source would still compile unchanged. That has already broken every mod built
on SpineLib once. New capability therefore arrives as a new method or a new
refinement, never as a new parameter on a signature that already shipped.

`SpineApi.Patching` is the single public entry point for guarded patch
installation. `CreateInstaller` owns one consumer-ID Harmony instance,
install-once keys, target-specific mandatory failure diagnostics, and the
standard safety policy. Use `PatchAllOnce`, `PatchTypeOnce`, or `TryPatch` for
the narrowest required installation.

## Release payload

The public SpineLib mod contains the runtime metadata, languages, documentation,
and the assemblies named in the release
allowlist. Stage those paths through RimWorld-Tooling's explicit release
allowlist; never upload the repository as the package. `Source/`, `Tests/`,
`Engineering/`, symbols, build logs, and intermediate outputs are development
material and are not part of the public SpineLib runtime.
