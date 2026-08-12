# Spine architecture

Spine is a feature-neutral shared runtime dependency. Its supported systems
are deliberately limited to facilities used by the mod suite:

- runtime capability negotiation;
- definition-driven settings pages and contextual settings navigation;
- owner-preserving guarded Harmony installation;
- opt-in stable tooltip sizing;
- a bounded cache used by Filter Signals, Faction Lens, and BWT's embedded
  renderer.

The fluent transpiler recipes, stack validation, and emitted-IL diagnostics are
an optional companion assembly. They do not inflate or initialize the core
runtime used by the eight gameplay mods.

BWT's work-grid snapshots, invalidation, rendering pipeline, profiling,
serialization isolation, animation, layout, and settings import/export remain
inside BWT. Their old location under BWT's `Source/Spine` folder does not make
them standalone Spine APIs.

Better Work Tab retains its self-contained embedded build and can also compile
an external-Spine candidate. Its allowlisted shared mirror is checked against
standalone Spine on every build. External mode references core Spine and the
optional transpiler companion because BWT is the only current fluent
transpiler consumer. Gameplay consumers reference only `Spine.dll`, declare
`CoolNether123.Spine` in `About.xml`, and never bundle the DLL themselves.

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

Capability enum values are stable protocol identifiers, not an ordinal list.
Retained capabilities keep their assigned bit even when an unrelated API is
removed, and focused tests pin those values so a refactor cannot silently
renumber already-built consumers.

The intended capability boundaries are runtime discovery/negotiation,
settings and contextual navigation, cooperative patching/transpiler safety,
and proven RimWorld-neutral primitives. Filter Signals production knowledge,
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

`SpineMod<TSettings>` provides inherited static settings and contextual-lease
access, eliminating consumer singleton plumbing. `SettingDefinitions` provides
compact factories. Preparation derives omitted sort order from registry
position and omitted defaults from a fresh `TSettings`; explicit scribe keys
remain available for established save keys and migration aliases.

`SettingsSchema<TSettings>` is an additive 1.1 builder over those same
definitions. Its scopes append rows in declaration order and only `Section`
adds a header; `Under` changes the parent target without adding a row. Typed
selectors intentionally accept direct fields only, keeping property access and
runtime reflection out of the public contract.

Toolbar density is definition-driven inside the facade. Fewer than five
configurable rows draw no search; ten or fewer use one unified view with no
category or simple/advanced filters. Larger pages keep search, while
Simple/Advanced appears only when Advanced contributes at least four additional
rows; otherwise all rows remain on the unified page.
Contextual navigation clears transient search/filter state and only centers and
highlights the resolved row. It never changes the visible setting population.

The patch-installer facade creates exactly one Harmony instance for a consumer
ID. It owns install-once keys, standard safety-result handling, and mandatory
target-specific diagnostics. Spine never owns consumer patches under a shared
fallback ID. This keeps ownership attributable and lets one mod unpatch its
work without affecting another consumer.

`SpineApi.Patching` is the only public patch-installation route. Its
`HarmonyPatchOptions` contract prevents consumers from binding to the internal
patch scanner and imported compatibility machinery.

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
