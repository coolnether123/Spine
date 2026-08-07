#if RWT_LEGACY_BOOTSTRAP
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Spine.Api;
using Verse;

namespace Spine.Api
{
    /// <summary>
    /// The pre-ModSettings runtime still exposes the stable Spine identity,
    /// bounded cache contract, and guarded Harmony entry point. Settings-page
    /// capabilities are intentionally not advertised because those APIs do
    /// not exist before RimWorld 0.17.
    /// </summary>
    public static class SpineApi
    {
        public static ISpineRuntimeFacade Runtime =>
            SpineLegacyRuntimeFacade.Instance;

        public static Spine.Harmony.IHarmonyPatchingFacade Patching =>
            Spine.Harmony.SpineLegacyHarmonyFacade.Instance;
    }

    internal sealed class SpineLegacyRuntimeFacade : ISpineRuntimeFacade
    {
        internal static readonly SpineLegacyRuntimeFacade Instance =
            new SpineLegacyRuntimeFacade();

        private static readonly SpineApiDescriptor CurrentDescriptor =
            new SpineApiDescriptor(
                "CoolNether123.Spine",
                new SemanticVersion(1, 0, 0),
                SpineCapability.BoundedCaches |
                SpineCapability.HarmonyPatching);

        public SpineApiDescriptor Descriptor => CurrentDescriptor;

        public SpineCompatibilityResult Check(SpineRequirement requirement)
        {
            SpineCapability missing = requirement.RequiredCapabilities &
                ~CurrentDescriptor.Capabilities;
            if (missing != SpineCapability.None)
            {
                return new SpineCompatibilityResult(
                    false,
                    missing,
                    requirement.ConsumerId +
                    " requires unavailable legacy Spine capabilities: " +
                    missing + ".");
            }

            return new SpineCompatibilityResult(
                true,
                SpineCapability.None,
                requirement.ConsumerId + " requirements are satisfied by " +
                "legacy Spine " + CurrentDescriptor.Version + ".");
        }

        public void Require(SpineRequirement requirement)
        {
            SpineCompatibilityResult result = Check(requirement);
            if (!result.IsCompatible)
            {
                throw new NotSupportedException(result.Detail);
            }
        }
    }
}

namespace Spine.Harmony
{
    public sealed class HarmonyPatchOptions
    {
        public string[] Before { get; set; }
        public string[] After { get; set; }
        public int? Priority { get; set; }
        public Action<object, string> OnResult { get; set; }
    }

    public interface IHarmonyPatchingFacade
    {
        SpineApiDescriptor Descriptor { get; }

        void PatchAll(
            HarmonyLib.Harmony harmony,
            Assembly assembly,
            HarmonyPatchOptions options = null);

        IList<MethodBase> PatchType(
            HarmonyLib.Harmony harmony,
            Type patchType,
            HarmonyPatchOptions options = null);

        bool TryPatch(
            HarmonyLib.Harmony harmony,
            MethodBase target,
            HarmonyMethod prefix = null,
            HarmonyMethod postfix = null,
            HarmonyMethod transpiler = null,
            Action<string> report = null);

        IHarmonyPatchInstaller CreateInstaller(
            string consumerId,
            string logPrefix);
    }

    public interface IHarmonyPatchInstaller
    {
        bool PatchAllOnce(
            Assembly assembly,
            HarmonyPatchOptions options = null);

        bool PatchTypeOnce(
            string patchName,
            Type patchType,
            int expectedPatchCount = 1,
            HarmonyPatchOptions options = null);

        bool TryPatch(
            string patchName,
            MethodBase target,
            HarmonyMethod prefix = null,
            HarmonyMethod postfix = null,
            HarmonyMethod transpiler = null);
    }

    internal sealed class SpineLegacyHarmonyFacade : IHarmonyPatchingFacade
    {
        internal static readonly SpineLegacyHarmonyFacade Instance =
            new SpineLegacyHarmonyFacade();

        private static readonly SpineApiDescriptor CurrentDescriptor =
            new SpineApiDescriptor(
                "CoolNether123.Spine.HarmonyPatching",
                new SemanticVersion(1, 0, 0),
                SpineCapability.HarmonyPatching);

        public SpineApiDescriptor Descriptor => CurrentDescriptor;

