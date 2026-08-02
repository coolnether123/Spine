# Spine 1.6 verification

## 1.0 development checkpoint — 2026-08-01

The 1.0 development line added the compact, feature-neutral
`ModSettingsPages` facade on top
of the existing contextual-settings navigation service. Acquisition is
transactional: failed Harmony hook installation or registry publication rolls
back without leaving a ghost consumer, and the final successful consumer
release removes Spine-owned contextual hooks.

The current contract suite passed 13 tests with zero failures. It covers
capability negotiation, duplicate registration, disposal, Alt-left-click
routing, ordinary-click rejection, overlap priority, multiple consumers,
deferred opening, exception isolation, exact/group/root fallback, scrolling,
and highlight lifetime. The dedicated stable-tooltip test also passed and
confirms that tooltip patches are opt-in, lease-scoped, idempotent, and restore
RimWorld's UI font state.

Commands:

```powershell
dotnet run --project Tests\Mod.Tests.csproj -c Release
powershell -ExecutionPolicy Bypass -File Tests\Test-StableTooltipSizing.ps1
```

The centralized RimWorld 1.6 build completed with zero warnings, zero errors,
and empty stderr. The tracked `Spine.dll` is 330,240 bytes with SHA-256
`196532364DE045CB10BE23C3C7A2AB1E2E5D4A66E4F12AFBA655DCDE1E7C12DB`.
That checkpoint's hashes remain historical build evidence, not published API
versions. Exact game,
Harmony, tooling, test, and runtime provenance is recorded in
`Engineering/evidence.json`.

Spine itself owns no map component, save data, player window, tick loop, or
gameplay state. Live behavior is therefore verified through its consumers.
The four earlier gameplay consumers and the four new consumers load one shared
Spine assembly, while gameplay semantics and consumer-owned Harmony patches
remain outside Spine. Better Work Tab remains a read-only behavioral source
reference and is not made dependent on external Spine by this checkpoint.

## 1.0 adaptive settings checkpoint — 2026-08-02

Spine now chooses settings chrome from the actual setting count: fewer than
five settings show no search or filters, five through ten show
search only, and larger pages add presentation controls only when they are
useful. Simple/Advanced is omitted when Advanced would expose three or fewer
additional settings, leaving every setting visible on one unified page.
Contextual Alt-click clears any active view/search constraint, opens the
ordinary page, scrolls the exact target into view, and highlights it; it never
filters the settings page or mutates gameplay state.

Contextual registrations no longer add a generic Alt-click tooltip. Existing
feature text is preserved when supplied, and hint-only bindings such as world
labels remain tooltip-free. The convention can be documented once without
covering each feature with repetitive hover text.

All 15 focused contracts and the stable-tooltip sizing test passed. The
centralized RimWorld 1.6 build completed with zero warnings and zero errors.
The tracked DLL for this checkpoint was 330,752 bytes with SHA-256
`4EAB0CE4DCEE0D9A31033997CA8B48C30CB4E5319AD79785839D7A3CFFDC449E`;
Spine remains version 1.0 until its first public release. Live all-consumer
evidence shows the
compact Task Break page without search/filter chrome and the larger Faction
Lens page retaining useful search while omitting redundant view controls.

The final audit also corrected compact-page state cleanup: removing hidden
filter state is now idempotent, so ordinary scrolling and contextual focus are
not reset on every immediate-mode draw pass. Pages with five through ten rows
also discard any legacy filter before drawing their unified settings view.

## 1.0 suite-facade consolidation — 2026-08-02

All six settings-owning gameplay consumers now derive from
`SpineMod<TSettings>`. The base negotiates the standard settings capabilities,
loads the settings object, and lazily creates the translated page and
contextual lease on first UI use. The lazy boundary is required because
RimWorld's translation database is not ready while mod constructors run.

All eight gameplay consumers install production Harmony patches through
`SpineApi.Patching` while retaining their own Harmony owner IDs. Filter by
Example uses only runtime negotiation and guarded patching. Caravan Readiness
uses those services plus the opt-in stable-tooltip lease; neither settings-free
mod acquires an unnecessary settings page.

Verification completed with:

- nine Release builds and nine package validations;
- nine pure automated suites passing;
- the stable-tooltip, Filter Signals UI, Faction Lens connector/selection, and
  SOS2 API/GUI boundary checks passing;
