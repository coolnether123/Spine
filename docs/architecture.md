# Spine architecture

Spine is a feature-neutral shared runtime dependency. It contains copied and
decoupled Better Work Tab infrastructure for:

- settings definitions, hierarchy, scribing, widgets, color picker, and layout;
- Harmony compatibility helpers, fluent transpiler recipes, diagnostics, and
  safety policies;
- bounded caches, revision/dirty tracking, immutable snapshots, render
  contracts, timing, and Scribe isolation.

Better Work Tab remains unchanged and continues compiling its embedded copy.
Consumer mods reference `Spine.dll` and declare `CoolNether123.Spine` in
`About.xml`; they never bundle the DLL themselves.

Harmony helpers require the consuming mod to pass its own `Harmony` instance.
Spine never owns consumer patches under a shared fallback ID. This keeps patch
diagnostics attributable and lets one mod unpatch its work without affecting
another consumer.

Spine does own one feature-neutral global UI correction under the dedicated
`CoolNether123.Spine.StableTooltipSizing` Harmony ID. RimWorld calculates
`ActiveTip.TipRect` before consistently selecting the small tooltip font, so
the inherited global font can produce a rectangle that disagrees with the text
actually drawn. Spine temporarily selects `GameFont.Small` only during that
measurement and restores the caller's font in both normal and exceptional
paths. This stabilizes tooltip height and prevents bottom-line flicker without
changing tooltip text, timing, placement policy, or consumer UI state.

BWT-only tutorials, work-tab behavior, drag/drop rendering, and external
`ModAPI` tooling were not copied. Three optional BWT Harmony-manager files were
also excluded because BWT does not compile them and they depend on unrelated
manager/logging systems.

Enum presentation is consumer-supplied through `EnumLabelProvider` and
`EnumDescriptionProvider`, avoiding global translation-key collisions.