        public void PatchAll(
            HarmonyLib.Harmony harmony,
            Assembly assembly,
            HarmonyPatchOptions options = null)
        {
            if (harmony == null || assembly == null)
            {
                return;
            }

            harmony.PatchAll(assembly);
            options?.OnResult?.Invoke(assembly, "patched");
        }

        public IList<MethodBase> PatchType(
            HarmonyLib.Harmony harmony,
            Type patchType,
            HarmonyPatchOptions options = null)
        {
            var result = new List<MethodBase>();
            if (harmony == null || patchType == null)
            {
                return result;
            }

            try
            {
                var processor = new PatchClassProcessor(harmony, patchType);
                var patched = processor.Patch();
                if (patched != null)
                {
                    result.AddRange(patched);
                }
                options?.OnResult?.Invoke(patchType, "patched");
            }
            catch (Exception exception)
            {
                options?.OnResult?.Invoke(
                    patchType,
                    "error: " + exception.Message);
            }

            return result;
        }

        public bool TryPatch(
            HarmonyLib.Harmony harmony,
            MethodBase target,
            HarmonyMethod prefix = null,
            HarmonyMethod postfix = null,
            HarmonyMethod transpiler = null,
            Action<string> report = null)
        {
            if (harmony == null || target == null ||
                prefix == null && postfix == null && transpiler == null)
            {
                report?.Invoke("invalid patch request");
                return false;
            }

            try
            {
                harmony.Patch(target, prefix, postfix, transpiler);
                report?.Invoke("patched");
                return true;
            }
            catch (Exception exception)
            {
                report?.Invoke("error: " + exception.Message);
                return false;
            }
        }

        public IHarmonyPatchInstaller CreateInstaller(
            string consumerId,
            string logPrefix)
        {
            return new SpineLegacyHarmonyInstaller(
                consumerId,
                logPrefix,
                this);
        }
    }

    internal sealed class SpineLegacyHarmonyInstaller : IHarmonyPatchInstaller
    {
        private readonly HarmonyLib.Harmony harmony;
        private readonly string logPrefix;
        private readonly SpineLegacyHarmonyFacade facade;
        private readonly Dictionary<string, bool> results =
            new Dictionary<string, bool>(StringComparer.Ordinal);

        internal SpineLegacyHarmonyInstaller(
            string consumerId,
            string logPrefix,
            SpineLegacyHarmonyFacade facade)
        {
            if (String.IsNullOrWhiteSpace(consumerId))
            {
                throw new ArgumentException(
                    "A stable Harmony consumer ID is required.",
                    nameof(consumerId));
            }

            harmony = new HarmonyLib.Harmony(consumerId);
            this.logPrefix = String.IsNullOrWhiteSpace(logPrefix)
                ? "[" + consumerId + "]"
                : logPrefix;
            this.facade = facade;
        }

        public bool PatchAllOnce(
            Assembly assembly,
            HarmonyPatchOptions options = null)
        {
            return RunOnce(
                "assembly",
                () =>
                {
                    facade.PatchAll(harmony, assembly, options);
                    return true;
                });
        }

        public bool PatchTypeOnce(
            string patchName,
            Type patchType,
            int expectedPatchCount = 1,
            HarmonyPatchOptions options = null)
        {
            return RunOnce(
                "type:" + patchName,
                () => facade.PatchType(
                    harmony,
                    patchType,
                    options).Count == expectedPatchCount);
        }

        public bool TryPatch(
            string patchName,
            MethodBase target,
            HarmonyMethod prefix = null,
            HarmonyMethod postfix = null,
            HarmonyMethod transpiler = null)
        {
            return RunOnce(
                "method:" + patchName,
                () => facade.TryPatch(
                    harmony,
                    target,
                    prefix,
                    postfix,
                    transpiler));
        }

        private bool RunOnce(string key, Func<bool> operation)
        {
            bool previous;
            if (results.TryGetValue(key, out previous))
            {
                return previous;
            }

            bool result;
            try
            {
                result = operation();
            }
            catch (Exception exception)
            {
                Log.Error(logPrefix + " legacy patch failed: " + exception);
                result = false;
            }

            results[key] = result;
            return result;
        }
    }

    internal static class SpineLegacyApi
    {
        private static bool initialized;

        internal static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            Log.Message(
                "[Spine] Legacy runtime ready: " +
                SpineApi.Runtime.Descriptor.Capabilities);
        }
    }
}
#endif
