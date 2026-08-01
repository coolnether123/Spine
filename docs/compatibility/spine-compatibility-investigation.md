# Spine compatibility and shared-library investigation

Investigation date: 2026-07-31 (America/Chicago). This is an evidence-only
report. No compatibility code or tests were changed.

## Release verdict

The four current consumers can load, save, reload, render their settings, and
retain independent Harmony ownership while sharing one `Spine.dll`. The
all-consumer run reached a three-map save/reload and 2,000 deterministic ticks
without a consumer or Spine runtime exception. That does not make the current
Spine contract release-ready. Two release blockers remain:

1. **Patch required — version/capability negotiation is not operational.**
   `Spine.dll` and three of four consumer assemblies have assembly version
   `0.0.0.0`; every consumer references `Spine, Version=0.0.0.0`. The
   `SpineApiDescriptor` type has no exported current descriptor, no Settings or
   Transpiler capability, and no consumer calls `Supports`. Metadata declares a
   package dependency but no minimum version. Missing Spine is rejected as a
   hard dependency, but older-than-required and capability-compatible newer
   Spine behavior cannot be distinguished or promised.
2. **Patch required — Spine is not idle as a library.**
   `StableTooltipSizing` is a `[StaticConstructorOnStartup]` class. It creates
   Harmony owner `CoolNether123.Spine.StableTooltipSizing` and installs a
   prefix, postfix, and finalizer on `Verse.ActiveTip.TipRect` even when no
   consumer requests a Spine service. Runtime ownership reported exactly those
   three Spine patches. Make this correction consumer-requested (or put it in a
   gameplay/UI mod); do not let an otherwise unused shared library patch the
   game automatically.

The smallest defensible response is to define and test one real runtime
handshake, then make tooltip stabilization opt-in. Do not add domain-specific
fallback behavior to Spine.

## Exact provenance and test environment

| Item | Exact source/version | Acquired or snapshot date |
|---|---|---|
| RimWorld | Local canonical runtime, in-game `1.6.4871 rev574`; harness manifest labels it `1.6.4871 rev573` | local install; tested 2026-07-31 |
| DLC | Core only; `ModsConfig.xml` has `<knownExpansions />` and the local install has only `Data\Core` | tested 2026-07-31 |
| Spine / `CoolNether123.Spine` | local source commit `f63a595ffc2a1ad7e60a6eec7b1d69fb48bf18b5`; tracked `Spine.dll` SHA-256 `F0773EC3E03DE4B35F5AA10AFFFAB42484BDB12BB38AFD7A061D97322F6D0C54` | source snapshot 2026-07-31 |
| TechSense Filters / `CoolNether123.TechSenseFilters` | local source commit `9ad803dedf896d237e90c71a00209fc743a449a9` | staged 2026-07-31 |
| Prisoner Interaction Timer / `CoolNether123.PrisonerInteractionTimer` | local source commit `7563e6870d0bc35c1d0d01390839ca601126d43f` | source snapshot 2026-07-31 |
| SOS2 Weapon Readouts / `CoolNether123.SOS2WeaponReadouts` | local source commit `7261c8a90034a378c8e642595021b3c6a3cdd305` | source snapshot 2026-07-31 |
| Faction Lens / `CoolNether123.FactionLens` | local source commit `4e889c44498360f8fc091512e2e532724c7c7731` | source snapshot 2026-07-30; tested 2026-07-31 |
| Save Our Ship 2 / `kentington.saveourship2` | local Git checkout `296ba9a2bec124981cff46e557a07934702a210b` from `https://github.com/Bqr1s/SaveOurShip2.git` | local snapshot 2026-07-30 |
| Vehicle Framework / `SmashPhil.VehicleFramework` | local runtime package, metadata version `1.6.2144` | local snapshot 2026-07-30 |
| Harmony / `brrainz.harmony` | local package metadata `2.4.2.0`; assembly SHA-256 `7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C` | local snapshot 2026-07-28 |
| Better Work Tab / `Coolnether123.BetterWorkTab` | read-only local source commit `1bd5969d485eb418b882fff86afdbc8b640956d7`; its tracked 1.6 DLL was already modified before this investigation | inspected 2026-07-31; not staged |

