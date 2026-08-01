using System;
using System.Collections.Generic;
using Spine.Api;
using Spine.Caching;
using Spine.Collections;
using Spine.DirtyTracking;
using Spine.Rendering;
using Spine.Revisions;
using Spine.UI.ContextualSettings;

namespace Spine.Tests
{
    internal static class Program
    {
        private static int passed;
        private static int failed;

        private static int Main()
        {
            Run("revision monotonicity", TestRevisionMonotonicity);
            Run("dirty-region merging", TestDirtyRegionMerging);
            Run("cache bounds eviction reset and accounting", TestBoundedCache);
            Run("registration duplicate rejection and disposal", TestRegistrationLifecycle);
            Run("pipeline ordering and provider exception isolation", TestPipelineIsolation);
            Run("semantic version and capability comparison", TestVersionCapabilities);
            Run("snapshot immutability and teardown clearing", TestSnapshotImmutability);
            Run("contextual Alt-left detection and rejection", TestContextualInput);
            Run("contextual overlap priority and duplicate binding", TestContextualOverlap);
            Run("contextual multiple consumers release and cleanup", TestContextualLifecycle);
            Run("contextual deferred opening and exception isolation", TestContextualDeferredActions);
            Run("contextual exact group and root navigation", TestContextualNavigation);
            Run("contextual scroll and highlight presentation", TestContextualPresentation);

            Console.WriteLine($"RESULT: {passed} passed, {failed} failed");
            return failed == 0 ? 0 : 1;
        }

        private static void TestRevisionMonotonicity()
        {
            var source = new RevisionSource(41);
            AssertEqual(41L, source.Revision, "initial revision");
            AssertEqual(42L, source.Advance(), "first advance");
            AssertEqual(43L, source.Advance(), "second advance");
            AssertEqual(43L, source.Revision, "published revision");
        }

        private static void TestDirtyRegionMerging()
        {
            var dirty = new SparseDirtyRegionSet();
            dirty.Add(10, 3);
            dirty.Add(0, 2);
            dirty.Add(2, 8);
            dirty.Add(20, 2);
            dirty.Add(18, 2);

            AssertEqual(2, dirty.DirtyRegionCount, "merged count");
            AssertEqual(
                new DirtyRegion(0, 13),
                dirty.DirtyRegions[0],
                "first merged range");
            AssertEqual(
                new DirtyRegion(18, 4),
                dirty.DirtyRegions[1],
                "second merged range");
        }

        private static void TestBoundedCache()
        {
            var released = new List<string>();
            var cache = new BoundedLruCache<string, string>(
                10,
                release: (key, value) => released.Add(key));
            Assert(cache.AddOrUpdate("a", "A", 4), "add a");
            Assert(cache.AddOrUpdate("b", "B", 4), "add b");
            Assert(
                cache.TryGet("a", out string value) && value == "A",
                "hit a");
            Assert(!cache.TryGet("missing", out _), "miss missing");
            Assert(cache.AddOrUpdate("c", "C", 4), "add c");
            AssertEqual(2, cache.EntryCount, "entry count after eviction");
            AssertEqual(8L, cache.UsedBytes, "bytes after eviction");
            AssertEqual(1L, cache.Evictions, "eviction count");
            Assert(released.Contains("b"), "least-recent b released");
            Assert(!cache.AddOrUpdate("oversized", "X", 11), "oversized rejected");
            AssertEqual(1L, cache.Hits, "hit counter");
            AssertEqual(1L, cache.Misses, "miss counter");

            cache.Reset();
            AssertEqual(0, cache.EntryCount, "reset count");
            AssertEqual(0L, cache.UsedBytes, "reset bytes");
            AssertEqual(0L, cache.Hits, "reset hits");
            AssertEqual(0L, cache.Misses, "reset misses");
            AssertEqual(0L, cache.Evictions, "reset evictions");
        }

