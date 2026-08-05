# Fluent transpilers

Spine's fluent transpiler API turns a small number of common IL-editing
intentions into guarded, readable operations. It ships inside the Spine package
as `1.6/Assemblies/Spine.Transpilers.dll`, alongside `Spine.dll`.

It is deliberately not described in `About.xml`, on the Workshop page, or in the
repository README. It is a modder-facing facility with no player-visible
behaviour, and advertising it would invite questions the current test coverage
cannot answer (see [`transpiler-readiness.md`](transpiler-readiness.md)). This
document is the reference for modders who go looking.

## When not to use it

Use a prefix or a postfix whenever either can express the change. A transpiler
is the tool for the case a wrapper cannot reach: a call in the middle of a
method whose behaviour or result you need to alter without replacing the whole
method. Reaching for IL when a postfix would do trades a stable patch for a
fragile one and gains nothing.

## Assembly and namespace

The implementation lives in `Spine.Transpilers.dll` under the `Spine.Harmony`
namespace. `Spine.dll` deliberately excludes the transpiler sources
(`Source/Mod.csproj` removes `Spine\harmony\Transpilers\**`), so the two
assemblies are independent: `Spine.Transpilers.dll` carries its own copies of
`HarmonyCompat` and `HarmonyLog` and does not reference `Spine.dll`.

Because both assemblies now ship in the same mod, RimWorld loads both. A
consumer that never touches the transpiler pays only the load cost.

## Entry point

Always go through `FluentTranspiler.Execute`. Pass the original method, and pass
the `ILGenerator` whenever an edit may create a label or a local.

```csharp
static IEnumerable<CodeInstruction> Transpiler(
    IEnumerable<CodeInstruction> instructions,
    MethodBase original,
    ILGenerator generator)
{
    return FluentTranspiler.Execute(
        instructions,
        original,
        generator,
        edit =>
        {
            FluentReplacementResult result = edit
                .ForCall(SourceMethod)
                .ReplaceWith(ReplacementMethod);

            if (!result.Succeeded())
            {
                throw new InvalidOperationException(
                    "Expected one source call; result=" + result);
            }
        });
}
```

Use `FluentTranspilerExecution.ExecuteOrOriginal` only when a patch explicitly
needs exception fallback to the untouched instruction stream.

## Recipes

Each recipe is a named intention rather than a sequence of index arithmetic.

| Recipe | Purpose |
| --- | --- |
| `ForCall(m).ReplaceWith(r)` | Replace exactly one call. |
| `ForCall(m).ReplaceAllWith(r)` | Replace every matching call. |
| `ForCall(m).WrapReturnValue(w)` | Transform a call's result. |
| `ForCall(m).InjectBefore(h)` / `InjectAfter(h)` | Hook around a call site. |
| `BeforeCall(m).SkipOriginalWhen(...)` | Guard a call behind a condition. |
| `Returns<T>().WrapAll(w)` | Transform every value the method returns. |
| `Returns<T>().InsertGuardBeforeReturn(h)` | Side effect on every exit. |
| `ForArgument(i).InRangeCheck(lo, hi)` | Validated bounds edit. |
| `ChangeConstant` / `ChangeConstantAll` | Explicit constant replacement. |
| `InsertAtStart` / `InsertAtExit` | Method entry or exit hooks. |

Choose singular or plural deliberately. A singular recipe refuses an ambiguous
match rather than silently patching an arbitrary occurrence.

## Results

Recipes return `FluentReplacementResult`. Inspect it whenever the edit is
required; a clean `NoMatch` is not an exception, because optional compatibility
paths may legitimately be absent.

| Value | Meaning | Produced |
| --- | --- | --- |
| `PatternReplaced` | The edit landed. | yes |
| `NoMatch` | Pattern absent. Normal for optional paths. | yes |
| `FallbackCallReplaced` | A declared fallback matched instead. | yes |
| `ReplacementAlreadyPresent` | The edit was already applied. | yes |
| `AmbiguousMatch` | Several candidates; refused to guess. | yes |
| `UnsafeMatch` | Matched, but rejected as unsafe to rewrite. | yes |
| `Failed` | The edit could not be completed. | yes |
| `AlreadyPatched` | Reserved for cross-mod conflict. | **no — never raised** |

`Succeeded()` treats `PatternReplaced`, `FallbackCallReplaced`,
`ReplacementAlreadyPresent`, and `AlreadyPatched` as success. Because
`AlreadyPatched` is never produced, a consumer cannot currently use it to detect
that another mod reached the site first.

## Safety contract

Every normal execution validates replacement-method signatures before mutating
anything, preserves labels and exception blocks across supported edits, rejects
branches into removed instruction ranges, performs stack and critical-operand
validation, treats critical findings as failures, and records structured
diagnostics for clean misses and unsafe matches.

`StackSentinel` performs the stack analysis as real basic-block dataflow: it
tracks evaluation-stack depth per instruction, detects underflow, and detects
height disagreement where branches converge. When it meets a type it does not
recognise — typically a local introduced by another mod — it records a
depth-preserving unknown rather than failing, so it degrades instead of
producing false positives on foreign code.

## Known limits

These are real and worth designing around:

- **Exception-handler methods skip stack validation entirely.** Any target
  containing `try`/`catch`/`finally` is not stack-checked.
- **A critical validation failure does not abort in the default profile.** It is
  logged, and the instruction stream is returned to Harmony regardless. Set the
  strict profile, or `TranspilerSafetyPolicy.FailFastOnCritical`, to turn that
  into a throw during development.
- **Cooperative transpiling is unproven.** No automated test covers two
  independent mods transpiling one target method, and `AlreadyPatched` is never
  raised, so a cross-mod collision surfaces as a silent `NoMatch` rather than a
  reported conflict.

Treat a required edit as required: check the result and fail loudly. That single
discipline converts the most likely collision outcome from a silent missing
feature into an actionable error.

## Portability

Nothing in the subsystem is RimWorld-specific. The stack discipline is the
CLR's, the instruction stream is what Roslyn emits for any C# assembly, and
Harmony patches Unity games generally. The pattern matching, label preservation,
exception-region handling, and dataflow analysis apply to any C# Unity game with
a Harmony install.

## Testing

Fixtures live under `Tests/TranspilerFixtures`; test-only machinery is not
shipped. `Tests/TranspilerFixtures/Program.cs` applies real Harmony patches to
four targets — branch labels, exception blocks, a call guard, and return
wrap/guard — asserts runtime behaviour, and unpatches afterwards.

Verify a patch against emitted IL and against the actual target method.
Compilation alone cannot establish that an IL pattern is correct for the loaded
game build and mod stack.
