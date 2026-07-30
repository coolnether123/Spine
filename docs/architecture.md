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

BWT-only tutorials, work-tab behavior, drag/drop rendering, and external
`ModAPI` tooling were not copied. Three optional BWT Harmony-manager files were
also excluded because BWT does not compile them and they depend on unrelated
manager/logging systems.

Enum presentation is consumer-supplied through `EnumLabelProvider` and
`EnumDescriptionProvider`, avoiding global translation-key collisions.
