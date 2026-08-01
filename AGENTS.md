# Spine local agent guidance

This repository owns the standalone shared Spine runtime for CoolNether123
RimWorld mods. Keep it feature-neutral. Mod-specific gameplay and compatibility
logic belongs in the consuming mod.

Better Work Tab is a read-only source reference for this extraction. Copy from
`A:\Dev\RimWorld\Mods\Better-Work-Tab\Source\Spine`; never edit Better Work Tab
as part of Spine or consumer-mod work.

Use these centralized interfaces:

- Agent harness: `A:\Dev\RimWorld\Infrastructure\AgenticHarness\worktrees\PhaseA`
- Agent CLI: `A:\Dev\RimWorld\Infrastructure\AgenticHarness\worktrees\PhaseA\Tools\rwa.cmd`
- General cascade: `A:\Dev\RimWorld\Worktrees\RimWorld-Tooling\phase-a\tools\Invoke-RimWorldCascade.ps1`
- General scaffold: `A:\Dev\RimWorld\Worktrees\rimworld-mods-new\phase-a`
- Shared build: `A:\Dev\RimWorld\Worktrees\RimWorld-Tooling\phase-a\tools\Invoke-RimWorldBuild.ps1`
- Package validation module: `A:\Dev\RimWorld\Worktrees\RimWorld-Tooling\phase-a\modules\RimWorld.Tooling.Build`
- RimWorld resolver manifest: `A:\Dev\RimWorld\Worktrees\RimWorld-Tooling\phase-a\manifests\rimworld-versions.json`
- RimWorld 1.6 executable: `H:\Games\RimWorld1-6-4871Win64\RimWorldWin64.exe`
- RimWorld 1.6 managed assemblies: `H:\Games\RimWorld1-6-4871Win64\RimWorldWin64_Data\Managed`
- RimWorld 1.6 Mods directory: `H:\Games\RimWorld1-6-4871Win64\Mods`
- Harmony package: `A:\Dev\BenchmarkGames\RimWorld-ConstructionPerformanceOptimizer-1.6\Mods\brrainz.harmony`

Start game work only through `rwa.cmd`. A lane owns its isolated profile, log,
IPC captures, output, and evidence below
`C:\Users\PrecisionX\AppData\Local\Temp\RimWorldAgentTasks\1.6\<session>`.

Build DRY, SOLID, and with separation of concerns. Organize the public surface
as a small set of cohesive capability facades, with explicit lifecycle,
failure-isolation, ownership, and version-negotiation contracts. Consumers
must request a capability from the appropriate facade instead of discovering
implementation types or calling unrelated static helpers directly.
The public entry point is `SpineApi`: negotiate requirements through
`SpineApi.Runtime` and acquire shared tooltip behavior through
`SpineApi.Tooltips`. Keep implementation classes internal.

Facade methods should expose exact, reusable operations with stable semantics.
Do not create a public method for every consumer action, screen, or special
case. Keep narrow, single-use, and single-consumer helpers internal to the
consumer (or internal to Spine when they only implement a facade), and report
when a helper has only one caller. Preserve the domain boundary between
settings/navigation, cooperative Harmony/transpiler safety, and genuinely
RimWorld-neutral utilities. Gameplay semantics remain consumer-owned.

Do not change an existing failing test merely to make it pass. Determine the
contract it protects and report if changing that contract is the only correct
option. Do not bundle Harmony or any consuming mod in Spine.

Tooltip sizing is an opt-in UI capability, not unconditional Spine startup
work. Its facade contract must make repeated requests idempotent and preserve
requesting-consumer ownership/failure isolation. The implementation must
measure `ActiveTip.TipRect` with `GameFont.Small`, matching RimWorld's tooltip
renderer, and restore the caller's font on success and failure. Run
`Tests\Test-StableTooltipSizing.ps1` before handoff.