- `Test-ConsumerFacadeUsage.ps1` confirming that no gameplay consumer creates
  its own `SettingsListDrawer`, contextual lease, or direct Harmony installer;
- an isolated `eight-new` runtime lane with all eight consumers, Ideology,
  Biotech, Vehicle Framework, and SOS2 active;
- all six settings pages opened and closed through ordinary RimWorld mod
  settings without an exception;
- expected Harmony owners for all eight consumers and no runtime error after
  settings-page exercise.

The final density-policy recheck is under session
`eight-new-f6eef65e1e104ce0b49e7e8ffe4e1214`. It opened all six settings
consumers, visually confirmed the compact and unified large-page states, and
ended with no runtime errors. The final Spine 1.0 DLL SHA-256 is
`317BF67F114808651EB8B288DA99CE18881D87A986777A0E66C20B3CC1D068A7`.
RimWorld still emits dependency-link warnings because Spine has no public
Workshop or repository download URL yet; no fake URL was added.

## 1.0 BWT boundary cleanup — 2026-08-02

Standalone Spine now contains only suite-proven shared facilities: capability
negotiation, definition-driven settings and contextual navigation,
owner-preserving Harmony installation, opt-in tooltip sizing, bounded caching,
and the fluent transpiler system. BWT-owned snapshots, dirty-region tracking,
render pipelines, profiling, serialization isolation, animation/layout,
settings import/export, and their single-purpose helpers were removed. Better
Work Tab itself was not edited.

The fluent transpiler remains, but its public guidance now starts from one
`FluentTranspiler.Execute` entry point and a short set of guarded recipes.
Cartography, stack analysis, diff/debug support, compatibility matching, and
safety policy are implementation details. Domain packs, cooperative patching,
Unity patterns, and the shipped test harness were removed; emitted-IL fixtures
remain under `Tests`.

Verification results:

- all eight gameplay consumers built through centralized tooling with zero
  stderr and the new pinned Spine dependency;
- all nine pure suites passed (10 Spine contracts plus every gameplay-mod
  suite), as did the separate emitted-IL transpiler fixtures;
- all package validators and focused UI/API boundary scripts passed;
- an isolated `eight-new-6662d3e1212343609d7ce5462d591aaa` lane loaded all
  eight consumers, opened and closed all six consumer settings pages, and
  produced no runtime error pattern;
- the standalone `RimWorldContracts.Tests` executable still cannot initialize
  Unity's native shader calls outside RimWorld, so that existing test remains
  unchanged and requires an in-game harness context.

The first live attempt also proved why capability values are protocol IDs: an
intermediate compacted enum caused tracked consumer DLLs to request the old
numeric bits and fail negotiation. The retained values are now explicit,
covered by tests, and all shipping consumer DLLs were rebuilt before the clean
lane above. No compatibility aliases or removed capability names were restored.

The resulting `Spine.dll` is 267,264 bytes with SHA-256
`51380A8BA11F3B76122C7A40D3C5895A9332262D973DE7BA458F17E0BB0972A8`,
down from 334,336 bytes at the preceding suite-facade checkpoint. The
dependency manifest pins the new hash.

## 1.0 efficiency and optional-transpiler consolidation — 2026-08-02

The final review moved the fluent transpiler implementation out of the core
runtime because Better Work Tab is its only real consumer. Core `Spine.dll`
now contains only facilities exercised by multiple gameplay mods: capability
negotiation, definition-driven settings and contextual navigation,
owner-preserving Harmony installation, opt-in tooltip sizing, bounded caches,
and failure-isolated runtime facades. The optional
`Spine.Transpilers.dll` companion retains the fluent API and its emitted-IL
fixtures without making eight unrelated gameplay mods load that code.

The same pass centralized settings defaults, ordering, and page preparation;
Harmony installation; shared test references; and debug-fixture placement.
Prisoner Interaction Timer now shares one Harmony instance and avoids repeated
enumeration allocations, Faction Lens uses a bounded LRU cache, Filter Signals
uses a smaller cosmetic patch surface, and SOS2 Weapon Readouts resolves its
optional integration through the runtime boundary. Production debug actions
were moved into test fixtures rather than shipped in gameplay assemblies.