        private static void TestRegistrationLifecycle()
        {
            var pipeline = new RenderPipeline<List<string>>();
            var layer = new TestLayer(
                "same",
                RenderPhase.BaseContent,
                0,
                context => context.Add("first"));
            RegistrationResult accepted = pipeline.Register(layer);
            RegistrationResult duplicate = pipeline.Register(layer);

            Assert(accepted.Accepted && accepted.Token.IsActive, "registration accepted");
            Assert(
                !duplicate.Accepted &&
                duplicate.RejectionReason.Contains("already registered"),
                "duplicate actionable rejection");
            accepted.Token.Dispose();
            Assert(!accepted.Token.IsActive, "token inactive after disposal");
            AssertEqual(0, pipeline.ActiveLayers.Count, "layer removed by token");
            Assert(pipeline.Register(layer).Accepted, "ID reusable after disposal");
        }

        private static void TestPipelineIsolation()
        {
            var diagnostics = new CollectingDiagnosticsSink();
            var pipeline = new RenderPipeline<List<string>>(diagnostics);
            RegistrationResult late = pipeline.Register(
                new TestLayer(
                    "late",
                    RenderPhase.Overlay,
                    100,
                    context => context.Add("late")));
            RegistrationResult stableA = pipeline.Register(
                new TestLayer(
                    "stable-a",
                    RenderPhase.BaseContent,
                    10,
                    context => context.Add("a")));
            RegistrationResult failing = pipeline.Register(
                new TestLayer(
                    "failing",
                    RenderPhase.BaseContent,
                    5,
                    context => throw new InvalidOperationException("boom")));
            RegistrationResult stableB = pipeline.Register(
                new TestLayer(
                    "stable-b",
                    RenderPhase.BaseContent,
                    5,
                    context => context.Add("b")));
            var rendered = new List<string>();

            pipeline.Render(rendered);
            AssertEqual("a,b,late", string.Join(",", rendered), "ordered layers");
            Assert(!failing.Token.IsActive, "throwing provider disabled");
            Assert(
                !pipeline.Register(
                    new TestLayer(
                        "failing",
                        RenderPhase.BaseContent,
                        5,
                        context => { })).Accepted,
                "throwing provider ID quarantined");
            Assert(
                stableA.Token.IsActive &&
                stableB.Token.IsActive &&
                late.Token.IsActive,
                "healthy providers remain active");
            AssertEqual(1, diagnostics.Records.Count, "failure diagnostic count");
        }

        private static void TestVersionCapabilities()
        {
            SemanticVersion preview = SemanticVersion.Parse("1.2.3-alpha.1");
            SemanticVersion release = SemanticVersion.Parse("1.2.3");
            SemanticVersion next = SemanticVersion.Parse("1.3.0");
            Assert(preview < release, "prerelease precedes release");
            Assert(release < next, "minor comparison");

            var descriptor = new SpineApiDescriptor(
                "spine",
                release,
                SpineCapability.Revisions |
                SpineCapability.BoundedCaches |
                SpineCapability.Diagnostics);
            Assert(
                descriptor.Supports(
                    new SemanticVersion(1, 2, 0),
                    SpineCapability.Revisions | SpineCapability.Diagnostics),
                "supported handshake");
            Assert(
                !descriptor.Supports(next, SpineCapability.Revisions),
                "minimum version rejected");
            Assert(
                !descriptor.Supports(release, SpineCapability.RenderAtlases),
                "missing capability rejected");

            var requirement = new SpineRequirement(
                "Spine.Tests",
                new SemanticVersion(1, 0, 0),
                SpineCapability.Settings |
                SpineCapability.TooltipSizing |
                SpineCapability.ContextualSettings);
            var runtime = SpineRuntimeFacade.Instance;
            var supported = runtime.Check(requirement);
            Assert(
                supported.IsCompatible,
                "runtime facade supports current requirement");
            AssertEqual(
                "CoolNether123.Spine",
                runtime.Descriptor.ApiId,
                "runtime facade API id");
            AssertEqual(
                new SemanticVersion(1, 1, 0),
                runtime.Descriptor.Version,
                "contextual capability runtime version");
            runtime.Require(requirement);

            var unavailable = runtime.Check(new SpineRequirement(
                "Future.Consumer",
                new SemanticVersion(1, 0, 0),
                SpineCapability.RenderAtlases));
            Assert(
                !unavailable.IsCompatible,
                "runtime facade rejects missing capability");
            AssertEqual(
                SpineCapability.RenderAtlases,
                unavailable.MissingCapabilities,
                "runtime facade reports exact missing capability");

            var tooNew = runtime.Check(new SpineRequirement(
                "Future.Consumer",
                new SemanticVersion(2, 0, 0),
                SpineCapability.None));
            Assert(
                !tooNew.IsCompatible,
                "runtime facade rejects newer minimum");
        }

