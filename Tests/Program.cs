using System;
using System.Collections.Generic;
using Spine.Api;
using Spine.Caching;
using Spine.Collections;
using Spine.DirtyTracking;
using Spine.Rendering;
using Spine.Revisions;

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
