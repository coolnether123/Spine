using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Spine.Harmony;

namespace Spine.TranspilerFixtures
{
    internal static class Program
    {
        private static readonly MethodInfo SourceMethod =
            AccessTools.Method(typeof(Program), nameof(Source));
        private static readonly MethodInfo ReplacementMethod =
            AccessTools.Method(typeof(Program), nameof(Replacement));
        private static readonly MethodInfo GuardedTargetMethod =
            AccessTools.Method(typeof(Program), nameof(GuardedTarget));
        private static readonly MethodInfo TypedGuardedTargetMethod =
            AccessTools.Method(typeof(Program), nameof(TypedGuardedTarget));
        private static readonly MethodInfo ShouldSkipMethod =
            AccessTools.Method(typeof(Program), nameof(ShouldSkip));
        private static readonly MethodInfo WrapMethod =
            AccessTools.Method(typeof(Program), nameof(Wrap));
        private static readonly MethodInfo OnReturnMethod =
            AccessTools.Method(typeof(Program), nameof(OnReturn));

        private static bool skipGuardedTarget;
        private static int guardedTargetCalls;
        private static int finallyCalls;
        private static int returnGuardCalls;

        private static int Main()
        {
            try
            {
                RunPatch(
                    "branch labels",
                    nameof(BranchingTarget),
                    nameof(ReplaceSourceTranspiler),
                    () =>
                    {
                        AssertEqual(2, BranchingTarget(1), "positive branch");
                        AssertEqual(-1, BranchingTarget(0), "negative branch");
                    });
                RunPatch(
                    "exception blocks",
                    nameof(ExceptionTarget),
                    nameof(ReplaceSourceTranspiler),
                    () =>
                    {
                        finallyCalls = 0;
                        AssertEqual(2, ExceptionTarget(), "try return");
                        AssertEqual(1, finallyCalls, "finally execution");
                    });
                RunPatch(
                    "call guard",
                    nameof(GuardTarget),
                    nameof(GuardTranspiler),
                    () =>
                    {
                        guardedTargetCalls = 0;
                        skipGuardedTarget = false;
                        GuardTarget();
                        skipGuardedTarget = true;
                        GuardTarget();
                        AssertEqual(
                            1,
                            guardedTargetCalls,
                            "guard skips only enabled call");
                    });
                RunPatch(
                    "typed call guard",
                    typeof(GuardHost),
                    nameof(GuardHost.InvokeGuardedTarget),
                    nameof(TypedGuardTranspiler),
                    () =>
                    {
                        var ordinary = new GuardHost();
                        var guarded = new GuardedHost();

                        guardedTargetCalls = 0;
                        skipGuardedTarget = false;
                        ordinary.InvokeGuardedTarget();
                        guarded.InvokeGuardedTarget();

                        skipGuardedTarget = true;
                        ordinary.InvokeGuardedTarget();
                        guarded.InvokeGuardedTarget();

                        AssertEqual(
                            3,
                            guardedTargetCalls,
                            "typed guard skips only matching instance when enabled");
                    });
                RunPatch(
                    "ambiguous call fallback",
                    nameof(AmbiguousTarget),
                    nameof(AmbiguousTranspiler),
                    () =>
                    {
                        AssertEqual(2, AmbiguousTarget(), "ambiguous calls remain original");
                    });
                RunPatch(
                    "ambiguous guard fallback",
                    nameof(AmbiguousGuardTarget),
                    nameof(AmbiguousGuardTranspiler),
                    () =>
                    {
                        guardedTargetCalls = 0;
                        skipGuardedTarget = true;
                        AmbiguousGuardTarget();
                        AssertEqual(2, guardedTargetCalls, "ambiguous guard leaves both calls original");
                    });
                RunPatch(
                    "return wrapper and guard",
                    nameof(ReturnTarget),
                    nameof(ReturnTranspiler),
                    () =>
                    {
                        returnGuardCalls = 0;
                        AssertEqual(13, ReturnTarget(true), "wrapped true return");
                        AssertEqual(14, ReturnTarget(false), "wrapped false return");
                        AssertEqual(2, returnGuardCalls, "guard before each return");
                    });

                Console.WriteLine(
                    "PASS: standalone fluent transpiler fixtures, including " +
                    "emitted branch, label, exception, guard, wrap, and return IL.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL: " + exception);
                return 1;
            }
        }

        private static void RunPatch(
            string name,
            string targetName,
            string transpilerName,
            Action assertion)
        {
            RunPatch(name, typeof(Program), targetName, transpilerName, assertion);
        }

        private static void RunPatch(
            string name,
            Type targetType,
            string targetName,
            string transpilerName,
            Action assertion)
        {
            var harmony = new HarmonyLib.Harmony(
                "CoolNether123.Spine.TranspilerFixtures." + name);
            var target = AccessTools.Method(targetType, targetName);
            var transpiler = new HarmonyMethod(
                AccessTools.Method(typeof(Program), transpilerName));
            harmony.Patch(target, transpiler: transpiler);
            try
            {
                assertion();
                Console.WriteLine("PASS emitted " + name);
            }
            finally
            {
                harmony.Unpatch(
                    target,
                    HarmonyPatchType.All,
                    harmony.Id);
            }
        }

