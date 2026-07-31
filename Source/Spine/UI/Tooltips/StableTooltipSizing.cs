using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Spine.UI.Tooltips
{
    /// <summary>
    /// Keeps RimWorld tooltip measurement consistent with tooltip rendering.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class StableTooltipSizing
    {
        private const string HarmonyId =
            "CoolNether123.Spine.StableTooltipSizing";
        private static readonly object InstallLock = new object();
        private static bool installed;

        static StableTooltipSizing()
        {
            EnsureInstalled();
        }

        /// <summary>
        /// Ensures the shared tooltip sizing correction is installed once.
        /// Spine also installs it automatically during RimWorld startup.
        /// </summary>
        public static void EnsureInstalled()
        {
            if (installed)
            {
                return;
            }

            lock (InstallLock)
            {
                if (installed)
                {
                    return;
                }

                MethodInfo target = AccessTools.PropertyGetter(
                    typeof(ActiveTip),
                    nameof(ActiveTip.TipRect));
                if (target == null)
                {
                    Log.Error(
                        "[Spine] Unable to stabilize tooltips: " +
                        "ActiveTip.TipRect was not found.");
                    return;
                }

                var harmony = new HarmonyLib.Harmony(HarmonyId);
                harmony.Patch(
                    target,
                    prefix: new HarmonyMethod(
                        typeof(StableTooltipSizing),
                        nameof(BeforeMeasure)),
                    postfix: new HarmonyMethod(
                        typeof(StableTooltipSizing),
                        nameof(AfterMeasure)),
                    finalizer: new HarmonyMethod(
                        typeof(StableTooltipSizing),
                        nameof(AfterMeasureFailure)));
                installed = true;
            }
        }

        private static void BeforeMeasure(out GameFont __state)
        {
            __state = Text.Font;
            Text.Font = GameFont.Small;
        }

        private static void AfterMeasure(GameFont __state)
        {
            Text.Font = __state;
        }

        private static Exception AfterMeasureFailure(
            Exception __exception,
            GameFont __state)
        {
            Text.Font = __state;
            return __exception;
        }
    }
}
