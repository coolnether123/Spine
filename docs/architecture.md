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

`SpineApi.Settings` is the stable mod-settings-page facade. It accepts a
consumer-owned settings object and definitions, then owns the standard drawer,
localized common labels, simple/advanced view state, contextual-navigation
lease, and page disposal. It also exposes the exact definition-driven scribing
operation. Consumers continue to own settings fields, definition IDs, labels,
tooltips, callbacks, write timing, and gameplay effects. This facade replaces
the same page wrapper and translation fallback previously repeated by each
gameplay mod.

Toolbar density is definition-driven inside the facade. Fewer than five
configurable rows draw no search; ten or fewer use one unified view with no
category or simple/advanced filters; larger pages may use the full toolbar.
Contextual navigation clears transient search/filter state and only centers and
highlights the resolved row. It never changes the visible setting population.

Harmony helpers require the consuming mod to pass its own `Harmony` instance.
Spine never owns consumer patches under a shared fallback ID. This keeps patch
diagnostics attributable and lets one mod unpatch its work without affecting
another consumer.

Tooltip stabilization belongs behind an opt-in UI capability facade; loading
Spine alone does not install it. The current implementation acquires and
releases the dedicated `CoolNether123.Spine.StableTooltipSizing` Harmony patch
through consumer-owned leases. RimWorld calculates
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
