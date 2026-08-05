# Fluent transpiler: shipping and advertising readiness

Status: reviewed 2026-08-05 against the Spine 1.0 tree.
Question asked: is cooperative transpiling — two independent mods applying a
Spine transpiler to the same target method — safe to advertise publicly?

**Answer: ship it, do not advertise it.** The decision is to include the
transpiler in the Spine package while making no store-page claim about it.
Section 2 is why the claim is withheld; section 1 is what must change for the
shipping half of that decision to actually happen.

## 1. Shipping it requires packaging changes that are not yet made

As of this review the transpiler **would not ship**. Three facts:

- `Engineering/build.json` declares `releaseIncludePaths` as `About`,
  `1.6/Assemblies/Spine.dll`, `Languages`, `LICENSE`, `README.md`. No
  transpiler assembly is listed, and the allowlist is explicit, so anything
  unlisted is excluded.
- `1.6/Assemblies/` contains only `Spine.dll`. There is no
  `Spine.Transpilers.dll` in the shipping folder to include.
- The transpiler is built as a separate mod under
  `Developer/Spine.Transpilers`, which carries its own `About` and `1.6`
  folders.

To ship it inside Spine, the transpiler assembly has to be built into
`Spine/1.6/Assemblies/` and added to `releaseIncludePaths`. Until both are done
the shipping decision is not in effect regardless of intent.

Two consequences worth deciding on deliberately:

- RimWorld loads every assembly in a mod's `Assemblies` folder, so bundling
  means every Spine subscriber loads roughly 170 KB of transpiler code that
  does nothing unless a consumer calls into it. That is harmless but real.
- `README.md` currently states the opposite policy in three places — lines 9,
  112, and 128 describe `Spine.Transpilers` as a separate developer mod that
  "must never be included in the ordinary Spine Workshop upload." Those
  passages contradict the new decision and must be rewritten, or the repository
  documents a rule the release violates.

The store page still omits the subsystem. Shipping something and advertising it
are separate choices, and the reasons for withholding the claim are below.

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