        private static void TestSnapshotImmutability()
        {
            int[] mutable = { 1, 2, 3 };
            ImmutableSnapshotArray<int> snapshot =
                ImmutableSnapshotArray<int>.CopyOf(mutable);
            mutable[0] = 99;
            AssertEqual(1, snapshot[0], "source mutation cannot alter snapshot");

            var slot = new SnapshotSlot<ImmutableSnapshotArray<int>>();
            slot.Publish(snapshot);
            Assert(slot.Current != null, "snapshot published");
            slot.Clear();
            Assert(slot.Current == null, "teardown releases snapshot");
        }

        private static void TestContextualInput()
        {
            var router = CreateContextRouter();
            Assert(Routes(router, ContextualPointerEventType.MouseDown, 0, true),
                "Alt-left routes and therefore consumes");
            Assert(!Routes(router, ContextualPointerEventType.MouseDown, 0, false),
                "ordinary left rejected");
            Assert(!Routes(router, ContextualPointerEventType.MouseDown, 1, true),
                "right-click rejected");
            Assert(!Routes(router, ContextualPointerEventType.MouseMove, 0, true),
                "Alt-hover rejected");
        }

        private static void TestContextualOverlap()
        {
            var router = new ContextualSettingsRouterCore();
            router.Acquire("consumer");
            var rect = new ContextualHitRect(0f, 0f, 30f, 30f);
            Assert(router.Register("consumer", rect,
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Group, "group"), 100, 10),
                "group registered");
            Assert(router.Register("consumer", rect,
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "exact"), -100, 10),
                "exact registered");
            Assert(!router.Register("consumer", rect,
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "exact"), -100, 10),
                "duplicate rejected");
            Assert(router.TryRoute(ContextClick(), 10, out ContextualBindingRecord winner),
                "overlap routed");
            AssertEqual("exact", winner.Target.SettingId,
                "specificity outranks explicit priority");

            router.Register("consumer", rect,
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "late"), -100, 10);
            router.TryRoute(ContextClick(), 10, out winner);
            AssertEqual("late", winner.Target.SettingId,
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
            AssertEqual(2, router.ConsumerCount, "two consumers acquired");
            router.Release("b");
            AssertEqual(1, router.ConsumerCount, "one consumer remains");
            Assert(router.TryRoute(ContextClick(), 5, out ContextualBindingRecord winner),
                "remaining consumer routes");
            AssertEqual("a", winner.ConsumerId, "released consumer removed");
            router.Release("a");
            AssertEqual(0, router.ConsumerCount, "final consumer released");
            AssertEqual(0, router.RegistrationCount, "registrations cleaned");
            Assert(!router.TryRoute(ContextClick(), 5, out _),
                "no routing or polling state without consumers");
        }

        private static void TestContextualDeferredActions()
        {
            var queue = new DeferredContextualActionQueue();
            int opened = 0;
            Assert(queue.Enqueue(() => opened++), "open queued");
            AssertEqual(0, opened, "opening is deferred");
            Assert(!queue.Enqueue(() => opened += 10), "stacked open rejected");
            Assert(queue.Drain(), "deferred open drained");
            AssertEqual(1, opened, "one settings window requested");

            Exception isolated = null;
            queue.Enqueue(() => throw new InvalidOperationException("bad target"));
            Assert(queue.Drain(exception => isolated = exception),
                "bad consumer action drained");
            Assert(isolated is InvalidOperationException,
                "bad target exception isolated");
            Assert(queue.Enqueue(() => opened++),
                "healthy consumer can queue after failure");
            queue.Drain();
            AssertEqual(2, opened, "healthy navigation survives failure");
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
            AssertEqual("exact", exact.TargetId, "exact target retained");
            Assert(exact.UseSimpleView, "exact target chooses simple view");
            Assert(!exact.IncludeChildren, "exact target remains narrow");

            ContextualNavigationPlan group = ContextualNavigationResolver.Resolve(
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Group, "group"),
                Lookup);
            AssertEqual("group", group.TargetId, "group target retained");
            Assert(!group.UseSimpleView, "advanced-only group chooses advanced view");
            Assert(group.IncludeChildren, "group includes its section");

            ContextualNavigationPlan fallback = ContextualNavigationResolver.Resolve(
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "hidden", "group"),
                Lookup);
            AssertEqual("group", fallback.TargetId, "hidden exact target falls back to group");
            Assert(fallback.IncludeChildren, "fallback group includes its section");

            ContextualNavigationPlan root = ContextualNavigationResolver.Resolve(
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Root),
                Lookup);
            Assert(root.IsRoot, "root target opens unfiltered settings");

            ContextualNavigationPlan missing = ContextualNavigationResolver.Resolve(
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "missing", "also-missing"),
                Lookup);
            Assert(missing.IsRoot, "missing exact and group safely fall back to root");
        }

        private static void TestContextualPresentation()
        {
            AssertEqual(75f,
                ContextualPresentationMath.CenteredScroll(100f, 100f, 50f, 300f),
                "target row centered");
            AssertEqual(0f,
                ContextualPresentationMath.CenteredScroll(10f, 100f, 30f, 300f),
                "scroll clamps at top");
            AssertEqual(200f,
                ContextualPresentationMath.CenteredScroll(290f, 100f, 30f, 300f),
                "scroll clamps at bottom");
            Assert(ContextualPresentationMath.IsHighlightActive(11.45f, 10f, 1.45f),
                "highlight remains active through its lifetime");
            Assert(!ContextualPresentationMath.IsHighlightActive(11.46f, 10f, 1.45f),
                "highlight expires after its lifetime");
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

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                passed++;
                Console.WriteLine("PASS: " + name);
            }
            catch (Exception exception)
            {
                failed++;
                Console.WriteLine("FAIL: " + name + " - " + exception.Message);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void AssertEqual<T>(
            T expected,
            T actual,
            string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    $"{message}: expected {expected}, actual {actual}");
            }
        }

        private sealed class TestLayer : IRenderLayer<List<string>>
        {
            private readonly Action<List<string>> render;

            public TestLayer(
                string id,
                RenderPhase phase,
                int priority,
                Action<List<string>> render)
            {
                Id = id;
                Phase = phase;
                Priority = priority;
                this.render = render;
            }

            public string Id { get; }
            public RenderPhase Phase { get; }
            public int Priority { get; }
            public void Render(List<string> context) => render(context);
        }

        private sealed class CollectingDiagnosticsSink :
            IRenderDiagnosticsSink
        {
            public bool Enabled => true;
            public List<RenderDiagnostic> Records { get; } =
                new List<RenderDiagnostic>();
            public void Record(RenderDiagnostic diagnostic) =>
                Records.Add(diagnostic);
        }
    }
}
