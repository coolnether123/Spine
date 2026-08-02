using System;
using System.Collections.Generic;
using Spine.Api;
using Spine.Caching;
using Spine.Harmony;
using Spine.UI.ContextualSettings;
using Spine.UI.SettingsFramework;
using static RimWorld.ModTestSupport.Test;

namespace Spine.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            Start("Spine contracts");
            Run("cache bounds eviction reset and accounting", TestBoundedCache);
            Run("semantic version and capability comparison", TestVersionCapabilities);
            Run("patch operation identities", TestPatchOperationIdentities);
            Run("contextual Alt-left detection and rejection", TestContextualInput);
            Run("contextual overlap priority and duplicate binding", TestContextualOverlap);
            Run("contextual multiple consumers release and cleanup", TestContextualLifecycle);
            Run("contextual deferred opening and exception isolation", TestContextualDeferredActions);
            Run("contextual exact group and root navigation", TestContextualNavigation);
            Run("contextual scroll and highlight presentation", TestContextualPresentation);
            Run("compact settings presentation thresholds", TestSettingsPresentationPolicy);
            Run("settings preparation is lazy and idempotent", TestSettingsPreparation);
            Run("contextual tooltips never add hints", TestContextualTooltipComposition);

            return Finish();
        }

        private static void TestBoundedCache()
        {
            var released = new List<string>();
            var cache = new BoundedLruCache<string, string>(
                10,
                release: (key, value) => released.Add(key));
            Require(cache.AddOrUpdate("a", "A", 4), "add a");
            Require(cache.AddOrUpdate("b", "B", 4), "add b");
            Require(
                cache.TryGet("a", out string value) && value == "A",
                "hit a");
            Require(!cache.TryGet("missing", out _), "miss missing");
            Require(cache.AddOrUpdate("c", "C", 4), "add c");
            Equal(2, cache.EntryCount, "entry count after eviction");
            Equal(8L, cache.UsedBytes, "bytes after eviction");
            Equal(1L, cache.Evictions, "eviction count");
            Require(released.Contains("b"), "least-recent b released");
            Require(!cache.AddOrUpdate("oversized", "X", 11), "oversized rejected");
            Equal(1L, cache.Hits, "hit counter");
            Equal(1L, cache.Misses, "miss counter");

            cache.Reset();
            Equal(0, cache.EntryCount, "reset count");
            Equal(0L, cache.UsedBytes, "reset bytes");
            Equal(0L, cache.Hits, "reset hits");
            Equal(0L, cache.Misses, "reset misses");
            Equal(0L, cache.Evictions, "reset evictions");
        }

        private static void TestVersionCapabilities()
        {
            SemanticVersion preview = SemanticVersion.Parse("1.2.3-alpha.1");
            SemanticVersion release = SemanticVersion.Parse("1.2.3");
            SemanticVersion next = SemanticVersion.Parse("1.3.0");
            Require(preview < release, "prerelease precedes release");
            Require(release < next, "minor comparison");
            Equal(1UL << 2, (ulong)SpineCapability.BoundedCaches,
                "bounded-cache capability id is stable");
            Equal(1UL << 8, (ulong)SpineCapability.Settings,
                "settings capability id is stable");
            Equal(1UL << 9, (ulong)SpineCapability.HarmonyPatching,
                "Harmony capability id is stable");
            Equal(1UL << 10, (ulong)SpineCapability.FluentTranspilers,
                "fluent-transpiler capability id is stable");
            Equal(1UL << 11, (ulong)SpineCapability.TooltipSizing,
                "tooltip capability id is stable");
            Equal(1UL << 12, (ulong)SpineCapability.ContextualSettings,
                "contextual-settings capability id is stable");
            Equal(1UL << 13, (ulong)SpineCapability.ModSettingsPages,
                "settings-page capability id is stable");

            var descriptor = new SpineApiDescriptor(
                "spine",
                release,
                SpineCapability.BoundedCaches |
                SpineCapability.FluentTranspilers);
            Require(
                descriptor.Supports(
                    new SemanticVersion(1, 2, 0),
                    SpineCapability.BoundedCaches | SpineCapability.FluentTranspilers),
                "supported handshake");
            Require(
                !descriptor.Supports(next, SpineCapability.BoundedCaches),
                "minimum version rejected");
            Require(
                !descriptor.Supports(release, SpineCapability.TooltipSizing),
                "missing capability rejected");

            var requirement = new SpineRequirement(
                "Spine.Tests",
                new SemanticVersion(1, 0, 0),
                SpineCapability.Settings |
                SpineCapability.TooltipSizing |
                SpineCapability.ContextualSettings |
                SpineCapability.ModSettingsPages);
            var runtime = SpineRuntimeFacade.Instance;
            var supported = runtime.Check(requirement);
            Require(
                supported.IsCompatible,
                "runtime facade supports current requirement");
            Equal(
                "CoolNether123.Spine",
                runtime.Descriptor.ApiId,
                "runtime facade API id");
            Equal(
                new SemanticVersion(1, 0, 0),
                runtime.Descriptor.Version,
                "settings-page capability runtime version");
            var descriptorField = typeof(SpineRuntimeFacade).GetField(
                "CurrentDescriptor",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static);
            Require(
                descriptorField != null && descriptorField.IsInitOnly,
                "runtime descriptor is cached in a static readonly field");
            runtime.Require(requirement);

            const SpineCapability unknownCapability =
                (SpineCapability)(1UL << 63);
            var unavailable = runtime.Check(new SpineRequirement(
                "Future.Consumer",
                new SemanticVersion(1, 0, 0),
                unknownCapability));
            Require(
                !unavailable.IsCompatible,
                "runtime facade rejects missing capability");
            Equal(
                unknownCapability,
                unavailable.MissingCapabilities,
                "runtime facade reports exact missing capability");

            var tooNew = runtime.Check(new SpineRequirement(
                "Future.Consumer",
                new SemanticVersion(2, 0, 0),
                SpineCapability.None));
            Require(
                !tooNew.IsCompatible,
                "runtime facade rejects newer minimum");
        }

        private static void TestSettingsPreparation()
        {
            var settings = new CountingSettings();
            CountingSettings.ConstructorCalls = 0;
            var definitions = new List<SettingDefinition>
            {
                SettingDefinitions.Toggle(
                    "enabled",
                    nameof(CountingSettings.Enabled),
                    "Enabled"),
                SettingDefinitions.Header("header", "Header")
            };

            SettingDefinitions.Prepare(settings, definitions);
            Equal(1, CountingSettings.ConstructorCalls,
                "first unresolved default creates one pristine settings object");
            Equal(true, definitions[0].DefaultValue,
                "default is read from the pristine settings object");
            Equal(0, definitions[0].SortOrder, "first sort order prepared");
            Equal(1, definitions[1].SortOrder, "second sort order prepared");

            SettingDefinitions.Prepare(settings, definitions);
            Equal(1, CountingSettings.ConstructorCalls,
                "prepared definitions do not create another pristine object");
        }

        private sealed class CountingSettings
        {
            internal static int ConstructorCalls;

            public CountingSettings()
            {
                ConstructorCalls++;
            }

            public bool Enabled = true;
        }

        private static void TestPatchOperationIdentities()
        {
            Equal(
                "method:first unresolved target",
                HarmonyPatchOperationKeys.ForMethod(
                    " first unresolved target "),
                "method identity trims its required stable name");
            Require(
                HarmonyPatchOperationKeys.ForMethod("first") !=
                HarmonyPatchOperationKeys.ForMethod("second"),
                "two unresolved targets retain distinct cache identities");

            bool rejected = false;
            try
            {
                HarmonyPatchOperationKeys.ForMethod(null);
            }
            catch (ArgumentException)
            {
                rejected = true;
            }

            Require(rejected, "an unnamed patch is rejected before caching");
        }

        private static void TestContextualInput()
        {
            var router = CreateContextRouter();
            Require(Routes(router, ContextualPointerEventType.MouseDown, 0, true),
                "Alt-left routes and therefore consumes");
            Require(!Routes(router, ContextualPointerEventType.MouseDown, 0, false),
                "ordinary left rejected");
            Require(!Routes(router, ContextualPointerEventType.MouseDown, 1, true),
                "right-click rejected");
            Require(!Routes(router, ContextualPointerEventType.MouseMove, 0, true),
                "Alt-hover rejected");
        }

        private static void TestContextualOverlap()
        {
            var router = new ContextualSettingsRouterCore();
            router.Acquire("consumer");
            var rect = new ContextualHitRect(0f, 0f, 30f, 30f);
            Require(router.Register("consumer", rect,
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Group, "group"), 100, 10),
                "group registered");
            Require(router.Register("consumer", rect,
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "exact"), -100, 10),
                "exact registered");
            Require(!router.Register("consumer", rect,
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "exact"), -100, 10),
                "duplicate rejected");
            Require(router.TryRoute(ContextClick(), 10, out ContextualBindingRecord winner),
                "overlap routed");
            Equal("exact", winner.Target.SettingId,
                "specificity outranks explicit priority");

            router.Register("consumer", rect,
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "late"), -100, 10);
            router.TryRoute(ContextClick(), 10, out winner);
            Equal("late", winner.Target.SettingId,
                "registration order deterministically breaks ties");
        }

        private static void TestContextualLifecycle()
        {
            var router = new ContextualSettingsRouterCore();
            var rect = new ContextualHitRect(0f, 0f, 30f, 30f);
            router.Acquire("a");
            router.Acquire("b");
            router.Register("a", rect,
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "a"), 0, 5);
            router.Register("b", rect,
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "b"), 1, 5);
            Equal(2, router.ConsumerCount, "two consumers acquired");
            router.Release("b");
            Equal(1, router.ConsumerCount, "one consumer remains");
            Require(router.TryRoute(ContextClick(), 5, out ContextualBindingRecord winner),
                "remaining consumer routes");
            Equal("a", winner.ConsumerId, "released consumer removed");
            router.Release("a");
            Equal(0, router.ConsumerCount, "final consumer released");
            Equal(0, router.RegistrationCount, "registrations cleaned");
            Require(!router.TryRoute(ContextClick(), 5, out _),
                "no routing or polling state without consumers");
        }

        private static void TestContextualDeferredActions()
        {
            var queue = new DeferredContextualActionQueue();
            int opened = 0;
            Require(queue.Enqueue(() => opened++), "open queued");
            Equal(0, opened, "opening is deferred");
            Require(!queue.Enqueue(() => opened += 10), "stacked open rejected");
            Require(queue.Drain(), "deferred open drained");
            Equal(1, opened, "one settings window requested");

            Exception isolated = null;
            queue.Enqueue(() => throw new InvalidOperationException("bad target"));
            Require(queue.Drain(exception => isolated = exception),
                "bad consumer action drained");
            Require(isolated is InvalidOperationException,
                "bad target exception isolated");
            Require(queue.Enqueue(() => opened++),
                "healthy consumer can queue after failure");
            queue.Drain();
            Equal(2, opened, "healthy navigation survives failure");
        }

        private static void TestContextualNavigation()
        {
            ContextualNavigationCandidate Lookup(string id)
            {
                switch (id)
                {
                    case "exact":
                        return new ContextualNavigationCandidate(id, true, true);
                    case "group":
                        return new ContextualNavigationCandidate(id, true, false);
                    case "hidden":
                        return new ContextualNavigationCandidate(id, false, true);
                    default:
                        return default(ContextualNavigationCandidate);
                }
            }

            ContextualNavigationPlan exact = ContextualNavigationResolver.Resolve(
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "exact", "group"),
                Lookup);
            Equal("exact", exact.TargetId, "exact target retained");
            Require(exact.UseSimpleView, "exact target chooses simple view");
            Require(!exact.IncludeChildren, "exact target remains narrow");

            ContextualNavigationPlan group = ContextualNavigationResolver.Resolve(
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Group, "group"),
                Lookup);
            Equal("group", group.TargetId, "group target retained");
            Require(!group.UseSimpleView, "advanced-only group chooses advanced view");
            Require(group.IncludeChildren, "group includes its section");

            ContextualNavigationPlan fallback = ContextualNavigationResolver.Resolve(
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "hidden", "group"),
                Lookup);
            Equal("group", fallback.TargetId, "hidden exact target falls back to group");
            Require(fallback.IncludeChildren, "fallback group includes its section");

            ContextualNavigationPlan root = ContextualNavigationResolver.Resolve(
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Root),
                Lookup);
            Require(root.IsRoot, "root target opens unfiltered settings");

            ContextualNavigationPlan missing = ContextualNavigationResolver.Resolve(
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "missing", "also-missing"),
                Lookup);
            Require(missing.IsRoot, "missing exact and group safely fall back to root");
        }

        private static void TestContextualPresentation()
        {
            Equal(75f,
                ContextualPresentationMath.CenteredScroll(100f, 100f, 50f, 300f),
                "target row centered");
            Equal(0f,
                ContextualPresentationMath.CenteredScroll(10f, 100f, 30f, 300f),
                "scroll clamps at top");
            Equal(200f,
                ContextualPresentationMath.CenteredScroll(290f, 100f, 30f, 300f),
                "scroll clamps at bottom");
            Require(ContextualPresentationMath.IsHighlightActive(11.45f, 10f, 1.45f),
                "highlight remains active through its lifetime");
            Require(!ContextualPresentationMath.IsHighlightActive(11.46f, 10f, 1.45f),
                "highlight expires after its lifetime");
        }

        private static void TestSettingsPresentationPolicy()
        {
            Require(!SettingsPresentationPolicy.ShowSearch(4),
                "search must stay hidden below five settings");
            Require(SettingsPresentationPolicy.ShowSearch(5),
                "search must appear at five settings");
            Require(!SettingsPresentationPolicy.ShowFilters(10),
                "filters must stay hidden for ten settings");
            Require(!SettingsPresentationPolicy.ShowViewModes(10, 8),
                "view filtering must stay hidden for ten settings");
            Require(SettingsPresentationPolicy.ShowFilters(11),
                "filters must appear above ten settings");
            Require(!SettingsPresentationPolicy.ShowViewModes(11, 3),
                "three advanced-only settings stay in one unified view");
            Require(SettingsPresentationPolicy.ShowViewModes(11, 4),
                "four advanced-only settings enable the view toggle");
        }

        private static void TestContextualTooltipComposition()
        {
            Equal(
                "Faction relationship",
                ContextualTooltipComposition.FeatureOnly(
                    "Faction relationship  "),
                "feature tooltip changed");
            Equal<string>(
                null,
                ContextualTooltipComposition.FeatureOnly(null),
                "hint-only binding created a tooltip");
            Equal<string>(
                null,
                ContextualTooltipComposition.FeatureOnly("   "),
                "empty feature tooltip created a tooltip");
        }

        private static ContextualSettingsRouterCore CreateContextRouter()
        {
            var router = new ContextualSettingsRouterCore();
            router.Acquire("consumer");
            router.Register(
                "consumer",
                new ContextualHitRect(0f, 0f, 30f, 30f),
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "setting"),
                0,
                1);
            return router;
        }

        private static bool Routes(
            ContextualSettingsRouterCore router,
            ContextualPointerEventType type,
            int button,
            bool alt) =>
            router.TryRoute(
                new ContextualPointerEvent(type, button, alt, 10f, 10f),
                1,
                out _);

        private static ContextualPointerEvent ContextClick() =>
            new ContextualPointerEvent(
                ContextualPointerEventType.MouseDown,
                0,
                true,
                10f,
                10f);

    }
}