The exact all-consumer load order was Core, Harmony, RimWorld Agent, Spine,
TechSense Filters, Prisoner Interaction Timer, Vehicle Framework, Save Our
Ship 2, SOS2 Weapon Readouts, and Faction Lens. This order follows declared
dependencies. Reversing Spine after a consumer is not a reasonable order.

The user's DLC correction was applied: ordinary work used the local Core-only
runtime. Spine has no DLC reference or DLC-specific branch, so no individual
Steam/DLC run is justified. If a grouped all-DLC lane is run later for consumer
features, its Spine assertions are: exactly one loaded `Spine` assembly,
successful settings open/write for every consumer, consumer-owned Harmony IDs,
no duplicate settings controls/tooltips, save/reload success, and no Spine work
except explicitly requested services.

## Runtime scenarios and results

### Spine alone

Existing dedicated evidence at `docs/verification.md` records Core + Harmony +
RimWorld Agent + Spine, a clean exit, and no red scan matches. Current build and
test verification below reproduces the tracked binary. Classification:
**patch required**, because successful startup coexists with the automatic
tooltip patch and absent version contract.

### One consumer

TechSense Filters + Spine used the same local runtime. Harness start-to-ready
wall time was 11.993 seconds; opening its settings through the synchronous IPC
command took 1.908 seconds including command polling and transport. These are
coarse harness measurements, not frame-time measurements. The settings page
rendered normally, and the process stopped cleanly. Evidence session:
`TechSenseFilters-0649ae35cae84c88986bc495cab48028`.
Classification: **compatible with a documented limitation** (systemic Spine
blockers above).

### All four consumers sharing Spine

Session `TechSenseFilters-2a52c4605fae4681aaa37f09320e8a9e` staged one
Spine source root. All requested package IDs were confirmed active in memory.
The run:

- opened and captured all four settings pages at 1920x1080; no overlap,
  clipped controls, or duplicate controls were visible;
- wrote and immediately read back TechSense `ShowClassificationToolbar=True`,
  PIT `ShowDetailedReasons=False`, SOS2 Readouts `ShowElectricalDraw=False`, and
  Faction Lens `ShowLegend=True`; the corresponding isolated XML files contain
  those values;
- created two additional 50x50 maps (three maps total), saved `SpineAllFour`,
  reloaded it paused, opened the world map, and ran 2,000 ticks at 1,733.40
  ticks/second;
- exited with code 0 without forced termination.

The final `Player.log` is 36,383 bytes, SHA-256
`5523A8FE782DF539DF76A1D4099B172BCB08ADCA42782DEFDB02DEE5E24B37E4`.
It contains no Spine or consumer error. Two Mono native-library fallback lines
occurred at startup and three `ThreadAbortException` records occurred only
during clean process shutdown; neither names a tested mod. Classification:
**compatible with documented limitations** for the shared-loading surface;
this grouped result does not replace each gameplay mod's pairwise compatibility
report.

Harmony ownership was isolated:

| Owner | Patch records |
|---|---:|
| `CoolNether123.TechSenseFilters` | 21 |
| `CoolNether123.PrisonerInteractionTimer` | 3 |
| `CoolNether123.SOS2WeaponReadouts` | 2 |
| `CoolNether123.FactionLens` | 2 |
| `CoolNether123.Spine.StableTooltipSizing` | 3 |

No consumer ships `Spine.dll`; only Spine's `1.6\Assemblies` directory contains
that assembly. BWT embeds source under the `Spine.*` namespaces in the distinct
`Better Work Tab` assembly, so this is not a duplicate Spine DLL.

### Missing, older, and newer Spine

- **Missing:** all four consumers declare `CoolNether123.Spine` as a required
  dependency and have a direct CLR assembly reference. The harness resolves or
  rejects that dependency before launch. Forced activation without the DLL is
  unsupported and would fail assembly binding. Classification: **compatible
  with a documented limitation**; add usable download metadata so RimWorld can
  direct the player to the dependency.
- **Older than required:** no minimum package or assembly version and no
  handshake call exist. Classification: **patch required**.
- **Newer with required capabilities:** the semantic comparison unit contract
  passes, but no runtime descriptor is advertised or consumed. Classification:
  **patch required**; current evidence cannot distinguish a compatible newer
  binary from an API-breaking one.