One review recommendation was deliberately not applied: Prisoner Interaction
Timer did not move its optional notification watches into a custom
`GameComponent`. RimWorld serializes component runtime types in the polymorphic
game-component list, while this mod has an established removal-from-copied-save
contract. Its two exact lifecycle postfixes keep the saved watch node optional
when the assembly is absent. They now share one Spine-owned consumer installer,
preserve target-specific failure reports, and the hot lookup no longer allocates
capturing LINQ delegates. That is the smallest defensible response without
trading away removal safety.

Verification completed with all eight consumer builds, pure test suites,
package validators, UI/API boundary checks, BWT's 39 focused tests, the Spine
transpiler fixtures, and the generalized harness tests passing. The final
one-DLL BWT lane was
`coolnether-suite-355cca1875a740909cbc91d9c1a59c57`; it reached a generated
map with all eight gameplay consumers active. BWT selected standalone Spine
1.0 and reported `BoundedCaches` plus the shared UI/runtime capabilities. It no
longer requested or reported `FluentTranspilers` because BWT owns that
implementation locally. The post-map Error scan found no target-mod exception,
and the session stopped through the harness without forced termination.

Live verification exposed two harness defects rather than mod defects. A new
lane could inherit an older active lane's source snapshot, and optional
`loadAfter`/`loadBefore` metadata was not considered. The harness now snapshots
the content requested at lane creation and honors optional ordering edges only
when both packages are present. Regression tests cover both behaviors. BWT's
obsolete common-root assemblies were also removed so RimWorld cannot load a
second stale BWT or Harmony payload alongside the conditional 1.6 assembly;
the removed files are recoverable from
the local RimWorld archive folder named
`Better-Work-Tab-root-assemblies-20260802`.

Final shipping hashes are:

- core `Spine.dll`: 109,056 bytes, SHA-256
  `3E857A09793BBFF839D0C18D197E480C9365B6384148F49F48669F068BBB9086`;
- optional developer `Spine.Transpilers.dll`: 171,520 bytes, SHA-256
  `59DF6A4036CDEB9B2FEF9B5339BAF6F49CD0170FB8EAB208893EDF10F1A661F3`.

## 1.0 second-review hardening

The runtime descriptor is now constructed once per assembly load, including
its optional-capability probe. Settings preparation occurs once per scribe
operation, and pristine settings no longer instantiate default objects after
definitions have initialized. Harmony patch operations require stable names,
so unresolved targets cannot collide in the installer cache. Focused tests
cover distinct unresolved keys and rejection of unnamed operations.

`HarmonyCompat.cs` now contains only transpiler preferences. Shared logging is
isolated in `HarmonyLog.cs`, whose keyed warnings use RimWorld's process-wide
`Log.WarningOnce` rather than assembly-local state. Spine's tests now use the
tooling-owned `RimWorld.ModTestSupport` runner instead of retaining another
copy. The pure suite passes 12 contracts and 95 assertions, the emitted-IL
fixtures pass, and all eight consumer-facade checks pass.

Better Work Tab retains the fluent transpiler source inside both of its own
assembly variants. It does not ship or reference the optional standalone
transpiler DLL; that developer companion remains outside BWT's payload and is
not part of BWT's standalone-Spine requirement.

RimWorld continues to report dependency-link warnings because unreleased Spine
does not yet have a truthful public download URL. The combined SOS2 stack also
reported a missing external `PlantPot_Bonsai` definition before SOS2 startup;
no CoolNether123 owner or stack frame was involved, so that content-stack issue
is recorded as external noise rather than hidden by a compatibility patch.

## 1.0 release-candidate packaging boundary

Spine metadata now reports mod version 1.0.0 and describes only the scoped core
runtime. The current compatibility matrix replaces the superseded pre-boundary
classifications while retaining the original investigation as historical
evidence.

The centralized release packager staged the public Spine candidate from an
explicit allowlist. The result contained `Spine.dll`, `About.xml`, the English
language file, and the public README. It contained no symbols, source, tests,
engineering evidence, or `Developer` directory. A parallel BWT candidate
contained its two conditional `Better Work Tab.dll` variants and existing
Multiplayer API dependencies, with no PDB or separate Spine assembly. The
tooling regression suite now places a PDB directly beside a fixture DLL and
proves that selecting the DLL still excludes the symbol and developer package.
