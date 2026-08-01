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