- **Registration failure/removal:** `RenderPipeline<T>` unit tests prove a
  throwing provider is quarantined, healthy providers continue, a disposed
  registration is removed, and an ID is reusable. None of the four consumers
  registers with that pipeline, so this is a library-unit result rather than a
  consumer integration result. Consumer removal remains governed by RimWorld's
  restart-based mod loading; hot unload is not supported. Classification:
  **compatible with a documented limitation**.

## Settings and contextual routing

Settings hierarchy, scribing, and list drawing are genuinely shared by
TechSense and PIT; Faction Lens uses shared setting widgets and the color
picker, while SOS2 Readouts uses shared RimWorld settings widgets. Repeated
settings opening in the grouped run did not throw or cross-write values.

The requested public contextual-settings convention does **not** exist.
`SettingsListDrawer` has private focus, centering, forced-ancestor visibility,
and a 1.45-second highlight, but there is no public registry/router, no hotspot
ownership arbitration, no fallback-to-main-page service, and none of the four
consumers implements Alt-click routing. Disabled or renamed targets therefore
cannot be tested through a public contract. Classification: **integration
opportunity** before any consumer publicly promises contextual Alt-click; if
that promise is already release scope, promote it to **patch required**.

The smallest implementation belongs in Spine only if at least two real
consumers adopt it together: an owner-scoped registration token, exact option
ID, open/fallback callback, and focus request. Screen-specific hotspots and
domain option mapping remain in each consumer.

## Fluent transpiler API

The existing repository test project does not compile or run the fluent
transpiler sources. The public README instructs consumers to call
`TranspilerTestHarness`, but that class exists only in BWT's embedded source and
is absent from standalone Spine. This is a **target-mod defect** in the public
verification story.

Without changing tests, a reflection fixture against the shipping DLL proved:

- one exact `Math.Abs(int)` call replaced with `Math.Sign(int)` returns
  `PatternReplaced`;
- zero matches returns `NoMatch` with one diagnostic;
- two matches returns `AmbiguousMatch` with one diagnostic;
- `ExecuteOrOriginal` returns the original two-instruction stream after a
  transformer throws;
- `StackSentinel` accepts `nop; ret` and rejects `pop; ret` with `Stack
  underflow at pop (Index 0)`.

Unique/no/ambiguous match, call replacement, fallback, and basic stack
validation are therefore **compatible**. Branches into replaced ranges,
label/block preservation, exception blocks, wrap/guard/return recipes, and
real emitted-IL execution are **inconclusive** because the advertised fixture
suite is not shipped or invoked. Do not declare this API stable until those
cases run from the standalone repository in CI. No existing test was altered.

## API placement and extraction audit

The public API should be delivered as a few capability-focused facades, not as
dozens of public single-purpose helpers. Each facade must own discovery,
version/capability negotiation, lifecycle, ownership, and failure isolation.
Its operations should be exact utilities with stable semantics that can serve
multiple consumers. Helpers with one caller remain internal to the facade;
helpers with one consumer remain in that consumer. This makes future internal
changes possible without expanding the compatibility surface or pushing
domain-specific behavior into Spine.

Stable public API candidates, based on use by at least two real external
consumers:

- settings definitions/hierarchy/scribing/list drawing and their explicit enum
  label/description providers;
- owner-supplied Harmony patch helpers, after their failure/log contract is
  documented;
- small settings widget/layout primitives where two consumers actually adopt
  the same behavior.

Experimental APIs:

- fluent transpilers, cooperative patching, stack sentinel, and diagnostics
  until the standalone fixture suite covers control flow and exceptions;
- semantic capability descriptors until a single advertised runtime descriptor
  and consumer handshake exist;
- render pipelines, dirty regions, snapshots, contiguous buffers, revisions,
  and profiling, which have unit coverage but no current external consumer pair.

Internal or domain-specific behavior that should stay out of Spine:

- TechSense production classification, map capability snapshots, and provider
  semantics;
- PIT prisoner timing, notification state, and inspect-tab behavior;
- SOS2 heat/network/fire semantics and placement/readout policy;
- Faction Lens ownership privacy, relation coloring, collision, and click
  selection;