        private static IEnumerable<CodeInstruction> ReplaceSourceTranspiler(
            IEnumerable<CodeInstruction> instructions,
            MethodBase original,
            ILGenerator generator)
        {
            return FluentTranspiler.Execute(
                instructions,
                original,
                generator,
                transpiler =>
                {
                    var result = transpiler
                        .ForCall(SourceMethod)
                        .ReplaceWith(ReplacementMethod);
                    if (result != FluentReplacementResult.PatternReplaced)
                    {
                        throw new InvalidOperationException(
                            "Expected one source call in " + original +
                            "; got " + result + ".");
                    }
                });
        }

        private static IEnumerable<CodeInstruction> GuardTranspiler(
            IEnumerable<CodeInstruction> instructions,
            MethodBase original,
            ILGenerator generator)
        {
            return FluentTranspiler.Execute(
                instructions,
                original,
                generator,
                transpiler =>
                {
                    var result = transpiler
                        .BeforeCall(GuardedTargetMethod)
                        .SkipOriginalWhen(
                            guard => guard.RequireCallTrue(
                                ShouldSkipMethod));
                    if (result != FluentReplacementResult.PatternReplaced)
                    {
                        throw new InvalidOperationException(
                            "Call guard failed: " + result + ".");
                    }
                });
        }

        private static IEnumerable<CodeInstruction> AmbiguousTranspiler(
            IEnumerable<CodeInstruction> instructions,
            MethodBase original,
            ILGenerator generator)
        {
            return FluentTranspilerExecution.ExecuteRequiredOrOriginal(
                instructions,
                original,
                generator,
                "Ambiguous call fixture",
                transpiler => transpiler
                    .ForCall(SourceMethod)
                    .ReplaceWith(ReplacementMethod));
        }

        private static IEnumerable<CodeInstruction> AmbiguousGuardTranspiler(
            IEnumerable<CodeInstruction> instructions,
            MethodBase original,
            ILGenerator generator)
        {
            return FluentTranspilerExecution.ExecuteRequiredOrOriginal(
                instructions,
                original,
                generator,
                "Ambiguous guard fixture",
                transpiler => transpiler
                    .BeforeCall(GuardedTargetMethod)
                    .SkipOriginalWhen(guard => guard.RequireCallTrue(ShouldSkipMethod)));
        }

        private static IEnumerable<CodeInstruction> TypedGuardTranspiler(
            IEnumerable<CodeInstruction> instructions,
            MethodBase original,
            ILGenerator generator)
        {
            return FluentTranspiler.Execute(
                instructions,
                original,
                generator,
                transpiler =>
                {
                    var result = transpiler
                        .BeforeCall(TypedGuardedTargetMethod)
                        .IncludingPreviousInstruction()
                        .SkipOriginalWhen(
                            guard => guard
                                .RequireCallTrue(ShouldSkipMethod)
                                .SkipIfThisIs(typeof(GuardedHost)));
                    if (result != FluentReplacementResult.PatternReplaced)
                    {
                        throw new InvalidOperationException(
                            "Typed call guard failed: " + result + ".");
                    }
                });
        }

        private static IEnumerable<CodeInstruction> ReturnTranspiler(
            IEnumerable<CodeInstruction> instructions,
            MethodBase original,
            ILGenerator generator)
        {
            return FluentTranspiler.Execute(
                instructions,
                original,
                generator,
                transpiler =>
                {
                    var wrapper = transpiler
                        .Returns<int>()
                        .WrapAll(WrapMethod);
                    var guard = transpiler
                        .Returns<int>()
                        .InsertGuardBeforeReturn(OnReturnMethod);
                    if (wrapper != FluentReplacementResult.PatternReplaced ||
                        guard != FluentReplacementResult.PatternReplaced)
                    {
                        throw new InvalidOperationException(
                            "Return recipes failed: wrapper=" + wrapper +
                            ", guard=" + guard + ".");
                    }
                });
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int BranchingTarget(int value)
        {
            return value > 0 ? Source() : -1;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ExceptionTarget()
        {
            try
            {
                return Source();
            }
            finally
            {
                finallyCalls++;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void GuardTarget()
        {
            GuardedTarget();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ReturnTarget(bool first)
        {
            return first ? 3 : 4;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int AmbiguousTarget()
        {
            return Source() + Source();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AmbiguousGuardTarget()
        {
            GuardedTarget();
            GuardedTarget();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Source() => 1;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Replacement() => 2;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void GuardedTarget()
        {
            guardedTargetCalls++;
        }

        private static void TypedGuardedTarget(int value)
        {
            guardedTargetCalls += value == 7 ? 1 : 1000;
        }

        private static bool ShouldSkip() => skipGuardedTarget;

        private static int Wrap(int value) => value + 10;

        private static void OnReturn()
        {
            returnGuardCalls++;
        }

        private class GuardHost
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public void InvokeGuardedTarget()
            {
                TypedGuardedTarget(7);
            }
        }

        private sealed class GuardedHost : GuardHost
        {
        }

        private static void AssertEqual<T>(
            T expected,
            T actual,
            string name)
        {
            if (!Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    name + ": expected " + expected +
                    ", got " + actual + ".");
            }
        }
    }
}
