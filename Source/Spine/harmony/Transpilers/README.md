# Fluent transpilers

Spine's fluent transpiler API turns a small number of common IL-editing
intentions into guarded, readable operations. It is not the default patching
tool: use a prefix or postfix whenever either can express the change.

## Normal entry point

Use `FluentTranspiler.Execute`. Always pass the original method, and pass the
IL generator when an edit may create a label or local.

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

## Supported recipes

- `ForCall(method).ReplaceWith(replacement)` for exactly one call.
- `ForCall(method).ReplaceAllWith(replacement)` for every matching call.
- `ForCall(method).WrapReturnValue(wrapper)` to transform a call result.
- `BeforeCall(method).SkipOriginalWhen(...)` for a guarded call.
- `Returns<T>().WrapAll(wrapper)` to transform every method return.
- `Returns<T>().InsertGuardBeforeReturn(hook)` for an exit side effect.
- `ForArgument(index).InRangeCheck(lower, upper)` for validated bounds edits.
- `ChangeConstant` and `ChangeConstantAll` for explicit constant replacement.
- `InsertAtStart` and `InsertAtExit` for method entry or exit hooks.

Choose singular or plural behavior deliberately. A singular recipe rejects an
ambiguous match instead of silently patching an arbitrary occurrence.

## Safety contract

Every normal execution:

- validates replacement method signatures before mutation;
- preserves labels and exception blocks across supported edits;
- rejects branches into removed instruction ranges;
- performs stack and critical-operand validation;
- treats critical validation findings as failures;
- records structured diagnostics for clean misses and unsafe matches.

Recipe methods return `FluentReplacementResult`. Consumers must inspect that
result when the edit is required. A clean `NoMatch` is not an exception because
optional compatibility paths may legitimately be absent.

Low-level cursor operations, stack analysis, compatibility matching,
cartography, and diff generation are implementation details. They remain
available to Spine's recipe implementation but are not separate public systems
that consumers need to understand.

## Testing

Transpiler fixtures live under `Tests/TranspilerFixtures`; test-only machinery
is not shipped in `Spine.dll`. Verify a patch against emitted IL and against the
actual target method. Compilation alone cannot establish that an IL pattern is
correct for the loaded RimWorld build and mod stack.
