# Spine 1.6 verification

## API 1.2 release candidate — 2026-08-01

Spine 1.2 adds the compact, feature-neutral `ModSettingsPages` facade on top
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
Its file and informational versions are `1.2.0.0` and `1.2.0`; the stable
assembly version remains unchanged for binary compatibility. Exact game,
Harmony, tooling, test, and runtime provenance is recorded in
`Engineering/evidence.json`.

Spine itself owns no map component, save data, player window, tick loop, or
gameplay state. Live behavior is therefore verified through its consumers.
The four earlier gameplay consumers and the four new consumers load one shared
Spine assembly, while gameplay semantics and consumer-owned Harmony patches
remain outside Spine. Better Work Tab remains a read-only behavioral source
reference and is not made dependent on external Spine by this release.

## API 1.3 adaptive settings follow-up — 2026-08-02

Spine now chooses settings chrome from the actual setting count: fewer than
five settings show no search or filters, five through ten show search only,
and larger pages show search plus Simple/Advanced presentation controls.
Contextual Alt-click clears any active view/search constraint, opens the
ordinary page, scrolls the exact target into view, and highlights it; it never
filters the settings page or mutates gameplay state.

Contextual registrations no longer add a generic Alt-click tooltip. Existing
feature text is preserved when supplied, and hint-only bindings such as world
labels remain tooltip-free. The convention can be documented once without
covering each feature with repetitive hover text.

All 15 focused contracts and the stable-tooltip sizing test passed. The
centralized RimWorld 1.6 build completed with zero warnings and zero errors.
The tracked DLL is 330,752 bytes with SHA-256
`4EAB0CE4DCEE0D9A31033997CA8B48C30CB4E5319AD79785839D7A3CFFDC449E`;
file/informational version is 1.3.0 while the assembly version remains stable
for consumer binary compatibility. Live all-consumer evidence shows the
compact Task Break page without search/filter chrome and the larger Faction
Lens page retaining its useful search and view controls.

The final audit also corrected compact-page state cleanup: removing hidden
filter state is now idempotent, so ordinary scrolling and contextual focus are
not reset on every immediate-mode draw pass. Pages with five through ten rows
also discard any legacy filter before drawing their unified settings view.
