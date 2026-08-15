# Legacy Fluent transpiler removal

Status: removed from Spine and the migrated Better Work Tab branch on
2026-08-15.

The former `Spine.Transpilers` companion was not an active gameplay contract.
The authorized BWT branch has six production transpiler entry points, all of
which now call the BWT-owned exact-profile engine under
`Source/Transpilers/BwtExactProfile`. The Spine production tree has no
remaining consumer of the legacy API. The only other workspace references were
tests, developer fixtures, documentation, historical payloads, or copied local
implementations such as Construction Performance Optimizer's local transpiler
code.

The removal boundary includes:

- the 20 legacy companion source files and the standalone
  `Source/Spine.Transpilers.csproj`;
- the emitted-IL fixture project and legacy bootstrap;
- tracked `Spine.Transpilers.dll` payloads;
- legacy mirror, build, capability, API-contract, and test-project references.

`SpineCapability` bit 10 remains reserved with its established numeric value so
already-built negotiators do not observe a renumbered protocol. Spine no longer
advertises that bit, and BWT rejects both embedded and external legacy
`Spine.Harmony.FluentTranspiler` types. The generalized framework at
`A:\Dev\Projects\FluentTranspiler` remains independent and is not a BWT or
Spine dependency.

This document replaces the former modder API guide. New transpiler work belongs
to the owning consumer and must retain its own emitted-IL, compatibility, and
runtime evidence; removal of the legacy companion is not a claim that any new
generalized transpiler framework is production-ready.
