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

## Public API shape

Spine exposes a small number of cohesive capability facades rather than a
catalog of public implementation helpers. Each facade owns one stable concern
and defines its capability identifier, lifecycle, failure behavior, ownership,
and compatibility contract. A consumer negotiates the capabilities it needs
through the runtime facade and then uses the exact utility operations offered
by the relevant capability facade. Consumers do not infer support from assembly
versions, reflect over implementation types, or depend on unrelated helpers.

Facade methods are deliberately precise: one operation has one documented
meaning that can serve multiple real consumers. Spine does not add a new public
entry point for each mod, screen, button, or compatibility case. A helper used
by only one facade implementation stays internal; a helper used by only one
consumer stays in that consumer. Reuse alone is insufficient when the shared
code would import gameplay semantics into Spine.

The intended capability boundaries are runtime discovery/negotiation,
settings and contextual navigation, cooperative patching/transpiler safety,
and proven RimWorld-neutral primitives. TechSense production knowledge,
prisoner timing, SOS2 weapon behavior, Faction Lens world-map policy, and Better
Work Tab behavior remain outside every Spine facade.

Harmony helpers require the consuming mod to pass its own `Harmony` instance.
Spine never owns consumer patches under a shared fallback ID. This keeps patch
diagnostics attributable and lets one mod unpatch its work without affecting
another consumer.

Tooltip stabilization belongs behind an opt-in UI capability facade; loading
Spine alone must not install it. The current implementation still installs a
global correction under the dedicated
`CoolNether123.Spine.StableTooltipSizing` Harmony ID and therefore does not yet
meet that contract. RimWorld calculates
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
