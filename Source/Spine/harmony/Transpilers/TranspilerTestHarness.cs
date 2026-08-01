using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Spine.Harmony
{
    /// <summary>
    /// Exact off-game fixture facade for fluent-transpiler contracts.
    /// </summary>
    public static class TranspilerTestHarness
    {
        public static FluentTranspiler FromInstructions(
            params CodeInstruction[] instructions)
        {
            return FluentTranspiler.For(instructions);
        }

        public static FluentTranspiler FromInstructions(
            IEnumerable<CodeInstruction> instructions,
            MethodBase originalMethod,
            ILGenerator generator = null)
        {
            return FluentTranspiler.For(
                instructions,
                originalMethod,
                generator);
        }

        public static List<CodeInstruction> RunTest(
            FluentTranspiler transpiler,
            bool strict = true,
            bool validateStack = true)
        {
            return transpiler
                .Build(strict: strict, validateStack: validateStack)
                .ToList();
        }

        public static List<CodeInstruction> RunTest(
            FluentTranspiler transpiler,
            FluentTranspiler.BuildProfile profile)
        {
            return transpiler.Build(profile).ToList();
        }

        public static void RunStackAnalysis(
            IEnumerable<CodeInstruction> instructions,
            out string error)
        {
            if (!StackSentinel.Validate(
                instructions.ToList(),
                null,
                out error))
            {
                throw new InvalidOperationException(
                    "Stack analysis failed: " + error);
            }
        }

        public static void AssertMatch(
            FluentTranspiler transpiler,
            string message = "Expected match not found")
        {
            if (!transpiler.HasMatch)
            {
                throw new InvalidOperationException(
                    message + ": " +
                    (transpiler.SoftFailures.LastOrDefault() ??
                     transpiler.Warnings.LastOrDefault() ??
                     "No diagnostic was recorded."));
            }
        }

        public static void AssertInstruction(
            IEnumerable<CodeInstruction> result,
            int index,
            OpCode expectedOpcode,
            object expectedOperand = null)
        {
            var instructions = result.ToList();
            if (index < 0 || index >= instructions.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    "Instruction index " + index +
                    " is outside a stream of " +
                    instructions.Count + ".");
            }

            var instruction = instructions[index];
            if (instruction.opcode != expectedOpcode ||
                expectedOperand != null &&
                !Equals(instruction.operand, expectedOperand))
            {
                throw new InvalidOperationException(
                    "Instruction " + index + " expected " +
                    expectedOpcode + " " + expectedOperand +
                    ", got " + instruction.opcode + " " +
                    instruction.operand + ".");
            }
        }

        public static IReadOnlyList<string> RunAllHarnessCases()
        {
            var results = new List<string>
            {
                CallReplacementCase(
                    "unique call",
                    new[] { Call(SourceMethod) },
                    FluentReplacementResult.PatternReplaced),
                CallReplacementCase(
                    "no call",
                    new[] { new CodeInstruction(OpCodes.Nop) },
                    FluentReplacementResult.NoMatch),
                CallReplacementCase(
                    "ambiguous calls",
                    new[] { Call(SourceMethod), Call(SourceMethod) },
                    FluentReplacementResult.AmbiguousMatch),
                LabelPreservationCase(),
                ExceptionBlockPreservationCase(),
                ReturnWrapperCase(),
                ReturnGuardCase(),
                CallGuardCase(),
                ExecuteFallbackCase(),
                StackValidationCase()
            };
            return results.AsReadOnly();
        }

        public static void AssertAllHarnessCasesPass()
        {
            var failures = RunAllHarnessCases()
                .Where(line => line.StartsWith(
                    "FAIL",
                    StringComparison.Ordinal))
                .ToList();
            if (failures.Count != 0)
            {
                throw new InvalidOperationException(
                    "TranspilerTestHarness reported " +
                    failures.Count + " failure(s):\n  " +
                    string.Join("\n  ", failures.ToArray()));
            }
        }

        private static readonly MethodInfo SourceMethod =
            typeof(FixtureHooks).GetMethod(nameof(FixtureHooks.Source));
        private static readonly MethodInfo ReplacementMethod =
            typeof(FixtureHooks).GetMethod(
                nameof(FixtureHooks.Replacement));
        private static readonly MethodInfo WrapperMethod =
            typeof(FixtureHooks).GetMethod(nameof(FixtureHooks.Wrap));
        private static readonly MethodInfo ReturnGuardMethod =
            typeof(FixtureHooks).GetMethod(
                nameof(FixtureHooks.OnReturn));
        private static readonly MethodInfo SkipMethod =
            typeof(FixtureHooks).GetMethod(nameof(FixtureHooks.ShouldSkip));
        private static readonly MethodInfo VoidTargetMethod =
            typeof(FixtureHooks).GetMethod(nameof(FixtureHooks.VoidTarget));

        private static string CallReplacementCase(
            string name,
            CodeInstruction[] instructions,
            FluentReplacementResult expected)
        {
            var transpiler = FromInstructions(instructions);
            var result = transpiler
                .ForCall(SourceMethod)
                .ReplaceWith(ReplacementMethod);
            var calls = transpiler.Instructions()
                .Where(instruction => instruction != null &&
                    instruction.opcode == OpCodes.Call &&
                    Equals(instruction.operand, ReplacementMethod))
                .Count();
            var expectedCalls = expected ==
                FluentReplacementResult.PatternReplaced
                ? 1
                : 0;
            return result == expected && calls == expectedCalls
                ? "PASS " + name + ": " + result
                : "FAIL " + name + ": expected " + expected +
                  "/replacementCalls=" + expectedCalls +
                  ", got " + result + "/replacementCalls=" + calls +
                  "; stream=" + string.Join(
                      ", ",
                      transpiler.Instructions()
                          .Select(instruction =>
                              instruction.opcode + " " +
                              instruction.operand)
                          .ToArray());
        }

        private static string LabelPreservationCase()
        {
            var label = default(Label);
            var original = Call(SourceMethod);
            original.labels.Add(label);
            var transpiler = FromInstructions(original);
            var result = transpiler
                .ForCall(SourceMethod)
                .ReplaceWith(ReplacementMethod);
            var replacement = transpiler.Instructions().Single();
            return result == FluentReplacementResult.PatternReplaced &&
                replacement.labels.Contains(label)
                ? "PASS labels survive exact replacement"
                : "FAIL labels survive exact replacement";
        }

        private static string ExceptionBlockPreservationCase()
        {
            var original = Call(SourceMethod);
            original.blocks.Add(new ExceptionBlock(
                ExceptionBlockType.BeginExceptionBlock));
            var transpiler = FromInstructions(original);
            var result = transpiler
                .ForCall(SourceMethod)
                .ReplaceWith(ReplacementMethod);
            var replacement = transpiler.Instructions().Single();
            return result == FluentReplacementResult.PatternReplaced &&
                replacement.blocks.Count == 1
                ? "PASS exception blocks survive aligned replacement"
                : "FAIL exception blocks survive aligned replacement";
        }

        private static string ReturnWrapperCase()
        {
            var transpiler = FromInstructions(
                new CodeInstruction(OpCodes.Ldc_I4_1),
                new CodeInstruction(OpCodes.Ret));
            var result = transpiler.Returns<int>().WrapAll(WrapperMethod);
            var instructions = transpiler.Instructions().ToList();
            return result == FluentReplacementResult.PatternReplaced &&
                instructions.Any(instruction =>
                    instruction.Calls(WrapperMethod))
                ? "PASS return wrapper recipe"
                : "FAIL return wrapper recipe: " + result;
        }

        private static string ReturnGuardCase()
        {
            var transpiler = FromInstructions(
                new CodeInstruction(OpCodes.Ret));
            var result = transpiler
                .Returns<object>()
                .InsertGuardBeforeReturn(ReturnGuardMethod);
            var instructions = transpiler.Instructions().ToList();
            return result == FluentReplacementResult.PatternReplaced &&
                instructions.Any(instruction =>
                    instruction.Calls(ReturnGuardMethod))
                ? "PASS return guard recipe"
                : "FAIL return guard recipe: " + result;
        }

        private static string CallGuardCase()
        {
            var method = new DynamicMethod(
                "SpineGuardFixture",
                typeof(void),
                Type.EmptyTypes);
            var transpiler = FromInstructions(
                new[]
                {
                    Call(VoidTargetMethod),
                    new CodeInstruction(OpCodes.Ret)
                },
                null,
                method.GetILGenerator());
            var result = transpiler
                .BeforeCall(VoidTargetMethod)
                .SkipOriginalWhen(
                    guard => guard.RequireCallTrue(SkipMethod));
            return result == FluentReplacementResult.PatternReplaced &&
                transpiler.Instructions().Any(instruction =>
                    instruction.Calls(SkipMethod))
                ? "PASS branch-safe call guard recipe"
                : "FAIL branch-safe call guard recipe: " + result;
        }

        private static string ExecuteFallbackCase()
        {
            var original = new[]
            {
                new CodeInstruction(OpCodes.Ldc_I4_1),
                new CodeInstruction(OpCodes.Ret)
            };
            var result = FluentTranspilerExecution.ExecuteOrOriginal(
                original,
                null,
                null,
                transpiler =>
                {
                    throw new InvalidOperationException("fixture failure");
                },
                null).ToList();
            return result.Count == 2 &&
                result[0].opcode == OpCodes.Ldc_I4_1 &&
                result[1].opcode == OpCodes.Ret
                ? "PASS exception fallback restores original stream"
                : "FAIL exception fallback restores original stream";
        }

        private static string StackValidationCase()
        {
            string validError;
            var valid = StackSentinel.Validate(
                new List<CodeInstruction>
                {
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Pop),
                    new CodeInstruction(OpCodes.Ret)
                },
                null,
                out validError);
            string invalidError;
            var invalid = StackSentinel.Validate(
                new List<CodeInstruction>
                {
                    new CodeInstruction(OpCodes.Pop),
                    new CodeInstruction(OpCodes.Ret)
                },
                null,
                out invalidError);
            return valid && !invalid
                ? "PASS stack validation accepts balance and rejects underflow"
                : "FAIL stack validation: valid=" + valid +
                  ", invalid=" + invalid +
                  ", validError=" + validError +
                  ", invalidError=" + invalidError;
        }

        private static CodeInstruction Call(MethodInfo method)
        {
            return new CodeInstruction(OpCodes.Call, method);
        }

        private static class FixtureHooks
        {
            public static int Source() => 1;
            public static int Replacement() => 2;
            public static int Wrap(int value) => value + 1;
            public static void OnReturn()
            {
            }
            public static bool ShouldSkip() => true;
            public static void VoidTarget()
            {
            }
        }
    }
}