- BWT drag/drop, work-grid rendering, tutorials, schedules, rules, migration,
  Multiplayer, and Work-tab contextual target mapping.

Current one-consumer helpers are `BoundedLruCache` (TechSense),
`Dialog_ColourPicker` (Faction Lens), and `HarmonyHelper` (Faction Lens). They
may remain general library utilities, but their current external use does not
by itself justify expanding their abstraction. No new helper was created in
this investigation.

The standalone source has 58 files also present in BWT (53 byte-identical and
5 divergent at inspection time), plus `StableTooltipSizing`. BWT-only drag/drop,
render lifecycle/atlas/viewport, tutorial, generic widget, and utility systems
were correctly not extracted. However, standalone Spine still compiles several
BWT-inherited systems with no current external consumer: `SpineTiming`,
`ConnectedOutlineDrawer`, `TextColorHelper`, `ContiguousBuffer`,
`RenderPipeline`, `ScribeIsolationGuard`, settings import/export, the fluent
transpiler family, cooperative patching, easing, and layout tracks. Treat these
as experimental/internal; removal should be API-governed, not line-count
cleanup.

Better Work Tab has not begun external-Spine integration: it has no Spine
dependency or assembly reference and explicitly compiles its embedded sources.
Classification: **integration opportunity**, not a tested consumer. Migrating
BWT is substantial and should not block fixing the standalone runtime contract.

## External frameworks and metadata

HugsLib, XML Extensions, third-party settings frameworks, Performance Fish,
RocketMan, Prepatcher/Fishery, and similar frameworks were not present in the
local catalog and were not downloaded for this bounded lane. Spine has no
static dependency on them. Classification: **inconclusive**, not compatible.

The grouped log reports five actionable metadata warnings: every consumer's
Spine dependency lacks `downloadUrl`/`steamWorkshopUrl`, and Faction Lens's
Harmony dependency lacks them too. This is a **patch required** metadata defect
because a missing dependency should direct the player to a supported source.

## Ranked actions

Release blockers:

1. Implement one runtime facade with a real version/capability handshake and
   consumer requirement checks; capability facades, not implementation types,
   are the supported public entry points.
2. Stop automatic gameplay/UI patching when no consumer requests a service.
3. If fluent transpilers are public release scope, ship and run the promised
   standalone control-flow fixture suite.

Worth implementing before public release:

1. Add supported Spine download metadata to every consumer and Harmony metadata
   to Faction Lens.
2. Publish the facade boundaries plus the stable/experimental/internal policy
   for additive newer versions, and keep one-caller helpers non-public.
3. Add an all-consumer CI/runtime smoke that asserts one Spine assembly and
   owner-separated patches.

Can wait until after release unless already advertised:

- public contextual Alt-click settings registry;
- BWT external-Spine migration;
- removal or promotion of currently unused inherited utilities;
- optional-framework stack tests not locally available.

No unsupported hard conflict was confirmed. The compact classification matrix
is in `docs/compatibility/spine-compatibility-matrix.md`.

## Evidence paths

- All-consumer manifest/log/profile/captures: harness session
  `TechSenseFilters-2a52c4605fae4681aaa37f09320e8a9e`
- One-consumer manifest/log/capture: harness session
  `TechSenseFilters-0649ae35cae84c88986bc495cab48028`
- Clean generalized build: external transient build artifact; not shipped
- Existing Spine-alone runtime evidence: `Engineering/evidence.json` and
  `docs/verification.md`
- Settings captures in the all-consumer session:
  `settings-TechSenseFilters-20260801-011928-743.png`,
  `settings-PrisonerInteractionTimer-20260801-011934-389.png`,
  `settings-SOS2WeaponReadouts-20260801-011939-907.png`, and
  `settings-FactionLens-20260801-011947-104.png`

## Verification commands

`dotnet run --project Tests\Mod.Tests.csproj -c Release` passed 7/7.
`Tests\Test-StableTooltipSizing.ps1` passed, while also confirming the idle
patch is intentional current behavior. The centralized clean Release build
from commit `f63a595` succeeded and reproduced the tracked DLL byte-for-byte;
build stdout SHA-256 is
`DCCF9380F7FB9CCF52E6C2EC861B040F638C94320BC4101F36D554058FDFB5E0`.
