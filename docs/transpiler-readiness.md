# Fluent transpiler: shipping and advertising readiness

Status: reviewed 2026-08-05 against the Spine 1.0 tree.
Question asked: is cooperative transpiling — two independent mods applying a
Spine transpiler to the same target method — safe to advertise publicly?

**Answer: ship it, do not advertise it.** The decision is to include the
transpiler in the Spine package while making no store-page claim about it.
Section 2 is why the claim is withheld; section 1 is what must change for the
shipping half of that decision to actually happen.

## 1. Packaging — implemented

The transpiler now ships inside the Spine package. Three changes made this
true, and they are the things to re-check if the packaging is ever revisited:

- `Source/Spine.Transpilers.csproj` now emits to `..\1.6\Assemblies\` instead of
  `..\Developer\Spine.Transpilers\1.6\Assemblies\`, so the assembly is built
  into Spine's own shipping folder.
- `Engineering/build.json` lists `1.6/Assemblies/Spine.Transpilers.dll` in
  `releaseIncludePaths`. That allowlist is explicit, so an unlisted file is
  excluded; this entry is what actually causes the assembly to ship.
- `README.md` no longer describes the transpiler as a separate developer mod
  that must never be included in the Workshop upload. That policy was reversed,
  and leaving the text in place would have documented a rule the release
  violates.

Two consequences accepted deliberately:

- RimWorld loads every assembly in a mod's `Assemblies` folder, so every Spine
  subscriber now loads roughly 170 KB of transpiler code that does nothing
  unless a consumer calls into it.
- `Spine.Transpilers.dll` carries its own copies of `HarmonyCompat` and
  `HarmonyLog`. Those type names therefore exist in two assemblies loaded
  together. This is legal and was already the case for anyone running both mods,
  but it is now universal rather than opt-in.
- `Developer/Spine.Transpilers/` remains in the tree with a now-stale assembly.
  It is excluded from the release allowlist either way, so it does not affect
  the package, but it is no longer the build target.

Shipping and advertising are separate choices. The package includes the
subsystem; the store page, `About.xml`, and the repository README say nothing
about it. The reasons for withholding the public claim are below.

## 2. Cooperative transpiling specifically is unproven

The subsystem is more mature than a first pass suggests. The following are
verified, not assumed:

- Roughly thirty **public** recipe methods return `FluentReplacementResult`, so
  the documented consumer contract ("inspect the result when the edit is
  required") is genuinely implementable. Verified across
  `FluentTranspilerCallRecipes.cs`, `FluentTranspilerFieldRecipes.cs`,
  `FluentTranspilerReturnRecipes.cs`, `FluentTranspilerGuardRecipes.cs`,
  `FluentTranspilerRangeCheckRecipes.cs`, `FluentTranspilerClampRecipes.cs`.
- Six of the eight result values are actually produced: `NoMatch` (26 sites),
  `PatternReplaced` (20), `UnsafeMatch` (17), `Failed` (16),
  `ReplacementAlreadyPresent` (4), `AmbiguousMatch` (4),
  `FallbackCallReplaced` (3). The README's claim that a singular recipe rejects
  an ambiguous match rather than patching an arbitrary occurrence is backed by
  real `AmbiguousMatch` production.
- `StackSentinel` is a real basic-block dataflow analyser with genuine stack
  underflow and branch-merge height-mismatch detection, and it degrades to a
  depth-preserving unknown type rather than false-positiving when it encounters
  locals or types introduced by another mod.
- IL-level fixtures exist and exercise emitted code, not just compilation:
  `Tests/TranspilerFixtures/Program.cs` applies real Harmony patches to four
  targets — branch labels, exception blocks, a call guard, and return
  wrap/guard — asserts runtime behaviour, and unpatches afterwards.

Three verified gaps are what make the cooperative case unproven:

1. **`AlreadyPatched` is inert.** The value is declared in
   `FluentReplacementResult.cs:9` and is treated as success by
   `Succeeded()` at `FluentReplacementResult.cs:22`. It is produced at **zero**
   sites anywhere in the tree. This is precisely the value that would report
   "another mod already rewrote this site," so the one API signal a consumer
   would use to detect a cross-mod conflict never fires. A consumer that
   correctly checks `Succeeded()` cannot distinguish cooperative success from a
   condition that is never raised.

2. **Stack validation is skipped entirely on methods with exception-handling
   clauses.** `FluentTranspiler.cs:2297` sets
   `skipStackValidationForExceptionHandlers` and the validation block is guarded
   by `if (!skipStackValidationForExceptionHandlers)`. Any target with
   `try`/`catch`/`finally` — a large share of interesting RimWorld methods — is
   never stack-checked.

3. **A critical validation failure does not abort the patch in the default
   profile.** At `FluentTranspiler.cs:2389` the throw requires
   `strict || (hasCriticalWarning && TranspilerSafetyPolicy.FailFastOnCritical)`.
   Otherwise control reaches `MMLog.WriteError(message)` and then
   `return _matcher.Instructions().ToList();` at `FluentTranspiler.cs:2405` —
   the IL that failed validation is returned to Harmony anyway.

There is also no automated test covering two independent transpilers applied to
one target method. Each fixture patches a distinct target and unpatches before
the next.

### Failure a player would see

Most likely: silent feature loss. Mod A rewrites the call site, mod B's pattern
no longer matches, mod B's edits no-op, and the build returns an unmodified
stream. Mod A works, mod B's feature is simply missing, and the player reports
the bug against the wrong mod.

Less likely but worse: if both patterns match against shifted IL and the target
contains an exception handler, the result is never stack-checked and ships. A
resulting `InvalidProgramException` surfaces at first invocation, far from the
patch site.

## What would make it advertisable

Produce `AlreadyPatched` where a cross-mod conflict is detectable; stop
returning IL that failed stack validation; stop skipping validation on
exception-handler methods; and land a fixture that applies two independent
transpilers to one target and asserts the second one's behaviour.

None of that is required for the Spine 1.0 release, because the transpiler is
not in the Workshop package. It is the prerequisite for making a public claim
about cooperative transpiling later.

## Correction to an earlier review

An earlier automated pass concluded that consumers could not obtain a
`FluentReplacementResult`, that most result values were dead, and that the
repository contained no tests. All three conclusions were artifacts of an
incomplete file set — the recipe files and the `Tests/` tree were not examined.
They are recorded here as refuted so they are not repeated.
